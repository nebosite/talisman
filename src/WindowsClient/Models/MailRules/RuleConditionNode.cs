using System;
using System.Collections.Generic;
using System.Linq;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// A condition/exception/action clause in the tree. For editable clauses
    /// (text-array conditions) its value children can be edited, cleared to remove,
    /// and added to via the [+] button. Holds references back to the original
    /// clause and rule so the model can compute a minimal set of edits at save.
    /// </summary>
    // --------------------------------------------------------------------------
    public class RuleConditionNode : MailRuleNode
    {
        readonly Action _markDirty;

        public RuleClauseData Clause { get; }
        public MailRuleData Rule { get; }
        public bool IsException { get; }

        /// <summary>Bindable: whether the [+] button and editable value boxes show.</summary>
        public bool Editable => Clause.Editable;

        public RuleConditionNode(MailRuleData rule, RuleClauseData clause, bool isException, Action markDirty)
            : base(clause.Kind)
        {
            Rule = rule;
            Clause = clause;
            IsException = isException;
            _markDirty = markDirty;

            foreach (var v in clause.Values)
                Children.Add(new RuleValueNode(this, v, clause.Editable));

            if (clause.Detail != null)
                Children.Add(new RuleValueNode(this, clause.Detail, false)); // read-only extra
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Called when one of this clause's value boxes changes. Clearing an editable
        /// value removes it; any change marks the rule set dirty.
        /// </summary>
        // --------------------------------------------------------------------------
        public void OnValueChanged(RuleValueNode node)
        {
            if (node.Editable && string.IsNullOrEmpty(node.Value))
                RemoveChild(node);

            _markDirty?.Invoke();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Add a new blank value box (the [+] button).
        /// </summary>
        // --------------------------------------------------------------------------
        public void AddBlankValue()
        {
            if (!Editable) return;
            Children.Add(new RuleValueNode(this, "", true));
            IsExpanded = true;
            _markDirty?.Invoke();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// The current (non-empty) editable values of this clause.
        /// </summary>
        // --------------------------------------------------------------------------
        public List<string> CurrentValues()
        {
            return Children.OfType<RuleValueNode>()
                .Where(n => n.Editable && !string.IsNullOrWhiteSpace(n.Value))
                .Select(n => n.Value)
                .ToList();
        }

        // Remove a child, deferring to the dispatcher in-app to avoid mutating the
        // bound collection during a binding update. Synchronous under test.
        void RemoveChild(RuleValueNode node)
        {
            var app = System.Windows.Application.Current;
            if (app != null) app.Dispatcher.BeginInvoke(new Action(() => Children.Remove(node)));
            else Children.Remove(node);
        }
    }
}
