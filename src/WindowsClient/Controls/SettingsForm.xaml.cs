using System;
using System.Collections.Generic;
using System.ComponentModel;
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
            this.IsVisibleChanged += SettingsForm_IsVisibleChanged;
        }


        bool _justActivated = false;
        // --------------------------------------------------------------------------
        /// <summary>
        /// When we get shown
        /// </summary>
        // --------------------------------------------------------------------------
        private void SettingsForm_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(_justActivated)
            {
                Debug.WriteLine("Focusing");
                TimerNameBox.Focus();
                TimerNameBox.SelectAll();
                _justActivated = false;
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Bring up the form for User input 
        /// </summary>
        // --------------------------------------------------------------------------
        public void Popup()
        {
            _justActivated = true;
            Show();
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

            var minutes = 10.0;
            if(button.Tag.ToString() == "Custom")
            {
                double.TryParse(_appModel.CustomQuickTime, out minutes);
            }
            else
            {
                double.TryParse(button.Tag.ToString(), out minutes);
            }
            
            _appModel.StartTimer(minutes);
            this.Hide();
        }
    }
}
