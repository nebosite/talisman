using System;
using System.Collections.Generic;
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
    /// Interaction logic for TimerDetailsWidget.xaml
    /// </summary>
    public partial class TimerDetailsWidget : UserControl
    {
        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public TimerDetailsWidget()
        {
            InitializeComponent();
        }

        TimerInstance Context => (TimerInstance)this.DataContext;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Move the time earlier by one minute
        /// </summary>
        // --------------------------------------------------------------------------
        private void AdjustSmallerClick(object sender, RoutedEventArgs e)
        {
            Context.EndsAt = Context.EndsAt.AddMinutes(-1);
            Context.NotifyAllPropertiesChanged();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Move the time later by one minute
        /// </summary>
        // --------------------------------------------------------------------------
        private void AdjustLargerClick(object sender, RoutedEventArgs e)
        {
            Context.EndsAt = Context.EndsAt.AddMinutes(1);
            Context.NotifyAllPropertiesChanged();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Delete this timer
        /// </summary>
        // --------------------------------------------------------------------------
        private void DeleteClick(object sender, RoutedEventArgs e)
        {
            Context.DeleteMe();
        }
    }
}
