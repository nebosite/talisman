using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Talisman.Properties;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The Application
    /// </summary>
    // --------------------------------------------------------------------------
    public partial class App : Application
    {
        System.Threading.Tasks.Task newVersionCheck;
        static readonly HttpClient _httpClient = new HttpClient();
        FileLogger _logger;
        bool _previousSessionCrashed;
        bool _launchedByAutoRestart;
        readonly RestartPolicy _restartPolicy = new RestartPolicy(maxRestartsInWindow: 3, window: TimeSpan.FromMinutes(5));
        const string AutoRestartArg = "--auto-restart";

        // --------------------------------------------------------------------------
        /// <summary>
        /// Stand up logging and global crash handlers before anything else runs, so
        /// failures during startup are captured too. Safe to call once.
        /// </summary>
        // --------------------------------------------------------------------------
        void InitializeDiagnostics()
        {
            try
            {
                _logger = new FileLogger();
                Log.Initialize(_logger);
                Trace.Listeners.Add(new LoggerTraceListener(_logger));
                Trace.AutoFlush = true;
            }
            catch (Exception ex)
            {
                // If logging itself cannot start, there is nowhere good to record
                // that - fall back to the debugger and keep the app running.
                Debug.WriteLine("Failed to initialize logging: " + ex);
            }

            // Catch exceptions from every thread we can reach.
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            // Windows logoff/shutdown is an ordinary way to stop, not a crash.
            SessionEnding += OnSessionEnding;

            // Classify how the previous session ended. CrashedLastTime alone is a
            // weak signal - it stays set after ANY ungraceful termination (reboot,
            // sleep, kill), which produces false "did not exit cleanly" alarms with
            // no log evidence. Only treat it as a crash when a terminating exception
            // was actually recorded.
            var uncleanShutdown = Settings.Default.CrashedLastTime;
            var fatalCrash = Settings.Default.LastSessionFatalCrash;
            var previousExit = PreviousExitClassifier.Classify(uncleanShutdown, fatalCrash);
            _previousSessionCrashed = previousExit == PreviousExitKind.Crashed;

            // Consume the crash marker so a given crash is only surfaced once.
            if (fatalCrash)
            {
                try { Settings.Default.LastSessionFatalCrash = false; Settings.Default.Save(); }
                catch (Exception ex) { Debug.WriteLine("Could not clear crash marker: " + ex); }
            }

            Log.Info("========================================================");
            Log.Info($"Talisman starting. {DescribeEnvironment()}");
            switch (previousExit)
            {
                case PreviousExitKind.Crashed:
                    Log.Warn("Previous session ended in an unhandled exception (see the earlier log for details).");
                    break;
                case PreviousExitKind.UncleanNoCrash:
                    Log.Info("Previous session did not shut down cleanly, but no crash was recorded (likely a reboot, sleep, or forced close).");
                    break;
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Record that this session is dying from a real (terminating) crash, so the
        /// next launch can tell it apart from an ordinary ungraceful termination.
        /// </summary>
        // --------------------------------------------------------------------------
        void RecordFatalCrash()
        {
            try { Settings.Default.LastSessionFatalCrash = true; Settings.Default.Save(); }
            catch (Exception ex) { Log.Error("Could not record fatal-crash marker.", ex); }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Windows is logging off / shutting down - a normal way to stop. Mark a
        /// clean shutdown so the next launch doesn't mistake the reboot for a crash.
        /// </summary>
        // --------------------------------------------------------------------------
        void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            Log.Info($"Windows session ending ({e.ReasonSessionEnding}); marking a clean shutdown.");
            try { Settings.Default.CrashedLastTime = false; Settings.Default.Save(); }
            catch (Exception ex) { Log.Error("Could not mark clean shutdown on session end.", ex); }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// One-line snapshot of the runtime environment for the log header.
        /// </summary>
        // --------------------------------------------------------------------------
        static string DescribeEnvironment()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            return $"v{version} | OS={Environment.OSVersion} | CLR={Environment.Version} | " +
                   $"64bit={Environment.Is64BitProcess} | user={Environment.UserName}@{Environment.MachineName}";
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Unhandled exception on the UI (dispatcher) thread. Log it and keep the
        /// app alive - a single bad event handler should not kill Talisman.
        /// </summary>
        // --------------------------------------------------------------------------
        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error("Unhandled exception on UI thread.", e.Exception);
            e.Handled = true;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Unhandled exception on a background thread. These are generally fatal -
        /// the runtime tears the process down after this - so log at Fatal and
        /// flush before we lose the chance.
        /// </summary>
        // --------------------------------------------------------------------------
        void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Fatal($"Unhandled exception (terminating={e.IsTerminating}).", e.ExceptionObject as Exception);
            if (e.IsTerminating)
            {
                RecordFatalCrash();
                AttemptAutoRestart();
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// After a fatal crash, relaunch Talisman - unless the user disabled it, or
        /// we have already restarted too many times in a short window (a crash loop).
        /// Runs inside the dying process, so keep it fast and best-effort.
        /// </summary>
        // --------------------------------------------------------------------------
        void AttemptAutoRestart()
        {
            try
            {
                if (!Settings.Default.AutoRestartOnCrash)
                {
                    Log.Info("Auto-restart is disabled; not relaunching.");
                    return;
                }

                var prior = ParseCrashTimes(Settings.Default.RecentCrashTimes);
                var decision = _restartPolicy.Evaluate(prior, DateTime.Now);

                try
                {
                    Settings.Default.RecentCrashTimes = JsonConvert.SerializeObject(decision.UpdatedHistory);
                    Settings.Default.Save();
                }
                catch (Exception saveError)
                {
                    Log.Error("Could not persist crash-restart history.", saveError);
                }

                if (!decision.ShouldRestart)
                {
                    Log.Warn("Auto-restart suppressed: too many crashes within 5 minutes (crash loop). Staying down.");
                    return;
                }

                var exe = Assembly.GetExecutingAssembly().Location;
                Log.Info("Auto-restarting Talisman: " + exe);
                Process.Start(new ProcessStartInfo(exe, AutoRestartArg) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Error("Auto-restart attempt failed.", ex);
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Parse the persisted crash timestamps. Tolerant of empty/garbage values.
        /// </summary>
        // --------------------------------------------------------------------------
        static System.Collections.Generic.List<DateTime> ParseCrashTimes(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new System.Collections.Generic.List<DateTime>();
            try
            {
                return JsonConvert.DeserializeObject<System.Collections.Generic.List<DateTime>>(raw)
                       ?? new System.Collections.Generic.List<DateTime>();
            }
            catch
            {
                return new System.Collections.Generic.List<DateTime>();
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Block until this is the only Talisman process, or the timeout elapses.
        /// </summary>
        // --------------------------------------------------------------------------
        static void WaitForOtherInstancesToExit(TimeSpan timeout)
        {
            var myId = Process.GetCurrentProcess().Id;
            var watch = Stopwatch.StartNew();
            while (watch.Elapsed < timeout)
            {
                var others = Process.GetProcessesByName("Talisman").Where(p => p.Id != myId).ToArray();
                if (others.Length == 0) return;
                System.Threading.Thread.Sleep(200);
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// A faulted Task whose exception was never observed. Log it and mark it
        /// observed so it does not escalate.
        /// </summary>
        // --------------------------------------------------------------------------
        void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Log.Error("Unobserved task exception.", e.Exception);
            e.SetObserved();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// If the previous session crashed, let the user know and offer to open the
        /// log folder so they can find (and send us) the details.
        /// </summary>
        // --------------------------------------------------------------------------
        void MaybeNotifyPreviousCrash()
        {
            if (!_previousSessionCrashed) return;

            var email = Settings.Default.CrashReportEmail;
            var hasEmail = !string.IsNullOrWhiteSpace(email);

            // Seamless auto-restart: don't interrupt the user with a dialog. Just log,
            // and quietly email the report if they configured an address.
            if (_launchedByAutoRestart)
            {
                Log.Info("Recovered via auto-restart after a crash.");
                if (hasEmail) SendCrashReport(email, silent: true);
                return;
            }

            if (hasEmail)
            {
                var result = MessageBox.Show(
                    $"Talisman closed unexpectedly last time.\r\n\r\nEmail the diagnostic log to {email}?",
                    "Talisman", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes) SendCrashReport(email, silent: false);
                else OpenLogFolder();
            }
            else
            {
                var result = MessageBox.Show(
                    "Talisman closed unexpectedly last time.\r\n\r\nOpen the log folder to see what happened?",
                    "Talisman", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes) OpenLogFolder();
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Email the recent diagnostic log to the configured address via Outlook.
        /// Runs off the UI thread because Outlook interop can be slow. When not
        /// silent, reports success/failure back to the user.
        /// </summary>
        // --------------------------------------------------------------------------
        void SendCrashReport(string toAddress, bool silent)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var subject = $"Talisman crash report from {Environment.UserName}@{Environment.MachineName}";
                    var logFolder = _logger?.LogDirectory ?? FileLogger.DefaultLogDirectory;
                    var body = "Talisman closed unexpectedly. Diagnostic details below.\r\n\r\n"
                             + DescribeEnvironment() + "\r\n"
                             + "Log folder: " + logFolder + "\r\n\r\n"
                             + "----- recent log -----\r\n"
                             + (_logger?.ReadRecentLines() ?? "(no log available)");

                    new OutlookHelper().SendMail(toAddress, subject, body);
                    Log.Info("Crash report emailed to " + toAddress);
                    if (!silent) ShowOnUi($"Crash report sent to {toAddress}.");
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to email crash report.", ex);
                    if (!silent) ShowOnUi("Could not send the crash report. Use the log folder to send it manually.");
                }
            });
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Show a simple message box on the UI thread from any thread.
        /// </summary>
        // --------------------------------------------------------------------------
        void ShowOnUi(string message)
        {
            Dispatcher?.Invoke(() => MessageBox.Show(message, "Talisman"));
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Open the folder containing the log files in Explorer.
        /// </summary>
        // --------------------------------------------------------------------------
        public void OpenLogFolder()
        {
            try
            {
                var folder = _logger?.LogDirectory ?? FileLogger.DefaultLogDirectory;
                Process.Start("explorer.exe", $"\"{folder}\"");
            }
            catch (Exception ex)
            {
                Log.Error("Could not open log folder.", ex);
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// OnStartup
        /// </summary>
        // --------------------------------------------------------------------------
        protected override void OnStartup(StartupEventArgs e)
        {
            InitializeDiagnostics();

            _launchedByAutoRestart = e.Args != null && e.Args.Contains(AutoRestartArg);
            if (_launchedByAutoRestart)
            {
                // We were spawned by a crashing predecessor that is still shutting
                // down. Wait for it to exit so the single-instance check below does
                // not mistake it for a real duplicate and shut us down / kill it.
                Log.Info("Launched by auto-restart; waiting for the previous instance to exit.");
                WaitForOtherInstancesToExit(TimeSpan.FromSeconds(10));
            }

            DraggingLogic.SetDpiAwareness();
            var talismanProcesses = Process.GetProcessesByName("Talisman");
            if(talismanProcesses.Length > 1)
            {

                Debug.WriteLine("Found an extra Talisman procress.");
#if DEBUG
                foreach(var process in talismanProcesses)
                {
                    Debug.WriteLine("Shutting down other Talisman ...");
                    var currentProcessId = Process.GetCurrentProcess().Id;
                    if (process.Id != currentProcessId)
                    {
                        try
                        {

                            process.Kill();
                        }
                        catch(Exception err)
                        {
                            Log.Warn("Could not end other Talisman process.", err);
                            Debug.WriteLine($"Could not end other talisman process: {err.Message}");
                        }
                    }
                }
#else
                Debug.WriteLine("Shutting down myself.");
                Application.Current.Shutdown();
#endif               
            }
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            if(Settings.Default.CurrentVersion != assemblyVersion)
            {
                Settings.Default.Upgrade();
                Settings.Default.Reload();
                Settings.Default.CurrentVersion = assemblyVersion;
                Settings.Default.Save();
                Debug.WriteLine("Upgraded Settings: " + Settings.Default.Location);
            }

            if(String.IsNullOrEmpty(Settings.Default.CheckForNewVersions))
            {
                var result = MessageBox.Show("Check for new versions of Talisman?", "Talisman", MessageBoxButton.YesNo);
                Settings.Default.CheckForNewVersions = result.ToString();
                Settings.Default.Save();
            }
            
            if(Settings.Default.CheckForNewVersions == "Yes")
            {
                this.newVersionCheck = System.Threading.Tasks.Task.Run(CheckForNewVersion);
            }

            base.OnStartup(e);

            // Defer the "closed unexpectedly" notice until the app is up and idle so
            // a modal dialog can't block the main window (and the MCP server) from
            // starting.
            Dispatcher.BeginInvoke(new Action(MaybeNotifyPreviousCrash),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Check github to see if there is a recent install
        /// </summary>
        // --------------------------------------------------------------------------
        async void CheckForNewVersion()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {

                Debug.WriteLine("Checking for a newer version");

                try
                {
                    var versionUrl = "https://raw.githubusercontent.com/nebosite/talisman/master/installs/currentVersion.txt";

                    // Runs on a thread-pool thread (no sync context), so blocking on
                    // the async call here is safe.
                    string responseText = _httpClient.GetStringAsync(versionUrl).GetAwaiter().GetResult().Trim();

                    var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    var localVersionParts = assemblyVersion.Split('.');
                    var currentVersionParts = responseText.Split('.');

                    for (int i = 0; i < localVersionParts.Length && i < currentVersionParts.Length; i++)
                    {
                        var localPart = int.Parse(localVersionParts[i]);
                        var currentPart = int.Parse(currentVersionParts[i]);
                        if (currentPart > localPart)
                        {
                            var result = MessageBox.Show($"There is a newer version of Talisman available ({responseText}).  Would you like to download it?",
                                "Talisman Version Check", MessageBoxButton.YesNo);
                            if (result == MessageBoxResult.Yes)
                            {
                                Process.Start("https://github.com/nebosite/talisman/tree/master/installs");
                            }
                            break;
                        }
                    }

                }
                catch (Exception e)
                {
                    Log.Warn("Error trying to check for a new version.", e);
                    Debug.WriteLine("Error trying to check version: " + e.ToString());
                }
            });
            
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// OnExit
        /// </summary>
        // --------------------------------------------------------------------------
        protected override void OnExit(ExitEventArgs e)
        {
            Settings.Default.CrashedLastTime = false;
            Settings.Default.Save();
            Log.Info("Talisman exiting cleanly.");
            Debug.WriteLine("Exit Settings: " + Settings.Default.Location);

        }
    }
}
