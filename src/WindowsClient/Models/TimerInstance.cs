using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// An Instance of a timer 
    /// </summary>
    // --------------------------------------------------------------------------
    public class TimerInstance : BaseModel
    {
        public DateTime EndsAt { get; set; }
        public string Name { get; set; }

        public string EndsAtText => EndsAt.ToString(@"hh\:mm tt");

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public TimerInstance(DateTime endTime, string timerName)
        {
            EndsAt = endTime;
            Name = timerName;
        }
    }
}
