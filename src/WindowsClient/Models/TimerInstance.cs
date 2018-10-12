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
    class TimerInstance : BaseModel
    {
        public DateTime EndsAt { get; set; }
        public string Name { get; set; }

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
