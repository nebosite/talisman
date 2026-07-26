using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// A text value that a set of rules share (used for the "find shared text"
    /// check that spots overlapping/duplicate rule criteria).
    /// </summary>
    // --------------------------------------------------------------------------
    public class SharedText
    {
        public string Text { get; set; }
        public List<string> RuleNames { get; set; } = new List<string>();

        /// <summary>Comma-joined rule names, for display.</summary>
        public string RuleNamesText => string.Join(", ", RuleNames);
    }

    // --------------------------------------------------------------------------
    /// <summary>
    /// Pure analysis over the flattened rule data: a rough byte-size estimate
    /// (against the ~256 KB Exchange rules limit) and the shared-text finder.
    /// Unit-tested independently of Outlook and the UI.
    /// </summary>
    // --------------------------------------------------------------------------
    public static class MailRulesAnalyzer
    {
        /// <summary>The classic Exchange rules-blob size limit.</summary>
        public const int LimitBytes = 256 * 1024;

        const int RuleOverhead = 64;
        const int ClauseOverhead = 24;

        static readonly StringComparer CI = StringComparer.OrdinalIgnoreCase;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Rough byte estimate of one rule's serialized content.
        /// </summary>
        // --------------------------------------------------------------------------
        public static int EstimateBytes(MailRuleData rule)
        {
            if (rule == null) return 0;
            var bytes = RuleOverhead + Bytes(rule.Name);
            foreach (var clause in AllClauses(rule))
            {
                bytes += ClauseOverhead + Bytes(clause.Kind) + Bytes(clause.Detail);
                foreach (var v in clause.Values) bytes += Bytes(v);
            }
            return bytes;
        }

        public static int EstimateTotalBytes(IEnumerable<MailRuleData> rules)
        {
            return (rules ?? Enumerable.Empty<MailRuleData>()).Sum(EstimateBytes);
        }

        public static double ToKb(int bytes) => bytes / 1024.0;

        // --------------------------------------------------------------------------
        /// <summary>
        /// Text values that overlap across more than one rule. Matching is
        /// case-insensitive and by SUBSTRING: a value counts as shared if it appears
        /// as a substring of some condition value in more than one distinct rule.
        /// e.g. "foo" is reported as shared between a rule that matches "foo" and one
        /// that matches "foobar", since anything matching "foobar" also matches "foo".
        /// </summary>
        // --------------------------------------------------------------------------
        public static List<SharedText> FindSharedText(IEnumerable<MailRuleData> rules)
        {
            var ruleList = (rules ?? Enumerable.Empty<MailRuleData>()).ToList();

            // Precompute each rule's values once, lowercased, so the inner loop is a
            // plain ORDINAL substring test (no per-call case folding, no re-parsing).
            var perRule = ruleList
                .Select(r => new
                {
                    Rule = r,
                    Lowered = RuleValues(r).Select(v => v.ToLowerInvariant()).Distinct().ToArray(),
                })
                .ToList();

            // Candidate needles: every distinct value (case-insensitive), keeping the
            // first-seen original casing for display.
            var candidates = new List<string>();
            var seen = new HashSet<string>(CI);
            foreach (var rule in ruleList)
                foreach (var v in RuleValues(rule))
                    if (seen.Add(v)) candidates.Add(v);

            var result = new List<SharedText>();
            foreach (var needle in candidates)
            {
                var lowerNeedle = needle.ToLowerInvariant();

                var matching = new List<string>();
                foreach (var pr in perRule)
                {
                    foreach (var v in pr.Lowered)
                    {
                        if (v.Contains(lowerNeedle, StringComparison.Ordinal))
                        {
                            matching.Add(pr.Rule.Name);
                            break;
                        }
                    }
                    if (matching.Count > 1 && matching.Count == perRule.Count) break;
                }

                if (matching.Count > 1)
                    result.Add(new SharedText { Text = needle, RuleNames = matching });
            }

            return result.OrderBy(s => s.Text, CI).ToList();
        }

        // All condition/exception match values of a rule (trimmed, non-empty).
        static IEnumerable<string> RuleValues(MailRuleData rule)
        {
            return rule.Conditions.Concat(rule.Exceptions)
                .SelectMany(c => c.Values)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim());
        }

        static IEnumerable<RuleClauseData> AllClauses(MailRuleData rule)
        {
            return rule.Conditions.Concat(rule.Exceptions).Concat(rule.Actions);
        }

        static int Bytes(string s) => string.IsNullOrEmpty(s) ? 0 : Encoding.Unicode.GetByteCount(s);
    }
}
