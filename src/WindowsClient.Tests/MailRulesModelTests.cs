using System.Linq;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for MailRulesModel: tree building + summary, incremental load,
    /// filtering, shared-text, dirty tracking, minimal edit harvest, and JSON
    /// export/import.
    /// </summary>
    // --------------------------------------------------------------------------
    public class MailRulesModelTests
    {
        static MailRuleData Rule(string name, int order = 1, bool enabled = true)
        {
            var rule = new MailRuleData { Name = name, Enabled = enabled, RuleType = "olRuleReceive", ExecutionOrder = order };
            var cond = new RuleClauseData { Kind = "olConditionBody", Editable = true, ValueTarget = RuleValueTarget.Text };
            cond.Values.Add("newsletter");
            rule.Conditions.Add(cond);
            var act = new RuleClauseData { Kind = "olActionMoveToFolder", Detail = "Inbox\\News" };
            rule.Actions.Add(act);
            return rule;
        }

        static RuleConditionNode FirstCondition(MailRuleNode ruleNode)
            => (RuleConditionNode)ruleNode.Children.First(c => c.Text.StartsWith("Conditions")).Children[0];

        [Fact]
        public void Load_BuildsOneNodePerRule_WithSummary()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A", 1), Rule("B", 2) });

            Assert.Equal(2, model.Nodes.Count);
            Assert.Contains("2 rules", model.SummaryText);
            Assert.Contains("/ 256 KB", model.SummaryText);
        }

        [Fact]
        public void Load_RuleNodeHasConditionAndActionGroups()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A", 1) });
            var rule = model.Nodes[0];
            Assert.Contains(rule.Children, c => c.Text.StartsWith("Conditions"));
            Assert.Contains(rule.Children, c => c.Text.StartsWith("Actions"));
        }

        [Fact]
        public void DisabledRule_IsMarked()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("Off", 1, enabled: false) });
            Assert.Contains("(disabled)", model.Nodes[0].Text);
        }

        [Fact]
        public void Conditions_ExpandIntoOneNodePerValue()
        {
            var rule = new MailRuleData { Name = "R", Enabled = true, RuleType = "olRuleReceive", ExecutionOrder = 1 };
            var cond = new RuleClauseData { Kind = "olConditionMessageHeader", Editable = true, ValueTarget = RuleValueTarget.Text };
            cond.Values.AddRange(new[] { "alpha.com", "beta.com", "gamma.com" });
            rule.Conditions.Add(cond);

            var model = new MailRulesModel();
            model.Load(new[] { rule });

            var clause = FirstCondition(model.Nodes[0]);
            Assert.Equal("olConditionMessageHeader", clause.Text);
            Assert.Equal(new[] { "alpha.com", "beta.com", "gamma.com" },
                clause.Children.Select(c => c.Text).ToArray());
        }

        [Fact]
        public void ConditionValues_AreSortedCaseInsensitively()
        {
            var rule = new MailRuleData { Name = "R", RuleType = "olRuleReceive", ExecutionOrder = 1 };
            var cond = new RuleClauseData { Kind = "olConditionMessageHeader", Editable = true, ValueTarget = RuleValueTarget.Text };
            cond.Values.AddRange(new[] { "Zebra", "apple", "Mango" });
            rule.Conditions.Add(cond);

            var model = new MailRulesModel();
            model.Load(new[] { rule });

            var clause = FirstCondition(model.Nodes[0]);
            Assert.Equal(new[] { "apple", "Mango", "Zebra" }, clause.Children.Select(c => c.Text).ToArray());
        }

        [Fact]
        public void FilterText_HidesNonMatchingRules()
        {
            var model = new MailRulesModel();
            var keep = Rule("KeepMe", 1);
            var drop = new MailRuleData { Name = "OtherRule", RuleType = "olRuleReceive", ExecutionOrder = 2 };
            model.Load(new[] { keep, drop });

            model.FilterText = "keepme"; // case-insensitive

            Assert.True(model.Nodes[0].IsVisible);
            Assert.False(model.Nodes[1].IsVisible);
        }

        [Fact]
        public void RunSharedTextCheck_PopulatesSharedResults()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A", 1), Rule("B", 2) }); // both match "newsletter"
            model.RunSharedTextCheck();

            var row = model.SharedResults.Single(s => s.Text == "newsletter");
            Assert.Contains("A", row.RuleNames);
            Assert.Contains("B", row.RuleNames);
            Assert.Equal("A, B", row.RuleNamesText);
            Assert.Equal(System.Windows.Visibility.Visible, model.DuplicateReportVisibility);
        }

        [Fact]
        public void RunSharedTextCheck_NothingShared_SaysSo()
        {
            var model = new MailRulesModel();
            var a = new MailRuleData { Name = "A", ExecutionOrder = 1 };
            a.Conditions.Add(new RuleClauseData { Values = { "one" } });
            var b = new MailRuleData { Name = "B", ExecutionOrder = 2 };
            b.Conditions.Add(new RuleClauseData { Values = { "two" } });
            model.Load(new[] { a, b });

            model.RunSharedTextCheck();
            Assert.Contains("No text", model.DuplicateReport);
        }

        [Fact]
        public void SetError_ClearsNodes_AndShowsMessage()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A", 1) });
            model.SetError("Outlook not running");
            Assert.Empty(model.Nodes);
            Assert.Equal("Outlook not running", model.SummaryText);
        }

        [Fact]
        public void IncrementalLoad_StreamsRulesAndTracksProgress()
        {
            var model = new MailRulesModel();
            model.BeginLoad();
            Assert.True(model.IsLoading);
            Assert.Empty(model.Nodes);
            Assert.True(model.IsConnecting);

            model.SetTotal(2);
            Assert.False(model.IsConnecting);
            Assert.Contains("0 / 2", model.SummaryText);

            model.AddRule(Rule("A", 1));
            Assert.Single(model.Nodes);
            Assert.Equal(1, model.LoadValue);

            model.AddRule(Rule("B", 2));
            model.EndLoad();
            Assert.False(model.IsLoading);
            Assert.Contains("2 rules", model.SummaryText);
        }

        // ---- editing / dirty / harvest ------------------------------------------

        [Fact]
        public void CleanAfterLoad_ThenDirtyOnEdit()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A", 1) });
            model.CaptureBaseline();

            Assert.False(model.IsDirty);
            Assert.False(model.CanSave);

            FirstValue(model, 0).Value = "changed";

            Assert.True(model.IsDirty);
            Assert.True(model.CanSave);
        }

        [Fact]
        public void HarvestEdits_ReturnsOnlyChangedConditions_Minimal()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A", 1), Rule("B", 2) });
            model.CaptureBaseline();

            FirstValue(model, 0).Value = "changed"; // edit rule A only

            var edits = model.HarvestEdits();
            Assert.Single(edits);                       // only rule A
            Assert.Equal(1, edits[0].ExecutionOrder);
            Assert.Single(edits[0].Conditions);
            Assert.Equal(RuleValueTarget.Text, edits[0].Conditions[0].Target);
            Assert.Equal(new[] { "changed" }, edits[0].Conditions[0].Values.ToArray());
        }

        [Fact]
        public void HarvestEdits_Empty_WhenNoChanges()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A", 1) });
            model.CaptureBaseline();
            Assert.Empty(model.HarvestEdits());
        }

        [Fact]
        public void AddedValue_ShowsInHarvest()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A", 1) });
            model.CaptureBaseline();

            var cond = FirstCondition(model.Nodes[0]);
            cond.AddBlankValue();
            ((RuleValueNode)cond.Children[1]).Value = "extra"; // the new second value

            var edits = model.HarvestEdits();
            Assert.Single(edits);
            Assert.Equal(new[] { "newsletter", "extra" }, edits[0].Conditions[0].Values.ToArray());
        }

        // ---- JSON ----------------------------------------------------------------

        [Fact]
        public void Json_RoundTrips_AndImportIsDirty()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A", 1) });
            var json = model.ToJson();

            var imported = new MailRulesModel();
            imported.LoadJson(json);

            Assert.Single(imported.Nodes);
            Assert.True(imported.IsDirty); // imported = unsaved
        }

        [Fact]
        public void ToJson_ReflectsEdits()
        {
            var model = new MailRulesModel();
            model.Load(new[] { Rule("A", 1) });
            model.CaptureBaseline();
            FirstValue(model, 0).Value = "edited-value";

            Assert.Contains("edited-value", model.ToJson());
        }

        static RuleValueNode FirstValue(MailRulesModel model, int ruleIndex)
            => (RuleValueNode)FirstCondition(model.Nodes[ruleIndex]).Children[0];
    }
}
