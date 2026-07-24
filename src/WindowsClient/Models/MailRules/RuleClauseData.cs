using System.Collections.Generic;
using System.Linq;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// One condition, exception, or action of a mail rule, flattened out of the
    /// Outlook object model into plain data (so the reading is decoupled from all
    /// the tree/filter/analysis logic, which is then unit-testable).
    /// </summary>
    // --------------------------------------------------------------------------
    public class RuleClauseData
    {
        /// <summary>The Outlook condition/action type name (e.g. "olConditionBody").</summary>
        public string Kind { get; set; } = "";

        /// <summary>Human-readable detail lines (folder path, importance, etc.).</summary>
        public List<string> Details { get; set; } = new List<string>();

        /// <summary>The raw match text values, used for the shared-text analysis.</summary>
        public List<string> Texts { get; set; } = new List<string>();

        // --------------------------------------------------------------------------
        /// <summary>
        /// One-line description for display in the tree.
        /// </summary>
        // --------------------------------------------------------------------------
        public string Describe()
        {
            return Details.Count == 0 ? Kind : Kind + ": " + string.Join("; ", Details);
        }
    }
}
