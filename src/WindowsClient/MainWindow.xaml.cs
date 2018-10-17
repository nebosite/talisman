using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using Talisman.Properties;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The main interaction window
    /// </summary>
    // --------------------------------------------------------------------------
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Correction values for dealing with magnified screens
        /// </summary>
        double _xCorrection;
        double _yCorrection;

        /// <summary>
        /// App model
        /// </summary>
        AppModel _theModel;

        /// <summary>
        /// The settings window
        /// </summary>
        SettingsForm _settingsWindow;

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public MainWindow()
        {
            _theModel = new AppModel((runme) =>
            {
                Dispatcher.Invoke(runme);
            });

            InitializeComponent();
            CompositionTarget.Rendering += AnimateFrame;
            this.Loaded += MainWindow_Loaded;
            this.DataContext = _theModel;
            _theModel.OnNotification += HandleNewNotification;
        }

        List<NotificationWidget> _notificationWindows = new List<NotificationWidget>();
        // --------------------------------------------------------------------------
        /// <summary>
        /// Notification handling - make a little animation to alert the user
        /// </summary>
        // --------------------------------------------------------------------------
        private void HandleNewNotification(NotificationData data)
        {
            Dispatcher.InvokeAsync(() =>
            {
                var newWidget = new NotificationWidget(data);
                newWidget.Top = Top;
                newWidget.Left = Left;
                newWidget.Closing += (sender, args) =>
                {
                    lock(_notificationWindows)
                    {
                        _notificationWindows.Remove(newWidget);
                    }
                };
                newWidget.Show();
                lock(_notificationWindows)
                {
                    _notificationWindows.Add(newWidget);
                }
            });
       }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Stuff to do when we know about the display mode
        /// </summary>
        // --------------------------------------------------------------------------
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var source = PresentationSource.FromVisual((Window)sender);
            _xCorrection = 1.0/source.CompositionTarget.TransformToDevice.M11;
            _yCorrection = 1.0/source.CompositionTarget.TransformToDevice.M22;
            _settingsWindow = new SettingsForm(_theModel);

            // For some reason, need to do this to see the ticks on the time picker
            _settingsWindow.Show();
            _settingsWindow.Hide();

            var locationSetting = Settings.Default.Location;
            if(!string.IsNullOrEmpty(locationSetting))
            {
                var location = JsonConvert.DeserializeObject<Point>(locationSetting);
                this.Left = location.X;
                this.Top = location.Y;
            }

            var screenArea = ScreenHelper.MainScreen.WorkingArea;
            _gravitationCenter = new Point(screenArea.Left + screenArea.Width/2, screenArea.Top + screenArea.Height/2);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Stuff to do when we are done
        /// </summary>
        // --------------------------------------------------------------------------
        protected override void OnClosing(CancelEventArgs e)
        {
            Settings.Default.Save();
        }

        DateTime _startTime = DateTime.Now;
        Point _gravitationCenter;
        int _frame = 0;
        int _frameSkip = 3;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Animations Handled here
        /// </summary>
        // --------------------------------------------------------------------------
        private void AnimateFrame(object sender, EventArgs e)
        {
            _frame++;
            if (_frame % _frameSkip != 0) return;

            var stepSize = 1;
            var radius = 400;

            var t = (DateTime.Now - _startTime).TotalSeconds;


            NotificationWidget[] itemsToMove;
            lock(_notificationWindows)
            {
                itemsToMove = _notificationWindows.ToArray();
            }

            var theta = 0.0;
            var thetaDelta = itemsToMove.Length > 0 ? (Math.PI * 2) / itemsToMove.Length : 1;
            foreach (var moveMe in itemsToMove)
            {
                moveMe.Animate();
                var thisTheta = theta + t / 15;
                /// Move the target center around in a big circle
                var cx = (_gravitationCenter.X + radius * Math.Cos(thisTheta)) * _xCorrection;
                var cy =( _gravitationCenter.Y + radius * Math.Sin(thisTheta)) * _yCorrection;

                var wx = moveMe.Left + moveMe.ActualWidth / 2;
                var wy = moveMe.Top + moveMe.ActualHeight / 2;
                var deltaVector = new Vector(cx - wx, cy - wy);


                if (deltaVector.Length > stepSize * 50)
                {
                    deltaVector = (deltaVector / deltaVector.Length) * stepSize * 50;
                }
                moveMe.Left += deltaVector.X;
                moveMe.Top += deltaVector.Y;
                theta += thetaDelta;
            }
        }


        bool _dragging = false;
        double _dragDelta = 0;
        Point _lastMousePosition;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Mouse Down
        /// </summary>
        // --------------------------------------------------------------------------

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

        // --------------------------------------------------------------------------
        /// <summary>
        /// Mouse Move
        /// </summary>
        // --------------------------------------------------------------------------
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

        // --------------------------------------------------------------------------
        /// <summary>
        /// Mouse Up
        /// </summary>
        // --------------------------------------------------------------------------
        private void HandleMouseUp(object sender, MouseButtonEventArgs e)
        {
            if(_dragDelta < 3)
            {
                _settingsWindow.Left = this.Left;
                _settingsWindow.Top = this.Top;
                _settingsWindow.Popup();
            }
            else if(_dragging)
            {
                Settings.Default.Location = JsonConvert.SerializeObject(new Point(Left, Top));
                Settings.Default.Save();
            }
            _dragging = false;
            Stone.ReleaseMouseCapture();
        }


        
     
    }
}
