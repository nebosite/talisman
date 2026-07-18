namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Debounces the "task ran out of time" signal so the circling notification is
    /// raised exactly once per task occurrence. Resets whenever the current task
    /// changes (including a deferred task coming back around, which is a fresh
    /// occurrence), so each fresh countdown can nag again.
    ///
    /// Pure logic - unit-tested independently of the controller and WPF.
    /// </summary>
    // --------------------------------------------------------------------------
    public class TaskTimeoutTracker
    {
        object _lastTask;
        bool _alreadyNotified;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Given the current task and whether it is currently timed out, return true
        /// on the single transition into the timed-out state for this occurrence.
        /// </summary>
        // --------------------------------------------------------------------------
        public bool ShouldNotify(object currentTask, bool timedOut)
        {
            if (!ReferenceEquals(currentTask, _lastTask))
            {
                _lastTask = currentTask;
                _alreadyNotified = false;
            }

            if (currentTask == null) return false;

            if (timedOut && !_alreadyNotified)
            {
                _alreadyNotified = true;
                return true;
            }
            return false;
        }
    }
}
