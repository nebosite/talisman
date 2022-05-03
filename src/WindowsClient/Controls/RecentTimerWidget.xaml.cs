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
    /// Interaction logic for RecentTimerWidget.xaml
    /// </summary>
    public partial class RecentTimerWidget : UserControl
    {
        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public RecentTimerWidget()
        {
            InitializeComponent();
        }

        TimerInstance Context => (TimerInstance)this.DataContext;

        // --------------------------------------------------------------------------
        /// <summary>
        /// PromoteMe this timer
        /// </summary>
        // --------------------------------------------------------------------------
        private void PromoteClick(object sender, RoutedEventArgs e)
        {
            Context.PromoteMe();
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
