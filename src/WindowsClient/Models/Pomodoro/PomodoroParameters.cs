using System;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tunable timing parameters for a Pomodoro day. Stored by the settings as
    /// plain minutes/hours; this type exposes them as TimeSpans for the session.
    /// </summary>
    // --------------------------------------------------------------------------
    public class PomodoroParameters
    {
        /// <summary>Per-task countdown length in the short phases (default 5 min).</summary>
        public TimeSpan TimePerTask { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>A short phase cannot end before this much time has passed (default 30 min).</summary>
        public TimeSpan MinShortTime { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>Minimum tasks completed before a short phase may end on time (default 5).</summary>
        public int MinTasks { get; set; } = 5;

        /// <summary>A short phase always ends once this much time has passed (default 60 min).</summary>
        public TimeSpan MaxShortTime { get; set; } = TimeSpan.FromMinutes(60);

        /// <summary>Total length of the joy block (default 2 hours).</summary>
        public TimeSpan JoyTime { get; set; } = TimeSpan.FromHours(2);

        /// <summary>Total length of the admin block (default 1 hour).</summary>
        public TimeSpan AdminTime { get; set; } = TimeSpan.FromHours(1);
    }
}
