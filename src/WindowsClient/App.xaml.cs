﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
            var talismanProcesses = Process.GetProcessesByName("Talisman");
            if(talismanProcesses.Length > 1)
            {
                Application.Current.Shutdown();
            }
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            if(Settings.Default.CurrentVersion != assemblyVersion)
            {
                Settings.Default.Upgrade();
                Settings.Default.Reload();
                Settings.Default.CurrentVersion = assemblyVersion;
                Settings.Default.Save();
            }

            base.OnStartup(e);
        }
    }
}
