﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Talisman
{
    /// <summary>
    /// Interaction logic for ReminderSummary.xaml
    /// </summary>
    public partial class ReminderSummary : UserControl
    {
        public ReminderSummary()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.ToString());
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            (this.DataContext as TimerInstance)?.Dismiss();
        }
    }
}
