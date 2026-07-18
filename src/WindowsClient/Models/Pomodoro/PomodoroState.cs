namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The state of a Pomodoro session. Running phases (ShortEasy/Joy/Admin/Extra)
    /// show the current-task widget; the *Prompt states pause for the user to click
    /// "start" before the Joy/Admin blocks begin; Finished shows the day summary.
    /// </summary>
    // --------------------------------------------------------------------------
    public enum PomodoroState
    {
        NotStarted,
        ShortEasy,
        JoyPrompt,
        Joy,
        AdminPrompt,
        Admin,
        Extra,
        Finished,
    }
}
