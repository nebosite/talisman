using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace Talisman
{

    // --------------------------------------------------------------------------
    /// <summary>
    /// Enables dragging of a WPF window in a way that is DPI sensitive.
    /// 
    /// HOW TO USE
    /// Put this code in your window constructor:
    ///     _draggingLogic = new DraggingLogic(this);
    ///     
    /// If you want to do special things when the window drags or when it is clicked:
    ///     _draggingLogic.OnPositionChanged += (xm, ym) => {/* whatever you want here */};
    ///     _draggingLogic.OnClick += () => {/* whatever you want here */};
    ///
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

        Window _dragMe;

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
            _dragMe = dragme;
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
                CaptureGlobalMouse();
                //(e.Source as UIElement).CaptureMouse();
                //var captureElement = sender as IInputElement;
                //Mouse.Capture(captureElement);
                //window.CaptureMouse();
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
                // Ignore normal mouse events if we are dragging since
                // we want to depend on the global events
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
            var newPosition = mouseLocation;// _dragMe.PointToScreen(mouseLocation);
            Debug.WriteLine($"Moving M:{mouseLocation}  New: {newPosition}" );
            var xMove = (newPosition.X - _lastMousePosition.X) * DpiCorrectionX;
            var yMove = (newPosition.Y - _lastMousePosition.Y) * DpiCorrectionY;
            _dragMe.Left += xMove;
            _dragMe.Top += yMove;
            _dragDelta += (_lastMousePosition - newPosition).Length;
            _lastMousePosition = newPosition;
            OnPositionChanged?.Invoke(xMove, yMove);
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
                if (wParam == WM_LBUTTONUP)
                {
                    HandleMouseUp(this, null);
                }
                if (wParam == WM_MOUSEMOVE)
                {
                    var mouseHookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    HandleGlobalMouseMove( new Point(mouseHookStruct.pt.x, mouseHookStruct.pt.y));
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
