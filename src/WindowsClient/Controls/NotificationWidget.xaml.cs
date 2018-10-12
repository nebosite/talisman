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
using System.Windows.Shapes;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The Notification widget is the thing that flies around that you have
    /// to click on to kill
    /// </summary>
    // --------------------------------------------------------------------------
    public partial class NotificationWidget : Window
    {
        NotificationData _data;

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public NotificationWidget(NotificationData data)
        {
            InitializeComponent();
            this.DataContext = data;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Click handling
        /// </summary>
        // --------------------------------------------------------------------------
        private void HandleClick(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }
    }
}
