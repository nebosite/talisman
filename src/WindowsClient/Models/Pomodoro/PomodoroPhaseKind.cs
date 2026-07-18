namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The four working phases of a Pomodoro day, in order.
    /// </summary>
    // --------------------------------------------------------------------------
    public enum PomodoroPhaseKind
    {
        /// <summary>Phase I - short &amp; easy tasks, per-task countdown.</summary>
        ShortEasy,
        /// <summary>Phase II - joyful tasks, per-task elapsed count-up.</summary>
        Joy,
        /// <summary>Phase III - administrative tasks, per-task elapsed count-up.</summary>
        Admin,
        /// <summary>Phase IV - leftover short tasks plus default admin tasks.</summary>
        Extra,
    }
}
