﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
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
        /// App model
        /// </summary>
        AppModel _theModel;

        /// <summary>
        /// The settings window
        /// </summary>
        SettingsForm _settingsWindow;

        /// <summary>
        /// Handles window dragging
        /// </summary>
        DraggingLogic _draggingLogic;


        List<double> _emptyNotificationLocations = new List<double>();

        List<NotificationWidget> _notificationWindows = new List<NotificationWidget>();
        Random rand = new Random();

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
            _theModel.OnCenter += HandleOnCenter;

            InitializeComponent();
            CompositionTarget.Rendering += AnimateFrame;
            this.Loaded += MainWindow_Loaded;
            this.DataContext = _theModel;
            _theModel.OnNotification += HandleNewNotification;

            int maxLocations = 10;
            var thetaSlice = (Math.PI * 2) / maxLocations;
            for (int i = 0; i < maxLocations; i++)
            {
                _emptyNotificationLocations.Add(thetaSlice * i);
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Center the talisman
        /// </summary>
        // --------------------------------------------------------------------------
        private void HandleOnCenter()
        {
            ScreenHelper.CenterWindowOnMainScreen(this);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Notification handling - make a little animation to alert the user
        /// </summary>
        // --------------------------------------------------------------------------
        private void HandleNewNotification(TimerInstance data)
        {
            Dispatcher.InvokeAsync(() =>
            {
                double theta = rand.NextDouble() * Math.PI * 2;
                lock(_emptyNotificationLocations)
                {
                    if(_emptyNotificationLocations.Count > 0)
                    {
                        int pick = rand.Next(_emptyNotificationLocations.Count);
                        theta = _emptyNotificationLocations[pick];
                        _emptyNotificationLocations.RemoveAt(pick);
                    }
                }
                var newWidget = new NotificationWidget(data, _theModel, theta);

                var screenArea = ScreenHelper.MainScreen.WorkingArea;
                newWidget.Center = new Point(screenArea.Left + screenArea.Width / 2, screenArea.Top + screenArea.Height / 2);

                if(newWidget.Center.X == 0)
                {
                    newWidget.Center = new Point(1200,800);
                }

                newWidget.Closing += (sender, args) =>
                {
                    lock(_notificationWindows)
                    {
                        _notificationWindows.Remove(newWidget);
                    }

                    lock(_emptyNotificationLocations)
                    {
                        _emptyNotificationLocations.Add(newWidget.LocationTheta);
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
        /// Once we have a window handle, register for hot keys
        /// </summary>
        // --------------------------------------------------------------------------
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _theModel.InitHotKeys(new HotKeyHelper(this));
        }

        Point? _newLocation = null;
        // --------------------------------------------------------------------------
        /// <summary>
        /// OnDpiChanged
        /// </summary>
        // --------------------------------------------------------------------------
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            // When placing the window for the first time, it won't know about the 
            // DPI, so we need to place it again to make sure position accounts for
            // the correct DPI
            if (_newLocation != null)
            {
                Left = _newLocation.Value.X;
                Top = _newLocation.Value.Y;
                _newLocation = null;
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Stuff to do when we know about the display mode
        /// </summary>
        // --------------------------------------------------------------------------
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _draggingLogic = new DraggingLogic(this, StoneArea);
            Debug.WriteLine("MainWindowLoaded Settings: Entry: " + Settings.Default.Location);

            _draggingLogic.OnPositionChanged += (xm, ym) =>
            {
                _newLocation = null;
                Settings.Default.Location = JsonConvert.SerializeObject(new Point((int)Left, (int)Top));
                Settings.Default.Save();
            };
            _draggingLogic.OnClick += () =>
            {
                // Try to position the settngs window well inside the main screen
                _settingsWindow.Left = this.Left;
                _settingsWindow.Top = this.Top;
                if (this.Left > ScreenHelper.MainScreen.WorkingArea.Left + ScreenHelper.MainScreen.WorkingArea.Width / 2)
                {
                    _settingsWindow.Left = this.Left + this.Width - _settingsWindow.Width;
                }
                if (this.Top > ScreenHelper.MainScreen.WorkingArea.Top + ScreenHelper.MainScreen.WorkingArea.Height / 2)
                {
                    _settingsWindow.Top = this.Top + this.Height - _settingsWindow.Height;
                }
                _settingsWindow.Popup();
            };

            _settingsWindow = new SettingsForm(_theModel);
            _settingsWindow.Left = this.Left;
            _settingsWindow.Top = this.Top;


            // For some reason, need to do this to see the ticks on the time picker
            _settingsWindow.Show();
            _settingsWindow.Hide();

            var locationSetting = "\"1000,800\""; // Settings.Default.Location;
            var resetLocation = false;
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                Debug.WriteLine("Resetting Location");
                // Hold down cntrl key on enter to reset location of new notifications
                resetLocation = true;
            }
            if (!Settings.Default.CrashedLastTime && !resetLocation)
            {
                if(!string.IsNullOrEmpty(Settings.Default.Location))
                {
                   Debug.WriteLine("Reading Location");
                   locationSetting = Settings.Default.Location;
                }
            }

            Debug.WriteLine("Location: " + locationSetting);

            Settings.Default.CrashedLastTime = true;
            Settings.Default.Save();
            Debug.WriteLine("MainWindow_Loaded Exit Settings: " + Settings.Default.Location);

            _newLocation = JsonConvert.DeserializeObject<Point>(locationSetting);
            Left = _newLocation.Value.X;
            Top = _newLocation.Value.Y;

            //ScreenHelper.EnsureWindowIsVisible(this);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Stuff to do when we are done
        /// </summary>
        // --------------------------------------------------------------------------
        protected override void OnClosing(CancelEventArgs e)
        {
            Settings.Default.Save();
            Debug.WriteLine("Closing Settings: " + Settings.Default.Location);

            _settingsWindow?.CloseForReal();
            foreach(var notificationWindow in _notificationWindows.ToArray())
            {
                notificationWindow.Close();
            }
        }

        DateTime _startTime = DateTime.Now;
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

            foreach (var moveMe in itemsToMove)
            {
                moveMe.Animate();
                var thisTheta = moveMe.LocationTheta + t / 15;
                /// Move the target center around in a big circle
                var cx = (moveMe.Center.X + radius * Math.Cos(thisTheta)) * _draggingLogic.DpiCorrectionX;
                var cy =(moveMe.Center.Y + radius * Math.Sin(thisTheta)) * _draggingLogic.DpiCorrectionY;

                var wx = moveMe.Left + moveMe.ActualWidth / 2;
                var wy = moveMe.Top + moveMe.ActualHeight / 2;
                var deltaVector = new Vector(cx - wx, cy - wy);


                if (deltaVector.Length > stepSize * 50)
                {
                    deltaVector = (deltaVector / deltaVector.Length) * stepSize * 50;
                }
                moveMe.Left += deltaVector.X;
                moveMe.Top += deltaVector.Y;
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Bye bye
        /// </summary>
        // --------------------------------------------------------------------------
        private void ExitAppClicked(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// kill all the reminder windows in one shot
        /// </summary>
        // --------------------------------------------------------------------------
        private void DismissFloaters(object sender, RoutedEventArgs e)
        {
            foreach(var floater in _notificationWindows.ToArray())
            {
                floater.Close();
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Clear the stopwatch
        /// </summary>
        // --------------------------------------------------------------------------
        private void StopwatchDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            _theModel.ClearStopWatch();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Toggle the stopwatch
        /// </summary>
        // --------------------------------------------------------------------------
        private void StopwatchClicked(object sender, MouseButtonEventArgs e)
        {
            _theModel.ToggleStopWatch();
        }
    }
}
