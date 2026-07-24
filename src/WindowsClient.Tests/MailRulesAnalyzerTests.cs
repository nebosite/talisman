using System.Collections.Generic;
using System.Linq;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for MailRulesAnalyzer: the size estimate scales with content, and the
    /// shared-text finder reports text that appears across more than one rule.
    /// </summary>
    // --------------------------------------------------------------------------
    public class MailRulesAnalyzerTests
    {
        static MailRuleData Rule(string name, params string[] conditionTexts)
        {
            var rule = new MailRuleData { Name = name, Enabled = true, RuleType = "olRuleReceive" };
            var clause = new RuleClauseData { Kind = "olConditionBody" };
            foreach (var t in conditionTexts) { clause.Details.Add(t); clause.Texts.Add(t); }
            rule.Conditions.Add(clause);
            return rule;
        }

        [Fact]
        public void EstimateBytes_IsPositive_AndGrowsWithContent()
        {
            var small = Rule("A", "x");
            var big = Rule("A really long rule name here", "newsletter", "promotions", "unsubscribe");

            Assert.True(MailRulesAnalyzer.EstimateBytes(small) > 0);
            Assert.True(MailRulesAnalyzer.EstimateBytes(big) > MailRulesAnalyzer.EstimateBytes(small));
        }

        [Fact]
        public void EstimateTotalBytes_SumsRules()
        {
            var rules = new[] { Rule("A", "x"), Rule("B", "y") };
            var total = MailRulesAnalyzer.EstimateTotalBytes(rules);
            Assert.Equal(MailRulesAnalyzer.EstimateBytes(rules[0]) + MailRulesAnalyzer.EstimateBytes(rules[1]), total);
        }

        [Fact]
        public void Limit_Is256Kb()
        {
            Assert.Equal(256 * 1024, MailRulesAnalyzer.LimitBytes);
        }

        [Fact]
        public void FindSharedText_ReturnsTextInMultipleRules()
        {
            var rules = new[]
            {
                Rule("Newsletters", "newsletter", "digest"),
                Rule("Promos", "newsletter", "sale"),
                Rule("Personal", "mom"),
            };

            var shared = MailRulesAnalyzer.FindSharedText(rules);

            Assert.Single(shared);
            Assert.Equal("newsletter", shared[0].Text);
            Assert.Equal(new[] { "Newsletters", "Promos" }, shared[0].RuleNames.ToArray());
        }

        [Fact]
        public void FindSharedText_IsCaseInsensitive()
        {
            var rules = new[] { Rule("A", "Newsletter"), Rule("B", "newsletter") };
            var shared = MailRulesAnalyzer.FindSharedText(rules);
            Assert.Single(shared);
            Assert.Equal(new[] { "A", "B" }, shared[0].RuleNames.ToArray());
        }

        [Fact]
        public void FindSharedText_IgnoresRepeatWithinOneRule()
        {
            var rule = Rule("A", "dup", "dup"); // same text twice in ONE rule
            var shared = MailRulesAnalyzer.FindSharedText(new[] { rule });
            Assert.Empty(shared);
        }

        [Fact]
        public void FindSharedText_ConsidersExceptionsToo()
        {
            var a = new MailRuleData { Name = "A" };
            a.Conditions.Add(new RuleClauseData { Texts = { "vip" } });
            var b = new MailRuleData { Name = "B" };
            b.Exceptions.Add(new RuleClauseData { Texts = { "vip" } });

            var shared = MailRulesAnalyzer.FindSharedText(new[] { a, b });
            Assert.Single(shared);
            Assert.Equal("vip", shared[0].Text);
        }

        [Fact]
        public void FindSharedText_EmptyWhenNothingShared()
        {
            var rules = new[] { Rule("A", "one"), Rule("B", "two") };
            Assert.Empty(MailRulesAnalyzer.FindSharedText(rules));
        }
    }
}
