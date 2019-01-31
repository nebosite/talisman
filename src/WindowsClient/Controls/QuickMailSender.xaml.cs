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
    /// <summary>
    /// Interaction logic for QuickMailSender.xaml
    /// </summary>
    public partial class QuickMailSender : Window
    {
        QuickMailItem _mailItem;
        public QuickMailSender(QuickMailItem mailItem)
        {
            InitializeComponent();
            DataContext = _mailItem = mailItem;
            this.Loaded += QuickMailSender_Loaded;
            this.Activated += QuickMailSender_Activated;
        }

        private void QuickMailSender_Activated(object sender, EventArgs e)
        {
            BodyTextBox.Focus();
        }

        private void QuickMailSender_Loaded(object sender, RoutedEventArgs e)
        {
            BodyTextBox.SelectAll();
            BodyTextBox.Focus();
        }

        private void PreviewBodyKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                _mailItem.Send();
                Close();
            }
        }
    }
}
