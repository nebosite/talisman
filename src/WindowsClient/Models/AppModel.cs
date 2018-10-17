using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public TimeSpan CurrentTimeRemaining=> (ActiveTimers.Count == 0) ? TimeSpan.Zero: DateTime.Now - ActiveTimers[0].EndsAt;
        public string CurrentTimeRemainingText => CurrentTimeRemaining.ToString(@"hh\:mm\:ss\.f");
        public string CurrentTimerName => (ActiveTimers.Count == 0) ? "No Timers Are Active." : ActiveTimers[0].Name + $" ({ActiveTimers[0].EndsAt.ToString(@"hh\:mm tt")})";


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

        string _customQuickTime = "60";
        public string CustomQuickTime
        {
            get => _customQuickTime;
            set
            {
                _customQuickTime = value;
                NotifyPropertyChanged(nameof(CustomQuickTime));
            }
        }

        /// <summary>
        /// Timer notifications
        /// </summary>
        public event NotificationHandler OnNotification;

        /// <summary>
        /// All the timers
        /// </summary>
        public ObservableCollection<TimerInstance> ActiveTimers { get; set; } = new ObservableCollection<TimerInstance>();

        Action<Action> _dispatch;
        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public AppModel(Action<Action> dispatch)
        {
            _dispatch = dispatch;
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
            if (ActiveTimers.Count == 0) return;

            var finishedTimers = ActiveTimers.Where(t => t.EndsAt < DateTime.Now).ToArray();
            foreach(var timer in finishedTimers)
            {
                OnNotification.Invoke(new NotificationData($"Times up!  {timer.Name}"));
                _dispatch(() =>
                {
                    ActiveTimers.Remove(timer);

                });
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
            var timerName = $"{QuickTimerName} [{minutes.ToString(".0")} min]";
            StartTimer(endTime, timerName);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a timer at some absolute time
        /// </summary>
        // --------------------------------------------------------------------------
        internal void StartTimer(DateTime endTime)
        {
            var timerName = $"{QuickTimerName} [{endTime.ToString(@"hh\:mm tt")}]";
            StartTimer(endTime, timerName);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a timer at some absolute time
        /// </summary>
        // --------------------------------------------------------------------------
        internal void StartTimer(DateTime endTime, string timerName)
        {
            var newTimer = new TimerInstance(endTime, timerName);
            for(int i = 0; i < ActiveTimers.Count; i++)
            {
                if(newTimer.EndsAt < ActiveTimers[i].EndsAt)
                {
                    ActiveTimers.Insert(i, newTimer);
                    newTimer = null;
                    break;
                }
            }
            if(newTimer != null) ActiveTimers.Add(newTimer);
            NotifyAllPropertiesChanged();
        }
    }
}
