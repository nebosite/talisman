namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// How the previous session ended.
    /// </summary>
    // --------------------------------------------------------------------------
    public enum PreviousExitKind
    {
        /// <summary>Shut down normally.</summary>
        Clean,
        /// <summary>Did not shut down cleanly, but no crash was recorded - almost
        /// always a reboot, sleep, or a forced close (Task Manager / OS). Not worth
        /// nagging the user about.</summary>
        UncleanNoCrash,
        /// <summary>A terminating unhandled exception was caught and logged - a real
        /// crash worth surfacing (and reporting).</summary>
        Crashed,
    }

    // --------------------------------------------------------------------------
    /// <summary>
    /// Decides what the previous exit was from the two persisted signals, so the
    /// app only nags about genuine crashes (which leave log evidence) and stays
    /// quiet about ordinary ungraceful terminations. Pure and unit-tested.
    ///
    /// - <paramref name="uncleanShutdown"/>: the CrashedLastTime flag - set while
    ///   running and cleared only on a graceful exit or Windows session end.
    /// - <paramref name="fatalCrash"/>: set only when a terminating unhandled
    ///   exception was actually caught and logged.
    /// </summary>
    // --------------------------------------------------------------------------
    public static class PreviousExitClassifier
    {
        public static PreviousExitKind Classify(bool uncleanShutdown, bool fatalCrash)
        {
            if (fatalCrash) return PreviousExitKind.Crashed;
            if (uncleanShutdown) return PreviousExitKind.UncleanNoCrash;
            return PreviousExitKind.Clean;
        }
    }
}
