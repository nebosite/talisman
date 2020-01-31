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
        TimerInstance _data;
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
        string[] IgnoreThese = { "min", "outlook", "bluejeans", "http", "https", "com" };

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public NotificationWidget(TimerInstance data, AppModel appModel, double location)
        {
            InitializeComponent();
            this._data = data;
            this._appModel = appModel;
            this.DataContext = data;
            LocationTheta = location;

            this.Loaded += (a, b) =>
            {
                var lowerText = _data.DecoratedDescription.ToLower();
                var words = Regex.Split(lowerText, @"[ 0123456789.,/\-?!@#$%^&*()\[\]="":|{ }<> +_]+")
                    .Where(w => w.Length > 2 && !IgnoreThese.Contains(w)).ToList();
                while (words.Count < 3)
                {
                    var anotherWord = SomeWords[Rand.Next(SomeWords.Length)];
                    words.Add(anotherWord);
                    this._data.DescriptionDecoration += $" {anotherWord}";
                }
                this._data.NotifyAllPropertiesChanged();
                lowerText = _data.DecoratedDescription.ToLower();

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

        // --------------------------------------------------------------------------
        /// <summary>
        /// Snooze the current timer
        /// </summary>
        // --------------------------------------------------------------------------
        void Snooze(double minutes)
        {
            _data.EndsAt = _data.EndsAt.AddMinutes(minutes);
            _appModel.StartTimer(_data);
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
            if (_data.DecoratedDescription.ToLower().Contains(clickText)) Snooze(.01);
            else Close();
        }
    }
}
