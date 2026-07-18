using System;
using System.Windows;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The little "click start to begin" dialog shown before the Joy and Admin
    /// blocks. Invokes a callback when Start is clicked; the controller closes it.
    /// </summary>
    // --------------------------------------------------------------------------
    public partial class PomodoroPromptWindow : Window
    {
        readonly Action _onStart;

        public PomodoroPromptWindow(string message, Action onStart)
        {
            InitializeComponent();
            _onStart = onStart;
            MessageText.Text = message;
        }

        private void StartClicked(object sender, RoutedEventArgs e)
        {
            _onStart?.Invoke();
        }
    }
}
