using Microsoft.Exchange.WebServices.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Talisman.Properties;

namespace Talisman
{
    // --------------------------------------------------------------------------
    /// <summary>
    /// The Application
    /// </summary>
    // --------------------------------------------------------------------------
    public partial class App : Application
    {
        System.Threading.Tasks.Task newVersionCheck;

        // --------------------------------------------------------------------------
        /// <summary>
        /// OnStartup
        /// </summary>
        // --------------------------------------------------------------------------
        protected override void OnStartup(StartupEventArgs e)
        {
            DraggingLogic.SetDpiAwareness();
            var talismanProcesses = Process.GetProcessesByName("Talisman");
            if(talismanProcesses.Length > 1)
            {

                Debug.WriteLine("Found an extra Talisman procress.");
#if DEBUG
                foreach(var process in talismanProcesses)
                {
                    Debug.WriteLine("Shutting down other Talisman ...");
                    var currentProcessId = Process.GetCurrentProcess().Id;
                    if (process.Id != currentProcessId)
                    {
                        try
                        {

                            process.Kill();
                        }
                        catch(Exception err)
                        {
                            Debug.WriteLine($"Could not end other talisman process: {err.Message}");
                        }
                    }
                }
#else
                Debug.WriteLine("Shutting down myself.");
                Application.Current.Shutdown();
#endif               
            }
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            if(Settings.Default.CurrentVersion != assemblyVersion)
            {
                Settings.Default.Upgrade();
                Settings.Default.Reload();
                Settings.Default.CurrentVersion = assemblyVersion;
                Settings.Default.Save();
                Debug.WriteLine("Upgraded Settings: " + Settings.Default.Location);
            }

            if(String.IsNullOrEmpty(Settings.Default.CheckForNewVersions))
            {
                var result = MessageBox.Show("Check for new versions of Talisman?", "Talisman", MessageBoxButton.YesNo);
                Settings.Default.CheckForNewVersions = result.ToString();
                Settings.Default.Save();
            }
            
            if(Settings.Default.CheckForNewVersions == "Yes")
            {
                this.newVersionCheck = System.Threading.Tasks.Task.Run(CheckForNewVersion);
            }

            TestEws();
            base.OnStartup(e);
        }

        /// <summary>
        /// Prototype code to check exchange over the web
        /// </summary>
        async void TestEws()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                //ExchangeService service = new ExchangeService(ExchangeVersion.Exchange2007_SP1);
                //service.Credentials = new WebCredentials("ejorgens@adobe.com", "lord i have been given much");
                //service.UseDefaultCredentials = false;
                //service.AutodiscoverUrl("ejorgens@adobe.com", (url) =>
                //{
                //    Debug.WriteLine($"Redirect validate: {url}");
                //    return new Uri(url).Scheme == "https";
                //});

                //// Initialize values for the start and end times, and the number of appointments to retrieve.
                //var startDate = DateTime.Now;
                //var endDate = startDate.AddDays(30);

                //const int NUM_APPTS = 5;
                //var calendar = CalendarFolder.Bind(service, WellKnownFolderName.Calendar, new PropertySet());
                //var cView = new CalendarView(startDate, endDate, NUM_APPTS);
                //cView.PropertySet = new PropertySet(AppointmentSchema.Subject, AppointmentSchema.Start, AppointmentSchema.End);

                //FindItemsResults<Appointment> appointments = calendar.FindAppointments(cView);
                //Debug.WriteLine("\nThe first " + NUM_APPTS + " appointments on your calendar from " + startDate.Date.ToShortDateString() +
                //                  " to " + endDate.Date.ToShortDateString() + " are: \n");

                //foreach (Appointment a in appointments)
                //{
                //    Debug.Write("Subject: " + a.Subject.ToString() + " ");
                //    Debug.Write("    Start: " + a.Start.ToString() + " ");
                //    Debug.Write("    End: " + a.End.ToString());
                //    Debug.WriteLine("");
                //}
            });

        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// Check github to see if there is a recent install
        /// </summary>
        // --------------------------------------------------------------------------
        async void CheckForNewVersion()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {

                Debug.WriteLine("Checking for a newer version");

                try
                {
                    var versionUrl = "https://raw.githubusercontent.com/nebosite/talisman/master/installs/currentVersion.txt";

                    var request = HttpWebRequest.Create(versionUrl);
                    var response = request.GetResponse();

                    string responseText = new StreamReader(response.GetResponseStream()).ReadToEnd().Trim();

                    var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                    var localVersionParts = assemblyVersion.Split('.');
                    var currentVersionParts = responseText.Split('.');

                    for (int i = 0; i < localVersionParts.Length && i < currentVersionParts.Length; i++)
                    {
                        var localPart = int.Parse(localVersionParts[i]);
                        var currentPart = int.Parse(currentVersionParts[i]);
                        if (currentPart > localPart)
                        {
                            var result = MessageBox.Show($"There is a newer version of Talisman available ({responseText}).  Would you like to download it?",
                                "Talisman Version Check", MessageBoxButton.YesNo);
                            if (result == MessageBoxResult.Yes)
                            {
                                Process.Start("https://github.com/nebosite/talisman/tree/master/installs");
                            }
                            break;
                        }
                    }

                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error trying to check version: " + e.ToString());
                }
            });
            
        }

        // --------------------------------------------------------------------------
        /// <summary>
        /// OnExit
        /// </summary>
        // --------------------------------------------------------------------------
        protected override void OnExit(ExitEventArgs e)
        {
            Settings.Default.CrashedLastTime = false;
            Settings.Default.Save();
            Debug.WriteLine("Exit Settings: " + Settings.Default.Location);

        }
    }
}
