using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Represents a hotKey
    /// </summary>
    // --------------------------------------------------------------------------
    public class HotKeyOption : BaseModel
    {
        public string Name { get; set; }

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public  HotKeyOption(string name)
        {
            Name = name;
        }
    }
}
