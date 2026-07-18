namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// A single Pomodoro task - just its title. Kept as a type (rather than a bare
    /// string) so identity is preserved as a task is deferred around the queue.
    /// </summary>
    // --------------------------------------------------------------------------
    public class PomodoroTask
    {
        public string Title { get; }

        public PomodoroTask(string title)
        {
            Title = title;
        }

        public override string ToString() => Title;
    }
}
