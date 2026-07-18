using System.Windows;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The small floating widget that shows the current Pomodoro task, its timer,
    /// and the Done/Defer buttons. Bound to the PomodoroController.
    /// </summary>
    // --------------------------------------------------------------------------
    public partial class PomodoroTaskWindow : Window
    {
        public PomodoroTaskWindow(PomodoroController controller)
        {
            InitializeComponent();
            DataContext = controller;
        }

        PomodoroController Controller => (PomodoroController)DataContext;

        private void DoneClicked(object sender, RoutedEventArgs e) => Controller.Done();
        private void DeferClicked(object sender, RoutedEventArgs e) => Controller.Defer();
    }
}
