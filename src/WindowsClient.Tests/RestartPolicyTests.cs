using System;
using System.Collections.Generic;
using System.Linq;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for the crash auto-restart circuit breaker: it should allow a few
    /// restarts but cut off a crash loop, and it should forget old failures once
    /// they age out of the window.
    /// </summary>
    // --------------------------------------------------------------------------
    public class RestartPolicyTests
    {
        static readonly DateTime T0 = new DateTime(2026, 7, 6, 12, 0, 0);

        [Fact]
        public void FirstFailure_AllowsRestart_AndRecordsIt()
        {
            var policy = new RestartPolicy(maxRestartsInWindow: 3, window: TimeSpan.FromMinutes(5));

            var decision = policy.Evaluate(new List<DateTime>(), T0);

            Assert.True(decision.ShouldRestart);
            Assert.Single(decision.UpdatedHistory);
            Assert.Equal(T0, decision.UpdatedHistory[0]);
        }

        [Fact]
        public void AllowsUpToThreeRestarts_ThenStops()
        {
            var policy = new RestartPolicy(maxRestartsInWindow: 3, window: TimeSpan.FromMinutes(5));
            var history = new List<DateTime>();

            // Three crashes 30s apart - all within the window - are all allowed.
            for (int i = 0; i < 3; i++)
            {
                var decision = policy.Evaluate(history, T0.AddSeconds(30 * i));
                Assert.True(decision.ShouldRestart, $"restart #{i + 1} should be allowed");
                history = decision.UpdatedHistory.ToList();
            }

            // The fourth crash within the same window is refused.
            var fourth = policy.Evaluate(history, T0.AddSeconds(90));
            Assert.False(fourth.ShouldRestart);
        }

        [Fact]
        public void Restart_AllowedAgain_AfterFailuresAgeOutOfWindow()
        {
            var policy = new RestartPolicy(maxRestartsInWindow: 3, window: TimeSpan.FromMinutes(5));

            // Three old failures, all more than 5 minutes before "now".
            var old = new List<DateTime> { T0, T0.AddSeconds(30), T0.AddSeconds(60) };
            var now = T0.AddMinutes(10);

            var decision = policy.Evaluate(old, now);

            Assert.True(decision.ShouldRestart);
            // The stale entries are pruned; only the new failure remains.
            Assert.Single(decision.UpdatedHistory);
            Assert.Equal(now, decision.UpdatedHistory[0]);
        }

        [Fact]
        public void MixedAgeHistory_PrunesOnlyStaleEntries()
        {
            var policy = new RestartPolicy(maxRestartsInWindow: 3, window: TimeSpan.FromMinutes(5));

            var now = T0.AddMinutes(10);
            var history = new List<DateTime>
            {
                T0,                       // stale (10 min old)
                now.AddMinutes(-2),       // recent
                now.AddMinutes(-1),       // recent
            };

            var decision = policy.Evaluate(history, now);

            // Two recent + this one = 3 in window; only 2 priors, so still allowed.
            Assert.True(decision.ShouldRestart);
            Assert.Equal(3, decision.UpdatedHistory.Count);
            Assert.DoesNotContain(T0, decision.UpdatedHistory);
        }

        [Fact]
        public void ThreeRecentPriors_BlocksEvenWithPrunedStaleEntries()
        {
            var policy = new RestartPolicy(maxRestartsInWindow: 3, window: TimeSpan.FromMinutes(5));

            var now = T0.AddMinutes(10);
            var history = new List<DateTime>
            {
                T0,                       // stale, pruned
                now.AddMinutes(-3),
                now.AddMinutes(-2),
                now.AddMinutes(-1),
            };

            var decision = policy.Evaluate(history, now);

            Assert.False(decision.ShouldRestart);   // 3 recent priors -> cut off
        }

        [Fact]
        public void NullHistory_IsTreatedAsEmpty()
        {
            var policy = new RestartPolicy();
            var decision = policy.Evaluate(null, T0);
            Assert.True(decision.ShouldRestart);
            Assert.Single(decision.UpdatedHistory);
        }

        [Fact]
        public void FutureTimestamps_AreIgnored()
        {
            var policy = new RestartPolicy(maxRestartsInWindow: 3, window: TimeSpan.FromMinutes(5));

            // Clock-skew garbage in the future should not count against the budget.
            var history = new List<DateTime> { T0.AddHours(1), T0.AddHours(2) };
            var decision = policy.Evaluate(history, T0);

            Assert.True(decision.ShouldRestart);
        }
    }
}
