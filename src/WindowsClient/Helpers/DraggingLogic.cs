using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

[assembly: DisableDpiAwareness]

namespace Talisman
{

    // --------------------------------------------------------------------------
    /// <summary>
    /// Enables dragging of a WPF window in a way that is per-monitor DPI sensitive.
    /// 
    /// HOW TO USE
    /// Add a DraggingLogic member variable and put this code in your window constructor:
    ///     _draggingLogic = new DraggingLogic(this);
    ///     
    /// If you want to do special things when the window moves or when it is clicked:
    ///     _draggingLogic.OnPositionChanged += (xm, ym) => {/* whatever you want here */};
    ///     _draggingLogic.OnClick += () => {/* whatever you want here */};
    ///
    /// </summary>
    // --------------------------------------------------------------------------
    public class DraggingLogic
    {
        public event Action<double, double> OnPositionChanged;
        public event Action OnClick;

        /// <summary>
        /// Factor to convert Horizontal screen coordinates
        /// </summary>
        public double DpiCorrectionX { get; set; }
        /// <summary>
        /// Factor to convertVertical  screen coordinates
        /// </summary>
        public double DpiCorrectionY { get; set; }

        public double WpfDpiX { get; set; }
        public double WpfDpiY { get; set; }

        #region INTERROP - Mouse interaction

        private static int _mouseHookHandle;
        private delegate int HookProc(int nCode, int wParam, IntPtr lParam);
        private static HookProc _mouseDelegate;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_MOUSEMOVE = 0x0200;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto,
        CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto,
           CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int UnhookWindowsHookEx(int idHook);

        [DllImport("user32.dll", CharSet = CharSet.Auto,
             CallingConvention = CallingConvention.StdCall)]
        private static extern int CallNextHookEx(int idHook, int nCode, int wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string name);

        #endregion

        #region INTERROP - DPI

        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromPoint([In]System.Drawing.Point pt, [In]uint dwFlags);

        [DllImport("Shcore.dll")]
        private static extern IntPtr GetDpiForMonitor([In]IntPtr hmonitor, [In]DpiType dpiType, [Out]out uint dpiX, [Out]out uint dpiY);
        [DllImport("Shcore.dll")]
        private static extern IntPtr SetProcessDpiAwareness([In]DpiAwareness dpiAwareness); 

        public enum DpiType
        {
            Effective = 0,
            Angular = 1,
            Raw = 2,
        }

        public enum DpiAwareness
        {
            Unaware = 0,
            System = 1,
            PerMonitor = 2,
        }

        #endregion

        Screen _currentScreen;
        Window _dragMe;
        bool _dragging = false;
        double _dragDelta = 0;
        Point _lastMousePosition;
        Point _mouseStickyPosition;

        // --------------------------------------------------------------------------
        /// <summary>
        /// SetDpiAwareness - call this when the app starts
        /// </summary>
        // --------------------------------------------------------------------------
        public static void SetDpiAwareness()
        {
            var result = SetProcessDpiAwareness(DpiAwareness.PerMonitor);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor - call after window is loaded
        /// </summary>
        // --------------------------------------------------------------------------
        public DraggingLogic(Window dragme, FrameworkElement mouseTarget)
        {
            mouseTarget.MouseDown += HandleMouseDown;
            mouseTarget.MouseMove += HandleMouseMove;
            mouseTarget.MouseUp += HandleMouseUp;
            _dragMe = dragme;

            dragme.Loaded += (s, a) =>
            {
                try
                {
                    var source = PresentationSource.FromVisual(_dragMe);
                    WpfDpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                    WpfDpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
                }
                catch (Exception e)
                {
                    throw new ApplicationException("Make sure you construct the dragging logic after the window is loaded.  Error: " + e.ToString());
                }
            };

        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Figure out scaling for the DPI on a certain monitor
        /// </summary>
        // --------------------------------------------------------------------------
        public void CalculateDpiScaleFactors(Screen screen, DpiType dpiType)
        {
            var point = new System.Drawing.Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1);
            var monitor = MonitorFromPoint(point, 2/*MONITOR_DEFAULTTONEAREST*/);
            var result = GetDpiForMonitor(monitor, dpiType, out var monitorDpiX, out var monitorDpiY);
            if(result != IntPtr.Zero)
            {
                monitorDpiX = monitorDpiY = 96;
            }
            DpiCorrectionX = 96.0 / monitorDpiX;
            DpiCorrectionY = 96.0 / monitorDpiY;
        }

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
                _mouseStickyPosition = Mouse.GetPosition(_dragMe);
                _lastMousePosition = _dragMe.PointToScreen(Mouse.GetPosition(_dragMe));
                _currentScreen = GetScreenFromPoint(_lastMousePosition);
                CalculateDpiScaleFactors(_currentScreen, DpiType.Effective);

                CaptureGlobalMouse();
                e.Handled = true;
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
                e.Handled = true;
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// HandleGlobalMouseMove
        /// </summary>
        // --------------------------------------------------------------------------
        private void HandleGlobalMouseMove(Point mouseLocation)
        {
            var newPosition = mouseLocation; // This arrives without DPI correction
            var screen = GetScreenFromPoint(newPosition);

            // We need to do some fix up when we drag to another screen because
            // the DPI on the other screen could be different
            if(screen != null &&  screen.DeviceName != _currentScreen.DeviceName)
            {
                CalculateDpiScaleFactors(screen, DpiType.Effective);
                _lastMousePosition = newPosition;

                // Move the window to match the mouse position
                _dragMe.InvalidateMeasure();
                _dragMe.Left = (newPosition.X - _mouseStickyPosition.X) * DpiCorrectionX;
                _dragMe.Top = (newPosition.Y - _mouseStickyPosition.Y) * DpiCorrectionY;
                _currentScreen = screen;
            }

            var xMove = (newPosition.X - _lastMousePosition.X)* DpiCorrectionX;
            var yMove = (newPosition.Y - _lastMousePosition.Y)* DpiCorrectionY;
            _dragMe.Left += xMove;
            _dragMe.Top += yMove;
            _dragDelta += (_lastMousePosition - newPosition).Length;
            _lastMousePosition = newPosition;
            OnPositionChanged?.Invoke(xMove, yMove);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// GetScreenFromPoint - return the screen from a raw point (presumably mouse coordinate)
        /// </summary>
        // --------------------------------------------------------------------------
        public Screen GetScreenFromPoint(Point point)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.ContainsPoint(point.X, point.Y)) return screen;
            }
            return null;
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
                // if the user didn't actually drag, then we want to treat this as a click
                if (_dragDelta < 3)
                {
                    OnClick?.Invoke();
                }
                _dragging = false;
                ReleaseGlobalMouse();
                if(e != null) e.Handled = true;
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// MouseHookProc- allows us to handle global mouse events
        /// </summary>
        // --------------------------------------------------------------------------
        private int MouseHookProc(int nCode, int wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                switch (wParam)
                {
                    case WM_LBUTTONUP: HandleMouseUp(this, null); break;
                    case WM_MOUSEMOVE:
                        {
                            var mouseHookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                            HandleGlobalMouseMove(new Point(mouseHookStruct.pt.x, mouseHookStruct.pt.y));
                            break;
                        }
                }
            }
            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// CaptureGlobalMouse
        /// </summary>
        // --------------------------------------------------------------------------
        private void CaptureGlobalMouse()
        {
            if (_mouseHookHandle == 0)
            {
                _mouseDelegate = MouseHookProc;
                _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL,
                    _mouseDelegate,
                    GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName),
                    0);
                if (_mouseHookHandle == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// ReleaseGlobalMouse
        /// </summary>
        // --------------------------------------------------------------------------
        private void ReleaseGlobalMouse()
        {
            if (_mouseHookHandle != 0)
            {
                int result = UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = 0;
                _mouseDelegate = null;
                if (result == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }
    }
}
