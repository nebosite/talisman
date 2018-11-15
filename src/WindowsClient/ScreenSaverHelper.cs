using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Talisman
{
    public class ScreenSaverHelper
    {
        [DllImport("user32")]
        public static extern void LockWorkStation();

        [DllImport("User32.dll")]
        public static extern int SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetDesktopWindow")]
        private static extern IntPtr GetDesktopWindow();


        public const uint WM_SYSCOMMAND = 0x112;
        public const uint SC_SCREENSAVE = 0xF140;

        public static void ActivateScreenSaver()
        {
            LockWorkStation();
            Thread.Sleep(500);
            SendMessage(GetDesktopWindow(), WM_SYSCOMMAND, (IntPtr)SC_SCREENSAVE, (IntPtr)0);
        }
    }
}
