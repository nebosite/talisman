using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Talisman.Properties;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Bindable wrapper around <see cref="PomodoroConfigData"/> for the settings
    /// tab. Setters push into the data object, raise change notifications, and
    /// invoke a persist callback. The parsing / session-building logic is
    /// independent of the persistence store so it can be unit-tested directly.
    /// </summary>
    // --------------------------------------------------------------------------
    public class PomodoroSettings : BaseModel
    {
        readonly PomodoroConfigData _data;
        readonly Action _persist;

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor. <paramref name="persist"/> is called after any change (null = none).
        /// </summary>
        // --------------------------------------------------------------------------
        public PomodoroSettings(PomodoroConfigData data = null, Action persist = null)
        {
            _data = data ?? new PomodoroConfigData();
            _persist = persist;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Load the settings from Settings.PomodoroConfig, wiring persistence back
        /// to the same setting.
        /// </summary>
        // --------------------------------------------------------------------------
        public static PomodoroSettings FromSettings()
        {
            PomodoroConfigData data = null;
            try
            {
                var json = Settings.Default.PomodoroConfig;
                if (!string.IsNullOrWhiteSpace(json))
                    data = JsonConvert.DeserializeObject<PomodoroConfigData>(json);
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read Pomodoro config; using defaults.", ex);
            }
            data = data ?? new PomodoroConfigData();

            return new PomodoroSettings(data, () =>
            {
                try
                {
                    Settings.Default.PomodoroConfig = JsonConvert.SerializeObject(data);
                    Settings.Default.Save();
                }
                catch (Exception ex)
                {
                    Log.Error("Could not save Pomodoro config.", ex);
                }
            });
        }

        void Set(Action apply, string propertyName)
        {
            apply();
            _persist?.Invoke();
            NotifyPropertyChanged(propertyName);
        }

        public string ShortTasks { get => _data.ShortTasks; set => Set(() => _data.ShortTasks = value, nameof(ShortTasks)); }
        public string JoyTasks { get => _data.JoyTasks; set => Set(() => _data.JoyTasks = value, nameof(JoyTasks)); }
        public string AdminTasks { get => _data.AdminTasks; set => Set(() => _data.AdminTasks = value, nameof(AdminTasks)); }
        public string DefaultShortTasks { get => _data.DefaultShortTasks; set => Set(() => _data.DefaultShortTasks = value, nameof(DefaultShortTasks)); }
        public string DefaultAdminTasks { get => _data.DefaultAdminTasks; set => Set(() => _data.DefaultAdminTasks = value, nameof(DefaultAdminTasks)); }

        public int TimePerTaskMinutes { get => _data.TimePerTaskMinutes; set => Set(() => _data.TimePerTaskMinutes = value, nameof(TimePerTaskMinutes)); }
        public int MinShortMinutes { get => _data.MinShortMinutes; set => Set(() => _data.MinShortMinutes = value, nameof(MinShortMinutes)); }
        public int MinTasks { get => _data.MinTasks; set => Set(() => _data.MinTasks = value, nameof(MinTasks)); }
        public int MaxShortMinutes { get => _data.MaxShortMinutes; set => Set(() => _data.MaxShortMinutes = value, nameof(MaxShortMinutes)); }
        public int JoyMinutes { get => _data.JoyMinutes; set => Set(() => _data.JoyMinutes = value, nameof(JoyMinutes)); }
        public int AdminMinutes { get => _data.AdminMinutes; set => Set(() => _data.AdminMinutes = value, nameof(AdminMinutes)); }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Split a newline-separated block into trimmed, non-empty task titles.
        /// </summary>
        // --------------------------------------------------------------------------
        public static List<string> ParseTasks(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();
            return text
                .Replace("\r\n", "\n")
                .Split('\n', '\r')
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Build the timing parameters from the configured minutes. Non-positive
        /// values fall back to the defaults so a blank field can't break a session.
        /// </summary>
        // --------------------------------------------------------------------------
        public PomodoroParameters BuildParameters()
        {
            var defaults = new PomodoroParameters();
            TimeSpan Minutes(int m, TimeSpan fallback) => m > 0 ? TimeSpan.FromMinutes(m) : fallback;

            return new PomodoroParameters
            {
                TimePerTask = Minutes(_data.TimePerTaskMinutes, defaults.TimePerTask),
                MinShortTime = Minutes(_data.MinShortMinutes, defaults.MinShortTime),
                MinTasks = _data.MinTasks > 0 ? _data.MinTasks : defaults.MinTasks,
                MaxShortTime = Minutes(_data.MaxShortMinutes, defaults.MaxShortTime),
                JoyTime = Minutes(_data.JoyMinutes, defaults.JoyTime),
                AdminTime = Minutes(_data.AdminMinutes, defaults.AdminTime),
            };
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Build a fresh session from the current configuration.
        /// </summary>
        // --------------------------------------------------------------------------
        public PomodoroSession CreateSession()
        {
            return new PomodoroSession(
                BuildParameters(),
                ParseTasks(_data.ShortTasks),
                ParseTasks(_data.JoyTasks),
                ParseTasks(_data.AdminTasks),
                ParseTasks(_data.DefaultShortTasks),
                ParseTasks(_data.DefaultAdminTasks));
        }
    }
}
