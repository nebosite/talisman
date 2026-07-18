using System;
using System.Linq;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for PomodoroSettings: task-list parsing, parameter conversion with
    /// fallbacks, session construction, and the persist callback.
    /// </summary>
    // --------------------------------------------------------------------------
    public class PomodoroSettingsTests
    {
        [Fact]
        public void ParseTasks_SplitsTrimsAndDropsBlankLines()
        {
            var result = PomodoroSettings.ParseTasks("  first \r\n\r\nsecond\n   \n third  ");
            Assert.Equal(new[] { "first", "second", "third" }, result.ToArray());
        }

        [Fact]
        public void ParseTasks_EmptyOrNull_ReturnsEmptyList()
        {
            Assert.Empty(PomodoroSettings.ParseTasks(null));
            Assert.Empty(PomodoroSettings.ParseTasks("   \r\n  "));
        }

        [Fact]
        public void BuildParameters_ConvertsMinutesToTimeSpans()
        {
            var data = new PomodoroConfigData
            {
                TimePerTaskMinutes = 7,
                MinShortMinutes = 20,
                MinTasks = 4,
                MaxShortMinutes = 45,
                JoyMinutes = 90,
                AdminMinutes = 40,
            };
            var p = new PomodoroSettings(data).BuildParameters();

            Assert.Equal(TimeSpan.FromMinutes(7), p.TimePerTask);
            Assert.Equal(TimeSpan.FromMinutes(20), p.MinShortTime);
            Assert.Equal(4, p.MinTasks);
            Assert.Equal(TimeSpan.FromMinutes(45), p.MaxShortTime);
            Assert.Equal(TimeSpan.FromMinutes(90), p.JoyTime);
            Assert.Equal(TimeSpan.FromMinutes(40), p.AdminTime);
        }

        [Fact]
        public void BuildParameters_FallsBackToDefaults_ForNonPositiveValues()
        {
            var data = new PomodoroConfigData
            {
                TimePerTaskMinutes = 0,
                MinShortMinutes = -5,
                MinTasks = 0,
                MaxShortMinutes = 0,
                JoyMinutes = 0,
                AdminMinutes = 0,
            };
            var p = new PomodoroSettings(data).BuildParameters();
            var defaults = new PomodoroParameters();

            Assert.Equal(defaults.TimePerTask, p.TimePerTask);
            Assert.Equal(defaults.MinShortTime, p.MinShortTime);
            Assert.Equal(defaults.MinTasks, p.MinTasks);
            Assert.Equal(defaults.MaxShortTime, p.MaxShortTime);
            Assert.Equal(defaults.JoyTime, p.JoyTime);
            Assert.Equal(defaults.AdminTime, p.AdminTime);
        }

        [Fact]
        public void CreateSession_WiresListsSoDefaultShortIsOnTopInPhaseI()
        {
            var data = new PomodoroConfigData
            {
                ShortTasks = "userShort",
                DefaultShortTasks = "defShort",
                JoyTasks = "joy1",
                AdminTasks = "admin1",
                DefaultAdminTasks = "defAdmin",
            };
            var session = new PomodoroSettings(data).CreateSession();
            session.Start(new DateTime(2026, 7, 15, 9, 0, 0));

            Assert.Equal("defShort", session.CurrentTask.Title); // default short on top
        }

        [Fact]
        public void Setter_InvokesPersistCallback_AndRaisesPropertyChanged()
        {
            var persisted = 0;
            var settings = new PomodoroSettings(new PomodoroConfigData(), () => persisted++);
            string changed = null;
            settings.PropertyChanged += (s, e) => changed = e.PropertyName;

            settings.JoyTasks = "guitar";

            Assert.Equal("guitar", settings.JoyTasks);
            Assert.Equal(1, persisted);
            Assert.Equal(nameof(settings.JoyTasks), changed);
        }

        [Fact]
        public void DefaultConfigData_MatchesSpecDefaults()
        {
            var d = new PomodoroConfigData();
            Assert.Equal(5, d.TimePerTaskMinutes);
            Assert.Equal(30, d.MinShortMinutes);
            Assert.Equal(5, d.MinTasks);
            Assert.Equal(60, d.MaxShortMinutes);
            Assert.Equal(120, d.JoyMinutes);
            Assert.Equal(60, d.AdminMinutes);
        }
    }
}
