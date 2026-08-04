using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Sends an SMS by driving the Microsoft Phone Link app through UI Automation,
    /// which relays the text through the user's connected phone. This is the
    /// reliable path on Windows 11: carrier email-to-SMS gateways are being
    /// deprecated and silently drop mail, whereas Phone Link goes over the real
    /// phone and lands in the normal conversation thread.
    ///
    /// Microsoft exposes no API for this, so the send is genuine GUI automation of
    /// Phone Link's Messages compose pane (found by AutomationId): click "New
    /// message", type the recipient and press Enter to commit it, set the body via
    /// the ValuePattern, then invoke "Send". It is therefore inherently coupled to
    /// Phone Link's UI - each step fails loudly (throws) rather than sending to the
    /// wrong place if that UI changes. The sequence here was verified live against
    /// Phone Link 1.26061.
    /// </summary>
    // --------------------------------------------------------------------------
    public static class PhoneLinkSmsSender
    {
        // AutomationIds of the Phone Link controls we drive.
        const string MessagesTabId   = "MessagingNodeAutomationId";
        const string NewMessageId    = "NewMessageButton";
        const string RecipientBoxId  = "ContactSuggestionsBox"; // wraps the "To" edit
        const string MessageInputId  = "InputTextBox";
        const string SendButtonId    = "SendMessageButton";

        // --------------------------------------------------------------------------
        /// <summary>
        /// Send <paramref name="message"/> to <paramref name="number"/> (US digits)
        /// via Phone Link. Blocks until the send is invoked; throws with a clear
        /// reason if Phone Link isn't available or the compose flow can't complete.
        /// Run this off the UI thread - it foregrounds Phone Link and synthesizes
        /// keystrokes.
        /// </summary>
        // --------------------------------------------------------------------------
        public static void Send(string number, string message)
        {
            var digits = new string((number ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
                throw new ArgumentException("No recipient number to send to.");

            var win = FindPhoneLinkWindow();
            if (win == null)
                throw new InvalidOperationException(
                    "Phone Link isn't running (or no window was found). Open Phone Link and make sure your phone is connected.");

            var hwnd = new IntPtr(win.Current.NativeWindowHandle);
            SelectMessagesTab(win);

            // Recipient entry needs real keyboard focus (to raise the number
            // suggestion), which means Phone Link must be the foreground window. As a
            // background app we have to force that, and focus/typing can still race,
            // so try the whole compose up to twice and let Phone Link itself confirm:
            // the Send button only arms with a committed recipient AND a message, so
            // an armed Send is our proof the draft is valid before we click it. (That
            // proof works for plain numbers and saved contacts alike; the retry covers
            // the occasional time the recipient doesn't resolve on the first pass.)
            AutomationElement send = null;
            for (int attempt = 1; attempt <= 2 && send == null; attempt++)
            {
                ForceForeground(hwnd);
                Invoke(Require(win, NewMessageId, "New message button"));
                Thread.Sleep(1200);

                var toEdit = FindRecipientEdit(win);
                if (toEdit == null)
                    throw new InvalidOperationException("Couldn't find Phone Link's recipient box.");

                ForceForeground(hwnd);
                toEdit.SetFocus();
                Thread.Sleep(400);
                TypeUnicode(digits);
                Thread.Sleep(1600);     // let the number suggestion appear
                PressEnter();           // commit it to a recipient chip
                Thread.Sleep(900);

                // Fill the body (ValuePattern handles any characters, no focus tricks)
                // then check whether Phone Link armed Send.
                var input = Require(win, MessageInputId, "message box");
                input.SetFocus();
                Thread.Sleep(200);
                SetValue(input, message);
                Thread.Sleep(400);

                var button = Require(win, SendButtonId, "Send button");
                if (button.Current.IsEnabled) send = button;
            }

            if (send == null)
                throw new InvalidOperationException(
                    "Phone Link wouldn't arm Send for that recipient, so nothing was sent. " +
                    "Make sure Phone Link is connected and not busy, then try again.");

            Invoke(send);
        }

        // ---- element lookup ------------------------------------------------------

        static AutomationElement FindPhoneLinkWindow()
        {
            var procs = Process.GetProcessesByName("PhoneExperienceHost");
            if (procs.Length == 0) return null;

            var cond = new PropertyCondition(AutomationElement.ProcessIdProperty, procs[0].Id);
            var wins = AutomationElement.RootElement.FindAll(TreeScope.Children, cond);
            foreach (AutomationElement w in wins)
                if (!string.IsNullOrEmpty(w.Current.Name)) return w;
            return wins.Count > 0 ? wins[0] : null;
        }

        static void SelectMessagesTab(AutomationElement win)
        {
            var tab = win.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, MessagesTabId));
            if (tab == null) return;
            try
            {
                ((SelectionItemPattern)tab.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
                Thread.Sleep(300);
            }
            catch { /* best effort - staying on the current tab is usually fine */ }
        }

        static AutomationElement FindRecipientEdit(AutomationElement win)
        {
            var box = WaitFor(win, RecipientBoxId, 3000);
            return box?.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        }

        static AutomationElement Require(AutomationElement win, string automationId, string label)
        {
            var el = WaitFor(win, automationId, 4000);
            if (el == null)
                throw new InvalidOperationException($"Phone Link's UI has changed - couldn't find the {label}.");
            return el;
        }

        // Phone Link's compose controls appear a beat after an action, so poll briefly.
        static AutomationElement WaitFor(AutomationElement win, string automationId, int timeoutMs)
        {
            var cond = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var el = win.FindFirst(TreeScope.Descendants, cond);
                if (el != null) return el;
                Thread.Sleep(150);
            }
            return null;
        }

        // Bring Phone Link to the foreground even though we're a background app.
        // Windows blocks a non-foreground process from stealing focus, so we briefly
        // attach our input queue to the current foreground thread - the standard
        // bypass - which lets SetForegroundWindow actually take effect.
        static void ForceForeground(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);

            var foreground = GetForegroundWindow();
            var targetThread = GetWindowThreadProcessId(foreground, out _);
            var thisThread = GetCurrentThreadId();
            var attached = targetThread != thisThread && AttachThreadInput(thisThread, targetThread, true);
            try
            {
                BringWindowToTop(hwnd);
                SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached) AttachThreadInput(thisThread, targetThread, false);
            }
            Thread.Sleep(200);
        }

        static void Invoke(AutomationElement el) =>
            ((InvokePattern)el.GetCurrentPattern(InvokePattern.Pattern)).Invoke();

        static void SetValue(AutomationElement el, string text) =>
            ((ValuePattern)el.GetCurrentPattern(ValuePattern.Pattern)).SetValue(text ?? "");

        // ---- synthesized keystrokes (SendInput) ----------------------------------

        static void TypeUnicode(string s)
        {
            foreach (var ch in s)
            {
                SendKey(0, ch, KEYEVENTF_UNICODE);
                SendKey(0, ch, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP);
            }
        }

        static void PressEnter()
        {
            SendKey(VK_RETURN, '\0', 0);
            SendKey(VK_RETURN, '\0', KEYEVENTF_KEYUP);
        }

        static void SendKey(ushort vk, char unicode, uint flags)
        {
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = unicode,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            };
            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_UNICODE = 0x0004;
        const ushort VK_RETURN = 0x0D;
        const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion U; }

        // Union sized to the largest member (MOUSEINPUT) so INPUT matches the OS size.
        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }
    }
}
