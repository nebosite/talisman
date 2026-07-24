using System.Collections.Generic;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// A single Outlook mail rule, flattened into plain data. Everything the
    /// Outlook object model exposes for the rule lands here; whatever it does NOT
    /// expose simply won't appear, which is the whole point of the spike (compare
    /// this against the Outlook UI to see what is missing).
    /// </summary>
    // --------------------------------------------------------------------------
    public class MailRuleData
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; }
        public string RuleType { get; set; } = "";
        public int ExecutionOrder { get; set; }

        public List<RuleClauseData> Conditions { get; set; } = new List<RuleClauseData>();
        public List<RuleClauseData> Exceptions { get; set; } = new List<RuleClauseData>();
        public List<RuleClauseData> Actions { get; set; } = new List<RuleClauseData>();
    }
}
