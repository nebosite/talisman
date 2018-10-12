using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Talisman
{
    public delegate void NotificationHandler(NotificationData data);

    // --------------------------------------------------------------------------
    /// <summary>
    /// The Application Model
    /// </summary>
    // --------------------------------------------------------------------------
    public class AppModel : BaseModel
    {
        Timer _tickTimer; 

        /// <summary>
        /// Current Timer properties
        /// </summary>
        public TimeSpan TimeRemaining=> (_activeTimers.Count == 0) ? TimeSpan.Zero: DateTime.Now - _activeTimers[0].EndsAt;
        public string TimeRemainingText => TimeRemaining.ToString(@"hh\:mm\:ss\.f");
        public string TimerName => (_activeTimers.Count == 0) ? "No Timers Are Active." : _activeTimers[0].Name;

        /// <summary>
        /// Timer notifications
        /// </summary>
        public event NotificationHandler OnNotification;

        /// <summary>
        /// All the timers
        /// </summary>
        private List<TimerInstance> _activeTimers = new List<TimerInstance>();


        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public AppModel()
        {
            _tickTimer = new Timer();
            _tickTimer.Elapsed += TimerTick;
            _tickTimer.Interval = 100;
            _tickTimer.Start();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Do this while the timer is going
        /// </summary>
        // --------------------------------------------------------------------------
        private void TimerTick(object sender, ElapsedEventArgs e)
        {
            if (_activeTimers.Count == 0) return;

            var finishedTimers = _activeTimers.Where(t => t.EndsAt < DateTime.Now).ToArray();
            foreach(var timer in finishedTimers)
            {
                OnNotification.Invoke(new NotificationData($"Times up!  {timer.Name}"));
                _activeTimers.Remove(timer);
            }
            NotifyPropertyChanged(nameof(TimeRemaining));
            NotifyPropertyChanged(nameof(TimeRemainingText));
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a timer for some span of time
        /// </summary>
        // --------------------------------------------------------------------------
        internal void StartTimer(double minutes)
        {
            var endTime = DateTime.Now.AddMinutes(minutes);
            var timerName = $"QuickTimer {minutes.ToString(".0")} min";
            _activeTimers.Add(new TimerInstance(endTime, timerName));
        }
    }
}
