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
        public int Id { get; set; }

        public string EndsAtText => EndsAt.ToString(@"hh\:mm tt");
        static int _idCounter = 0;
        Action<int> DeleteMeHandler;

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public TimerInstance(DateTime endTime, string timerName, Action<int> deleteMe)
        {
            EndsAt = endTime;
            Name = timerName;
            Id = _idCounter++;
            DeleteMeHandler = deleteMe;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        internal void DeleteMe()
        {
            DeleteMeHandler?.Invoke(Id);
        }
    }
}
