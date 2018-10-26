using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Talisman.Properties;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The Application
    /// </summary>
    // --------------------------------------------------------------------------
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Settings.Default.Upgrade();
            Settings.Default.Reload();

            base.OnStartup(e);
        }
    }
}
