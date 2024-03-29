﻿using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Talisman.Properties;

namespace Talisman
{
    public delegate void NotificationHandler(TimerInstance data);

    class RecentTimerData { 
        public string EndsAt;
        public string visibleTime;
        public string location;
        public string Description;
        public TimerInstance.LinkDetails[] links;
    }


    // --------------------------------------------------------------------------
    /// <summary>
    /// The Application Model
    /// </summary>
    // --------------------------------------------------------------------------
    public class AppModel : BaseModel
    {
        public string Title => "Talisman " + VersionText;
        public string VersionText => "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString();
        DispatcherTimer _tickTimer;

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

        public HotKeyOption[] HotKeyOptions { get; set; }

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
        public TimeSpan CurrentTimeRemaining => (ActiveTimers.Count == 0) ? TimeSpan.Zero : DateTime.Now - ActiveTimers[0].EndsAt;
        public string CurrentTimeRemainingText => CurrentTimeRemaining.ToString(@"hh\:mm\:ss\.f");
        public string CurrentTimerName => (ActiveTimers.Count == 0) ? "No Timers Are Active." : ActiveTimers[0].Description + $" ({ActiveTimers[0].VisibleTime.ToString(@"hh\:mm tt")})";

        /// <summary>
        /// Stopwatch properties
        /// </summary>
        private Stopwatch _stopwatch = new Stopwatch();
        public string StopwatchText => _stopwatch.Elapsed.ToString(@"h\:mm\:ss\.ff");
        public Brush StopwatchColor => new SolidColorBrush(Color.FromArgb(_stopwatch.IsRunning || _stopwatch.ElapsedMilliseconds > 0 ? (byte)180 : (byte)50, 0, 0, 0));

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

        public bool CheckForNewVersion
        {
            get => Settings.Default.CheckForNewVersions == "Yes";
            set
            {
                Settings.Default.CheckForNewVersions = value ? "Yes" : "No";
                Settings.Default.Save();
                NotifyPropertyChanged(nameof(CheckForNewVersion));
            }
        }

        public bool RunAtStartup
        {
            get {
                var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                return runKey.GetValue("Talisman") != null;
            }
            set
            {
                var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (value)
                {
                    try
                    {
                        runKey.SetValue("Talisman", Assembly.GetEntryAssembly().Location);
                    }
                    catch(Exception)
                    {
                        MessageBox.Show("Talisman must be run as administrator to set this value.");
                    }
                }
                else
                {
                    runKey.DeleteValue("Talisman");
                }

                NotifyPropertyChanged(nameof(RunAtStartup));
            }
        }

        public string[] LinkIgnorePatterns
        {
            get {
                var json = Settings.Default.LinkIgnorePatterns;
                if (string.IsNullOrEmpty(json)) return new string[0];
                return JsonConvert.DeserializeObject<string[]>(json);
            }
            set
            {
                Settings.Default.LinkIgnorePatterns = JsonConvert.SerializeObject(value);
                Settings.Default.Save();
                NotifyPropertyChanged(nameof(LinkIgnorePatterns));
            }
        }

        public class LinkRename
        {
            public string pattern;
            public string newName;
        }

        public LinkRename[] LinkRenamePatterns
        {
            get
            {
                var json = Settings.Default.LinkRenamePatterns;
                if (string.IsNullOrEmpty(json)) return new LinkRename[0];
                return JsonConvert.DeserializeObject<LinkRename[]>(json);
            }
            set
            {
                Settings.Default.LinkRenamePatterns = JsonConvert.SerializeObject(value);
                Settings.Default.Save();
                NotifyPropertyChanged(nameof(LinkRenamePatterns));
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
        /// All the timers
        /// </summary>
        public ObservableCollection<TimerInstance> RecentTimers { get; set; } = new ObservableCollection<TimerInstance>();

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
            _tickTimer = new DispatcherTimer();
            _tickTimer.Tick += TimerTick;
            _tickTimer.Interval = TimeSpan.FromSeconds(.017);
            Task.Delay(50).ContinueWith((t) => ReadTimersFromSettings());

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

        // --------------------------------------------------------------------------
        /// <summary>
        /// Load up remembered timers
        /// </summary>
        // --------------------------------------------------------------------------
        private void ReadTimersFromSettings()
        {
            var currentTimersSetting = Settings.Default.CurrentTimers;
            if (currentTimersSetting != null && currentTimersSetting.Trim() != "")
            {
                try
                {
                    var timers = JsonConvert.DeserializeObject<RecentTimerData[]>(currentTimersSetting);
                    foreach (var timer in timers)
                    {
                        try
                        {
                            var timerDate = DateTime.Parse(timer.EndsAt);
                            var newInstance = new TimerInstance(
                                DateTime.Parse(timer.EndsAt),
                                DateTime.Parse(timer.visibleTime),
                                timer.location,
                                timer.Description,
                                timer.links);
                            StartTimer(newInstance);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine($"Failed on one of the timer settings. ({e.Message}) reading : " + currentTimersSetting);
                        }

                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Major failure ({e.Message}) reading the recent timers setting: " + currentTimersSetting);
                }
            }

            var recentTimersSetting = Settings.Default.RecentTimers;
            if (recentTimersSetting != null && recentTimersSetting.Trim() != "")
            {
                try
                {
                    var recents = JsonConvert.DeserializeObject<RecentTimerData[]>(recentTimersSetting);
                    foreach (var timer in recents)
                    {
                        try
                        {
                            var timerDate = DateTime.Parse(timer.EndsAt);
                            var newInstance = new TimerInstance(timerDate, timerDate, "recent", timer.Description);
                            AddToRecents(newInstance, true);
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine($"Failed on one of the timer settings. ({e.Message}) reading : " + recentTimersSetting);
                        }

                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Major failure ({e.Message}) reading the recent timers setting: " + recentTimersSetting);
                }
            }

            RecentTimers.CollectionChanged += RecentTimers_CollectionChanged;
            ActiveTimers.CollectionChanged += ActiveTimers_CollectionChanged;
        }
        private void RecentTimers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Settings.Default.RecentTimers = JsonConvert.SerializeObject(this.RecentTimers.Select(t => new { 
                EndsAt = t.EndsAt, 
                Description = t.Description }));
        }

        private void ActiveTimers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Settings.Default.CurrentTimers = JsonConvert.SerializeObject(this.ActiveTimers.Select(t => new { 
                EndsAt = t.EndsAt, 
                visibleTime = t.VisibleTime,
                location = t.Location,
                links = t.Links.ToArray(),
                Description = t.Description 
            }));
            Debug.WriteLine("ACTIVE TIMERS CHANGED: " + Settings.Default.CurrentTimers);
        }


        // --------------------------------------------------------------------------
        /// <summary>
        /// PromoteRecentTimer
        /// </summary>
        // --------------------------------------------------------------------------
        private void PromoteRecentTimer(TimerInstance instance)
        {
            Debug.Write($"Promoting {instance.Description}");
            var now = DateTime.Now;
            var endTime = new DateTime(now.Year, now.Month, now.Day, instance.EndsAt.Hour, instance.EndsAt.Minute, instance.EndsAt.Second);
            if (DateTime.Now > endTime) endTime = DateTime.Now.AddHours(1);
            var visibleTime = endTime.AddMinutes(-3);
            var newInstance = new TimerInstance(endTime, visibleTime, instance.Location, instance.Description, instance.Links?.ToArray());
            newInstance.OnDeleted = () => RemoveTimers(ActiveTimers, new int[] { newInstance.Id });
            StartTimer(newInstance);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// AddToRecents
        /// </summary>
        // --------------------------------------------------------------------------
        private void AddToRecents(TimerInstance instance, Boolean addToEnd = false)
        {
            instance.OnPromote = () => PromoteRecentTimer(instance);
            instance.OnDeleted = () => RemoveTimers(RecentTimers,new int[] { instance.Id }, false);
            this.RecentTimers.Insert(addToEnd ? this.RecentTimers.Count : 0, instance);
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
            var seenUrls = new HashSet<string>();

            // Extract links
            var urlMatches = Regex.Matches(item.Location + " " + item.Contents, @"(?<links>(http.*?://[^\s^;^\>]+)+)");
            foreach (Match urlMatch in urlMatches)
            {
                var url = urlMatch.Groups[1].Value.TrimEnd('>');
             
                Debug.WriteLine("Found link match: " + urlMatch.Groups[1].Value);

                // ignore links with user-specified keywords (eg:  "dialin", "mysettings")
                var ignore = false;
                foreach(var pattern in this.LinkIgnorePatterns)
                {
                    if(Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
                    {
                        Debug.WriteLine("    Ingnoring because " + pattern);
                        ignore = true;
                        break;
                    }
                }
                if (ignore) continue;

                // TODO: Apply names to links with user-specified matches + keywords (eg:  meetup-join => "Join Teams Meeting"


                if (!seenUrls.Contains(url.ToLowerInvariant()))
                {
                    seenUrls.Add(url.ToLowerInvariant());
                    var Text = Regex.Replace(url, "^ht.*?//", "");

                    foreach (var pattern in this.LinkRenamePatterns)
                    {
                        if (Regex.IsMatch(url, pattern.pattern, RegexOptions.IgnoreCase))
                        {
                            Text = pattern.newName;
                            Debug.WriteLine("    Renaming to " + Text);
                            break;
                        }
                    }

                    links.Add(new TimerInstance.LinkDetails()
                    {
                        Uri = url,
                        Text = Text
                    });

                    if (links.Count >= 4) break;
                }

            }

            // Shrink the location and pull out any links
            foreach (var untrimmedPart in locationParts)
            {
                Debug.WriteLine("Location parts: " + untrimmedPart);
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
            newInstance.OnDismiss += () => { AddToRecents(newInstance); };
          
            if (!this.TimerExists(newInstance))
            {
                StartTimer(newInstance);
            }
        }

  

        int _frame = 0;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Do this while the timer is going
        /// </summary>
        // --------------------------------------------------------------------------
        private void TimerTick(object sender, EventArgs e)
        {
            var shouldUpdate = (_frame % 1) == 0;
            _frame++;

            if(DateTime.Now > _nextCalendarCheck)
            {
                _nextCalendarCheck = DateTime.Now.AddMinutes(10);
                CheckCalendars();
            }

            if(_stopwatch.IsRunning && shouldUpdate) NotifyPropertyChanged(nameof(StopwatchText));
            if (ActiveTimers.Count == 0) return;

            TimerInstance[] finishedTimers;
            lock(ActiveTimers)
            {
                finishedTimers = ActiveTimers.Where(t => t.EndsAt < DateTime.Now).ToArray();
            }

            if(finishedTimers.Length > 0)
            {
                RemoveTimers(ActiveTimers, finishedTimers.Select(t => t.Id).ToArray());
                foreach(var timer in finishedTimers)
                {
                    OnNotification.Invoke(timer);
                }
            }
            if (shouldUpdate || ActiveTimers.Count == 0)
            {
                NotifyPropertyChanged(nameof(CurrentTimeRemaining));
                NotifyPropertyChanged(nameof(CurrentTimeRemainingText));
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Safely remove some timers
        /// </summary>
        // --------------------------------------------------------------------------
        private void RemoveTimers(ObservableCollection<TimerInstance> timerCollection, int[] timerIds, bool cancel = true)
        {
            var timers = timerCollection.Where(t => timerIds.Contains(t.Id)).ToArray();
            foreach (var timer in timers)
            {
                _dispatch(() =>
                {
                    lock (timerCollection)
                    {
                        timerCollection.Remove(timer);
                        if(cancel) _cancelledInstances.Add(timer);
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
            newInstance.OnDismiss += () => { AddToRecents(newInstance); };

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
                instance.OnDeleted = () => RemoveTimers(ActiveTimers, new int[] { instance.Id });
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
