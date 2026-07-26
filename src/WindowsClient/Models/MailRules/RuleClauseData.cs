using System.Collections.Generic;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Which settable Outlook string-array property an editable condition writes
    /// back to. Only these (all plain string[] properties) are edited; recipient,
    /// folder, importance, etc. conditions are shown read-only.
    /// </summary>
    // --------------------------------------------------------------------------
    public enum RuleValueTarget
    {
        None,
        Text,       // TextRuleCondition.Text
        Address,    // AddressRuleCondition.Address
        Categories, // CategoryRuleCondition.Categories
        FormName,   // FormNameRuleCondition.FormName
    }

    // --------------------------------------------------------------------------
    /// <summary>
    /// One condition, exception, or action of a mail rule, flattened out of the
    /// Outlook object model into plain data. Editable text conditions carry their
    /// values in <see cref="Values"/> and a <see cref="ValueTarget"/> for writing
    /// them back; everything else is shown via <see cref="Detail"/> or read-only
    /// <see cref="Values"/>.
    /// </summary>
    // --------------------------------------------------------------------------
    public class RuleClauseData
    {
        /// <summary>The Outlook condition/action type name (e.g. "olConditionMessageHeader").</summary>
        public string Kind { get; set; } = "";

        /// <summary>True if the values can be edited and written back to Outlook.</summary>
        public bool Editable { get; set; }

        /// <summary>For an editable clause, which string[] property to write.</summary>
        public RuleValueTarget ValueTarget { get; set; } = RuleValueTarget.None;

        /// <summary>Individual values (editable text, or read-only recipient names).</summary>
        public List<string> Values { get; set; } = new List<string>();

        /// <summary>A single non-editable extra (folder path, importance, account, ...).</summary>
        public string Detail { get; set; }
    }
}
