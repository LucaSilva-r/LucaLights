using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LTEK_ULed.Code;
using System;
using System.IO.Ports;

namespace LTEK_ULed.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        serialComboBox.ItemsSource = SerialPort.GetPortNames();
        serialComboBox.SelectedIndex = 0;

        PipeManager.Start();
        LightingManager.Start();

    }

    private void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }

    private void ConnectSerial(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!DDPStreamer.connected)
        {
            if (serialComboBox.SelectedItem != null && serialComboBox.SelectedItem.GetType() == typeof(string))
            {
                DDPStreamer.Connect("192.168.1.84", 60);
                serialConnect.Content = "Disconnect";
                serialConnect.Classes.Add("Danger");

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

    private void RefreshSerial(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        serialComboBox.ItemsSource = SerialPort.GetPortNames();
        serialComboBox.SelectedIndex = 0;
    }

    private async void SettingsClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {

        Window settings = new PadSettings();
        await settings.ShowDialog(this);
        
    }
}
