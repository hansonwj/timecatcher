using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.Windows.AppNotifications;
using System.Diagnostics;
using Microsoft.Windows.AppLifecycle;
using System.Runtime.InteropServices;
using Microsoft.Windows.AppNotifications.Builder;
using Windows.Security.Cryptography.Core;
using Newtonsoft.Json;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Microsoft.Win32;
using Windows.UI.Notifications.Management;
using System.Timers;
using Windows.Media.Protection.PlayReady;
using Windows.System;
using Windows.UI.Notifications;
using System.Numerics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace timecatcher
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        // Import the necessary function from user32.dll
        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }


        public struct TimeEntry
        {
            public string Client;
            public string Project;
            public string Notes;
            public string Datetime;
            public string Manual;
        };

        private static bool UserIsInactive()
        {
            // Get the last input information
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            lastInputInfo.dwTime = 0;

            if (GetLastInputInfo(ref lastInputInfo))
            {
                // Calculate the time since the last input event in milliseconds
                uint idleTime = (uint)Environment.TickCount - lastInputInfo.dwTime;

                // If the system has been idle for more than the specified time (15 minutes in this case),
                // it is considered locked
                return idleTime > 300000; // 5 minutes = 900,000 milliseconds
            }

            return false;
        }

        static DateTime GetLastMonday(DateTime currentDate)
        {
            int daysUntilMonday = ((int)currentDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;

            if (daysUntilMonday == 0)
            {
                // If it's Monday, return the current date
                return currentDate;
            }
            else
            {
                // Calculate and return the previous Monday
                return currentDate.AddDays(-daysUntilMonday);
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>

        private void CreateCsvFile(string FilePath, string Headings)
        {
            try
            {
                using (StreamWriter fileWriter = new StreamWriter(FilePath))
                {
                    fileWriter.WriteLine(Headings);
                }

                Debug.WriteLine("'timeEntries.csv' file has been created with headings.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("An error occurred while creating the file: " + ex.Message);
            }
        }

        public static class Globals
        {
            public static String CurrentClient = ""; // Modifiable
            public static int Interval = 15;

        }

        public static Timer timer;
        private Window m_window;

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            //m_window.Activate(); //// Create process & dispatcher

            AppNotificationManager notificationManager = AppNotificationManager.Default;
            notificationManager.NotificationInvoked += NotificationManager_NotificationInvoked;
            notificationManager.Register();
            // Create a 30 min timer 

            DateTime currentDate = DateTime.Today;
            DateTime lastMonday = GetLastMonday(currentDate);

            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string ConfigDir = System.IO.Path.Combine(docPath, "timecatcher");
            string ConfigFile = System.IO.Path.Combine(ConfigDir, "config.json");
            string TimeEntriesFile = System.IO.Path.Combine(ConfigDir, "timeentries_" + lastMonday.ToString("yyyy-MM-dd") + ".csv");
            string _configJson = "";

            if (!Directory.Exists(ConfigDir))
            {
                Directory.CreateDirectory(ConfigDir);
            }

            if (File.Exists(ConfigFile))
            {
                _configJson = File.ReadAllText(ConfigFile);
            }
            else
            {
                // Create a new Config object with the default values
                var configObject = new
                {
                    CurrentClient = "",
                    Interval = 15
                };

                // Serialize the Config object to JSON
                _configJson = JsonConvert.SerializeObject(configObject, Formatting.Indented);

                // Write the JSON to the config.json file
                File.WriteAllText(ConfigFile, _configJson);
            }

            if (!File.Exists(TimeEntriesFile))
            {
                string Headings = "timestamp,client,notes,manual";
                CreateCsvFile(TimeEntriesFile, Headings);
            }

            Globals.CurrentClient = JsonConvert.DeserializeObject<ConfigData>(_configJson).CurrentClient;
            Globals.Interval = JsonConvert.DeserializeObject<ConfigData>(_configJson).Interval;


            // Get the current time
            DateTime currentTime = DateTime.Now;

            // Check if the current minute matches one of the specified intervals (00, 15, 30, 45)
            if (currentTime.Minute % Globals.Interval == 0)
            {
                // Create the notification time using the current hour and the matched minute
                string notificationTime = currentTime.ToString("dd/MM/yyyy HH:mm");


                CreateTimeEntryNotification(notificationTime);

                timer = new System.Timers.Timer(14 * 1000 * 60);

                // Hook up the Elapsed event for the timer.
                timer.Elapsed += (sender, e) => CleanUpNotification(sender, e, notificationManager, notificationTime);

                timer.Enabled = true;


                var activatedArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
                var activationKind = activatedArgs.Kind;
                if (activationKind != ExtendedActivationKind.AppNotification)
                {
                    ; // LaunchAndBringToForegroundIfNeeded();
                }
                else
                {
                    HandleNotification((AppNotificationActivatedEventArgs)activatedArgs.Data);
                }

            }




        }

        private async static void CleanUpNotification(object sender, EventArgs e, AppNotificationManager notificationManager, String NotificationTime)
        {
            // Create a time entry for 15 mins ago and clear out existing notifications
            // if they haven't been clicked
            IEnumerable<AppNotification> notifications = await notificationManager.GetAllAsync();
            if (notifications.Count() > 0)
            {
                var entry = new TimeEntry();

                if (UserIsInactive())
                {
                    entry.Client = "Inactive";
                }
                else
                {
                    entry.Client = Globals.CurrentClient;
                }
                entry.Project = "";
                entry.Notes = "";
                entry.Datetime = NotificationTime;
                entry.Manual = "False";
                ProcessNewTimeEntry(entry);
            }
            await notificationManager.RemoveAllAsync();
            timer.Enabled = false;
            Environment.Exit(0);
        }

        private void NotificationManager_NotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
        {
            HandleNotification(args);
        }

        public class ConfigData
        {
            public string CurrentClient { get; set; }
            public int Interval { get; set; }
        }


        private static void CreateTimeEntryNotification(string triggeredTime)
        {
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DateTime currentDate = DateTime.Today;
            DateTime lastMonday = GetLastMonday(currentDate);

            string ConfigDir = System.IO.Path.Combine(docPath, "timecatcher");
            string ConfigFile = System.IO.Path.Combine(ConfigDir, "config.json");
            string TimeEntriesFile = System.IO.Path.Combine(ConfigDir, "timeentries_" + lastMonday.ToString("yyyy-MM-dd") + ".csv");
            string _configJson = File.ReadAllText(System.IO.Path.Combine(docPath, "timecatcher", "config.json"));
            var _CurrentClient = "";
            if (UserIsInactive())
            {
                _CurrentClient = "Inactive";
                Globals.CurrentClient = "Inactive";
            }
            else
            {
                _CurrentClient = JsonConvert.DeserializeObject<ConfigData>(_configJson).CurrentClient;
            }

            var appNotificationBuilder = new AppNotificationBuilder();
            appNotificationBuilder.MuteAudio();

            appNotificationBuilder.AddArgument("action", triggeredTime);
            appNotificationBuilder.AddText("What have you been working on?");
            appNotificationBuilder.AddText("15 mins prior to " + triggeredTime);

            appNotificationBuilder.AddTextBox("Client", _CurrentClient, "Client");

            appNotificationBuilder.AddTextBox("Notes", "Notes", "");
            appNotificationBuilder.AddButton(new AppNotificationButton("Submit")
                .AddArgument("action", triggeredTime));
            //appNotificationBuilder.AddButton(new AppNotificationButton("Manager")
            //    .AddArgument("action", "manager"));
            appNotificationBuilder.SetDuration(AppNotificationDuration.Long);


            var appNotification = appNotificationBuilder.BuildNotification();
            

            AppNotificationManager.Default.Show(appNotification);

        }

        private void HandleNotification(AppNotificationActivatedEventArgs args)
        {

            // Use the dispatcher from the window if present, otherwise the app dispatcher
            var dispatcherQueue = m_window?.DispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            var client = args.UserInput["Client"];

            if (client == "")
            {
                client = Globals.CurrentClient;
            }

            dispatcherQueue.TryEnqueue(() =>
            {
                if (args.Arguments["action"] != "manager")
                {
                    Debug.WriteLine("Handling Time Entry");
                    var entry = new TimeEntry();
                    entry.Client = client;
                    entry.Project = "";
                    entry.Notes = args.UserInput["Notes"];
                    entry.Datetime = args.Arguments["action"];
                    entry.Manual = "True";
                    ProcessNewTimeEntry(entry);
                    WriteCurrentClientToJson(client);
                }
                else
                {
                    Debug.WriteLine("Opening Manager");
                    LaunchAndBringToForegroundIfNeeded();
                }
            });

            var shutdownTimer = new System.Timers.Timer(5 * 1000); // 5 seconds then shutdown

            // Hook up the Elapsed event for the timer.
            shutdownTimer.Elapsed += (sender, e) => ShutdownApp(sender, e);

            shutdownTimer.Enabled = true;

        }

        private static void ShutdownApp(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }


            public static void ProcessNewTimeEntry(TimeEntry timeentry)
        {
            // Set a variable to the Documents path.
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DateTime currentDate = DateTime.Today;
            DateTime lastMonday = GetLastMonday(currentDate);

            string ConfigDir = System.IO.Path.Combine(docPath, "timecatcher");
            string TimeEntriesFile = System.IO.Path.Combine(ConfigDir, "timeentries_" + lastMonday.ToString("yyyy-MM-dd") + ".csv");

            // Append text to an existing file named "WriteLines.txt".
            using (StreamWriter outputFile = new StreamWriter(TimeEntriesFile, true))
            {
                outputFile.WriteLine($"{timeentry.Datetime},{timeentry.Client},{timeentry.Notes},{timeentry.Manual}");
            }
        }

        public static void WriteCurrentClientToJson(string CurrentClient)
        {
            ConfigData data = new ConfigData
            {
                CurrentClient = CurrentClient,
                Interval = Globals.Interval
            };

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string ConfigDir = System.IO.Path.Combine(docPath, "timecatcher");
            string ConfigFile = System.IO.Path.Combine(ConfigDir, "config.json");

            try
            {
                // Write the JSON data to the file
                File.WriteAllText(ConfigFile, json);

                Console.WriteLine("Current client data has been written to the JSON file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to JSON file: {ex.Message}");
            }
        }

        private void LaunchAndBringToForegroundIfNeeded()
        {
            if (m_window == null)
            {
                m_window = new MainWindow();
                m_window.Activate();

                // Additionally we show using our helper, since if activated via a app notification, it doesn't
                // activate the window correctly
                WindowHelper.ShowWindow(m_window);
            }
            else
            {
                WindowHelper.ShowWindow(m_window);
            }
        }

        private static class WindowHelper
        {
            [DllImport("user32.dll")]
            private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool SetForegroundWindow(IntPtr hWnd);

            public static void ShowWindow(Window window)
            {
                // Bring the window to the foreground... first get the window handle...
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

                // Restore window if minimized... requires DLL import above
                ShowWindow(hwnd, 0x00000009);

                // And call SetForegroundWindow... requires DLL import above
                SetForegroundWindow(hwnd);
            }
        }
    }
}