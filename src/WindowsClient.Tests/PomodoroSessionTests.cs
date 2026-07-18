using System;
using System.Collections.Generic;
using System.Linq;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for the PomodoroSession state machine: task rotation, done/defer,
    /// the phase-end rules for the short and block phases, leftovers flowing into
    /// Phase IV, the timers, and the day summary.
    /// </summary>
    // --------------------------------------------------------------------------
    public class PomodoroSessionTests
    {
        static readonly DateTime T0 = new DateTime(2026, 7, 15, 9, 0, 0);
        static DateTime At(int minutes) => T0.AddMinutes(minutes);

        static PomodoroParameters Params(
            int timePerTask = 5, int minShort = 30, int minTasks = 5,
            int maxShort = 60, int joy = 120, int admin = 60)
        {
            return new PomodoroParameters
            {
                TimePerTask = TimeSpan.FromMinutes(timePerTask),
                MinShortTime = TimeSpan.FromMinutes(minShort),
                MinTasks = minTasks,
                MaxShortTime = TimeSpan.FromMinutes(maxShort),
                JoyTime = TimeSpan.FromMinutes(joy),
                AdminTime = TimeSpan.FromMinutes(admin),
            };
        }

        static PomodoroSession Session(
            PomodoroParameters p = null,
            IEnumerable<string> shortTasks = null,
            IEnumerable<string> joy = null,
            IEnumerable<string> admin = null,
            IEnumerable<string> defShort = null,
            IEnumerable<string> defAdmin = null)
        {
            return new PomodoroSession(
                p ?? Params(),
                shortTasks ?? new string[0],
                joy ?? new string[0],
                admin ?? new string[0],
                defShort ?? new string[0],
                defAdmin ?? new string[0]);
        }

        // ---- Phase I basics ------------------------------------------------------

        [Fact]
        public void Start_EntersShortEasy_WithFirstTaskOnTop()
        {
            var s = Session(shortTasks: new[] { "A", "B", "C" });
            s.Start(T0);

            Assert.Equal(PomodoroState.ShortEasy, s.State);
            Assert.Equal("A", s.CurrentTask.Title);
            Assert.True(s.IsCountdownPhase);
            Assert.Equal(PomodoroPhaseKind.ShortEasy, s.CurrentPhaseKind);
        }

        [Fact]
        public void Start_PrependsDefaultShortTasksAboveUserTasks()
        {
            var s = Session(shortTasks: new[] { "user1" }, defShort: new[] { "def1", "def2" });
            s.Start(T0);

            Assert.Equal("def1", s.CurrentTask.Title);
            s.Done(T0);
            Assert.Equal("def2", s.CurrentTask.Title);
            s.Done(T0);
            Assert.Equal("user1", s.CurrentTask.Title);
        }

        [Fact]
        public void Done_RecordsTask_AndAdvances()
        {
            var s = Session(shortTasks: new[] { "A", "B" });
            s.Start(T0);
            s.Done(At(1));

            Assert.Equal("B", s.CurrentTask.Title);
            Assert.Contains("A", s.Results[0].DoneTasks);
        }

        [Fact]
        public void Defer_KeepsTask_SendsItToBack_AndAdvances()
        {
            var s = Session(shortTasks: new[] { "A", "B", "C" });
            s.Start(T0);
            s.Defer(T0);

            Assert.Equal("B", s.CurrentTask.Title);
            Assert.Equal(1, s.Results[0].DeferredCount);

            // A was sent to the back: after B and C, A comes around again.
            s.Done(T0); // B
            s.Done(T0); // C
            Assert.Equal("A", s.CurrentTask.Title);
        }

        // ---- Phase I end conditions ---------------------------------------------

        [Fact]
        public void ShortPhase_DoesNotEnd_WhenMinTasksDoneButBeforeMinTime()
        {
            var s = Session(Params(minShort: 10, minTasks: 2), shortTasks: new[] { "A", "B", "C", "D" });
            s.Start(T0);
            s.Done(At(1)); // 1 done
            s.Done(At(2)); // 2 done, but only 2 min elapsed (< 10)

            Assert.Equal(PomodoroState.ShortEasy, s.State);
        }

        [Fact]
        public void ShortPhase_Ends_WhenMinTasksDoneAfterMinTime()
        {
            var s = Session(Params(minShort: 10, minTasks: 2), shortTasks: new[] { "A", "B", "C", "D" });
            s.Start(T0);
            s.Done(At(1));
            s.Done(At(2));
            s.Tick(At(11)); // now past min time with min tasks done

            Assert.Equal(PomodoroState.JoyPrompt, s.State);
        }

        [Fact]
        public void ShortPhase_Ends_WhenMaxTimeReached_EvenWithoutMinTasks()
        {
            var s = Session(Params(minShort: 10, minTasks: 5, maxShort: 30), shortTasks: new[] { "A", "B", "C" });
            s.Start(T0);
            s.Tick(At(30));

            Assert.Equal(PomodoroState.JoyPrompt, s.State);
        }

        [Fact]
        public void ShortPhase_Ends_WhenTasksRunOut()
        {
            var s = Session(Params(minTasks: 10), shortTasks: new[] { "A", "B" });
            s.Start(T0);
            s.Done(T0);
            s.Done(T0); // queue now empty

            Assert.Equal(PomodoroState.JoyPrompt, s.State);
            Assert.Null(s.CurrentTask);
        }

        [Fact]
        public void ShortPhase_Leftovers_IncludeCurrentAndRemaining()
        {
            var s = Session(Params(maxShort: 5), shortTasks: new[] { "A", "B", "C", "D" });
            s.Start(T0);
            s.Done(At(1)); // A done, current = B, remaining C,D
            s.Tick(At(5)); // max time -> end; leftovers = B,C,D

            // Verify leftovers by driving to Phase IV and reading them back.
            s.BeginPromptedPhase(At(5));           // Joy (empty) -> AdminPrompt
            s.BeginPromptedPhase(At(6));           // Admin (empty) -> Extra
            Assert.Equal(PomodoroState.Extra, s.State);
            Assert.Equal("B", s.CurrentTask.Title);
            s.Done(At(6));
            Assert.Equal("C", s.CurrentTask.Title);
        }

        // ---- Joy / Admin blocks --------------------------------------------------

        [Fact]
        public void JoyPrompt_Start_EntersJoy_WithCountUpTimer()
        {
            var s = Session(joy: new[] { "J1", "J2" });
            s.Start(T0);                    // empty short -> JoyPrompt immediately
            Assert.Equal(PomodoroState.JoyPrompt, s.State);

            s.BeginPromptedPhase(At(1));
            Assert.Equal(PomodoroState.Joy, s.State);
            Assert.Equal("J1", s.CurrentTask.Title);
            Assert.False(s.IsCountdownPhase);
            Assert.Equal(TimeSpan.Zero, s.CurrentTaskRemaining(At(3))); // no countdown in blocks
            Assert.Equal(TimeSpan.FromMinutes(2), s.CurrentTaskElapsed(At(3)));
        }

        [Fact]
        public void JoyPhase_Ends_WhenTimeBudgetSpent()
        {
            var s = Session(Params(joy: 60), joy: new[] { "J1", "J2", "J3" });
            s.Start(T0);
            s.BeginPromptedPhase(At(1));
            s.Tick(At(61)); // 60 minutes into the joy block

            Assert.Equal(PomodoroState.AdminPrompt, s.State);
        }

        [Fact]
        public void JoyPhase_Ends_WhenAllTasksDone()
        {
            var s = Session(Params(joy: 120), joy: new[] { "J1", "J2" });
            s.Start(T0);
            s.BeginPromptedPhase(At(1));
            s.Done(At(2));
            s.Done(At(3)); // both done

            Assert.Equal(PomodoroState.AdminPrompt, s.State);
        }

        // ---- Phase IV composition + finish --------------------------------------

        [Fact]
        public void Extra_PutsDefaultAdminTasksOnTopOfLeftoverShortTasks()
        {
            var s = Session(
                Params(maxShort: 1, minTasks: 99),
                shortTasks: new[] { "S1", "S2" },
                defAdmin: new[] { "AdminDefault" });
            s.Start(T0);
            s.Tick(At(1));               // short phase over at max time; leftovers S1,S2
            s.BeginPromptedPhase(At(1)); // Joy empty -> AdminPrompt
            s.BeginPromptedPhase(At(1)); // Admin empty -> Extra

            Assert.Equal(PomodoroState.Extra, s.State);
            Assert.Equal("AdminDefault", s.CurrentTask.Title); // default admin on top
            s.Done(At(1));
            Assert.Equal("S1", s.CurrentTask.Title);
        }

        [Fact]
        public void FullFlow_ReachesFinished_WithFourPhaseResults()
        {
            var s = Session(
                Params(minTasks: 1, minShort: 0, maxShort: 60, joy: 60, admin: 30),
                shortTasks: new[] { "S1" },
                joy: new[] { "J1" },
                admin: new[] { "A1" });

            s.Start(T0);
            s.Done(At(1));                 // S1 done, short queue empty -> JoyPrompt
            Assert.Equal(PomodoroState.JoyPrompt, s.State);

            s.BeginPromptedPhase(At(2));
            s.Done(At(3));                 // J1 done -> AdminPrompt
            Assert.Equal(PomodoroState.AdminPrompt, s.State);

            s.BeginPromptedPhase(At(4));
            s.Done(At(5));                 // A1 done -> Extra (empty) -> Finished
            Assert.Equal(PomodoroState.Finished, s.State);

            var kinds = s.Results.Select(r => r.Kind).ToList();
            Assert.Equal(
                new[] { PomodoroPhaseKind.ShortEasy, PomodoroPhaseKind.Joy, PomodoroPhaseKind.Admin, PomodoroPhaseKind.Extra },
                kinds);
        }

        [Fact]
        public void Summary_RecordsDoneTasksDeferralsAndDuration()
        {
            var s = Session(Params(minTasks: 2, minShort: 5, maxShort: 60),
                            shortTasks: new[] { "A", "B", "C" });
            s.Start(T0);
            s.Defer(At(1));   // A deferred
            s.Done(At(2));    // B done
            s.Done(At(3));    // C done
            s.Done(At(6));    // A done (came around) -> 3 done, past min time & min tasks -> end

            var phaseI = s.Results[0];
            Assert.Equal(new[] { "B", "C", "A" }, phaseI.DoneTasks.ToArray());
            Assert.Equal(1, phaseI.DeferredCount);
            Assert.NotNull(phaseI.Ended);
            Assert.Equal(TimeSpan.FromMinutes(6), phaseI.Duration(At(99)));
        }

        // ---- timers & guards -----------------------------------------------------

        [Fact]
        public void CurrentTaskRemaining_CountsDownFromTimePerTask()
        {
            var s = Session(Params(timePerTask: 5), shortTasks: new[] { "A" });
            s.Start(T0);
            Assert.Equal(TimeSpan.FromMinutes(5), s.CurrentTaskRemaining(T0));
            Assert.Equal(TimeSpan.FromMinutes(2), s.CurrentTaskRemaining(At(3)));
            Assert.True(s.CurrentTaskRemaining(At(7)) < TimeSpan.Zero); // overtime
        }

        [Fact]
        public void DoneAndDefer_AreNoOps_WhenNoTaskActive()
        {
            var s = Session(); // all empty
            s.Start(T0);       // -> JoyPrompt, no active task
            var ex1 = Record.Exception(() => s.Done(T0));
            var ex2 = Record.Exception(() => s.Defer(T0));
            Assert.Null(ex1);
            Assert.Null(ex2);
            Assert.Equal(PomodoroState.JoyPrompt, s.State);
        }

        [Fact]
        public void Start_IsIgnored_WhenAlreadyStarted()
        {
            var s = Session(shortTasks: new[] { "A", "B" });
            s.Start(T0);
            s.Done(T0);
            s.Start(At(1)); // should not reset
            Assert.Equal("B", s.CurrentTask.Title);
        }
    }
}
