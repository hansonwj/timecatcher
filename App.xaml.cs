﻿using Microsoft.UI.Xaml;
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

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate(); //// Create process & dispatcher

            AppNotificationManager notificationManager = AppNotificationManager.Default;
            notificationManager.NotificationInvoked += NotificationManager_NotificationInvoked;
            notificationManager.Register();
            // Create a 30 min timer 
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string _configJson = File.ReadAllText(System.IO.Path.Combine(docPath, "timecatcher", "config.json"));
            var _schedule = JsonConvert.DeserializeObject<ConfigData>(_configJson).schedule;
            var _timerEnabled = JsonConvert.DeserializeObject<ConfigData>(_configJson).timerEnabled;

            var timer = new System.Timers.Timer(_schedule*1000*60);

            // Hook up the Elapsed event for the timer.
            timer.Elapsed += (sender, e) => OnTimedEvent(sender, e, notificationManager); ;

            timer.Enabled = _timerEnabled;
            

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

        private Window m_window;



        private async static void OnTimedEvent(object sender, EventArgs e, AppNotificationManager notificationManager)
        {
            await notificationManager.RemoveAllAsync();
            CreateTimeEntryNotification();
        }

        private void  NotificationManager_NotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
        {
            HandleNotification(args);
        }

        public class ConfigData
        {
            public List<ClientConfig> clients { get; set; }
            public int schedule { get; set; }
            public bool timerEnabled { get; set; }
        }

        public class ClientConfig
        {
            public string name { get; set; }
            public List<ClientProject> projects { get; set; }
        }

        public class ClientProject
        {
            public string name { get; set; }
        }

        private static async void CreateTimeEntryNotification()
        {
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string _configJson = File.ReadAllText(System.IO.Path.Combine(docPath, "timecatcher", "config.json"));
            var _config = JsonConvert.DeserializeObject<ConfigData>(_configJson);

            var appNotificationBuilder = new AppNotificationBuilder();
            appNotificationBuilder.MuteAudio();

            //.AddArgument("action", "ToastClick") // Disabled action on popup click
            appNotificationBuilder.AddText("What have you been working on?");
            var clientcombo = new AppNotificationComboBox("Client");
            // Read in 
            foreach (ClientConfig client in _config.clients)
            {
                clientcombo.AddItem(client.name, client.name);
            }
            appNotificationBuilder.AddComboBox(clientcombo);

            appNotificationBuilder.AddTextBox("Notes", "Notes", "");
            appNotificationBuilder.AddButton(new AppNotificationButton("Submit")
                .AddArgument("action", "submit"));
            appNotificationBuilder.AddButton(new AppNotificationButton("Manager")
                .AddArgument("action", "manager"));
            appNotificationBuilder.SetDuration(AppNotificationDuration.Long);


            var appNotification = appNotificationBuilder.BuildNotification();


            AppNotificationManager.Default.Show(appNotification);
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

        private void HandleNotification(AppNotificationActivatedEventArgs args)
        {
            
            // Use the dispatcher from the window if present, otherwise the app dispatcher
            var dispatcherQueue = m_window?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            dispatcherQueue.TryEnqueue(() =>
            {
                if (args.Arguments["action"] == "submit") {
                    Debug.WriteLine("Handling Time Entry");
                    var entry = new TimeEntry();
                    entry.Client = args.UserInput["Client"];
                    entry.Project = "";
                    entry.Notes = args.UserInput["Notes"];
                    entry.Datetime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                    ProcessNewTimeEntry(entry);
                }
                else {
                    Debug.WriteLine("Opening Manager");
                    LaunchAndBringToForegroundIfNeeded();
                }
            });
        }

        public struct TimeEntry
        {
            public string Client;
            public string Project;
            public string Notes;
            public string Datetime;
        };

        public static void ProcessNewTimeEntry(TimeEntry timeentry)
        {
            // Set a variable to the Documents path.
            string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Append text to an existing file named "WriteLines.txt".
            using (StreamWriter outputFile = new StreamWriter(System.IO.Path.Combine(docPath, "timecatcher", "timeentries.csv"), true))
            {
                outputFile.WriteLine($"{timeentry.Client},{timeentry.Project},{timeentry.Notes},{timeentry.Datetime}");
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