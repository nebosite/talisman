using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The Application Model
    /// </summary>
    // --------------------------------------------------------------------------
    public class AppModel : BaseModel
    {
        DateTime _endTime;
        Timer _tickTimer; 

        /// <summary>
        /// Time remaining on current timer
        /// </summary>
        public TimeSpan TimeRemaining
        {
            get
            {
                var remaining = _endTime - DateTime.Now;
                if (remaining.TotalSeconds < 0) remaining = TimeSpan.Zero;
                return remaining;
            }
        }

        /// <summary>
        /// Text view of time remaining
        /// </summary>
        public string TimeRemainingText => TimeRemaining.ToString(@"hh\:mm\:ss");

        /// <summary>
        /// Name of the current active timer
        /// </summary>
        string _timerName = "No Timers Are Active.";
        public string TimerName
        {
            get => _timerName;
            set
            {
                _timerName = value;
                if(_timerName == null) _timerName = "No Timers Are Active.";
                NotifyPropertyChanged(nameof(TimerName));
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public AppModel()
        {
            _tickTimer = new Timer();
            _tickTimer.Elapsed += TimerTick;
            _tickTimer.Interval = 200;
            _tickTimer.Start();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Do this while the timer is going
        /// </summary>
        // --------------------------------------------------------------------------
        private void TimerTick(object sender, ElapsedEventArgs e)
        {
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
            _endTime = DateTime.Now.AddMinutes(minutes);
            TimerName = $"QuickTimer {minutes.ToString(".0")} min";
        }
    }
}
