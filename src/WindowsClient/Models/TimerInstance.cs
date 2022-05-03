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
        public DateTime VisibleTime { get; set; }
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



        public string TimeText => VisibleTime.ToString(@"h\:mm tt");
        public string NotificationTimeText => EndsAt.ToString(@"h\:mm tt");
        static int _idCounter = 0;
        public Action OnDeleted = ()=> { };

        string[] SomeWords = {
            "scientific","cellar","suffer","return",
            "structure","flight","food","majestic","rest","hall","experience","plough","shy","include",
            "satisfying","blink","poison","jumbled","learn","bit","grubby","spicy","hunt","boy","weak","twig","drain",
            "jam","fearless","downtown","doubtful","sad","decision","hysterical","follow","right","miniature",
            "humor","pot","wire","horses","probable","alleged","door","obeisant","long","bent","trace","stormy",
            "didactic","whip","cheerful","flock","weight","capable",
            "plastic","company","joyful","injure","fall","lying","defiant","unpack","adorable",
            "abandoned","plantation","wobble","elegant","complete","team","mellow","acrid","massive",
            "mailbox","fragile","parallel","used","futuristic","equal","wash","humorous","finger",
            "last","wealth","abiding","hook","credit","rely","twig",
            "shivering","gather","makeshift","anxious","overflow","exist","trees","violent","box","base",
            "important","eyes","elated","handsome","gentle","juggle","long","beautiful","hydrant","meaty",
            "lamentable","toy","helpful","lackadaisical","float","flag","sparkle","allow","eggnog",
            "history","furniture","heavenly","lunchroom","teeny","hard-to-find","spotted","nondescript","act",
            "challenge","size","agreeable","pizzas","sneeze","shelter","modern","trucks","month","three",
            "miniature","hushed","late","bulb","little","letters","crawl","wholesale","snails","tray",
            "absolutely","accepted","acclaimed","accomplish","accomplishment","achievement","action","active",
            "admire","adorable","adventure","affirmative","affluent","agree","agreeable","amazing","angelic","appealing",
            "approve","aptitude","attractive","awesome","beaming","beautiful","believe","beneficial","bliss","bountiful",
            "bounty","brave","bravo","brilliant","bubbly","calm","celebrated","certain","champ","champion","charming",
            "cheery","choice","classic","classical","clean","commend","composed","congratulation","constant","cool",
            "courageous","creative","cute","dazzling","delight","delightful","distinguished","divine","earnest","easy",
            "ecstatic","effective","effervescent","efficient","effortless","electrifying","elegant","enchanting",
            "encouraging","endorsed","energetic","energized","engaging","enthusiastic","essential","esteemed","ethical",
            "excellent","exciting","exquisite","fabulous","fair","familiar","famous","fantastic","favorable","fetching",
            "fine","fitting","flourishing","fortunate","free","fresh","friendly","fun","funny","generous","genius","genuine",
            "giving","glamorous","glowing","good","gorgeous","graceful","great","green","grin","growing","handsome",
            "happy","harmonious","healing","healthy","hearty","heavenly","honest","honorable","honored","hug","idea",
            "ideal","imaginative","imagine","impressive","independent","innovate","innovative","instant","instantaneous",
            "instinctive","intellectual","intelligent","intuitive","inventive","jovial","joy","jubilant","keen","kind",
            "knowing","knowledgeable","laugh","learned","legendary","light","lively","lovely","lucid","lucky","luminous",
            "marvelous","masterful","meaningful","merit","meritorious","miraculous","motivating","moving","natural","nice",
            "novel","now","nurturing","nutritious","okay","one","one-hundred percent","open","optimistic","paradise",
            "perfect","phenomenal","pleasant","pleasurable","plentiful","poised","polished","popular","positive","powerful",
            "prepared","pretty","principled","productive","progress","prominent","protected","proud","quality","quick",
            "quiet","ready","reassuring","refined","refreshing","rejoice","reliable","remarkable","resounding",
            "respected","restored","reward","rewarding","right","robust","safe","satisfactory","secure","seemly",
            "simple","skilled","skillful","smile","soulful","sparkling","special","spirited","spiritual","stirring",
            "stunning","stupendous","success","successful","sunny","super","superb","supporting","surprising",
            "terrific","thorough","thrilling","thriving","tops","tranquil","transformative","transforming","trusting",
            "truthful","unreal","unwavering","up","upbeat","upright","upstanding","V","valued","vibrant","victorious",
            "victory","vigorous","virtuous","vital","vivacious","W","wealthy","welcome","well","whole","wholesome","willing",
            "wonderful","wondrous","worthy","wow","Y","yes","yummy","Z","zeal","zealous"

        };
        string[] IgnoreThese = { "min", "outlook", "bluejeans", "http", "https", "com" };

        public object AttentionWords { get; set; }
        static Random Rand = new Random();


        public event Action OnDismiss;

        // --------------------------------------------------------------------------
        /// <summary>
        /// ctor
        /// </summary>
        // --------------------------------------------------------------------------
        public TimerInstance(DateTime endTime, DateTime visibleTime, string location, string description, LinkDetails[] links)
        {
            EndsAt = endTime;
            VisibleTime = visibleTime;
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
        /// Dismiss this timer
        /// </summary>
        // --------------------------------------------------------------------------
        public void Dismiss() { OnDismiss?.Invoke();  }

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
