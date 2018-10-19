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
    /// Interaction logic for AddInternetCalendarDialog.xaml
    /// </summary>
    public partial class AddInternetCalendarDialog : Window
    {
        public string CalendarUrl { get; internal set; }

        public AddInternetCalendarDialog()
        {
            InitializeComponent();
            this.DataContext = this;
        }

    }
}
