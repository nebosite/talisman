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

        // Rough per-structure overheads. The real on-the-wire size isn't exposed by
        // the object model, so this is only an estimate for comparison/inspection.
        const int RuleOverhead = 64;
        const int ClauseOverhead = 24;

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
                bytes += ClauseOverhead + Bytes(clause.Kind);
                foreach (var d in clause.Details) bytes += Bytes(d);
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
        /// Text values that appear (in conditions or exceptions) across more than
        /// one rule - i.e. rules that "match on the same text". Case-insensitive;
        /// a value repeated within a single rule does not count.
        /// </summary>
        // --------------------------------------------------------------------------
        public static List<SharedText> FindSharedText(IEnumerable<MailRuleData> rules)
        {
            var byText = new Dictionary<string, List<MailRuleData>>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in rules ?? Enumerable.Empty<MailRuleData>())
            {
                var texts = rule.Conditions.Concat(rule.Exceptions)
                    .SelectMany(c => c.Texts)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var t in texts)
                {
                    if (!byText.TryGetValue(t, out var list)) byText[t] = list = new List<MailRuleData>();
                    list.Add(rule);
                }
            }

            return byText
                .Where(kv => kv.Value.Count > 1)
                .Select(kv => new SharedText { Text = kv.Key, RuleNames = kv.Value.Select(r => r.Name).ToList() })
                .OrderBy(s => s.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        static IEnumerable<RuleClauseData> AllClauses(MailRuleData rule)
        {
            return rule.Conditions.Concat(rule.Exceptions).Concat(rule.Actions);
        }

        static int Bytes(string s) => string.IsNullOrEmpty(s) ? 0 : Encoding.Unicode.GetByteCount(s);
    }
}
