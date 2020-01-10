using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The Notification widget is the thing that flies around that you have
    /// to click on to kill
    /// </summary>
    // --------------------------------------------------------------------------
    public partial class NotificationWidget : Window
    {
        static Random Rand = new Random();
        NotificationData _data;
        AppModel _appModel;

        public double LocationTheta { get; set; }
        public Point Center { get; internal set; }

        DraggingLogic _draggingLogic;

        string[] SomeWords = {
            "scientific",
            "cellar",
            "suffer",
            "return",
            "structure",
            "flight",
            "food",
            "majestic",
            "rest",
            "hall",
            "overconfident",
            "experience",
            "plough",
            "shy",
            "include",
            "satisfying",
            "blink",
            "poison",
            "jumbled",
            "learn",
            "bit",
            "grubby",
            "spicy",
            "hunt",
            "boy",
            "weak",
            "twig",
            "drain",
            "jam",
            "fearless",
            "downtown",
            "doubtful",
            "sad",
            "decision",
            "hysterical",
            "follow",
            "right",
            "miniature",
            "humor",
            "pot",
            "wire",
            "horses",
            "probable",
            "alleged",
            "door",
            "obeisant",
            "long",
            "bent",
            "trace",
            "stormy" };

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public NotificationWidget(NotificationData data, AppModel appModel, double location)
        {
            InitializeComponent();
            this._data = data;
            this._appModel = appModel;
            this.DataContext = data;
            LocationTheta = location;

            this.Loaded += (a, b) =>
            {
                var lowerText = _data.NotificationText.ToLower();
                Regex.Split(lowerText, @"[ .,/\-?!@#$%^&*()\[\]="":|{ }<> +_]+");
                var words = Regex.Split(_data.NotificationText.ToLower(), @"[ .,/\-?!@#$%^&*()\[\]="":|{ }<> +_]+")
                    .Where(w => w.Length > 0).ToList();
                if (words.Count == 0) words.Add("hobartium");

                string randomWord() => words[Rand.Next(words.Count)];

                var Word1 = randomWord();
                var Word2 = randomWord();
                var Word3 = randomWord();
                var nonWordIndex = Rand.Next(SomeWords.Length);
                while(lowerText.Contains(SomeWords[nonWordIndex]))
                {
                    nonWordIndex = Rand.Next(SomeWords.Length);
                }
                switch(Rand.Next(3))
                {
                    case 0: Word1 = SomeWords[nonWordIndex]; break;
                    case 1: Word2 = SomeWords[nonWordIndex]; break;
                    case 2: Word3 = SomeWords[nonWordIndex]; break;
                }

                var pickedWords = new { Word1, Word2, Word3};
                DismissButtons.DataContext = pickedWords;
                _draggingLogic = new DraggingLogic(this, this);
                _draggingLogic.OnPositionChanged += (xm, ym) =>
                {
                    Center = new Point(Center.X + xm / _draggingLogic.DpiCorrectionX, Center.Y + ym / _draggingLogic.DpiCorrectionY);
                };
            };
        }

        DateTime _startTime = DateTime.Now;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Click handling
        /// </summary>
        // --------------------------------------------------------------------------
        public void Animate()
        {
            var delta = (Math.Cos((DateTime.Now - _startTime).TotalSeconds * 5) + 1) / 2;
            byte bigDelta = (byte)(255 * delta);
            byte partDelta = (byte)(127 * delta);

            var brush = new SolidColorBrush(Color.FromArgb(127, partDelta, bigDelta, bigDelta));
            MyBorder.BorderBrush = brush;
        }

        void Snooze(double minutes)
        {
            _appModel.StartTimer(minutes, _data.NotificationText);
            this.Close();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Snooze buttons
        /// </summary>
        // --------------------------------------------------------------------------
        private void SnoozeClicked(object sender, RoutedEventArgs e)
        {
            double.TryParse((sender as Button).Tag.ToString(), out var minutes);
            Snooze(minutes);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Dismiss buttons
        /// </summary>
        // --------------------------------------------------------------------------
        private void DismissClicked(object sender, RoutedEventArgs e)
        {
            var clickText = (sender as Button).Content.ToString().ToLower();
            if (_data.NotificationText.ToLower().Contains(clickText)) Snooze(.01);
            else Close();
        }
    }
}
