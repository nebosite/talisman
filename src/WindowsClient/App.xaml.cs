using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
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
        // --------------------------------------------------------------------------
        /// <summary>
        /// OnStartup
        /// </summary>
        // --------------------------------------------------------------------------
        protected override void OnStartup(StartupEventArgs e)
        {
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

            base.OnStartup(e);
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
            Debug.WriteLine("Exit Settings: " + Settings.Default.Location);

        }
    }
}
