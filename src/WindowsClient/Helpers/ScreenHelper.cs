using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Base model for stuff that can be visualized
    /// </summary>
    // --------------------------------------------------------------------------
    public class ScreenHelper 
    {
        public static Screen MainScreen => Screen.AllScreens.Where(s => s.Primary).First();
    }
}
