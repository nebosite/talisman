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

        DateTime _startTime = DateTime.Now;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Click handling
        /// </summary>
        // --------------------------------------------------------------------------
        public void Animate()
        {
            var delta = (Math.Cos((DateTime.Now - _startTime).TotalSeconds * 5) + 1) / 2;
            byte bigDelta = (byte)(255 * delta);
            byte partDelta = (byte)(127 * delta);

            var brush = new SolidColorBrush(Color.FromArgb(127, partDelta, bigDelta, bigDelta));
            MyBorder.BorderBrush = brush;
        }
    }
}
