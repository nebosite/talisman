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
    /// Base model for stuff that can be visualized
    /// </summary>
    // --------------------------------------------------------------------------
    public class BaseModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // --------------------------------------------------------------------------
        /// <summary>
        /// NotifyPropertyChanged
        /// </summary>
        // --------------------------------------------------------------------------
        public void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
