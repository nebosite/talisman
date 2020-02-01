using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// An Instance of a timer 
    /// </summary>
    // --------------------------------------------------------------------------
    public class TimerInstance : BaseModel
    {
        public int Id { get; set; }
        public string UniqueId { get; set; }
        public DateTime EndsAt { get; set; }
        string _description = "";
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                NotifyPropertyChanged(nameof(Description));
                NotifyPropertyChanged(nameof(DecoratedDescription));
            }
        }

        string _descriptionDecoration = "";
        public string DescriptionDecoration
        {
            get => _descriptionDecoration;
            set
            {
                _descriptionDecoration = value;
                NotifyPropertyChanged(nameof(DescriptionDecoration));
                NotifyPropertyChanged(nameof(DecoratedDescription));
            }
        }

        public string DecoratedDescription => Description + " " + DescriptionDecoration;

        public class LinkDetails
        {
            public string Uri { get; set; }
            public string Text { get; set; }
        }

        public string Location { get; set; }
        public List<LinkDetails> Links { get; set; } = new List<LinkDetails>();
        public Visibility LinkVisibility => Links.Count > 0 ? Visibility.Visible : Visibility.Collapsed;



        public string TimeText => EndsAt.ToString(@"h\:mm tt");
        static int _idCounter = 0;
        public Action OnDeleted = ()=> { };

        string[] SomeWords = {
            "scientific","cellar","suffer","return",
            "structure","flight","food","majestic","rest","hall","overconfident","experience","plough","shy","include",
            "satisfying","blink","poison","jumbled","learn","bit","grubby","spicy","hunt","boy","weak","twig","drain",
            "jam","fearless","downtown","doubtful","sad","decision","hysterical","follow","right","miniature",
            "humor","pot","wire","horses","probable","alleged","door","obeisant","long","bent","trace","stormy",
            "didactic","whip","cheerful","flock","weight","capable",
            "plastic","company","nasty","injure","fall","lying","defiant","unpack","adorable",
            "abandoned","plantation","wobble","elegant","complete","team","mellow","acrid","massive",
            "mailbox","fragile","parallel","used","futuristic","equal","wash","overconfident","finger",
            "last","wealth","abiding","hook","credit","rely","incompetent","twig","helpless",
            "shivering","gather","makeshift","anxious","overflow","exist","trees","violent","box","base",
            "important","eyes","elated","boy","gentle","juggle","long","male","hydrant","meaty",
            "lamentable","toy","helpful","lackadaisical","float","flag","sparkle","allow","eggnog",
            "history","furniture","hellish","lunchroom","teeny","hard-to-find","spotted","nondescript","act",
            "challenge","size","agreeable","pizzas","cough","shelter","modern","trucks","month","three",
            "miniature","hushed","late","bulb","little","letters","crawl","wholesale","snails","tray",
        };
        string[] IgnoreThese = { "min", "outlook", "bluejeans", "http", "https", "com" };

        public object AttentionWords { get; set; }
        static Random Rand = new Random();

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public TimerInstance(DateTime endTime, string location, string description, LinkDetails[] links)
        {
            EndsAt = endTime;
            Location = location;
            Description = description;
            UniqueId = $"{endTime}|{location}|{description}";
            Id = _idCounter++;
            if (links != null)
            {
                links.ToList().ForEach(i => Links.Add(i));
            }

            SetAttentionWords();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Attention words allow us to create attention buttons to the user
        /// must read the descript to dismiss the appointment
        /// </summary>
        // --------------------------------------------------------------------------
        private void SetAttentionWords()
        {
            // get a list of lowercase words from the description
            var lowerText = Description.ToLower();
            var words = Regex
                .Split(lowerText, @"[ 0123456789.,/\-?!@#$%^&*()\[\]="":|{ }<> +_]+")
                .Where(w => w.Length > 2 && !IgnoreThese.Contains(w)) // at least 3 letters and not on the ignore list
                .ToList();

            // Make sure we have at least three words in the decorated description
            while (words.Count < 3)
            {
                var anotherWord = SomeWords[Rand.Next(SomeWords.Length)];
                words.Add(anotherWord);
                DescriptionDecoration += $" {anotherWord}";
            }

            string randomWord()
            {
                var index = Rand.Next(words.Count);
                var pickedWord = words[index];
                words.RemoveAt(index);
                return pickedWord;
            }

            // Pick three words at random from the description
            var Word1 = randomWord();
            var Word2 = randomWord();
            var Word3 = randomWord();

            // Pick a word that is definitely not in our decorated description
            lowerText = DecoratedDescription.ToLower();
            var nonWordIndex = Rand.Next(SomeWords.Length);
            while (lowerText.Contains(SomeWords[nonWordIndex]))
            {
                nonWordIndex = Rand.Next(SomeWords.Length);
            }

            // Assign the non-present word to one of our picked words
            switch (Rand.Next(3))
            {
                case 0: Word1 = SomeWords[nonWordIndex]; break;
                case 1: Word2 = SomeWords[nonWordIndex]; break;
                case 2: Word3 = SomeWords[nonWordIndex]; break;
            }

            AttentionWords = new { Word1, Word2, Word3 };

            NotifyAllPropertiesChanged();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// DeleteMe
        /// </summary>
        // --------------------------------------------------------------------------
        public void DeleteMe()
        {
            OnDeleted();
        }
    }
}
