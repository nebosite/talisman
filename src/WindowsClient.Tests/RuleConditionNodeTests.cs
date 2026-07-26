using System.Linq;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for the editable clause node: adding, editing, and clearing values,
    /// dirty notification, and the read-only behaviour of non-editable clauses.
    /// (Child removal is synchronous under test since Application.Current is null.)
    /// </summary>
    // --------------------------------------------------------------------------
    public class RuleConditionNodeTests
    {
        static RuleConditionNode Make(out System.Func<int> dirtyCount, bool editable, params string[] values)
        {
            var count = 0;
            dirtyCount = () => count;
            var clause = new RuleClauseData
            {
                Kind = "olConditionBody",
                Editable = editable,
                ValueTarget = RuleValueTarget.Text,
            };
            clause.Values.AddRange(values);
            var rule = new MailRuleData { Name = "R", ExecutionOrder = 1 };
            return new RuleConditionNode(rule, clause, isException: false, markDirty: () => count++);
        }

        static RuleValueNode Val(RuleConditionNode node, int i) => (RuleValueNode)node.Children[i];

        [Fact]
        public void BuildsOneEditableValueNodePerValue()
        {
            var node = Make(out _, editable: true, "a", "b");
            Assert.Equal(2, node.Children.Count);
            Assert.All(node.Children, c => Assert.True(((RuleValueNode)c).Editable));
        }

        [Fact]
        public void EditingValue_MarksDirty_AndShowsInCurrentValues()
        {
            var node = Make(out var dirty, editable: true, "a");
            Val(node, 0).Value = "changed";
            Assert.True(dirty() > 0);
            Assert.Equal(new[] { "changed" }, node.CurrentValues().ToArray());
        }

        [Fact]
        public void ClearingValue_RemovesItEntirely_AndMarksDirty()
        {
            var node = Make(out var dirty, editable: true, "a", "b");
            Val(node, 0).Value = "";
            Assert.Single(node.Children);
            Assert.True(dirty() > 0);
            Assert.Equal(new[] { "b" }, node.CurrentValues().ToArray());
        }

        [Fact]
        public void AddBlankValue_AddsEditableChild_AndMarksDirty()
        {
            var node = Make(out var dirty, editable: true, "a");
            node.AddBlankValue();
            Assert.Equal(2, node.Children.Count);
            Assert.True(dirty() > 0);
        }

        [Fact]
        public void NonEditableClause_AddBlankValueIsNoOp_AndValuesNotHarvested()
        {
            var node = Make(out _, editable: false, "readonly");
            node.AddBlankValue();
            Assert.Single(node.Children);
            Assert.Empty(node.CurrentValues());
        }

        [Fact]
        public void DetailClause_AddsReadOnlyChild()
        {
            var clause = new RuleClauseData { Kind = "olActionMoveToFolder", Detail = "Inbox\\News" };
            var node = new RuleConditionNode(new MailRuleData { Name = "R" }, clause, false, () => { });
            Assert.Single(node.Children);
            Assert.True(((RuleValueNode)node.Children[0]).ReadOnly);
        }
    }
}
