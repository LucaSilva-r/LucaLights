using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LTEK_ULed.Code;
using System;
using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using Avalonia.Media;

namespace LTEK_ULed.Views;

public partial class MainWindow : Window
{
    FileInfo file;
    Settings settings = new Settings();
    GameState gameState = GameState.gameState;

    public MainWindow()
    {
        InitializeComponent();

        PipeManager.Start();
        LightingManager.Start();

        file = new FileInfo(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/LtekULED/settings.json");
        Debug.WriteLine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/LtekULED/settings.json");
        if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/LtekULED/settings.json"))
        {
            Settings? temp = JsonSerializer.Deserialize<Settings>(File.ReadAllText(file.FullName));
            if(temp != null)
            {
                settings = temp;
            }
            ipAddressTextBox.Text = settings.ip;        }
        else
        {
            file.Directory?.Create();
        }

    }

    private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void ConnectSerial(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!DDPStreamer.connected)
        {

            if (ipAddressTextBox.Text != null && Regex.IsMatch(ipAddressTextBox.Text, "^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$"))
            {
                DDPStreamer.Connect(ipAddressTextBox.Text, 60);
                serialConnect.Content = "Disconnect";
                serialConnect.Classes.Add("Danger");


                settings.ip = ipAddressTextBox.Text;
                File.WriteAllText(file.FullName, JsonSerializer.Serialize(settings));

            }
        }
        else
        {
            DDPStreamer.Disconnect();

            serialConnect.Content = "Connect";
            serialConnect.Classes.Remove("Danger");
            serialConnect.IsEnabled = false;

            DispatcherTimer.RunOnce(() =>
            {
                serialConnect.IsEnabled = true;
            }, new TimeSpan(0, 0, 0, 2));
        }
    }

    private async void SettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {

        Window settings = new PadSettings();
        await settings.ShowDialog(this);

    }
}
