﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Interaction logic for SettingsForm.xaml
    /// </summary>
    // --------------------------------------------------------------------------
    public partial class SettingsForm : Window
    {
        AppModel _appModel;

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public SettingsForm(AppModel appModel)
        {
            _appModel = appModel;
            InitializeComponent();
            this.DataContext = appModel;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Closing 
        /// </summary>
        // --------------------------------------------------------------------------
        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a quick timer
        /// </summary>
        // --------------------------------------------------------------------------
        private void QuickTimerClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            _appModel.StartTimer(double.Parse(button.Tag.ToString()));
            this.Hide();
        }
    }
}
