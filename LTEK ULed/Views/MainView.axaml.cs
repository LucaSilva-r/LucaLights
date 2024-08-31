using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Diagnostics;
using LTEK_ULed.Code;
using System.Threading;
using Avalonia.Media;
using System.IO.Ports;
using System.Timers;
using Avalonia.Threading;
using System;

namespace LTEK_ULed.Views;

public partial class MainView : UserControl
{
    public MainView()
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
        if (!SerialManager.connected)
        {
            if (serialComboBox.SelectedItem != null && serialComboBox.SelectedItem.GetType() == typeof(string))
            {
                SerialManager.Connect((string)serialComboBox.SelectedItem);
                serialConnect.Content = "Disconnect";
                serialConnect.Classes.Add("Danger");

            }
        }
        else
        {
            SerialManager.Disconnect();
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
}
