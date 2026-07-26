using System.Linq;
using Talisman;
using Xunit;

namespace Talisman.Tests
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Tests for MailRulesAnalyzer: size estimate scales with content; shared-text
    /// finding is substring-based and case-insensitive.
    /// </summary>
    // --------------------------------------------------------------------------
    public class MailRulesAnalyzerTests
    {
        static MailRuleData Rule(string name, params string[] conditionValues)
        {
            var rule = new MailRuleData { Name = name, Enabled = true, RuleType = "olRuleReceive" };
            var clause = new RuleClauseData { Kind = "olConditionBody", Editable = true, ValueTarget = RuleValueTarget.Text };
            clause.Values.AddRange(conditionValues);
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
            Assert.Equal(
                MailRulesAnalyzer.EstimateBytes(rules[0]) + MailRulesAnalyzer.EstimateBytes(rules[1]),
                MailRulesAnalyzer.EstimateTotalBytes(rules));
        }

        [Fact]
        public void Limit_Is256Kb() => Assert.Equal(256 * 1024, MailRulesAnalyzer.LimitBytes);

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

            var newsletter = shared.Single(s => s.Text == "newsletter");
            Assert.Equal(new[] { "Newsletters", "Promos" }, newsletter.RuleNames.ToArray());
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
        public void FindSharedText_MatchesSubstrings()
        {
            // "foo" is a substring of "foobar", so anything matching "foobar" also
            // matches "foo" -> the two rules share "foo".
            var rules = new[] { Rule("Broad", "foo"), Rule("Narrow", "FOOBAR") };
            var shared = MailRulesAnalyzer.FindSharedText(rules);

            Assert.Contains(shared, s => s.Text == "foo" && s.RuleNames.Count == 2);
            Assert.DoesNotContain(shared, s => s.Text == "FOOBAR"); // only in Narrow
        }

        [Fact]
        public void FindSharedText_IgnoresRepeatWithinOneRule()
        {
            var rule = Rule("A", "dup", "dup");
            Assert.Empty(MailRulesAnalyzer.FindSharedText(new[] { rule }));
        }

        [Fact]
        public void FindSharedText_ConsidersExceptionsToo()
        {
            var a = new MailRuleData { Name = "A" };
            a.Conditions.Add(new RuleClauseData { Values = { "vip" } });
            var b = new MailRuleData { Name = "B" };
            b.Exceptions.Add(new RuleClauseData { Values = { "vip" } });

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
