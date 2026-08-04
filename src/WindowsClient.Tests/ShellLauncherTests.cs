using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for ShellLauncher's start-info builder - the regression guard for the
    /// .NET 8 break where Process.Start(url) stopped shell-executing. Every target
    /// must carry UseShellExecute = true or URLs/shell targets throw at launch.
    /// </summary>
    // --------------------------------------------------------------------------
    public class ShellLauncherTests
    {
        [Fact]
        public void StartInfoFor_Url_SetsUseShellExecute()
        {
            var psi = ShellLauncher.StartInfoFor("https://example.com/x");
            Assert.True(psi.UseShellExecute);
            Assert.Equal("https://example.com/x", psi.FileName);
        }

        [Fact]
        public void StartInfoFor_ShellAppTarget_SetsUseShellExecute()
        {
            var psi = ShellLauncher.StartInfoFor(@"shell:AppsFolder\Microsoft.ScreenSketch_8wekyb3d8bbwe!App");
            Assert.True(psi.UseShellExecute);
            Assert.Equal(@"shell:AppsFolder\Microsoft.ScreenSketch_8wekyb3d8bbwe!App", psi.FileName);
        }

        [Fact]
        public void StartInfoFor_Null_DoesNotThrowAndBlankFileName()
        {
            var psi = ShellLauncher.StartInfoFor(null);
            Assert.Equal("", psi.FileName);
            Assert.True(psi.UseShellExecute);
        }
    }
}
