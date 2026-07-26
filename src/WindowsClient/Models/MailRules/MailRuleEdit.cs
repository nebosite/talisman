using System.Collections.Generic;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// A minimal, plain description of the edits to apply to one rule - just the
    /// conditions whose values changed. Passed to the STA write thread so the
    /// Outlook-touching code stays small and the diff logic stays testable.
    /// </summary>
    // --------------------------------------------------------------------------
    public class MailRuleEdit
    {
        /// <summary>Stable key used to find the rule in the store.</summary>
        public int ExecutionOrder { get; set; }
        public string RuleName { get; set; } = "";
        public List<ConditionEdit> Conditions { get; set; } = new List<ConditionEdit>();
    }

    // --------------------------------------------------------------------------
    /// <summary>
    /// The new values for one editable condition (or exception) of a rule.
    /// </summary>
    // --------------------------------------------------------------------------
    public class ConditionEdit
    {
        /// <summary>Condition type name, used to locate the condition on the rule.</summary>
        public string Kind { get; set; } = "";
        public bool IsException { get; set; }
        public RuleValueTarget Target { get; set; }
        public List<string> Values { get; set; } = new List<string>();
    }
}
