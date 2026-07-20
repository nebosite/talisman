using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for PreviousExitClassifier: a recorded crash wins; an unclean
    /// shutdown with no crash is not treated as a crash; a clean flag is clean.
    /// </summary>
    // --------------------------------------------------------------------------
    public class PreviousExitClassifierTests
    {
        [Fact]
        public void CleanShutdown_IsClean()
        {
            Assert.Equal(PreviousExitKind.Clean,
                PreviousExitClassifier.Classify(uncleanShutdown: false, fatalCrash: false));
        }

        [Fact]
        public void UncleanButNoCrash_IsUncleanNoCrash()
        {
            // The common false-alarm case: reboot / sleep / forced close.
            Assert.Equal(PreviousExitKind.UncleanNoCrash,
                PreviousExitClassifier.Classify(uncleanShutdown: true, fatalCrash: false));
        }

        [Fact]
        public void RecordedCrash_IsCrashed()
        {
            Assert.Equal(PreviousExitKind.Crashed,
                PreviousExitClassifier.Classify(uncleanShutdown: true, fatalCrash: true));
        }

        [Fact]
        public void CrashFlagWins_EvenIfShutdownLookedClean()
        {
            // Defensive: if a crash was recorded, surface it regardless of the
            // shutdown flag's state.
            Assert.Equal(PreviousExitKind.Crashed,
                PreviousExitClassifier.Classify(uncleanShutdown: false, fatalCrash: true));
        }
    }
}
