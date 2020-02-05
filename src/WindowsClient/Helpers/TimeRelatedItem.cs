using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Generic representation of something like a calendar item
    /// </summary>
    // --------------------------------------------------------------------------
    public class TimeRelatedItem
    {
        public string Title { get; internal set; }
        public DateTime Start { get; internal set; }
        public DateTime End { get; internal set; }
        public string Location { get; internal set; }
        public bool Recurring { get; internal set; }
        public string Contents { get; internal set; }
    }


}
