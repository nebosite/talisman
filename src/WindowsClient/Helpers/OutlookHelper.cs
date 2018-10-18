﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Office.Interop.Outlook;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// Helper class for working with outlook
    /// </summary>
    // --------------------------------------------------------------------------
    class OutlookHelper
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
        /// 
        /// </summary>
        // --------------------------------------------------------------------------
        Application GetApplicationObject()
        {
            try
            {
                Application application = null;

                // Check whether there is an Outlook process running.
                if (Process.GetProcessesByName("OUTLOOK").Count() > 0)
                {

                    // If so, use the GetActiveObject method to obtain the process and cast it to an Application object.
                    application = Marshal.GetActiveObject("Outlook.Application") as Application;
                }
                else
                {

                    // If not, create a new instance of Outlook and log on to the default profile.
                    application = new Application();
                    NameSpace nameSpace = application.GetNamespace("MAPI");
                    nameSpace.Logon("", "", Missing.Value, Missing.Value);
                    nameSpace = null;
                }

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
        /// Get items from outlook that are time sensitive in some lookahead window
        /// </summary>
        // --------------------------------------------------------------------------
        public TimeRelatedItem[] GetNextTimerRelatedItems(double hoursAhead = 8)
        {
            var folders = OutlookApp.Session.Folders;
            List<TimeRelatedItem> returnItems = new List<TimeRelatedItem>();

            var endDate = DateTime.Now.AddHours(8);
            GetTimeRelatedTimesFromFolder(OlDefaultFolders.olFolderCalendar, endDate, returnItems);
            GetTimeRelatedTimesFromFolder(OlDefaultFolders.olFolderTasks, endDate, returnItems);

            return returnItems.ToArray();
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
                    newItem.Title = appointment.Subject;
                    newItem.Start = appointment.Start;
                    newItem.End = appointment.End;
                    newItem.Location = appointment.Location;

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
                    // TODO: What about these?
                }
            }

            foreach (Folder subFolder in folder.Folders)
            {
                ProcessFolder(subFolder, endTime, returnItems);
            }
        }
    }
}
