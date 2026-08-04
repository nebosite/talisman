using System;
using System.Diagnostics;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Opens URLs, files, folders, and shell targets (e.g. "shell:AppsFolder\...")
    /// with the OS shell - the default browser, Explorer, or the registered app.
    ///
    /// On .NET Framework <c>Process.Start("https://…")</c> shell-executed by
    /// default; on modern .NET (this app since the .NET 8 migration)
    /// <see cref="ProcessStartInfo.UseShellExecute"/> defaults to <c>false</c>, so
    /// that same call throws <see cref="System.ComponentModel.Win32Exception"/>
    /// ("The system cannot find the file specified"). Every shell-style launch must
    /// therefore set <c>UseShellExecute = true</c>. Centralised here so the fix
    /// lives in one place and can't regress site by site.
    /// </summary>
    // --------------------------------------------------------------------------
    public static class ShellLauncher
    {
        // --------------------------------------------------------------------------
        /// <summary>
        /// Build the shell-execute start info for a target. Separated from the launch
        /// so the (untestable) <see cref="Process.Start(ProcessStartInfo)"/> call is
        /// the only thing not covered by tests.
        /// </summary>
        // --------------------------------------------------------------------------
        public static ProcessStartInfo StartInfoFor(string target) =>
            new ProcessStartInfo(target ?? "") { UseShellExecute = true };

        // --------------------------------------------------------------------------
        /// <summary>
        /// Open a URL / file / folder / shell target in its default handler. Never
        /// throws - a bad or blocked target is logged and swallowed.
        /// </summary>
        // --------------------------------------------------------------------------
        public static void Open(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return;
            try { Process.Start(StartInfoFor(target)); }
            catch (Exception ex) { Log.Warn($"Could not open '{target}'.", ex); }
        }
    }
}
