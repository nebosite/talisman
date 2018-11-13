using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;

namespace Talisman
{
    /// <summary>
    /// Simpler way to expose key modifiers
    /// </summary>
    [Flags]
    public enum HotKeyModifiers
    {
        None = 0,
        Alt = 1,            // MOD_ALT
        Control = 2,        // MOD_CONTROL
        Shift = 4,          // MOD_SHIFT
        WindowsKey = 8,     // MOD_WIN
    }

    public interface IHotKeyTool : IDisposable
    {
        int ListenForHotKey(System.Windows.Input.Key key, HotKeyModifiers modifiers, Action keyAction);
        void StopListeningForHotKey(int id);
    }

    // --------------------------------------------------------------------------
    /// <summary>
    /// A nice generic class to register multiple hotkeys for your app
    /// </summary>
    // --------------------------------------------------------------------------
    public class HotKeyHelper : IHotKeyTool
    {
        // Required interop declarations for working with hotkeys
        [DllImport("user32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        protected static extern bool RegisterHotKey(IntPtr hwnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32", SetLastError = true)]
        protected static extern int UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("kernel32", SetLastError = true)]
        protected static extern short GlobalAddAtom(string lpString);
        [DllImport("kernel32", SetLastError = true)]
        protected static extern short GlobalDeleteAtom(short nAtom);

        protected const int WM_HOTKEY = 0x312;

        /// <summary>
        /// The unique ID to receive hotkey messages
        /// </summary>
        int _idSeed;

        /// <summary>
        /// Handle to the window listening to hotkeys
        /// </summary>
        private IntPtr _windowHandle;

        /// <summary>
        /// Remember what to do with the hot keys
        /// </summary>
        Dictionary<int, Action> _hotKeyActions = new Dictionary<int, Action>();

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------

        public HotKeyHelper(Window handlerWindow)
        {
            // Create a unique Id seed
            _idSeed = (int)(DateTime.Now.Ticks % 1000000000 + 500000000);

            // Set up the hook to listen for hot keys
            _windowHandle = new WindowInteropHelper(handlerWindow).Handle;
            if(_windowHandle == null)
            {
                throw new ApplicationException("Cannot find window handle.  Try calling this on or after OnSourceInitialized()");
            }
            var source = HwndSource.FromHwnd(_windowHandle);
            source.AddHook(HwndHook);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Intermediate processing of hotkeys
        /// </summary>
        // --------------------------------------------------------------------------
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY) 
            {
                var id = wParam.ToInt32();
                if (_hotKeyActions.ContainsKey(id))
                {
                    _hotKeyActions[id]();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Tell what key you want to listen for.  Returns an id representing
        /// this particular key combination.  Use this in your handler to 
        /// disambiguate what key was pressed.
        /// </summary>
        // --------------------------------------------------------------------------
        public int ListenForHotKey(System.Windows.Input.Key key, HotKeyModifiers modifiers, Action doThis)
        {
            var formsKey = (Keys)KeyInterop.VirtualKeyFromKey(key);

            RegisterHotKey(_windowHandle, _idSeed, (uint)modifiers, (uint)formsKey);
            var id = _idSeed++;
            _hotKeyActions[id] = doThis;
            return id;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Stop listening for hotkeys
        /// </summary>
        // --------------------------------------------------------------------------
        public void StopListeningForHotKey(int id)
        {
            UnregisterHotKey(_windowHandle, id);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Dispose
        /// </summary>
        // --------------------------------------------------------------------------
        public void Dispose()
        {
            foreach(var id in _hotKeyActions.Keys)
            {
                StopListeningForHotKey(id);
            }
        }
    }
}
