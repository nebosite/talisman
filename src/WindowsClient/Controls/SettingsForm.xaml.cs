using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Interaction logic for SettingsForm.xaml
    /// </summary>
    // --------------------------------------------------------------------------
    public partial class SettingsForm : Window
    {
        AppModel _appModel;
        bool _hardClose = false;
        bool _exiting = false;

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public SettingsForm(AppModel appModel)
        {
            _appModel = appModel;
            InitializeComponent();
            this.DataContext = appModel;
            this.IsVisibleChanged += SettingsForm_IsVisibleChanged;
            this.Loaded += SettingsForm_Loaded;
            this.Deactivated += SettingsForm_Deactivated;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Deactivated
        /// </summary>
        // --------------------------------------------------------------------------
        private void SettingsForm_Deactivated(object sender, EventArgs e)
        {
            if(!_exiting) this.Close();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// OnLoaded
        /// </summary>
        // --------------------------------------------------------------------------
        private void SettingsForm_Loaded(object sender, RoutedEventArgs e)
        {
            HelpText.NavigateToStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("Talisman.Assets.Help.htm"));
            HelpText.Navigating += HelpText_Navigating;
        }

        private void HelpText_Navigating(object sender, System.Windows.Navigation.NavigatingCancelEventArgs e)
        {
            e.Cancel = true;
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }

        bool _justActivated = false;
        // --------------------------------------------------------------------------
        /// <summary>
        /// When we get shown
        /// </summary>
        // --------------------------------------------------------------------------
        private void SettingsForm_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(_justActivated)
            {
                _justActivated = false;
                Debug.WriteLine("Focusing");
                var elementWithFocus = Keyboard.FocusedElement as UIElement;
                if(elementWithFocus != null)
                {
                    elementWithFocus.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
                }
                else
                {
                    TimerNameBox.Focus();
                }
                TimerNameBox.SelectAll();

                double pixelsPerMinute = TimeClicker.ActualWidth / TimeClickerWindowInHours / 60;
                var hour = DateTime.Now.Hour;
                var minute = DateTime.Now.Minute;
                minute = ((((int)minute) / 5) * 5);

                double x = (minute - DateTime.Now.Minute) * pixelsPerMinute;
                x += 5 * pixelsPerMinute;
                while (x < TimeClicker.ActualWidth)
                {
                    minute += 5;
                    var length = 3;
                    if (minute % 15 == 0) length = 5;
                    if (minute == 60)
                    {
                        length = 8;
                        minute = 0;
                        hour++;
                        if (hour >= 24) hour = 0;
                        var labelText = new DateTime(2000, 1, 1, hour, 0, 0).ToString(@"htt");
                        var timeLable = new Label() { Content = labelText, FontSize = 8, Padding = new Thickness(1), IsHitTestVisible=false };
                        Canvas.SetLeft(timeLable, x - 10);
                        Canvas.SetTop(timeLable, 9);
                        TimeClicker.Children.Add(timeLable);
                    }
                    var rect = new Rectangle() { Width = 1, Height = length, Fill = Brushes.Black, Stroke=Brushes.Black, StrokeThickness = 1 };
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, 0);
                    TimeClicker.Children.Add(rect);
                    x += 5 * pixelsPerMinute;
                }
                TimeClicker.InvalidateVisual();
                this.UpdateLayout();

                //ScreenHelper.EnsureWindowIsVisible(this);
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Bring up the form for User input 
        /// </summary>
        // --------------------------------------------------------------------------
        public void Popup()
        {
            _justActivated = true;
            TimeClicker.Children.Clear();
            ClearHotKey(null, null);

            Show();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Closing 
        /// </summary>
        // --------------------------------------------------------------------------
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_hardClose)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a quick timer
        /// </summary>
        // --------------------------------------------------------------------------
        private void QuickTimerClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            var minutes = 10.0;
            if(button.Tag.ToString() == "Custom")
            {
                double.TryParse(_appModel.CustomQuickTime, out minutes);
            }
            else
            {
                double.TryParse(button.Tag.ToString(), out minutes);
            }
            
            _appModel.StartTimer(minutes);
            this.Hide();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Pick an absolute time for quick timer
        /// </summary>
        // --------------------------------------------------------------------------
        private void TimeClickerMouseUp(object sender, MouseButtonEventArgs e)
        {
            var clickTime = GetTimeClickerSelectedTime(sender as Canvas, e);
            _appModel.StartTimer(clickTime);
            this.Hide();
        }

        const double TimeClickerWindowInHours = 10;
        // --------------------------------------------------------------------------
        /// <summary>
        /// Get the time from the mouse position
        /// </summary>
        // --------------------------------------------------------------------------
        DateTime GetTimeClickerSelectedTime(Canvas timeClickerCanvas, MouseEventArgs e)
        {
            var position = e.GetPosition(timeClickerCanvas);
            var delta = position.X / timeClickerCanvas.ActualWidth;
            return DateTime.Now.AddHours(TimeClickerWindowInHours * delta);
        }


        // --------------------------------------------------------------------------
        /// <summary>
        /// Update the displayed time when mouse goes over time clicker
        /// </summary>
        // --------------------------------------------------------------------------
        private void TimeClickerMouseMove(object sender, MouseEventArgs e)
        {
            var canvas = sender as Canvas;

            var clickTime = GetTimeClickerSelectedTime(canvas, e);


            TimeClickerLabel.Content = clickTime.ToString(@"hh\:mm tt");
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Hide the absolute time when the mouse is out of the window
        /// </summary>
        // --------------------------------------------------------------------------
        private void TimeClickerMouseLeave(object sender, MouseEventArgs e)
        {
            TimeClickerLabel.Content = "";
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Add an outlook calendar
        /// </summary>
        // --------------------------------------------------------------------------
        private void AddOutlookCalendarClick(object sender, RoutedEventArgs e)
        {
            _appModel.AddCalendar("Outlook");
            _appModel.CheckCalendars();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Add an internet calendar
        /// </summary>
        // --------------------------------------------------------------------------
        private void AddInternetCalendarClick(object sender, RoutedEventArgs e)
        {
            var dialog = new AddInternetCalendarDialog();
            dialog.Left = this.Left + 50;
            dialog.Top = this.Top + 50;
            if (dialog.ShowDialog() ?? false)
            {
                _appModel.AddCalendar(dialog.CalendarUrl);
                _appModel.CheckCalendars();
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Make sure the window actually closes
        /// </summary>
        // --------------------------------------------------------------------------
        public void CloseForReal()
        {
            _hardClose = true;
            this.Close();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Add an internet calendar
        /// </summary>
        // --------------------------------------------------------------------------
        private void ExitAppClicked(object sender, RoutedEventArgs e)
        {
            _exiting = true;
            Application.Current.Shutdown();
        }

        bool _newHotKey_Shift = false;
        bool _newHotKey_Ctrl = false;
        bool _newHotKey_Alt = false;
        Key _newHotKey_Letter = Key.None;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Preview key strokes for hotkey assignment
        /// </summary>
        // --------------------------------------------------------------------------
        private void PreviewHotKeyEvent(object sender, KeyEventArgs e)
        {
            Debug.WriteLine($"KEY: {e.Key} [{e.SystemKey}] ({e.KeyStates}) ");
            var key = e.Key;
            if (key == Key.System) key = e.SystemKey;
            _appModel.OpenHotKey.AddModifier(key);
            e.Handled = true;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Assign the designated key combination to a hotkey
        /// </summary>
        // --------------------------------------------------------------------------
        private void AssignHotKey(object sender, RoutedEventArgs e)
        {
            try
            {
                _appModel.AssignHotKey();
                _appModel.OpenHotKey = new HotKeyAssignment();
                AssignmentError.Content = "";
                _appModel.UpdateSettings();
            }
            catch(Exception err)
            {
                AssignmentError.Content = err.Message;
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Clear the hotkey combination
        /// </summary>
        // --------------------------------------------------------------------------
        private void ClearHotKey(object sender, RoutedEventArgs e)
        {
            _appModel.OpenHotKey = new HotKeyAssignment();
            AssignmentError.Content = "";
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Clear the hotkey combination
        /// </summary>
        // --------------------------------------------------------------------------
        private void DeleteHotKeyClick(object sender, RoutedEventArgs e)
        {
            var sourceButton = sender as Button;
            _appModel.DeleteHotHey((int)(sourceButton.Tag));
            _appModel.UpdateSettings();
        }
    }
}
