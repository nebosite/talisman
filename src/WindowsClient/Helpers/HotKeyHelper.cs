using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

    /// <summary>
    /// A helpful interface for abstracting this
    /// </summary>
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
            _idSeed = (int)((DateTime.Now.Ticks % 0x60000000) + 0x10000000);

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
        /// Listen generally for hotkeys and route to the assigned action
        /// </summary>
        // --------------------------------------------------------------------------
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY) 
            {
                var hotkeyId = wParam.ToInt32();
                if (_hotKeyActions.ContainsKey(hotkeyId))
                {
                    _hotKeyActions[hotkeyId]();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Assign a key to a specific action.  Returns an id to allow you to stop
        /// listening to this key.
        /// </summary>
        // --------------------------------------------------------------------------
        public int ListenForHotKey(System.Windows.Input.Key key, HotKeyModifiers modifiers, Action doThis)
        {
            var formsKey = (Keys)KeyInterop.VirtualKeyFromKey(key);

            var hotkeyId = _idSeed++;
            _hotKeyActions[hotkeyId] = doThis;
            RegisterHotKey(_windowHandle, hotkeyId, (uint)modifiers, (uint)formsKey);
            return hotkeyId;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Stop listening for hotkeys. 
        ///     hotkeyId      The id returned from ListenForHotKey
        /// </summary>
        // --------------------------------------------------------------------------
        public void StopListeningForHotKey(int hotkeyId)
        {
            UnregisterHotKey(_windowHandle, hotkeyId);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Dispose - automatically clean up the hotkey assignments
        /// </summary>
        // --------------------------------------------------------------------------
        public void Dispose()
        {
            foreach(var hotkeyId in _hotKeyActions.Keys)
            {
                StopListeningForHotKey(hotkeyId);
            }
        }
    }
}
