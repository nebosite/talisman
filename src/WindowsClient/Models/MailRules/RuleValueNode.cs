namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// A single editable value of a condition (e.g. one header word / address).
    /// Two-way bound to a text box; clearing the text removes the value entirely.
    /// Read-only when its condition type can't be written back (recipients,
    /// folders, importance, actions).
    /// </summary>
    // --------------------------------------------------------------------------
    public class RuleValueNode : MailRuleNode
    {
        readonly RuleConditionNode _owner;

        public bool Editable { get; }
        public bool ReadOnly => !Editable;

        string _value;
        public string Value
        {
            get => _value;
            set
            {
                if (_value == value) return;
                _value = value;
                NotifyPropertyChanged(nameof(Value));
                _owner?.OnValueChanged(this);
            }
        }

        protected override string MatchText => _value;

        public RuleValueNode(RuleConditionNode owner, string value, bool editable)
            : base(value)
        {
            _owner = owner;
            _value = value ?? "";
            Editable = editable;
        }
    }
}
