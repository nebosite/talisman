using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Office.Interop.Outlook;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Helper class for working with outlook
    /// </summary>
    // --------------------------------------------------------------------------
    public class OutlookHelper
    {
        /// <summary>
        /// 
        /// </summary>
        Application _outlookApplication;
        Application OutlookApp
        {
            get
            {
                if(_outlookApplication == null)
                {
                    _outlookApplication = GetApplicationObject();
                }
                return _outlookApplication;
            }
        }



        // --------------------------------------------------------------------------
        /// <summary>
        /// Get the running instance of a COM app by ProgID. Replaces
        /// Marshal.GetActiveObject, which was removed in modern .NET. Throws a
        /// COMException with 0x800401E3 (MK_E_UNAVAILABLE) when nothing is running.
        /// </summary>
        // --------------------------------------------------------------------------
        static object GetActiveObject(string progId)
        {
            CLSIDFromProgIDEx(progId, out Guid clsid);
            GetActiveObject(ref clsid, IntPtr.Zero, out object instance);
            return instance;
        }

        [DllImport("ole32.dll", PreserveSig = false)]
        static extern void CLSIDFromProgIDEx([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid lpclsid);

        [DllImport("oleaut32.dll", PreserveSig = false)]
        static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        // --------------------------------------------------------------------------
        /// <summary>
        ///
        /// </summary>
        // --------------------------------------------------------------------------
        Application GetApplicationObject()
        {
            try
            {
                Application application = null;

                var processes = Process.GetProcessesByName("OUTLOOK");
                var all = Process.GetProcesses();
                // Check whether there is an Outlook process running.
                if (Process.GetProcessesByName("OUTLOOK").Count() > 0)
                {
                    try
                    {
                        // If so, obtain the running instance and cast it to an Application object.
                        application = GetActiveObject("Outlook.Application") as Application;
                        return application;
                    }
                    catch (COMException e)
                    {
                        if ((uint)e.ErrorCode != 0x800401E3)
                        {
                            throw new ApplicationException("Error trying to access outlook: " + e.ErrorCode + " " + e.Message, e);
                        }
                    }
                }


                // If not, create a new instance of Outlook and log on to the default profile.
                application = new Application();
                NameSpace nameSpace = application.GetNamespace("MAPI");
                nameSpace.Logon("", "", Missing.Value, Missing.Value);
                nameSpace = null;

                // Return the Outlook Application object.
                return application;
            }
            catch (COMException e)
            {
                if ((uint)e.ErrorCode == 0x800401E3)
                {
                    throw new ApplicationException("This user does not have rights to access the running outlook application.  \r\n" +
                        "Workarounds: Run under a different context (non-administrator)\r\n" +
                        "             Close Outlook (This will bring up a dialog box)\r\n" +
                        "             Turn off UAC.");
                }
                throw;
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Send an email through outlook
        /// </summary>
        // --------------------------------------------------------------------------
        public void SendMail(string toAddress, string subject, string body)
        {
            var newMail = (MailItem)OutlookApp.CreateItem(OlItemType.olMailItem);

            foreach (var recipient in toAddress.Split(new char[] { ';', ',' }))
            {
                newMail.Recipients.Add(recipient.Trim());
            }

            newMail.Subject = subject;
            newMail.Body = body;

            Account acc = null;

            //Look for our account in the Outlook
            foreach (Account account in OutlookApp.Session.Accounts)
            {
                acc = account;
                break;
            }

            newMail.SendUsingAccount = acc;
            newMail.Send();
        }

        int failureCount = 0;
        // --------------------------------------------------------------------------
        /// <summary>
        /// Get items from outlook that are time sensitive in some lookahead window
        /// </summary>
        // --------------------------------------------------------------------------
        public TimeRelatedItem[] GetNextTimerRelatedItems(double hoursAhead = 8)
        {
            try
            {
                var folders = OutlookApp.Session.Folders;
                List<TimeRelatedItem> returnItems = new List<TimeRelatedItem>();

                var endDate = DateTime.Now.AddHours(8);
                GetTimeRelatedTimesFromFolder(OlDefaultFolders.olFolderCalendar, endDate, returnItems);
                GetTimeRelatedTimesFromFolder(OlDefaultFolders.olFolderTasks, endDate, returnItems);

                failureCount = 0;
                return returnItems.ToArray();
            }
            catch(System.Exception ex)
            {
                _outlookApplication = null;  // Forget the reference so that we will reconnect next time.
                failureCount++;
                // Outlook is an external dependency that can be closed, busy, or
                // mid-restart. Never let that propagate - log it and return nothing;
                // the next poll will try to reconnect.
                Log.Warn($"Failed to read Outlook calendar/tasks (failure #{failureCount}). Will reconnect next poll.", ex);
            }
            return new TimeRelatedItem[0];
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Process a folder for time related items
        /// </summary>
        // --------------------------------------------------------------------------
        private void GetTimeRelatedTimesFromFolder(OlDefaultFolders defaultFolderId, DateTime endTime, List<TimeRelatedItem> returnItems)
        {
            var folder = (Folder)OutlookApp.Session.GetDefaultFolder(defaultFolderId);
            ProcessFolder(folder, endTime, returnItems);
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Process a folder for time related items
        /// </summary>
        // --------------------------------------------------------------------------
        private void ProcessFolder(Folder folder, DateTime endTime, List<TimeRelatedItem> returnItems)
        {
            DateTime now = DateTime.Now;

            // Initial restriction is Jet query for date range
            string timeSlotFilter =
                "[Start] >= '" + now.ToString("g")
                + "' AND [Start] <= '" + endTime.ToString("g") + "'";

            var items = folder.Items;
            items.Sort("[Start]");
            items.IncludeRecurrences = true;
            items = items.Restrict(timeSlotFilter);
            foreach (object item in items)
            {
                var appointment = item as AppointmentItem;
                var task = item as TaskItem;

                if (appointment != null)
                {
                    var newItem = new TimeRelatedItem();
                    var locationText = string.IsNullOrWhiteSpace(appointment.Location) ? "" : $" in {appointment.Location}";
                    newItem.Title = appointment.Subject;

                    newItem.Start = appointment.Start;
                    newItem.End = appointment.End;
                    newItem.Location = appointment.Location;
                    newItem.Contents = appointment.Body;

                    var recurrencePattern = appointment.GetRecurrencePattern();
                    if (appointment.RecurrenceState == OlRecurrenceState.olApptOccurrence &&
                        recurrencePattern.RecurrenceType == OlRecurrenceType.olRecursWeekly)
                    {
                        // TODO: Think about weekly recurrence?
                    }
                    else
                    {
                        // TODO: No weekly recurring?
                    }

                    returnItems.Add(newItem);
                }
                else if (task != null)
                {
                    var newItem = new TimeRelatedItem();
                    newItem.Title = task.Subject;
                    newItem.Start = task.ReminderTime;

                    // TODO: incorperate outlook tasks
                }
                else
                {
                    // TODO: What about these things that are not tasks?
                }
            }

            foreach (Folder subFolder in folder.Folders)
            {
                ProcessFolder(subFolder, endTime, returnItems);
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Read every mail rule from the default store and flatten it into plain
        /// data. Read-only. Defensive throughout - a single unreadable rule, clause,
        /// or property is logged and skipped rather than failing the whole read.
        /// Note: the Outlook object model only exposes a subset of the condition and
        /// action types, so anything it doesn't model simply won't appear here.
        /// </summary>
        // --------------------------------------------------------------------------
        public MailRuleData[] ReadMailRules()
        {
            var result = new List<MailRuleData>();
            ReadMailRulesIncremental(null, result.Add);
            return result.ToArray();
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Read the mail rules one at a time, reporting the total up front and each
        /// rule as it is read - so a caller can show progress and stream results in
        /// without waiting for the whole (potentially slow) read to finish. Meant to
        /// be called on a dedicated STA thread, since it touches Outlook COM.
        /// </summary>
        // --------------------------------------------------------------------------
        public void ReadMailRulesIncremental(Action<int> onTotalKnown, Action<MailRuleData> onRuleRead)
        {
            Rules rules = OutlookApp.Session.DefaultStore.GetRules();
            onTotalKnown?.Invoke(rules.Count);

            foreach (Rule rule in rules)
            {
                MailRuleData data = null;
                try { data = ReadOneRule(rule); }
                catch (System.Exception ex) { Log.Warn("Failed to read a mail rule; skipping it.", ex); }
                if (data != null) onRuleRead?.Invoke(data);
            }
        }

        MailRuleData ReadOneRule(Rule rule)
        {
            var data = new MailRuleData
            {
                Name = rule.Name ?? "(unnamed)",
                Enabled = rule.Enabled,
                RuleType = rule.RuleType.ToString(),
                ExecutionOrder = rule.ExecutionOrder,
            };
            ReadConditions(rule.Conditions, data.Conditions);
            ReadConditions(rule.Exceptions, data.Exceptions);
            ReadActions(rule.Actions, data.Actions);
            return data;
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Flatten the enabled conditions (or exceptions) of a rule.
        /// </summary>
        // --------------------------------------------------------------------------
        void ReadConditions(RuleConditions conditions, List<RuleClauseData> into)
        {
            if (conditions == null) return;
            foreach (RuleCondition cond in conditions)
            {
                try
                {
                    if (cond == null || !cond.Enabled) continue;
                    var clause = new RuleClauseData { Kind = cond.ConditionType.ToString() };

                    switch (cond)
                    {
                        case TextRuleCondition t:
                            AddMatchTexts(clause, ToStrings(t.Text));
                            break;
                        case AddressRuleCondition a:
                            AddMatchTexts(clause, ToStrings(a.Address));
                            break;
                        case ToOrFromRuleCondition tf:
                            AddMatchTexts(clause, RecipientNames(tf.Recipients));
                            break;
                        case CategoryRuleCondition c:
                            AddMatchTexts(clause, ToStrings(c.Categories));
                            break;
                        case FormNameRuleCondition f:
                            AddMatchTexts(clause, ToStrings(f.FormName));
                            break;
                        case SenderInAddressListRuleCondition s:
                            clause.Details.Add(SafeGet(() => s.AddressList?.Name, "(address list)"));
                            break;
                        case ImportanceRuleCondition i:
                            clause.Details.Add(i.Importance.ToString());
                            break;
                        case AccountRuleCondition ac:
                            clause.Details.Add(SafeGet(() => ac.Account?.DisplayName, "(account)"));
                            break;
                    }

                    into.Add(clause);
                }
                catch (System.Exception ex)
                {
                    Log.Warn("Failed to read a mail-rule condition.", ex);
                }
            }
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Flatten the enabled actions of a rule.
        /// </summary>
        // --------------------------------------------------------------------------
        void ReadActions(RuleActions actions, List<RuleClauseData> into)
        {
            if (actions == null) return;
            foreach (RuleAction act in actions)
            {
                try
                {
                    if (act == null || !act.Enabled) continue;
                    var clause = new RuleClauseData { Kind = act.ActionType.ToString() };

                    switch (act)
                    {
                        case MoveOrCopyRuleAction m:
                            clause.Details.Add(SafeGet(() => m.Folder?.FolderPath, "(no folder)"));
                            break;
                        case SendRuleAction s:
                            foreach (var n in RecipientNames(s.Recipients)) clause.Details.Add(n);
                            break;
                        case AssignToCategoryRuleAction c:
                            foreach (var cat in ToStrings(c.Categories)) clause.Details.Add(cat);
                            break;
                        case NewItemAlertRuleAction n:
                            clause.Details.Add(n.Text ?? "");
                            break;
                        case PlaySoundRuleAction p:
                            clause.Details.Add(p.FilePath ?? "");
                            break;
                        case MarkAsTaskRuleAction mt:
                            clause.Details.Add("mark " + mt.MarkInterval);
                            break;
                    }

                    into.Add(clause);
                }
                catch (System.Exception ex)
                {
                    Log.Warn("Failed to read a mail-rule action.", ex);
                }
            }
        }

        // Add match text values both as display details and as searchable texts.
        static void AddMatchTexts(RuleClauseData clause, IEnumerable<string> texts)
        {
            foreach (var t in texts)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                clause.Details.Add(t);
                clause.Texts.Add(t);
            }
        }

        // Coerce a COM property (string, string[]/object[], or null) into strings.
        static IEnumerable<string> ToStrings(object value)
        {
            if (value == null) yield break;
            if (value is string s) { yield return s; yield break; }
            if (value is System.Collections.IEnumerable items)
                foreach (var item in items)
                    if (item != null) yield return item.ToString();
        }

        static IEnumerable<string> RecipientNames(Recipients recipients)
        {
            if (recipients == null) yield break;
            foreach (Recipient r in recipients)
            {
                string name = null;
                try { name = r.Name ?? r.Address; } catch { }
                if (!string.IsNullOrWhiteSpace(name)) yield return name;
            }
        }

        static string SafeGet(Func<string> get, string fallback)
        {
            try { return get() ?? fallback; } catch { return fallback; }
        }
    }
}
