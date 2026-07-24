using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for the tree filter: a node stays visible if it matches OR a
    /// descendant matches; an empty filter restores everything.
    /// </summary>
    // --------------------------------------------------------------------------
    public class MailRuleNodeTests
    {
        static MailRuleNode Tree()
        {
            // rule "Newsletters"
            //   Conditions (1)
            //     olConditionBody: newsletter
            //   Actions (1)
            //     olActionMoveToFolder: Inbox\News
            var rule = new MailRuleNode("Newsletters  ·  ~0.2 KB");
            var conditions = new MailRuleNode("Conditions (1)");
            conditions.Children.Add(new MailRuleNode("olConditionBody: newsletter"));
            var actions = new MailRuleNode("Actions (1)");
            actions.Children.Add(new MailRuleNode("olActionMoveToFolder: Inbox\\News"));
            rule.Children.Add(conditions);
            rule.Children.Add(actions);
            return rule;
        }

        [Fact]
        public void EmptyFilter_ShowsEverything()
        {
            var root = Tree();
            root.ApplyFilter("");
            Assert.True(root.IsVisible);
            Assert.True(root.Children[0].IsVisible);
            Assert.True(root.Children[0].Children[0].IsVisible);
        }

        [Fact]
        public void MatchInDescendant_KeepsAncestorsVisible_AndExpandsThem()
        {
            var root = Tree();
            root.ApplyFilter("newsletter"); // only the condition leaf matches

            Assert.True(root.IsVisible);                       // ancestor kept
            Assert.True(root.IsExpanded);                      // and expanded
            Assert.True(root.Children[0].IsVisible);           // Conditions group kept
            Assert.True(root.Children[0].Children[0].IsVisible); // the matching leaf
        }

        [Fact]
        public void NonMatchingBranches_AreHidden()
        {
            var root = Tree();
            root.ApplyFilter("newsletter");

            // The Actions branch has no match -> hidden.
            Assert.False(root.Children[1].IsVisible);
            Assert.False(root.Children[1].Children[0].IsVisible);
        }

        [Fact]
        public void NoMatchAnywhere_HidesTheRule()
        {
            var root = Tree();
            root.ApplyFilter("zzz-nothing");
            Assert.False(root.IsVisible);
        }

        [Fact]
        public void MatchOnAncestorText_KeepsTheRuleVisible()
        {
            var root = Tree();
            root.ApplyFilter("Newsletters"); // matches the rule header itself
            Assert.True(root.IsVisible);
        }

        [Fact]
        public void Visibility_TracksIsVisible()
        {
            var node = new MailRuleNode("x");
            node.ApplyFilter("nomatch");
            Assert.Equal(System.Windows.Visibility.Collapsed, node.Visibility);
            node.ApplyFilter("");
            Assert.Equal(System.Windows.Visibility.Visible, node.Visibility);
        }
    }
}
