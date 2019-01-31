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
using System.Windows.Input;
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

        HotKeyAssignment _openHotKey = null;
        public HotKeyAssignment OpenHotKey
        {
            get => _openHotKey;
            set
            {
                _openHotKey = value;
                NotifyPropertyChanged(nameof(OpenHotKey));
                _openHotKey.NotifyAllPropertiesChanged();
            }
        }

        public HotKeyOption[]  HotKeyOptions { get; set; }

        HotKeyOption _selectedHotKeyOption;
        public HotKeyOption SelectedHotKeyOption
        {
            get => _selectedHotKeyOption;
            set
            {
                _selectedHotKeyOption = value;
                NotifyPropertyChanged(nameof(SelectedHotKeyOption));
            }
        }

        public ObservableCollection<HotKeyAssignment> HotKeyAssignments { get; set; } = new ObservableCollection<HotKeyAssignment>();

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

        string _hotKeyOptionValue = "";
        public string HotKeyOptionValue
        {
            get => _hotKeyOptionValue;
            set
            {
                _hotKeyOptionValue = value;
                NotifyPropertyChanged(nameof(HotKeyOptionValue));
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
        public ObservableCollection<Calendar> Calendars { get; set; } = new ObservableCollection<Calendar>();


        Action<Action> _dispatch;
        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public AppModel(Action<Action> dispatch)
        {
            HotKeyOptions = JsonConvert.DeserializeObject<HotKeyOption[]>(AssemblyHelper.GetResourceText("HotKeyOptions.json"));
            SelectedHotKeyOption = HotKeyOptions[0];
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
        IHotKeyTool _hotKeys;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Set up hotkey listening
        /// </summary>
        // --------------------------------------------------------------------------
        internal void InitHotKeys(IHotKeyTool hotKeyTool)
        {
            _hotKeys = hotKeyTool;
            var hotKeys = JsonConvert.DeserializeObject<HotKeyAssignment[]>(Settings.Default.HotKeys);
            foreach (var hotKey in hotKeys)
            {
                AssignHotKey(hotKey);
            }
        }

        List<UniqueInstance> _cancelledInstances = new List<UniqueInstance>();

        DateTime _nextCalendarErrorOKTime = DateTime.MinValue;

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

                        foreach(var item in _outlook.GetNextTimerRelatedItems(10))
                        {
                            StartTimer(item.Start.AddMinutes(-3), "Outlook: " + item.Title, noDuplicates: true, instanceInfo: item.InstanceInfo);
                        }
                    }
                }
                catch(Exception e)
                {
                    if(DateTime.Now > _nextCalendarErrorOKTime)
                    {
                        MessageBox.Show("Calendar error: " + e.ToString());
                        _nextCalendarErrorOKTime = DateTime.Now.AddDays(1);
                    }
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
                        if(timer.InstanceInfo != null)
                        {
                            _cancelledInstances.Add(timer.InstanceInfo.Value);
                        }
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
        internal void StartTimer(double minutes, string name = null, bool noDuplicates = false, UniqueInstance? instanceInfo = null)
        {
            var endTime = DateTime.Now.AddMinutes(minutes);
            var timerName = name ?? $"{QuickTimerName} [{minutes.ToString(".0")} min]";
            StartTimerInternal(endTime, timerName, noDuplicates, instanceInfo);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a timer at some absolute time
        /// </summary>
        // --------------------------------------------------------------------------
        internal void StartTimer(DateTime endTime, string name = null, bool noDuplicates = false, UniqueInstance? instanceInfo = null)
        {
            var timerName = name ?? $"{QuickTimerName} [{endTime.ToString(@"hh\:mm tt")}]";
            StartTimerInternal(endTime, timerName, noDuplicates, instanceInfo);
        }


        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a timer at some absolute time
        /// </summary>
        // --------------------------------------------------------------------------
        internal void StartTimerInternal(DateTime endTime, string timerName, bool noDuplicates, UniqueInstance? instanceInfo)
        {
            // Since calendar items are added multiple times per day, we want to not add the item
            // if it is already on the timer list
            if (noDuplicates)
            {
                if (ActiveTimers.Where(t => t.Name == timerName).Any())
                {
                    return;
                }
            }

            // If an item has information about a unique instance, then we don't
            // want to start the timer of the instance is already cancelled.   e.g.:  When the
            // user does an early cancellation of a calendar item, we don't want to bring it up 
            // again when the app re-reads the calendar looking for new appointments.
            if (instanceInfo != null)
            {
                foreach (var cancelledInstance in _cancelledInstances.ToArray())
                {
                    // Remove old cancelled instances
                    if (cancelledInstance.Date.Day != DateTime.Now.Day)
                    {
                        _cancelledInstances.Remove(cancelledInstance);
                    }
                    else if (instanceInfo.Value.Id == cancelledInstance.Id)
                    {
                        return;
                    }
                }
            }

            _dispatch(() =>
            {
                var newTimer = new TimerInstance(endTime, timerName,
                    (id) => RemoveTimers(ActiveTimers.Where(t => t.Id == id).ToArray()));
                newTimer.InstanceInfo = instanceInfo;
                newTimer.PropertyChanged += (sender, args) =>
                {
                    NotifyPropertyChanged(nameof(CurrentTimerName));
                    NotifyPropertyChanged(nameof(CurrentTimeRemaining));
                    NotifyPropertyChanged(nameof(CurrentTimeRemainingText));
                };
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
        /// Assign a hotkey to the selected hotkey item
        /// </summary>
        // --------------------------------------------------------------------------
        internal void AssignHotKey(HotKeyAssignment assignment = null)
        {
            var clearOpenKey = true;
            if (assignment == null)
            {
                assignment = OpenHotKey;
                assignment.OptionName = SelectedHotKeyOption.Name;
                assignment.OptionValue = HotKeyOptionValue;
                clearOpenKey = false;
            }
            
            assignment.Validate();
            if(HotKeyAssignments.Where(hk => hk == assignment).Any())
            {
                throw new ApplicationException("That hotkey is already assigned.");
            }

            Action hotKeyAction = null;
            switch(assignment.OptionName)
            {
                case "Quick Timer":
                    if (double.TryParse(assignment.OptionValue, out var minutes))
                    {
                        hotKeyAction = () => StartTimer(minutes, "Quick Timer");
                    }
                    else
                    {
                        hotKeyAction = () => MessageBox.Show($"Failed action {assignment.OptionName}- bad argument.");
                    }
                    break;
                case "Quick Email":
                    hotKeyAction = () => new QuickMailSender(new QuickMailItem(assignment.OptionValue, _outlook)).ShowDialog();
                    break;
                case "Lock + Screensaver":
                    hotKeyAction = () => ScreenSaverHelper.ActivateScreenSaver(lockWorkstation: true);
                    break;
                case "Screensaver":
                    hotKeyAction = () => ScreenSaverHelper.ActivateScreenSaver(lockWorkstation: false);
                    break;
                case "StartSnip":
                    hotKeyAction = () => ScreenHelper.StartSnippingTool();
                    break;
                default:
                    hotKeyAction = () => MessageBox.Show($"No action available for {assignment.OptionName}");
                    break;
            }

            HotKeyAssignments.Add(assignment);
            Activate(assignment, hotKeyAction);

            if(clearOpenKey) OpenHotKey = new HotKeyAssignment();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Delete the specified HotKey
        /// </summary>
        // --------------------------------------------------------------------------
        internal void DeleteHotHey(int hotkeyId)
        {
            _hotKeys.StopListeningForHotKey(hotkeyId);
            var assignment = this.HotKeyAssignments.Where(a => a.Id == hotkeyId).FirstOrDefault();
            if (assignment != null) HotKeyAssignments.Remove(assignment);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Engage this hotkey with the system
        /// </summary>
        // --------------------------------------------------------------------------
        private void Activate(HotKeyAssignment hotKeyAssignment, Action hotKeyAction)
        {
            hotKeyAssignment.Id =  _hotKeys.ListenForHotKey(hotKeyAssignment.Letter, hotKeyAssignment.Modifiers, hotKeyAction);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Update the remembered user settings
        /// </summary>
        // --------------------------------------------------------------------------
        public void UpdateSettings()
        {
            Settings.Default.HotKeys = JsonConvert.SerializeObject(HotKeyAssignments.ToArray());
            Settings.Default.Save();
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
                Calendars.Add(new Calendar( calendarPointer, DeleteCalendar));
                SaveCalendarSettings();
            }
        }
    }
}
