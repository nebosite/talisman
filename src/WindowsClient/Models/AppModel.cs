using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Talisman
{
    public class AppModel : BaseModel
    {
        DateTime _endTime;
        Timer _tickTimer; 


        public TimeSpan TimeRemaining
        {
            get
            {
                var remaining = _endTime - DateTime.Now;
                if (remaining.TotalSeconds < 0) remaining = TimeSpan.Zero;
                return remaining;
            }
        }

        public string TimeRemainingText => TimeRemaining.ToString(@"hh\:mm\:ss");

        public AppModel()
        {
            _tickTimer = new Timer();
            _tickTimer.Elapsed += _tickTimer_Elapsed;
            _tickTimer.Interval = 200;
            _tickTimer.Start();
        }

        private void _tickTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            NotifyPropertyChanged(nameof(TimeRemaining));
            NotifyPropertyChanged(nameof(TimeRemainingText));
        }

        internal void StartTimer(double minutes)
        {
            _endTime = DateTime.Now.AddMinutes(minutes);   
        }
    }
}
