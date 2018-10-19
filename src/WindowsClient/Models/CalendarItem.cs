using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// An Instance of a calendar 
    /// </summary>
    // --------------------------------------------------------------------------
    public class CalendarItem : BaseModel
    {
        public string EndPoint { get; set; }
        Action<string> DeleteMeHandler;

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public CalendarItem(string endPoint, Action<string> deleteMe)
        {
            EndPoint = endPoint;
            DeleteMeHandler = deleteMe;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        internal void DeleteMe()
        {
            DeleteMeHandler?.Invoke(EndPoint);
        }
    }
}
