using System.Windows;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// End-of-day summary of the Pomodoro session (tasks done, deferrals, time per
    /// phase). Read-only; closes on the Close button.
    /// </summary>
    // --------------------------------------------------------------------------
    public partial class PomodoroSummaryWindow : Window
    {
        public PomodoroSummaryWindow(string summary)
        {
            InitializeComponent();
            SummaryText.Text = summary;
        }

        private void CloseClicked(object sender, RoutedEventArgs e) => Close();
    }
}
