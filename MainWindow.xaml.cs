using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.AppNotifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using MUXC = Microsoft.UI.Xaml.Controls;
using System.Globalization;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace timecatcher
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
                string docPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string inputFilePath = System.IO.Path.Combine(docPath, "timecatcher", "timeentries.csv");
                string outputFilePath = System.IO.Path.Combine(docPath, "timecatcher", "summary.csv");

                Dictionary<string, Dictionary<string, Dictionary<DateTime, TimeSpan>>> clientProjectData = new Dictionary<string, Dictionary<string, Dictionary<DateTime, TimeSpan>>>();

                // Read input CSV file
                using (var reader = new StreamReader(inputFilePath))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var values = line.Split(',');


                        string client = values[0];
                        string project = values[1];
                        string notes = values[2];
                        DateTime fromDate = DateTime.Parse(values[3], new CultureInfo("fr-FR")).AddMinutes(-15);
                        DateTime toDate = DateTime.Parse(values[3], new CultureInfo("fr-FR"));

                    TimeSpan timeSpent = toDate - fromDate;

                        // Add data to the dictionary
                        if (!clientProjectData.ContainsKey(client))
                            clientProjectData.Add(client, new Dictionary<string, Dictionary<DateTime, TimeSpan>>());

                        if (!clientProjectData[client].ContainsKey(project))
                            clientProjectData[client].Add(project, new Dictionary<DateTime, TimeSpan>());

                        if (!clientProjectData[client][project].ContainsKey(fromDate.Date))
                            clientProjectData[client][project].Add(fromDate.Date, TimeSpan.Zero);

                        clientProjectData[client][project][fromDate.Date] += timeSpent;
                    }
                }

                // Write output CSV file
                using (var writer = new StreamWriter(outputFilePath))
                {
                    writer.WriteLine("client,project,date,time spent");

                    foreach (var client in clientProjectData)
                    {
                        foreach (var project in client.Value)
                        {
                            foreach (var date in project.Value)
                            {
                                string dateString = date.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                                string timeSpentString = date.Value.TotalHours.ToString();

                                writer.WriteLine($"{client.Key},{project.Key},{dateString},{timeSpentString}");
                            }
                        }
                    }
                }

                Console.WriteLine("Output file generated successfully.");
        }

        // 
        private void NavigationViewControl_ItemInvoked_1(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                if (SettingPanel.Visibility == Visibility.Collapsed)
                {
                    SettingPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    SettingPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MUXC.NavigationViewItem newSubItem = new MUXC.NavigationViewItem();
            newSubItem.Content = "newSubItem";

            foreach (var item in NavigationViewControl.MenuItems)
            {
                var menuItem = item as MUXC.NavigationViewItem;
                var selectItem = NavigationViewControl.SelectedItem as MUXC.NavigationViewItem;
                //find selectedItem
                if (menuItem != null &&
                    selectItem != null &&
                    menuItem.Content.ToString() == selectItem.Content.ToString())
                {
                    menuItem.MenuItems.Add(newSubItem);
                }
            }

        }



    }
}
