using System;
using System.Collections.Generic;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Record of what happened during one phase - used to build the end-of-day
    /// summary. Captures which tasks were completed, how many were deferred, and
    /// the wall-clock span of the phase.
    /// </summary>
    // --------------------------------------------------------------------------
    public class PomodoroPhaseResult
    {
        public PomodoroPhaseKind Kind { get; }
        public DateTime Started { get; }
        public DateTime? Ended { get; set; }

        /// <summary>Titles of tasks marked done, in completion order.</summary>
        public List<string> DoneTasks { get; } = new List<string>();

        /// <summary>How many "defer" actions happened this phase.</summary>
        public int DeferredCount { get; set; }

        public PomodoroPhaseResult(PomodoroPhaseKind kind, DateTime started)
        {
            Kind = kind;
            Started = started;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Elapsed wall-clock time of the phase (or so far, if not yet ended).
        /// </summary>
        // --------------------------------------------------------------------------
        public TimeSpan Duration(DateTime now)
        {
            return (Ended ?? now) - Started;
        }
    }
}
