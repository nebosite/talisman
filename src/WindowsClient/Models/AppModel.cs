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
        public TimeSpan CurrentTimeRemaining=> (_activeTimers.Count == 0) ? TimeSpan.Zero: DateTime.Now - _activeTimers[0].EndsAt;
        public string CurrentTimeRemainingText => CurrentTimeRemaining.ToString(@"hh\:mm\:ss\.f");
        public string CurrentTimerName => (_activeTimers.Count == 0) ? "No Timers Are Active." : _activeTimers[0].Name;


        string _quickTimerName = "Quick Timer";
        public string QuickTimerName
        {
            get => _quickTimerName;
            set
            {
                _quickTimerName = value;
                NotifyPropertyChanged(nameof(QuickTimerName));
            }
        }

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
            NotifyPropertyChanged(nameof(CurrentTimeRemaining));
            NotifyPropertyChanged(nameof(CurrentTimeRemainingText));
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a timer for some span of time
        /// </summary>
        // --------------------------------------------------------------------------
        internal void StartTimer(double minutes)
        {
            var endTime = DateTime.Now.AddMinutes(minutes);
            var timerName = $"{QuickTimerName} {minutes.ToString(".0")} min, ({endTime.ToString(@"hh\:mm tt")})";
            _activeTimers.Add(new TimerInstance(endTime, timerName));
            NotifyAllPropertiesChanged();
        }
    }
}
