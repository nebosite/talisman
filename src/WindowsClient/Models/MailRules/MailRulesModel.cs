using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// View model for the Mail Rules inspection tab. Rules stream in from a
    /// background read (see AppModel.LoadMailRules): BeginLoad clears the tree,
    /// SetTotal sizes the progress bar, AddRule appends a rule as it arrives, and
    /// EndLoad finalises the size summary. Also owns the type-to-filter and the
    /// shared-text check. All these methods run on the UI/Dispatcher thread.
    /// </summary>
    // --------------------------------------------------------------------------
    public class MailRulesModel : BaseModel
    {
        readonly List<MailRuleData> _rules = new List<MailRuleData>();

        public ObservableCollection<MailRuleNode> Nodes { get; } = new ObservableCollection<MailRuleNode>();

        string _summaryText = "No rules loaded. Click Refresh (Outlook must be running).";
        public string SummaryText
        {
            get => _summaryText;
            private set { _summaryText = value; NotifyPropertyChanged(nameof(SummaryText)); }
        }

        // ---- loading / progress state -------------------------------------------

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
            }
        }

        public Visibility ProgressVisibility => _isLoading ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>True while connecting/before the rule count is known - drives an
        /// indeterminate progress bar; becomes determinate once the total is set.</summary>
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

        // ---- filter --------------------------------------------------------------

        string _filterText = "";
        public string FilterText
        {
            get => _filterText;
            set { _filterText = value; NotifyPropertyChanged(nameof(FilterText)); ApplyFilter(); }
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

        // ==========================================================================
        //  incremental loading
        // ==========================================================================

        // --------------------------------------------------------------------------
        /// <summary>
        /// Start a fresh load: clear the tree and show the connecting state.
        /// </summary>
        // --------------------------------------------------------------------------
        public void BeginLoad()
        {
            _rules.Clear();
            Nodes.Clear();
            LoadTotal = 0;
            LoadValue = 0;
            DuplicateReport = "";
            IsLoading = true;
            SummaryText = "Connecting to Outlook…";
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// The number of rules to expect (sizes the progress bar).
        /// </summary>
        // --------------------------------------------------------------------------
        public void SetTotal(int total)
        {
            LoadTotal = total;
            SummaryText = $"Loading rules… 0 / {total}";
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Append one freshly-read rule to the tree and advance the progress.
        /// </summary>
        // --------------------------------------------------------------------------
        public void AddRule(MailRuleData rule)
        {
            if (rule == null) return;
            _rules.Add(rule);

            var node = BuildRuleNode(rule);
            node.ApplyFilter(_filterText); // respect an active filter as rules stream in
            Nodes.Add(node);

            LoadValue = _rules.Count;
            SummaryText = LoadTotal > 0
                ? $"Loading rules… {_rules.Count} / {LoadTotal}"
                : $"Loading rules… {_rules.Count}";
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Finish the load: hide the progress bar and show the size summary.
        /// </summary>
        // --------------------------------------------------------------------------
        public void EndLoad()
        {
            IsLoading = false;
            SummaryText = BuildSizeSummary();
            ApplyFilter();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Bulk load (used by tests and any non-incremental caller).
        /// </summary>
        // --------------------------------------------------------------------------
        public void Load(IEnumerable<MailRuleData> rules)
        {
            BeginLoad();
            foreach (var rule in rules ?? Enumerable.Empty<MailRuleData>()) AddRule(rule);
            EndLoad();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Show a failure message instead of a tree (e.g. Outlook not reachable).
        /// </summary>
        // --------------------------------------------------------------------------
        public void SetError(string message)
        {
            _rules.Clear();
            Nodes.Clear();
            IsLoading = false;
            SummaryText = message;
            DuplicateReport = "";
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Run the shared-text check and put the result in DuplicateReport.
        /// </summary>
        // --------------------------------------------------------------------------
        public void RunSharedTextCheck()
        {
            if (IsLoading) { DuplicateReport = "Still loading rules…"; return; }
            if (_rules.Count == 0) { DuplicateReport = "Load some rules first."; return; }

            var shared = MailRulesAnalyzer.FindSharedText(_rules);
            if (shared.Count == 0)
            {
                DuplicateReport = "No text value is shared across multiple rules.";
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"{shared.Count} text value(s) appear in more than one rule:");
            foreach (var s in shared)
                sb.AppendLine($"  \"{s.Text}\"  ->  {string.Join(", ", s.RuleNames)}");
            DuplicateReport = sb.ToString().TrimEnd();
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

        // --------------------------------------------------------------------------
        /// <summary>
        /// Build the tree node for one rule: a header, an info line, and grouped
        /// conditions/exceptions/actions. Each clause expands into one child node
        /// per individual value, so every match text is its own filterable node.
        /// </summary>
        // --------------------------------------------------------------------------
        internal static MailRuleNode BuildRuleNode(MailRuleData rule)
        {
            var kb = MailRulesAnalyzer.ToKb(MailRulesAnalyzer.EstimateBytes(rule));
            var disabled = rule.Enabled ? "" : "  (disabled)";
            var node = new MailRuleNode($"{rule.Name}{disabled}  ·  ~{kb:0.0} KB");

            node.Children.Add(new MailRuleNode($"[type: {rule.RuleType} · order: {rule.ExecutionOrder}]"));

            AddClauseGroup(node, "Conditions", rule.Conditions);
            AddClauseGroup(node, "Exceptions", rule.Exceptions);
            AddClauseGroup(node, "Actions", rule.Actions);
            return node;
        }

        static void AddClauseGroup(MailRuleNode parent, string title, List<RuleClauseData> clauses)
        {
            if (clauses == null || clauses.Count == 0) return;

            var group = new MailRuleNode($"{title} ({clauses.Count})");
            foreach (var clause in clauses)
            {
                // Clause node names the type; each individual value is its own child
                // so a multi-value condition becomes a list, not one joined string.
                var clauseNode = new MailRuleNode(clause.Kind);
                foreach (var detail in clause.Details)
                    clauseNode.Children.Add(new MailRuleNode(detail));
                group.Children.Add(clauseNode);
            }
            parent.Children.Add(group);
        }

        void ApplyFilter()
        {
            foreach (var node in Nodes) node.ApplyFilter(_filterText);
        }
    }
}
