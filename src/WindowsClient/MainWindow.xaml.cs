using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace Talisman
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        double _xCorrection;
        double _yCorrection;

        AppModel _theModel = new AppModel();

        public MainWindow()
        {
            InitializeComponent();
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            this.Loaded += MainWindow_Loaded;
            this.DataContext = _theModel;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var source = PresentationSource.FromVisual((Window)sender);
            _xCorrection = 1.0/source.CompositionTarget.TransformToDevice.M11;
            _yCorrection = 1.0/source.CompositionTarget.TransformToDevice.M22;
            _settingsWindow = new SettingsForm(_theModel);
        }

        int frame = 0;
        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if(frame == 0)
            {
            }
            frame++;
            //if (frame % 3 == 0)
            //{
            //    this.Left -= 2;
            //    this.Top += 1;
            //    Glow.Opacity = (Math.Sin(frame / 20) + 1) / 2;
            //}
        }


        bool _dragging = false;
        double _dragDelta = 0;
        Point _lastMousePosition;
        private void HandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _dragging = true;
                _dragDelta = 0;
                _lastMousePosition = this.PointToScreen(Mouse.GetPosition(this));
                Stone.CaptureMouse();
            }
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                var newPosition = this.PointToScreen(Mouse.GetPosition(this));
                this.Left += (newPosition.X - _lastMousePosition.X) * _xCorrection;
                this.Top += (newPosition.Y - _lastMousePosition.Y) * _yCorrection;
                _dragDelta += (_lastMousePosition - newPosition).Length;
                _lastMousePosition = newPosition;

            }
        }

        Window _settingsWindow;
        private void HandleMouseUp(object sender, MouseButtonEventArgs e)
        {
            if(_dragDelta < 3)
            {
                _settingsWindow.Left = this.Left;
                _settingsWindow.Top = this.Top;
                _settingsWindow.Show();
            }
            _dragging = false;
            Stone.ReleaseMouseCapture();
        }



     
    }
}
