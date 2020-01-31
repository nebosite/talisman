using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
