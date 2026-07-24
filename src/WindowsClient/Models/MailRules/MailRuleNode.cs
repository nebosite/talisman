using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// A node in the mail-rules inspection tree. Holds its display text, children,
    /// and expand/visibility state, and implements the type-to-filter behaviour:
    /// a node stays visible if it matches the filter OR any descendant does.
    /// The filter logic is pure and unit-tested.
    /// </summary>
    // --------------------------------------------------------------------------
    public class MailRuleNode : BaseModel
    {
        public string Text { get; }
        public ObservableCollection<MailRuleNode> Children { get; } = new ObservableCollection<MailRuleNode>();

        public MailRuleNode(string text)
        {
            Text = text ?? "";
        }

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
        /// descendant matches. An empty filter shows everything and collapses back
        /// to the top level; a non-empty filter hides non-matching branches and
        /// expands the ones that contain a match.
        /// </summary>
        // --------------------------------------------------------------------------
        public bool ApplyFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                IsVisible = true;
                IsExpanded = false;
                foreach (var child in Children) child.ApplyFilter(filter);
                return true;
            }

            var selfMatches = Text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

            var anyChildMatches = false;
            foreach (var child in Children)
                anyChildMatches |= child.ApplyFilter(filter);

            IsVisible = selfMatches || anyChildMatches;
            if (anyChildMatches) IsExpanded = true;
            return IsVisible;
        }
    }
}
