using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// View model for the Mail Rules tab. Rules stream in from a background read
    /// (AppModel.LoadMailRules); editable text conditions can be changed, cleared
    /// (to remove a value), or added to. It tracks a dirty flag and computes the
    /// MINIMAL set of edits to write back, diffing the tree against the baseline
    /// captured from the last Outlook read (so an imported JSON set also diffs
    /// correctly). Also owns the filter, the substring shared-text check, and JSON
    /// export/import. All methods run on the UI/Dispatcher thread.
    /// </summary>
    // --------------------------------------------------------------------------
    public class MailRulesModel : BaseModel
    {
        readonly List<MailRuleData> _rules = new List<MailRuleData>();
        readonly List<RuleConditionNode> _conditionNodes = new List<RuleConditionNode>();
        readonly Dictionary<string, List<string>> _baseline = new Dictionary<string, List<string>>();

        // Auto-expand is scheduled on a delay after a filter change (expanding is the
        // expensive part). Short/broad terms wait longer so a quick keystroke doesn't
        // trigger a big expand before the user narrows the term.
        readonly DispatcherTimer _expandTimer = new DispatcherTimer();

        public MailRulesModel()
        {
            _expandTimer.Tick += (s, e) => { _expandTimer.Stop(); ExpandMatches(); };
        }

        public ObservableCollection<MailRuleNode> Nodes { get; } = new ObservableCollection<MailRuleNode>();

        string _summaryText = "No rules loaded. Click Refresh (Outlook must be running).";
        public string SummaryText
        {
            get => _summaryText;
            private set { _summaryText = value; NotifyPropertyChanged(nameof(SummaryText)); }
        }

        // ---- loading / progress --------------------------------------------------

        bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                _isLoading = value;
                NotifyPropertyChanged(nameof(IsLoading));
                NotifyPropertyChanged(nameof(ProgressVisibility));
                NotifyPropertyChanged(nameof(IsConnecting));
                NotifyPropertyChanged(nameof(CanSave));
            }
        }

        public Visibility ProgressVisibility => _isLoading ? Visibility.Visible : Visibility.Collapsed;
        public bool IsConnecting => _isLoading && _loadTotal == 0;

        int _loadTotal;
        public int LoadTotal
        {
            get => _loadTotal;
            private set { _loadTotal = value; NotifyPropertyChanged(nameof(LoadTotal)); NotifyPropertyChanged(nameof(IsConnecting)); }
        }

        int _loadValue;
        public int LoadValue
        {
            get => _loadValue;
            private set { _loadValue = value; NotifyPropertyChanged(nameof(LoadValue)); }
        }

        // ---- dirty / save --------------------------------------------------------

        bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set { _isDirty = value; NotifyPropertyChanged(nameof(IsDirty)); NotifyPropertyChanged(nameof(CanSave)); }
        }

        /// <summary>The Save button is enabled only when there are unsaved edits.</summary>
        public bool CanSave => _isDirty && !_isLoading;

        // ---- filter --------------------------------------------------------------

        string _filterText = "";
        public string FilterText
        {
            get => _filterText;
            set { _filterText = value; NotifyPropertyChanged(nameof(FilterText)); ApplyFilter(); ScheduleExpand(); }
        }

        // ---- shared-text report --------------------------------------------------

        string _duplicateReport = "";
        public string DuplicateReport
        {
            get => _duplicateReport;
            private set
            {
                _duplicateReport = value;
                NotifyPropertyChanged(nameof(DuplicateReport));
                NotifyPropertyChanged(nameof(DuplicateReportVisibility));
            }
        }

        public Visibility DuplicateReportVisibility =>
            string.IsNullOrEmpty(_duplicateReport) ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>The shared-text rows, each filterable by its term.</summary>
        public ObservableCollection<SharedText> SharedResults { get; } = new ObservableCollection<SharedText>();

        // ==========================================================================
        //  incremental loading
        // ==========================================================================

        public void BeginLoad()
        {
            _rules.Clear();
            _conditionNodes.Clear();
            Nodes.Clear();
            LoadTotal = 0;
            LoadValue = 0;
            DuplicateReport = "";
            SharedResults.Clear();
            IsDirty = false;
            IsLoading = true;
            SummaryText = "Connecting to Outlook…";
        }

        public void SetTotal(int total)
        {
            LoadTotal = total;
            SummaryText = $"Loading rules… 0 / {total}";
        }

        public void AddRule(MailRuleData rule)
        {
            if (rule == null) return;
            SortClauseValues(rule);
            _rules.Add(rule);

            var node = BuildRuleNode(rule);
            node.ApplyFilter(_filterText);
            Nodes.Add(node);

            LoadValue = _rules.Count;
            SummaryText = LoadTotal > 0
                ? $"Loading rules… {_rules.Count} / {LoadTotal}"
                : $"Loading rules… {_rules.Count}";
        }

        public void EndLoad()
        {
            IsLoading = false;
            SummaryText = BuildSizeSummary();
            ApplyFilter();
        }

        public void Load(IEnumerable<MailRuleData> rules)
        {
            BeginLoad();
            foreach (var rule in rules ?? Enumerable.Empty<MailRuleData>()) AddRule(rule);
            EndLoad();
        }

        public void SetError(string message)
        {
            _rules.Clear();
            _conditionNodes.Clear();
            Nodes.Clear();
            IsLoading = false;
            IsDirty = false;
            SummaryText = message;
            DuplicateReport = "";
            SharedResults.Clear();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Snapshot the current values as the baseline that edits are diffed against.
        /// Called by the Outlook load path (not by import) so an imported set is
        /// diffed against what's really in Outlook.
        /// </summary>
        // --------------------------------------------------------------------------
        public void CaptureBaseline()
        {
            _baseline.Clear();
            foreach (var rule in _rules)
            {
                foreach (var c in rule.Conditions.Where(c => c.Editable))
                    _baseline[Key(rule.ExecutionOrder, false, c.Kind)] = c.Values.ToList();
                foreach (var c in rule.Exceptions.Where(c => c.Editable))
                    _baseline[Key(rule.ExecutionOrder, true, c.Kind)] = c.Values.ToList();
            }
        }

        // ==========================================================================
        //  editing / saving
        // ==========================================================================

        void MarkDirty() => IsDirty = true;

        // --------------------------------------------------------------------------
        /// <summary>
        /// The minimal set of edits: only conditions whose current values differ
        /// from the Outlook baseline, on the rules that changed.
        /// </summary>
        // --------------------------------------------------------------------------
        public List<MailRuleEdit> HarvestEdits()
        {
            var edits = new List<MailRuleEdit>();

            foreach (var group in _conditionNodes.Where(n => n.Editable).GroupBy(n => n.Rule.ExecutionOrder))
            {
                var order = group.Key;
                var conditionEdits = new List<ConditionEdit>();

                foreach (var node in group)
                {
                    var current = node.CurrentValues();
                    var baseline = _baseline.TryGetValue(Key(order, node.IsException, node.Clause.Kind), out var b)
                        ? b : node.Clause.Values;

                    if (!current.SequenceEqual(baseline, StringComparer.Ordinal))
                    {
                        conditionEdits.Add(new ConditionEdit
                        {
                            Kind = node.Clause.Kind,
                            IsException = node.IsException,
                            Target = node.Clause.ValueTarget,
                            Values = current,
                        });
                    }
                }

                if (conditionEdits.Count > 0)
                    edits.Add(new MailRuleEdit { ExecutionOrder = order, RuleName = group.First().Rule.Name, Conditions = conditionEdits });
            }

            return edits;
        }

        // ==========================================================================
        //  JSON export / import
        // ==========================================================================

        // --------------------------------------------------------------------------
        /// <summary>
        /// Serialize the current (edited) rule set to JSON.
        /// </summary>
        // --------------------------------------------------------------------------
        public string ToJson() => JsonConvert.SerializeObject(BuildCurrentRules(), Formatting.Indented);

        // --------------------------------------------------------------------------
        /// <summary>
        /// Load a rule set from JSON into the tree and mark it dirty (so it can be
        /// saved back to Outlook). The Outlook baseline is left untouched so edits
        /// diff against what's really in Outlook.
        /// </summary>
        // --------------------------------------------------------------------------
        public void LoadJson(string json)
        {
            var rules = JsonConvert.DeserializeObject<MailRuleData[]>(json) ?? new MailRuleData[0];
            Load(rules);
            IsDirty = true;
            SummaryText = BuildSizeSummary() + "  ·  imported (unsaved)";
        }

        // Build a deep copy of the rules reflecting the current edited values.
        List<MailRuleData> BuildCurrentRules()
        {
            var clones = _rules.Select(Clone).ToDictionary(r => r.ExecutionOrder);

            foreach (var node in _conditionNodes.Where(n => n.Editable))
            {
                if (!clones.TryGetValue(node.Rule.ExecutionOrder, out var clone)) continue;
                var list = node.IsException ? clone.Exceptions : clone.Conditions;
                var clause = list.FirstOrDefault(c => c.Kind == node.Clause.Kind);
                if (clause != null) clause.Values = node.CurrentValues();
            }

            return _rules.Select(r => clones[r.ExecutionOrder]).ToList();
        }

        // ==========================================================================
        //  shared text
        // ==========================================================================

        public void RunSharedTextCheck()
        {
            SharedResults.Clear();
            if (IsLoading) { DuplicateReport = "Still loading rules…"; return; }
            if (_rules.Count == 0) { DuplicateReport = "Load some rules first."; return; }

            var shared = MailRulesAnalyzer.FindSharedText(BuildCurrentRules());
            if (shared.Count == 0)
            {
                DuplicateReport = "No text value is shared across multiple rules.";
                return;
            }

            DuplicateReport = $"{shared.Count} text value(s) overlap across multiple rules " +
                              "(substring, case-insensitive). Click the button to filter by a term:";
            foreach (var s in shared) SharedResults.Add(s);
        }

        // ==========================================================================
        //  tree building
        // ==========================================================================

        string BuildSizeSummary()
        {
            var totalKb = MailRulesAnalyzer.ToKb(MailRulesAnalyzer.EstimateTotalBytes(_rules));
            var limitKb = MailRulesAnalyzer.ToKb(MailRulesAnalyzer.LimitBytes);
            return $"{_rules.Count} rules  ·  ~{totalKb:0.0} / {limitKb:0} KB (estimated)";
        }

        MailRuleNode BuildRuleNode(MailRuleData rule)
        {
            var kb = MailRulesAnalyzer.ToKb(MailRulesAnalyzer.EstimateBytes(rule));
            var disabled = rule.Enabled ? "" : "  (disabled)";
            var node = new MailRuleNode($"{rule.Name}{disabled}  ·  ~{kb:0.0} KB");

            node.Children.Add(new MailRuleNode($"[type: {rule.RuleType} · order: {rule.ExecutionOrder}]"));

            AddClauseGroup(node, "Conditions", rule, rule.Conditions, isException: false);
            AddClauseGroup(node, "Exceptions", rule, rule.Exceptions, isException: true);
            AddClauseGroup(node, "Actions", rule, rule.Actions, isException: false);
            return node;
        }

        void AddClauseGroup(MailRuleNode parent, string title, MailRuleData rule, List<RuleClauseData> clauses, bool isException)
        {
            if (clauses == null || clauses.Count == 0) return;

            var group = new MailRuleNode($"{title} ({clauses.Count})");
            foreach (var clause in clauses)
            {
                var clauseNode = new RuleConditionNode(rule, clause, isException, MarkDirty);
                group.Children.Add(clauseNode);
                if (clause.Editable) _conditionNodes.Add(clauseNode);
            }
            parent.Children.Add(group);
        }

        void ApplyFilter()
        {
            foreach (var node in Nodes) node.ApplyFilter(_filterText);
        }

        // Auto-expand to reveal matches, on a delay. Expanding realizes value boxes
        // and is the expensive part, so a short/broad term (< 3 chars, which tends to
        // match a lot) waits 2s before expanding - long enough for the user to keep
        // typing and narrow it - while a 3+ char term expands after a snappy 300ms.
        // An empty filter collapses everything immediately.
        void ScheduleExpand()
        {
            _expandTimer.Stop();
            if (string.IsNullOrWhiteSpace(_filterText))
            {
                ExpandMatches();
                return;
            }
            _expandTimer.Interval = TimeSpan.FromMilliseconds(_filterText.Trim().Length < 3 ? 2000 : 300);
            _expandTimer.Start();
        }

        void ExpandMatches()
        {
            foreach (var node in Nodes) node.ExpandForFilter(_filterText);
        }

        static string Key(int order, bool isException, string kind) => $"{order}|{(isException ? "X" : "C")}|{kind}";

        // Always show a condition's values sorted (case-insensitive). Done on load
        // so the tree, the diff baseline, and the harvest all use the same order.
        static void SortClauseValues(MailRuleData rule)
        {
            foreach (var clause in rule.Conditions.Concat(rule.Exceptions).Concat(rule.Actions))
                clause.Values.Sort(StringComparer.OrdinalIgnoreCase);
        }

        static MailRuleData Clone(MailRuleData r) => new MailRuleData
        {
            Name = r.Name,
            Enabled = r.Enabled,
            RuleType = r.RuleType,
            ExecutionOrder = r.ExecutionOrder,
            Conditions = r.Conditions.Select(CloneClause).ToList(),
            Exceptions = r.Exceptions.Select(CloneClause).ToList(),
            Actions = r.Actions.Select(CloneClause).ToList(),
        };

        static RuleClauseData CloneClause(RuleClauseData c) => new RuleClauseData
        {
            Kind = c.Kind,
            Editable = c.Editable,
            ValueTarget = c.ValueTarget,
            Values = c.Values.ToList(),
            Detail = c.Detail,
        };
    }
}
