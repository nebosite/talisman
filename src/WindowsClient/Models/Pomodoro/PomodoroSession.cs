using System;
using System.Collections.Generic;
using System.Linq;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The Pomodoro-day state machine. Pure logic: every method that depends on
    /// the clock takes the current time as a parameter, so the whole thing is
    /// deterministic and unit-testable without any timers or UI.
    ///
    /// Flow: Start() -> ShortEasy -> (JoyPrompt) Joy -> (AdminPrompt) Admin ->
    /// Extra -> Finished. The short phases (ShortEasy/Extra) run a per-task
    /// countdown and end on the min/max-time + min-task rules; the block phases
    /// (Joy/Admin) run a per-task elapsed count-up and end when their time budget
    /// is spent or their tasks run out.
    /// </summary>
    // --------------------------------------------------------------------------
    public class PomodoroSession
    {
        readonly PomodoroParameters _p;
        readonly List<string> _joySource;
        readonly List<string> _adminSource;
        readonly List<string> _defaultAdmin;

        // The waiting tasks for the current phase (front = next up).
        List<PomodoroTask> _remaining = new List<PomodoroTask>();
        // Short tasks not finished during Phase I, held for Phase IV.
        List<PomodoroTask> _leftoverShort = new List<PomodoroTask>();

        DateTime _phaseStart;
        DateTime _taskStart;
        PomodoroPhaseResult _currentResult;

        public PomodoroParameters Parameters => _p;
        public PomodoroState State { get; private set; } = PomodoroState.NotStarted;
        public PomodoroTask CurrentTask { get; private set; }

        /// <summary>Per-phase records, in order, for the day summary.</summary>
        public List<PomodoroPhaseResult> Results { get; } = new List<PomodoroPhaseResult>();

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor. Task lists are the parsed, already-trimmed titles for each bucket.
        /// </summary>
        // --------------------------------------------------------------------------
        public PomodoroSession(
            PomodoroParameters parameters,
            IEnumerable<string> shortTasks,
            IEnumerable<string> joyTasks,
            IEnumerable<string> adminTasks,
            IEnumerable<string> defaultShortTasks,
            IEnumerable<string> defaultAdminTasks)
        {
            _p = parameters ?? new PomodoroParameters();
            _joySource = (joyTasks ?? Enumerable.Empty<string>()).ToList();
            _adminSource = (adminTasks ?? Enumerable.Empty<string>()).ToList();
            _defaultAdmin = (defaultAdminTasks ?? Enumerable.Empty<string>()).ToList();

            // Phase I list is the default short tasks (on top) followed by the
            // user's short & easy tasks.
            _phaseIList = (defaultShortTasks ?? Enumerable.Empty<string>())
                .Concat(shortTasks ?? Enumerable.Empty<string>())
                .ToList();
        }

        readonly List<string> _phaseIList;

        // --------------------------------------------------------------------------
        /// <summary>
        /// True while a task is on screen with Done/Defer buttons active.
        /// </summary>
        // --------------------------------------------------------------------------
        public bool IsTaskActive =>
            State == PomodoroState.ShortEasy || State == PomodoroState.Extra
            || State == PomodoroState.Joy || State == PomodoroState.Admin;

        // --------------------------------------------------------------------------
        /// <summary>
        /// True in the short phases (per-task countdown); false in the block phases
        /// (per-task count-up).
        /// </summary>
        // --------------------------------------------------------------------------
        public bool IsCountdownPhase =>
            State == PomodoroState.ShortEasy || State == PomodoroState.Extra;

        // --------------------------------------------------------------------------
        /// <summary>
        /// The phase kind matching the current state, or null when prompting/done.
        /// </summary>
        // --------------------------------------------------------------------------
        public PomodoroPhaseKind? CurrentPhaseKind
        {
            get
            {
                switch (State)
                {
                    case PomodoroState.ShortEasy: return PomodoroPhaseKind.ShortEasy;
                    case PomodoroState.Joy: return PomodoroPhaseKind.Joy;
                    case PomodoroState.Admin: return PomodoroPhaseKind.Admin;
                    case PomodoroState.Extra: return PomodoroPhaseKind.Extra;
                    default: return null;
                }
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Begin the day at Phase I (short &amp; easy).
        /// </summary>
        // --------------------------------------------------------------------------
        public void Start(DateTime now)
        {
            if (State != PomodoroState.NotStarted) return;
            EnterShortPhase(now, PomodoroPhaseKind.ShortEasy, _phaseIList);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Mark the current task done and advance. No-op if no task is active.
        /// </summary>
        // --------------------------------------------------------------------------
        public void Done(DateTime now)
        {
            if (!IsTaskActive) return;
            if (CurrentTask != null) _currentResult.DoneTasks.Add(CurrentTask.Title);
            PullNext(now);
            CheckPhaseComplete(now);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Keep the current task (send it to the back of the queue) and advance.
        /// </summary>
        // --------------------------------------------------------------------------
        public void Defer(DateTime now)
        {
            if (!IsTaskActive) return;
            if (CurrentTask != null)
            {
                _currentResult.DeferredCount++;
                _remaining.Add(CurrentTask);
            }
            PullNext(now);
            CheckPhaseComplete(now);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// From a JoyPrompt/AdminPrompt, begin the joy or admin block.
        /// </summary>
        // --------------------------------------------------------------------------
        public void BeginPromptedPhase(DateTime now)
        {
            if (State == PomodoroState.JoyPrompt) EnterBlockPhase(now, PomodoroPhaseKind.Joy, _joySource);
            else if (State == PomodoroState.AdminPrompt) EnterBlockPhase(now, PomodoroPhaseKind.Admin, _adminSource);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Re-evaluate time-based phase completion. The UI calls this on a timer.
        /// </summary>
        // --------------------------------------------------------------------------
        public void Tick(DateTime now)
        {
            if (IsTaskActive) CheckPhaseComplete(now);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Elapsed time on the current task (zero if none).
        /// </summary>
        // --------------------------------------------------------------------------
        public TimeSpan CurrentTaskElapsed(DateTime now)
        {
            return CurrentTask == null ? TimeSpan.Zero : now - _taskStart;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Countdown remaining on the current task in the short phases (can go
        /// negative once the user runs over time). Zero outside the short phases.
        /// </summary>
        // --------------------------------------------------------------------------
        public TimeSpan CurrentTaskRemaining(DateTime now)
        {
            if (!IsCountdownPhase || CurrentTask == null) return TimeSpan.Zero;
            return _p.TimePerTask - CurrentTaskElapsed(now);
        }

        // ==========================================================================
        //  internals
        // ==========================================================================

        // --------------------------------------------------------------------------
        /// <summary>
        /// Enter a short phase (Phase I or IV): build the queue, start the clock,
        /// and pull up the first task.
        /// </summary>
        // --------------------------------------------------------------------------
        void EnterShortPhase(DateTime now, PomodoroPhaseKind kind, IEnumerable<string> tasks)
        {
            _phaseStart = now;
            _remaining = tasks.Select(t => new PomodoroTask(t)).ToList();
            _currentResult = new PomodoroPhaseResult(kind, now);
            Results.Add(_currentResult);
            State = kind == PomodoroPhaseKind.ShortEasy ? PomodoroState.ShortEasy : PomodoroState.Extra;
            PullNext(now);
            CheckPhaseComplete(now);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Enter a block phase (Joy or Admin) after its start prompt.
        /// </summary>
        // --------------------------------------------------------------------------
        void EnterBlockPhase(DateTime now, PomodoroPhaseKind kind, IEnumerable<string> tasks)
        {
            _phaseStart = now;
            _remaining = tasks.Select(t => new PomodoroTask(t)).ToList();
            _currentResult = new PomodoroPhaseResult(kind, now);
            Results.Add(_currentResult);
            State = kind == PomodoroPhaseKind.Joy ? PomodoroState.Joy : PomodoroState.Admin;
            PullNext(now);
            CheckPhaseComplete(now);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Pull the next task from the front of the queue and reset the task clock.
        /// Leaves CurrentTask null when the queue is empty.
        /// </summary>
        // --------------------------------------------------------------------------
        void PullNext(DateTime now)
        {
            if (_remaining.Count == 0)
            {
                CurrentTask = null;
                return;
            }
            CurrentTask = _remaining[0];
            _remaining.RemoveAt(0);
            _taskStart = now;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// If the active phase's end condition is met, advance to the next phase.
        /// </summary>
        // --------------------------------------------------------------------------
        void CheckPhaseComplete(DateTime now)
        {
            switch (State)
            {
                case PomodoroState.ShortEasy:
                case PomodoroState.Extra:
                    if (ShortPhaseComplete(now)) EndShortPhase(now);
                    break;
                case PomodoroState.Joy:
                case PomodoroState.Admin:
                    if (BlockPhaseComplete(now)) EndBlockPhase(now);
                    break;
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// A short phase ends when it runs out of tasks, when max short time is
        /// reached, or once min tasks are done and min short time has passed.
        /// </summary>
        // --------------------------------------------------------------------------
        bool ShortPhaseComplete(DateTime now)
        {
            if (CurrentTask == null && _remaining.Count == 0) return true;

            var elapsed = now - _phaseStart;
            if (elapsed >= _p.MaxShortTime) return true;
            if (_currentResult.DoneTasks.Count >= _p.MinTasks && elapsed >= _p.MinShortTime) return true;
            return false;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// A block phase ends when it runs out of tasks or its time budget is spent.
        /// </summary>
        // --------------------------------------------------------------------------
        bool BlockPhaseComplete(DateTime now)
        {
            if (CurrentTask == null && _remaining.Count == 0) return true;

            var budget = State == PomodoroState.Joy ? _p.JoyTime : _p.AdminTime;
            return now - _phaseStart >= budget;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Close out a short phase and move on. Phase I hands its leftovers to
        /// Phase IV and pauses at the joy prompt; Phase IV ends the day.
        /// </summary>
        // --------------------------------------------------------------------------
        void EndShortPhase(DateTime now)
        {
            _currentResult.Ended = now;

            if (State == PomodoroState.ShortEasy)
            {
                _leftoverShort = new List<PomodoroTask>();
                if (CurrentTask != null) _leftoverShort.Add(CurrentTask);
                _leftoverShort.AddRange(_remaining);

                CurrentTask = null;
                _remaining.Clear();
                State = PomodoroState.JoyPrompt;
            }
            else // Extra
            {
                CurrentTask = null;
                _remaining.Clear();
                State = PomodoroState.Finished;
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Close out a block phase. Joy pauses at the admin prompt; Admin flows into
        /// Phase IV (default admin tasks on top of the leftover short tasks).
        /// </summary>
        // --------------------------------------------------------------------------
        void EndBlockPhase(DateTime now)
        {
            _currentResult.Ended = now;
            CurrentTask = null;
            _remaining.Clear();

            if (State == PomodoroState.Joy)
            {
                State = PomodoroState.AdminPrompt;
            }
            else // Admin -> Phase IV (Extra)
            {
                var extraTasks = _defaultAdmin.Concat(_leftoverShort.Select(t => t.Title));
                EnterShortPhase(now, PomodoroPhaseKind.Extra, extraTasks);
            }
        }
    }
}
