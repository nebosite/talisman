using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using Talisman.Properties;

namespace Talisman
{
    public delegate void NotificationHandler(NotificationData data);

    // --------------------------------------------------------------------------
    /// <summary>
    /// The Application Model
    /// </summary>
    // --------------------------------------------------------------------------
    public class AppModel : BaseModel
    {
        public string VersionText => "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString();
        Timer _tickTimer; 

        /// <summary>
        /// Current Timer properties
        /// </summary>
        public TimeSpan CurrentTimeRemaining=> (ActiveTimers.Count == 0) ? TimeSpan.Zero: DateTime.Now - ActiveTimers[0].EndsAt;
        public string CurrentTimeRemainingText => CurrentTimeRemaining.ToString(@"hh\:mm\:ss\.f");
        public string CurrentTimerName => (ActiveTimers.Count == 0) ? "No Timers Are Active." : ActiveTimers[0].Name + $" ({ActiveTimers[0].EndsAt.ToString(@"hh\:mm tt")})";


        string _quickTimerName = "Quick Timer";
        public string QuickTimerName
        {
            get => _quickTimerName;
            set
            {
                _quickTimerName = value;
                NotifyPropertyChanged(nameof(QuickTimerName));
            }
        }

        string _customQuickTime = "60";
        public string CustomQuickTime
        {
            get => _customQuickTime;
            set
            {
                _customQuickTime = value;
                NotifyPropertyChanged(nameof(CustomQuickTime));
            }
        }

        /// <summary>
        /// Timer notifications
        /// </summary>
        public event NotificationHandler OnNotification;

        /// <summary>
        /// All the timers
        /// </summary>
        public ObservableCollection<TimerInstance> ActiveTimers { get; set; } = new ObservableCollection<TimerInstance>();

        /// <summary>
        /// All the calendars
        /// </summary>
        public ObservableCollection<CalendarItem> Calendars { get; set; } = new ObservableCollection<CalendarItem>();


        Action<Action> _dispatch;
        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public AppModel(Action<Action> dispatch)
        {
            _dispatch = dispatch;
            _tickTimer = new Timer();
            _tickTimer.Elapsed += TimerTick;
            _tickTimer.Interval = 100;

            if(!string.IsNullOrEmpty(Settings.Default.Calendars))
            {
                var endPoints = JsonConvert.DeserializeObject<string[]>(Settings.Default.Calendars);
                foreach (var endPoint in endPoints)
                {
                    AddCalendar(endPoint);
                }
            }
            _tickTimer.Start();
        }

        OutlookHelper _outlook;
        DateTime _nextCalendarCheck = DateTime.MinValue;
        HotKeyHelper _hotKeys;
        uint key_5min;
        uint key_quickTimer;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Set up hotkey listening
        /// </summary>
        // --------------------------------------------------------------------------
        internal void InitHotKeys(MainWindow mainWindow)
        {
            _hotKeys = new HotKeyHelper(mainWindow, HandleHotKey);
            key_5min = _hotKeys.ListenForHotKey(System.Windows.Forms.Keys.D5, HotKeyModifiers.Control);
            key_quickTimer = _hotKeys.ListenForHotKey(System.Windows.Forms.Keys.Z, HotKeyModifiers.Control | HotKeyModifiers.Shift);
            //hotKey2 = _hotKeys.ListenForHotKey(System.Windows.Forms.Keys.F9, HotKeyModifiers.WindowsKey | HotKeyModifiers.Shift);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Hotkey handler.  The keyId is the return value from ListenForHotKey()
        /// </summary>
        // --------------------------------------------------------------------------
        void HandleHotKey(int keyId)
        {
            if (keyId == key_5min)
            {
                this.StartTimer(5, "Hotkey Timer");
            }
            else if(keyId == key_quickTimer)
            {
                this.StartTimer(.01, "Hotkey Timer");
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Look at the outlook calendar for stuff
        /// </summary>
        // --------------------------------------------------------------------------
        internal void CheckCalendars()
        {
            var stopwatch = Stopwatch.StartNew();
            foreach(var calendarItem in Calendars.ToArray())
            {
                try
                {
                    if(calendarItem.EndPoint == "Outlook")
                    {
                        if (_outlook == null) _outlook = new OutlookHelper();

                        foreach(var item in _outlook.GetNextTimerRelatedItems())
                        {
                            StartTimer(item.Start, "Outlook: " + item.Title, noDuplicates: true);
                        }
                    }
                }
                catch(Exception e)
                {
                    MessageBox.Show("Calendar error: " + e.ToString());
                }
            }
            stopwatch.Stop();
            Debug.WriteLine($"Check Calendars took {stopwatch.ElapsedMilliseconds}ms");
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Do this while the timer is going
        /// </summary>
        // --------------------------------------------------------------------------
        private void TimerTick(object sender, ElapsedEventArgs e)
        {
            if(DateTime.Now > _nextCalendarCheck)
            {
                _nextCalendarCheck = DateTime.Now.AddMinutes(10);
                CheckCalendars();
            }

            if (ActiveTimers.Count == 0) return;

            TimerInstance[] finishedTimers;
            lock(ActiveTimers)
            {
                finishedTimers = ActiveTimers.Where(t => t.EndsAt < DateTime.Now).ToArray();
            }

            if(finishedTimers.Length > 0)
            {
                RemoveTimers(finishedTimers);
                foreach(var timer in finishedTimers)
                {
                    OnNotification.Invoke(new NotificationData("Times up!", timer.Name));
                }
            }
            NotifyPropertyChanged(nameof(CurrentTimeRemaining));
            NotifyPropertyChanged(nameof(CurrentTimeRemainingText));
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Safely remove some timers
        /// </summary>
        // --------------------------------------------------------------------------
        private void RemoveTimers(TimerInstance[] timers)
        {
            foreach (var timer in timers)
            {
                _dispatch(() =>
                {
                    lock (ActiveTimers)
                    {
                        ActiveTimers.Remove(timer);
                    }

                });
            }
            NotifyAllPropertiesChanged();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a timer for some span of time
        /// </summary>
        // --------------------------------------------------------------------------
        internal void StartTimer(double minutes, string name = null, bool noDuplicates = false)
        {
            var endTime = DateTime.Now.AddMinutes(minutes);
            var timerName = name ?? $"{QuickTimerName} [{minutes.ToString(".0")} min]";
            StartTimerInternal(endTime, timerName, noDuplicates);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a timer at some absolute time
        /// </summary>
        // --------------------------------------------------------------------------
        internal void StartTimer(DateTime endTime, string name = null, bool noDuplicates = false)
        {
            var timerName = name ?? $"{QuickTimerName} [{endTime.ToString(@"hh\:mm tt")}]";
            StartTimerInternal(endTime, timerName, noDuplicates);
        }


        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a timer at some absolute time
        /// </summary>
        // --------------------------------------------------------------------------
        internal void StartTimerInternal(DateTime endTime, string timerName, bool noDuplicates)
        {
            if(noDuplicates)
            {
                if(ActiveTimers.Where(t => t.Name == timerName).Any())
                {
                    return;
                }
            }

            _dispatch(() =>
            {
                var newTimer = new TimerInstance(endTime, timerName,
                    (id) => RemoveTimers(ActiveTimers.Where(t => t.Id == id).ToArray()));
                for (int i = 0; i < ActiveTimers.Count; i++)
                {
                    if (newTimer.EndsAt < ActiveTimers[i].EndsAt)
                    {

                        lock (ActiveTimers)
                        {
                            ActiveTimers.Insert(i, newTimer);
                        }
                        newTimer = null;
                        break;
                    }
                }
                if (newTimer != null)
                {
                    lock (ActiveTimers)
                    {
                        ActiveTimers.Add(newTimer);
                    }
                }
                NotifyAllPropertiesChanged();

            });
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Delete a calendar item
        /// </summary>
        // --------------------------------------------------------------------------
        void DeleteCalendar(string endpoint)
        {
            var calendarItem = Calendars.Where(c => c.EndPoint.ToLower() == endpoint.ToLower()).FirstOrDefault();
            if (calendarItem == null) return;
            _dispatch.Invoke(() =>
            {
                lock(Calendars)
                {
                    Calendars.Remove(calendarItem);
                }

                SaveCalendarSettings();
            });

        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Save off current calendar settings
        /// </summary>
        // --------------------------------------------------------------------------
        void SaveCalendarSettings()
        {
            Settings.Default.Calendars = JsonConvert.SerializeObject(Calendars.Select(c => c.EndPoint).ToArray());
            Settings.Default.Save();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Add a calendar
        /// </summary>
        // --------------------------------------------------------------------------
        internal void AddCalendar(string calendarPointer)
        {
            if(!Calendars.Where(c => c.EndPoint.ToLower() == calendarPointer.ToLower()).Any())
            {
                Calendars.Add(new CalendarItem( calendarPointer, DeleteCalendar));
                SaveCalendarSettings();
            }
        }
    }
}
