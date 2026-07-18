namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Plain serializable holder for everything on the Pomodoro settings tab. The
    /// whole object is persisted as one JSON string in Settings.PomodoroConfig, so
    /// adding a field here does not require touching the settings plumbing.
    /// Field defaults are the spec defaults.
    /// </summary>
    // --------------------------------------------------------------------------
    public class PomodoroConfigData
    {
        // Task buckets - newline separated, entered by the user.
        public string ShortTasks { get; set; } = "";
        public string JoyTasks { get; set; } = "";
        public string AdminTasks { get; set; } = "";
        public string DefaultShortTasks { get; set; } = "";
        public string DefaultAdminTasks { get; set; } = "";

        // Timing parameters.
        public int TimePerTaskMinutes { get; set; } = 5;
        public int MinShortMinutes { get; set; } = 30;
        public int MinTasks { get; set; } = 5;
        public int MaxShortMinutes { get; set; } = 60;
        public int JoyMinutes { get; set; } = 120;
        public int AdminMinutes { get; set; } = 60;
    }
}
