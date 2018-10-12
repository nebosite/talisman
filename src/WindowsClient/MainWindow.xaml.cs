﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        AppModel _theModel = new AppModel();

        /// <summary>
        /// The settings window
        /// </summary>
        Window _settingsWindow;

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public MainWindow()
        {
            InitializeComponent();
            CompositionTarget.Rendering += AnimateFrame;
            this.Loaded += MainWindow_Loaded;
            this.DataContext = _theModel;
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

            var locationSetting = Settings.Default.Location;
            if(!string.IsNullOrEmpty(locationSetting))
            {
                var location = JsonConvert.DeserializeObject<Point>(locationSetting);
                this.Left = location.X;
                this.Top = location.Y;
            }
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

        // --------------------------------------------------------------------------
        /// <summary>
        /// Animations Handled here
        /// </summary>
        // --------------------------------------------------------------------------
        private void AnimateFrame(object sender, EventArgs e)
        {
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
                _settingsWindow.Show();
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
