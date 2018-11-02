using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace Talisman
{

    // --------------------------------------------------------------------------
    /// <summary>
    /// Enables dragging of windows that is DPI sensitive
    /// </summary>
    // --------------------------------------------------------------------------
    public class DraggingLogic 
    {
        bool _dragging = false;
        double _dragDelta = 0;
        Point _lastMousePosition;

        /// <summary>
        /// Correction values for dealing with magnified screens
        /// </summary>
        public double DpiCorrectionX { get; set; }
        public double DpiCorrectionY { get; set; }

        public event Action<double, double> OnPositionChanged;
        public event Action OnClick;


        // --------------------------------------------------------------------------
        /// <summary>
        /// Get resource text using a loose naming scheme
        /// </summary>
        // --------------------------------------------------------------------------
        public DraggingLogic(Window dragme)
        {
            dragme.MouseDown += HandleMouseDown;
            dragme.MouseMove += HandleMouseMove;
            dragme.MouseUp += HandleMouseUp;
            dragme.Loaded += Dragme_Loaded;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Figure out DPI measures
        /// </summary>
        // --------------------------------------------------------------------------
        private void Dragme_Loaded(object sender, RoutedEventArgs e)
        {
            var source = PresentationSource.FromVisual((Window)sender);
            DpiCorrectionX = 1.0 / source.CompositionTarget.TransformToDevice.M11;
            DpiCorrectionY = 1.0 / source.CompositionTarget.TransformToDevice.M22;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Mouse Down
        /// </summary>
        // --------------------------------------------------------------------------

        private void HandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            var window = sender as Window;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _dragging = true;
                _dragDelta = 0;
                _lastMousePosition = window.PointToScreen(Mouse.GetPosition(window));
                window.CaptureMouse();
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Mouse Move
        /// </summary>
        // --------------------------------------------------------------------------
        private void HandleMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_dragging)
            {
                var window = sender as Window;
                var newPosition = window.PointToScreen(Mouse.GetPosition(window));
                var xMove = (newPosition.X - _lastMousePosition.X) * DpiCorrectionX;
                var yMove = (newPosition.Y - _lastMousePosition.Y) * DpiCorrectionY;
                window.Left += xMove;
                window.Top += yMove;
                _dragDelta += (_lastMousePosition - newPosition).Length;
                _lastMousePosition = newPosition;
                OnPositionChanged?.Invoke(xMove, yMove);
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Mouse Up
        /// </summary>
        // --------------------------------------------------------------------------
        private void HandleMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragging)
            {
                var window = sender as Window;
                if (_dragDelta < 3)
                {
                    OnClick?.Invoke();
                }
                _dragging = false;
                window.ReleaseMouseCapture();
            }

        }
    }
}
