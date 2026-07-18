using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Drives a Pomodoro day on the UI: owns the session and a tick timer, shows
    /// the floating current-task widget beneath the talisman, and surfaces the
    /// start-phase prompt and end-of-day summary dialogs. The session itself holds
    /// all the rules; this class is just the glue to WPF.
    /// </summary>
    // --------------------------------------------------------------------------
    public class PomodoroController : BaseModel
    {
        readonly Window _owner;
        readonly PomodoroSettings _settings;
        readonly Action<string> _onTaskTimedOut;
        readonly DispatcherTimer _timer;
        readonly TaskTimeoutTracker _timeoutTracker = new TaskTimeoutTracker();

        PomodoroSession _session;
        PomodoroTaskWindow _taskWindow;
        PomodoroPromptWindow _promptWindow;
        PomodoroState _prevState = PomodoroState.NotStarted;

        // Bindable display for the task widget.
        public string CurrentTaskTitle { get; private set; } = "";
        public string TimerText { get; private set; } = "";
        public string PhaseLabel { get; private set; } = "";
        public Brush TimerBrush { get; private set; } = Brushes.White;

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor. <paramref name="owner"/> is used only to position the task widget.
        /// <paramref name="onTaskTimedOut"/> is invoked with the task title when a
        /// short-phase task's countdown first hits zero (to raise a floating nag).
        /// </summary>
        // --------------------------------------------------------------------------
        public PomodoroController(Window owner, PomodoroSettings settings, Action<string> onTaskTimedOut = null)
        {
            _owner = owner;
            _settings = settings;
            _onTaskTimedOut = onTaskTimedOut;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _timer.Tick += (s, e) => OnTick();
        }

        public bool IsRunning =>
            _session != null && _session.State != PomodoroState.NotStarted && _session.State != PomodoroState.Finished;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Begin (or restart) a Pomodoro day.
        /// </summary>
        // --------------------------------------------------------------------------
        public void Start()
        {
            if (IsRunning)
            {
                _taskWindow?.Activate();
                return;
            }

            try
            {
                _session = _settings.CreateSession();
                _prevState = PomodoroState.NotStarted;
                _session.Start(DateTime.Now);
                _timer.Start();
                Log.Info("Pomodoro day started.");
                HandleState();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to start Pomodoro session.", ex);
                MessageBox.Show("Could not start the Pomodoro session. See the log for details.", "Talisman");
            }
        }

        public void Done()
        {
            _session?.Done(DateTime.Now);
            HandleState();
        }

        public void Defer()
        {
            _session?.Defer(DateTime.Now);
            HandleState();
        }

        void OnTick()
        {
            if (_session == null) return;
            _session.Tick(DateTime.Now);
            HandleState();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// React to the current session state: refresh the widget, and on a state
        /// transition show/hide the right windows. Guarded so the tick loop never
        /// re-opens a dialog that is already up.
        /// </summary>
        // --------------------------------------------------------------------------
        void HandleState()
        {
            if (_session == null) return;
            var state = _session.State;

            RefreshDisplay();

            if (state != _prevState)
            {
                switch (state)
                {
                    case PomodoroState.JoyPrompt:
                        ShowPrompt("Nice work! Click start to begin the joy time.");
                        break;
                    case PomodoroState.AdminPrompt:
                        ShowPrompt("Joy time is over. Click start to begin the admin time.");
                        break;
                    case PomodoroState.ShortEasy:
                    case PomodoroState.Extra:
                    case PomodoroState.Joy:
                    case PomodoroState.Admin:
                        ClosePrompt();
                        ShowTaskWindow();
                        break;
                    case PomodoroState.Finished:
                        Finish();
                        break;
                }
                _prevState = state;
            }

            if (_session.IsTaskActive) PositionTaskWindow();
        }

        void RefreshDisplay()
        {
            var now = DateTime.Now;
            var task = _session.CurrentTask;
            var timedOut = false;

            CurrentTaskTitle = task?.Title ?? "";
            PhaseLabel = PhaseName(_session.CurrentPhaseKind);

            if (task == null)
            {
                TimerText = "";
            }
            else if (_session.IsCountdownPhase)
            {
                var remaining = _session.CurrentTaskRemaining(now);
                timedOut = remaining <= TimeSpan.Zero;
                TimerText = FormatSpan(remaining);
                TimerBrush = timedOut ? Brushes.Orange : Brushes.White;
            }
            else
            {
                TimerText = FormatSpan(_session.CurrentTaskElapsed(now));
                TimerBrush = Brushes.Black;
            }

            NotifyAllPropertiesChanged();

            // When a short-phase task first runs out of time, raise a circling
            // notification (once per task occurrence).
            if (_timeoutTracker.ShouldNotify(task, timedOut) && task != null)
            {
                Log.Info("Pomodoro task timed out: " + task.Title);
                _onTaskTimedOut?.Invoke(task.Title);
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Format a span as [-]H:MM:SS (hours omitted when zero). Negative values
        /// (short-phase overtime) are shown with a leading minus.
        /// </summary>
        // --------------------------------------------------------------------------
        static string FormatSpan(TimeSpan span)
        {
            var sign = span < TimeSpan.Zero ? "-" : "";
            var s = span.Duration();
            return s.Hours > 0
                ? $"{sign}{s.Hours}:{s.Minutes:00}:{s.Seconds:00}"
                : $"{sign}{s.Minutes}:{s.Seconds:00}";
        }

        static string PhaseName(PomodoroPhaseKind? kind)
        {
            switch (kind)
            {
                case PomodoroPhaseKind.ShortEasy: return "Short & Easy";
                case PomodoroPhaseKind.Joy: return "Joy Time";
                case PomodoroPhaseKind.Admin: return "Admin Time";
                case PomodoroPhaseKind.Extra: return "Extra";
                default: return "";
            }
        }

        void ShowTaskWindow()
        {
            if (_taskWindow == null)
            {
                _taskWindow = new PomodoroTaskWindow(this);
                _taskWindow.Closed += (s, e) => _taskWindow = null;
            }
            if (!_taskWindow.IsVisible) _taskWindow.Show();
            PositionTaskWindow();
        }

        void PositionTaskWindow()
        {
            if (_taskWindow == null || _owner == null) return;
            var width = _taskWindow.Width;
            _taskWindow.Left = _owner.Left + (_owner.Width / 2) - (width / 2);
            _taskWindow.Top = _owner.Top + _owner.Height - 20;
        }

        void ShowPrompt(string message)
        {
            if (_taskWindow != null && _taskWindow.IsVisible) _taskWindow.Hide();

            ClosePrompt();
            _promptWindow = new PomodoroPromptWindow(message, () =>
            {
                _session.BeginPromptedPhase(DateTime.Now);
                HandleState();
            });
            _promptWindow.Closed += (s, e) => _promptWindow = null;
            PositionWindowUnderOwner(_promptWindow);
            _promptWindow.Show();
        }

        void ClosePrompt()
        {
            if (_promptWindow != null)
            {
                var w = _promptWindow;
                _promptWindow = null;
                w.Close();
            }
        }

        void PositionWindowUnderOwner(Window w)
        {
            if (_owner == null) return;
            w.Left = _owner.Left + (_owner.Width / 2) - (w.Width / 2);
            w.Top = _owner.Top + _owner.Height - 20;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// End the day: stop the timer, tear down the widget, and show the summary.
        /// </summary>
        // --------------------------------------------------------------------------
        void Finish()
        {
            _timer.Stop();
            ClosePrompt();
            if (_taskWindow != null)
            {
                var w = _taskWindow;
                _taskWindow = null;
                w.Close();
            }

            Log.Info("Pomodoro day finished.");
            var summary = new PomodoroSummaryWindow(BuildSummary());
            PositionWindowUnderOwner(summary);
            summary.Show();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Build the human-readable end-of-day summary from the phase results.
        /// </summary>
        // --------------------------------------------------------------------------
        public string BuildSummary()
        {
            var now = DateTime.Now;
            var sb = new StringBuilder();
            sb.AppendLine("Pomodoro day summary");
            sb.AppendLine("====================");
            sb.AppendLine();

            var totalDone = 0;
            foreach (var r in _session.Results)
            {
                totalDone += r.DoneTasks.Count;
                sb.AppendLine($"{PhaseName(r.Kind)}  ({(int)r.Duration(now).TotalMinutes} min)");
                sb.AppendLine($"  Completed: {r.DoneTasks.Count}   Deferred: {r.DeferredCount}");
                foreach (var t in r.DoneTasks) sb.AppendLine($"    ✓ {t}");
                sb.AppendLine();
            }

            sb.AppendLine($"Total tasks completed: {totalDone}");
            return sb.ToString();
        }
    }
}
