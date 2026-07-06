using System;
using System.Collections.Generic;
using System.Linq;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The outcome of evaluating whether to auto-restart after a crash: whether we
    /// should relaunch, and the pruned failure history to persist for next time.
    /// </summary>
    // --------------------------------------------------------------------------
    public class RestartDecision
    {
        public bool ShouldRestart { get; }
        public IReadOnlyList<DateTime> UpdatedHistory { get; }

        public RestartDecision(bool shouldRestart, IReadOnlyList<DateTime> updatedHistory)
        {
            ShouldRestart = shouldRestart;
            UpdatedHistory = updatedHistory;
        }
    }

    // --------------------------------------------------------------------------
    /// <summary>
    /// Circuit breaker for crash auto-restart. Allows up to <c>maxRestartsInWindow</c>
    /// restarts inside a rolling time window; once that many failures have already
    /// happened in the window, it stops restarting so a crash-loop cannot spin
    /// forever. Pure logic (time is passed in) so it is fully unit-testable.
    /// </summary>
    // --------------------------------------------------------------------------
    public class RestartPolicy
    {
        readonly int _maxRestartsInWindow;
        readonly TimeSpan _window;

        public RestartPolicy(int maxRestartsInWindow = 3, TimeSpan? window = null)
        {
            _maxRestartsInWindow = maxRestartsInWindow;
            _window = window ?? TimeSpan.FromMinutes(5);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Decide whether to restart given the timestamps of prior crash-restarts.
        /// Records <paramref name="now"/> as a failure and drops anything older than
        /// the window. Restart is allowed only if fewer than the max failures have
        /// occurred within the window before this one.
        /// </summary>
        // --------------------------------------------------------------------------
        public RestartDecision Evaluate(IEnumerable<DateTime> priorFailures, DateTime now)
        {
            var recent = (priorFailures ?? Enumerable.Empty<DateTime>())
                .Where(t => now - t < _window && t <= now)
                .OrderBy(t => t)
                .ToList();

            var shouldRestart = recent.Count < _maxRestartsInWindow;

            recent.Add(now);
            return new RestartDecision(shouldRestart, recent);
        }
    }
}
