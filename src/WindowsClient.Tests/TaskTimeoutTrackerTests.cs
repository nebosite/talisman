using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for TaskTimeoutTracker: fire once per task occurrence, reset when the
    /// task changes, and treat a returning (deferred) task as a fresh occurrence.
    /// </summary>
    // --------------------------------------------------------------------------
    public class TaskTimeoutTrackerTests
    {
        [Fact]
        public void DoesNotNotify_WhileTaskHasTimeLeft()
        {
            var tracker = new TaskTimeoutTracker();
            var task = new object();
            Assert.False(tracker.ShouldNotify(task, timedOut: false));
            Assert.False(tracker.ShouldNotify(task, timedOut: false));
        }

        [Fact]
        public void NotifiesOnce_WhenTaskTimesOut()
        {
            var tracker = new TaskTimeoutTracker();
            var task = new object();

            Assert.False(tracker.ShouldNotify(task, false));
            Assert.True(tracker.ShouldNotify(task, true));   // first time out -> notify
            Assert.False(tracker.ShouldNotify(task, true));  // still timed out -> no repeat
            Assert.False(tracker.ShouldNotify(task, true));
        }

        [Fact]
        public void ResetsForNewTask()
        {
            var tracker = new TaskTimeoutTracker();
            var a = new object();
            var b = new object();

            Assert.True(tracker.ShouldNotify(a, true));   // a times out
            Assert.False(tracker.ShouldNotify(a, true));  // no repeat for a
            Assert.True(tracker.ShouldNotify(b, true));   // b is a new task -> notifies
        }

        [Fact]
        public void ReturningTask_IsAFreshOccurrence()
        {
            var tracker = new TaskTimeoutTracker();
            var a = new object();
            var b = new object();

            Assert.True(tracker.ShouldNotify(a, true));    // a times out
            Assert.True(tracker.ShouldNotify(b, true));    // switch to b (deferral)
            Assert.True(tracker.ShouldNotify(a, true));    // a comes back around -> notifies again
        }

        [Fact]
        public void NullTask_NeverNotifies()
        {
            var tracker = new TaskTimeoutTracker();
            Assert.False(tracker.ShouldNotify(null, true));
            Assert.False(tracker.ShouldNotify(null, false));
        }
    }
}
