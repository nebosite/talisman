using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// A node in the mail-rules inspection/edit tree. Holds its display text,
    /// children, and expand/visibility state, and implements the type-to-filter
    /// behaviour: a node stays visible if it matches OR any descendant does.
    /// Subclasses (RuleConditionNode, RuleValueNode) add editing. Filtering matches
    /// case-insensitively against <see cref="MatchText"/>.
    /// </summary>
    // --------------------------------------------------------------------------
    public class MailRuleNode : BaseModel
    {
        public string Text { get; protected set; }
        public ObservableCollection<MailRuleNode> Children { get; } = new ObservableCollection<MailRuleNode>();

        public MailRuleNode(string text = "")
        {
            Text = text ?? "";
        }

        /// <summary>The text a filter is matched against (overridden by value nodes).</summary>
        protected virtual string MatchText => Text;

        bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; NotifyPropertyChanged(nameof(IsExpanded)); }
        }

        bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            private set
            {
                _isVisible = value;
                NotifyPropertyChanged(nameof(IsVisible));
                NotifyPropertyChanged(nameof(Visibility));
            }
        }

        public Visibility Visibility => _isVisible ? Visibility.Visible : Visibility.Collapsed;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Apply a filter to this subtree. Returns true if this node or any
        /// descendant matches. A non-empty filter hides non-matching branches;
        /// matching is case-insensitive. This is a pure VISIBILITY filter - it does
        /// not auto-expand, because auto-expanding matching branches would force WPF
        /// to realize hundreds of value boxes and make broad filters hang. Expansion
        /// is left to the user.
        /// </summary>
        // --------------------------------------------------------------------------
        public bool ApplyFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                IsVisible = true;
                foreach (var child in Children) child.ApplyFilter(filter);
                return true;
            }

            var self = MatchText != null &&
                       MatchText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

            var anyChild = false;
            foreach (var child in Children)
                anyChild |= child.ApplyFilter(filter);

            IsVisible = self || anyChild;
            return IsVisible;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Expand branches that lead to a match, to reveal them. Call this AFTER
        /// ApplyFilter has set visibility (a branch leads to a match iff it has a
        /// visible child). Kept separate from ApplyFilter so it can be run on a
        /// delay - expanding realizes value boxes and is the expensive part.
        /// An empty filter collapses everything.
        /// </summary>
        // --------------------------------------------------------------------------
        public void ExpandForFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                IsExpanded = false;
                foreach (var child in Children) child.ExpandForFilter(filter);
                return;
            }

            IsExpanded = Children.Any(c => c.IsVisible);
            foreach (var child in Children) child.ExpandForFilter(filter);
        }
    }
}
