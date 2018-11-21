using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;

namespace Talisman
{
    public static class ScreenExtensions
    {
        public static bool ContainsPoint(this Screen screen, double x, double y)
        {
            return !(x < screen.Bounds.Left
                || x > screen.Bounds.Right
                || y < screen.Bounds.Top
                || y > screen.Bounds.Bottom);
        }
    }
    // --------------------------------------------------------------------------
    /// <summary>
    /// Base model for stuff that can be visualized
    /// </summary>
    // --------------------------------------------------------------------------
    public class ScreenHelper 
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hWnd);

        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);


        [DllImport("User32.dll")]
        public static extern int FindWindowEx(
             int hwndParent,
             int hwndChildAfter,
             string strClassName,
             string strWindowName);

#pragma warning disable 649
        internal struct INPUT
        {
            public UInt32 Type;
            public MOUSEKEYBDHARDWAREINPUT Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct MOUSEKEYBDHARDWAREINPUT
        {
            [FieldOffset(0)]
            public MOUSEINPUT Mouse;
        }

        internal struct MOUSEINPUT
        {
            public Int32 X;
            public Int32 Y;
            public UInt32 MouseData;
            public UInt32 Flags;
            public UInt32 Time;
            public IntPtr ExtraInfo;
        }
#pragma warning restore 649

        public static Screen MainScreen => Screen.AllScreens.Where(s => s.Primary).First();

        // --------------------------------------------------------------------------
        /// <summary>
        /// Make sure some app is running
        /// </summary>
        // --------------------------------------------------------------------------
        public static IntPtr OpenOrStartApp(string searchName, string processStartCommand)
        {
            //  FIrst find by window title
            var windowHandle = FindWindow(null, searchName);
            if(windowHandle != IntPtr.Zero)
            {
                ShowWindow(windowHandle, SW_RESTORE);
                SetForegroundWindow(windowHandle);
                return windowHandle;
            }

            // now look for the process
            var processes = Process.GetProcessesByName(searchName);
            if (!processes.Any())
            {
                Process.Start(processStartCommand);
                Thread.Sleep(1000);
            }

            // Look again for the window title
            windowHandle = FindWindow(null, searchName);
            if (windowHandle != IntPtr.Zero)
            {
                ShowWindow(windowHandle, SW_RESTORE);
                SetForegroundWindow(windowHandle);
                return windowHandle;
            }

            // Look again for the process
            processes = Process.GetProcessesByName(searchName);
            if(processes.Any())
            {
                var handle = processes.First().MainWindowHandle;
                ShowWindow(handle, SW_RESTORE);
                SetForegroundWindow(handle);
                return handle;
            }

            return IntPtr.Zero;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Click somewhere on an open appliation
        /// </summary>
        // --------------------------------------------------------------------------
        public static void ClickOnPoint(IntPtr wndHandle, System.Windows.Point clientPoint)
        {
            var oldPos = Cursor.Position;

            RECT rect = new RECT();
            GetWindowRect(wndHandle, ref rect);

            /// set cursor on coords, and press mouse
            Cursor.Position = new System.Drawing.Point((int)clientPoint.X + rect.Left, (int)clientPoint.Y + rect.Top);

            var inputMouseDown = new INPUT();
            inputMouseDown.Type = 0; /// input type mouse
            inputMouseDown.Data.Mouse.Flags = 0x0002; /// left button down

            var inputMouseUp = new INPUT();
            inputMouseUp.Type = 0; /// input type mouse
            inputMouseUp.Data.Mouse.Flags = 0x0004; /// left button up

            var inputs = new INPUT[] { inputMouseDown, inputMouseUp };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));

            /// return mouse 
            Cursor.Position = oldPos;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// If any part of the window is off screen, move it into the screen all the way
        /// </summary>
        // --------------------------------------------------------------------------
        public static void EnsureWindowIsVisible(Window window)
        {
            var source = PresentationSource.FromVisual(window);
            var xCorrection = 1.0 / source.CompositionTarget.TransformToDevice.M11;
            var yCorrection = 1.0 / source.CompositionTarget.TransformToDevice.M22;

            var left = window.Left / xCorrection;
            var right = left + window.Width / xCorrection;
            var top = window.Top / yCorrection;
            var bottom = top + window.Height / yCorrection;


            Screen foundScreen = null;
            // Find the screen that is us
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.ContainsPoint(left, top)
                    || screen.ContainsPoint(right, top)
                    || screen.ContainsPoint(right, bottom)
                    || screen.ContainsPoint(left, bottom))
                {
                    foundScreen = screen;
                    break;
                }
            }

            if (foundScreen == null) foundScreen = MainScreen;

            if (left < foundScreen.Bounds.Left) window.Left = foundScreen.Bounds.Left * xCorrection;
            if (top < foundScreen.Bounds.Top) window.Top = foundScreen.Bounds.Top * yCorrection;
            if (right > foundScreen.Bounds.Right) window.Left = (foundScreen.Bounds.Right - window.Width)*xCorrection;
            if (bottom + window.Height > foundScreen.Bounds.Bottom) window.Top = (foundScreen.Bounds.Bottom - window.Height)*yCorrection;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start the snipping tool for taking a screenshot
        /// </summary>
        // --------------------------------------------------------------------------
        public static void StartSnippingTool()
        {
            var sketchLocation = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Packages\Microsoft.ScreenSketch_8wekyb3d8bbwe");

            if (Directory.Exists(sketchLocation))
            {
                var handle = OpenOrStartApp("Snip & Sketch", @"shell:AppsFolder\Microsoft.ScreenSketch_8wekyb3d8bbwe!App");
                ClickOnPoint(handle, new Point(55,55));
                return;
            }

            //var snippingToolLocation = Path.Combine(Environment.SystemDirectory,"SnippingTool.exe");

            //if (File.Exists(snippingToolLocation))
            //{
            //    StartSnippingTool(snippingToolLocation);
            //    return;
            //}
        }

    }
}
