using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Talisman.Properties;

namespace Talisman
{
    public delegate void NotificationHandler(TimerInstance data);

    // --------------------------------------------------------------------------
    /// <summary>
    /// The Application Model
    /// </summary>
    // --------------------------------------------------------------------------
    public class AppModel : BaseModel
    {
        public string Title => "Talisman " + VersionText;
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

        public event Action OnCenter;

        /// <summary>
        /// Current Timer properties
        /// </summary>
        public TimeSpan CurrentTimeRemaining=> (ActiveTimers.Count == 0) ? TimeSpan.Zero: DateTime.Now - ActiveTimers[0].EndsAt;
        public string CurrentTimeRemainingText => CurrentTimeRemaining.ToString(@"hh\:mm\:ss\.f");
        public string CurrentTimerName => (ActiveTimers.Count == 0) ? "No Timers Are Active." : ActiveTimers[0].Description + $" ({ActiveTimers[0].VisibleTime.ToString(@"hh\:mm tt")})";

        /// <summary>
        /// Stopwatch properties
        /// </summary>
        private Stopwatch _stopwatch = new Stopwatch();
        public string StopwatchText => _stopwatch.Elapsed.ToString(@"h\:mm\:ss\.ff");
        public Brush StopwatchColor => new SolidColorBrush(Color.FromArgb(_stopwatch.IsRunning || _stopwatch.ElapsedMilliseconds > 0 ? (byte)180 : (byte)50, 0,0,0));

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
            _tickTimer.Interval = 30;

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

        List<TimerInstance> _cancelledInstances = new List<TimerInstance>();

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

                        foreach (var item in _outlook.GetNextTimerRelatedItems(10))
                            StartOutlookTimer(item);
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
        /// StartOutlookTimer
        /// </summary>
        // --------------------------------------------------------------------------
        private void StartOutlookTimer(TimeRelatedItem item)
        {
            if (item.Location == null) item.Location = "";
            var locationParts = item.Location.Split(';');
            var locationText = "";
            var links = new List<TimerInstance.LinkDetails>();

            // Extract links
            var urlMatches = Regex.Matches(item.Location + " " + item.Contents, @"(?<links>(http.*?://[^\s^;]+)+)");
            var previousLinkText = "ZZZ *** not set ***";
            foreach (Match urlMatch in urlMatches)
            {
                var url = urlMatch.Groups[1].Value;
                if (links.Count > 0) previousLinkText = links[0].Text.ToLowerInvariant();
                if (!url.ToLowerInvariant().Contains(previousLinkText))
                {
                    links.Add(new TimerInstance.LinkDetails()
                    {
                        Uri = url,
                        Text = Regex.Replace(url, "^ht.*?//", "")
                    });

                    if (links.Count >= 4) break;
                }

            }

            // Shrink the location and pull out any links
            foreach (var untrimmedPart in locationParts)
            {
                var part = untrimmedPart.Trim();
                if (part == "") continue;
                if(part.ToLowerInvariant().Contains("cr sea "))
                {
                    var match = Regex.Match(part, "CR SEA (.*) ");
                    if (match.Success) locationText += $"[{match.Groups[1].Value}]";
                    else locationText += $"[{part}]";
                }
            }

            var newInstance = new TimerInstance(
                item.Start.AddMinutes(-3),
                item.Start,
                locationText,
                item.Title,
                links.ToArray());
            if (!this.TimerExists(newInstance))
            {
                StartTimer(newInstance);
            }
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

            if(_stopwatch.IsRunning) NotifyPropertyChanged(nameof(StopwatchText));
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
                    OnNotification.Invoke(timer);
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
                        _cancelledInstances.Add(timer);
                    }
                });
            }
            NotifyAllPropertiesChanged();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Clear the stopwatch
        /// </summary>
        // --------------------------------------------------------------------------
        internal void ClearStopWatch()
        {
            _stopwatch.Stop();
            _stopwatch.Reset();
            NotifyPropertyChanged(nameof(StopwatchColor));
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start/Stop stopwatch
        /// </summary>
        // --------------------------------------------------------------------------
        internal void ToggleStopWatch()
        {
            if (_stopwatch.IsRunning) _stopwatch.Stop();
            else _stopwatch.Start();
            NotifyPropertyChanged(nameof(StopwatchColor));
        }


        // --------------------------------------------------------------------------
        /// <summary>
        /// Have we already seen this timer at some point in time?
        /// </summary>
        // --------------------------------------------------------------------------
        bool TimerExists(TimerInstance instance)
        {
            return ActiveTimers.ToArray().Where(t => t.UniqueId == instance.UniqueId).Any()
                || _cancelledInstances.ToArray().Where(t => t.UniqueId == instance.UniqueId).Any();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Quick timer
        /// </summary>
        // --------------------------------------------------------------------------
        internal void StartQuickTimer(double minutes, string message = null)=>
            StartQuickTimer(DateTime.Now.AddMinutes(minutes), message);

        internal void StartQuickTimer(DateTime time, string message = null)
        {
            if (message == null)
            {
                message = this.QuickTimerName;
            }
            var newInstance = new TimerInstance(time, time, "", message, null);
            StartTimer(newInstance);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Remove a timer from our lists
        /// </summary>
        // --------------------------------------------------------------------------
        internal void RemoveTimer(TimerInstance timerItem)
        {
            _cancelledInstances.Remove(timerItem);
            ActiveTimers.Remove(timerItem);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a timer at some absolute time
        /// </summary>
        // --------------------------------------------------------------------------
        internal void StartTimer(TimerInstance instance)
        {
            if (_cancelledInstances.ToArray().Where(t => t.Id == instance.Id).Any()
                || ActiveTimers.ToArray().Where(t => t.Id == instance.Id).Any())
            {
                throw new ApplicationException("Timer with the same id was already added");
            }

            _dispatch(() =>
            {
                instance.OnDeleted = () => RemoveTimers(ActiveTimers.Where(t => t.Id == instance.Id).ToArray());
                instance.PropertyChanged += (sender, args) =>
                {
                    NotifyPropertyChanged(nameof(CurrentTimerName));
                    NotifyPropertyChanged(nameof(CurrentTimeRemaining));
                    NotifyPropertyChanged(nameof(CurrentTimeRemainingText));
                };

                var added = false;
                for (int i = 0; i < ActiveTimers.Count; i++)
                {
                    if (instance.EndsAt < ActiveTimers[i].EndsAt)
                    {

                        lock (ActiveTimers)
                        {
                            ActiveTimers.Insert(i, instance);
                        }
                        added = true;
                        break;
                    }
                }
                if (!added)
                {
                    lock (ActiveTimers)
                    {
                        ActiveTimers.Add(instance);
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
                        hotKeyAction = () => StartQuickTimer(minutes, "Quick Timer");
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
                case "Recenter Talisman":
                    hotKeyAction = () => OnCenter?.Invoke();
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
