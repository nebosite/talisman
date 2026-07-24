using System.Linq;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for MailRulesModel: building the tree, the size summary, filtering,
    /// and the shared-text report.
    /// </summary>
    // --------------------------------------------------------------------------
    public class MailRulesModelTests
    {
        static MailRuleData Rule(string name, bool enabled = true)
        {
            var rule = new MailRuleData { Name = name, Enabled = enabled, RuleType = "olRuleReceive" };
            var cond = new RuleClauseData { Kind = "olConditionBody" };
            cond.Details.Add("newsletter"); cond.Texts.Add("newsletter");
            rule.Conditions.Add(cond);
            var act = new RuleClauseData { Kind = "olActionMoveToFolder" };
            act.Details.Add("Inbox\\News");
            rule.Actions.Add(act);
            return rule;
        }

        [Fact]
        public void Load_BuildsOneNodePerRule_WithSummary()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A"), Rule("B") });

            Assert.Equal(2, model.Nodes.Count);
            Assert.Contains("2 rules", model.SummaryText);
            Assert.Contains("/ 256 KB", model.SummaryText);
        }

        [Fact]
        public void Load_RuleNodeHasConditionAndActionGroups()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A") });

            var rule = model.Nodes[0];
            // info line + Conditions + Actions
            Assert.Contains(rule.Children, c => c.Text.StartsWith("Conditions"));
            Assert.Contains(rule.Children, c => c.Text.StartsWith("Actions"));
        }

        [Fact]
        public void DisabledRule_IsMarked()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("Off", enabled: false) });
            Assert.Contains("(disabled)", model.Nodes[0].Text);
        }

        [Fact]
        public void FilterText_HidesNonMatchingRules()
        {
            var model = new MailRulesModel();
            var keep = Rule("KeepMe");
            var drop = new MailRuleData { Name = "OtherRule", RuleType = "olRuleReceive" };
            model.Load(new[] { keep, drop });

            model.FilterText = "KeepMe";

            Assert.True(model.Nodes[0].IsVisible);
            Assert.False(model.Nodes[1].IsVisible);
        }

        [Fact]
        public void RunSharedTextCheck_ReportsSharedText()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A"), Rule("B") }); // both match "newsletter"

            model.RunSharedTextCheck();

            Assert.Contains("newsletter", model.DuplicateReport);
            Assert.Equal(System.Windows.Visibility.Visible, model.DuplicateReportVisibility);
        }

        [Fact]
        public void RunSharedTextCheck_NothingShared_SaysSo()
        {
            var model = new MailRulesModel();
            var a = new MailRuleData { Name = "A" };
            a.Conditions.Add(new RuleClauseData { Texts = { "one" } });
            var b = new MailRuleData { Name = "B" };
            b.Conditions.Add(new RuleClauseData { Texts = { "two" } });
            model.Load(new[] { a, b });

            model.RunSharedTextCheck();
            Assert.Contains("No text", model.DuplicateReport);
        }

        [Fact]
        public void SetError_ClearsNodes_AndShowsMessage()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A") });
            model.SetError("Outlook not running");

            Assert.Empty(model.Nodes);
            Assert.Equal("Outlook not running", model.SummaryText);
        }

        [Fact]
        public void Conditions_ExpandIntoOneNodePerValue()
        {
            var rule = new MailRuleData { Name = "R", Enabled = true, RuleType = "olRuleReceive" };
            var cond = new RuleClauseData { Kind = "olConditionMessageHeader" };
            cond.Details.AddRange(new[] { "alpha.com", "beta.com", "gamma.com" });
            rule.Conditions.Add(cond);

            var model = new MailRulesModel();
            model.Load(new[] { rule });

            var group = model.Nodes[0].Children.First(c => c.Text.StartsWith("Conditions"));
            var clause = group.Children[0];
            Assert.Equal("olConditionMessageHeader", clause.Text);
            Assert.Equal(new[] { "alpha.com", "beta.com", "gamma.com" },
                clause.Children.Select(c => c.Text).ToArray());
        }

        [Fact]
        public void IncrementalLoad_StreamsRulesAndTracksProgress()
        {
            var model = new MailRulesModel();

            model.BeginLoad();
            Assert.True(model.IsLoading);
            Assert.Empty(model.Nodes);
            Assert.True(model.IsConnecting); // total not known yet
            Assert.Equal(System.Windows.Visibility.Visible, model.ProgressVisibility);

            model.SetTotal(2);
            Assert.False(model.IsConnecting);
            Assert.Equal(2, model.LoadTotal);
            Assert.Contains("0 / 2", model.SummaryText);

            model.AddRule(Rule("A"));
            Assert.Single(model.Nodes);
            Assert.Equal(1, model.LoadValue);
            Assert.Contains("1 / 2", model.SummaryText);

            model.AddRule(Rule("B"));
            Assert.Equal(2, model.Nodes.Count);

            model.EndLoad();
            Assert.False(model.IsLoading);
            Assert.Equal(System.Windows.Visibility.Collapsed, model.ProgressVisibility);
            Assert.Contains("2 rules", model.SummaryText);
        }

        [Fact]
        public void Filter_AppliesToRulesAddedDuringLoad()
        {
            var model = new MailRulesModel();
            model.FilterText = "KeepMe"; // filter set before rules stream in

            model.BeginLoad();
            model.SetTotal(2);
            model.AddRule(Rule("KeepMe"));
            model.AddRule(new MailRuleData { Name = "OtherRule", RuleType = "olRuleReceive" });

            Assert.True(model.Nodes[0].IsVisible);
            Assert.False(model.Nodes[1].IsVisible);
        }
    }
}
