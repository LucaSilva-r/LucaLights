using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using LTEK_ULed.Code;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LTEK_ULed;

public partial class DeviceSetup : Window
{

    //Device? device = new Device("New Device", "192.168.1.1", new());

    public DeviceSetup(Device device)
    {
        DataContext = device;
        InitializeComponent();

    }

    private void TextBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        (sender as TextBox)!.Text = (sender as TextBox)!.Text.Truncate(15,"");
    }

    private void Cancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Close(null);

    }

    private void Save(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Close(DataContext as Device);
    }
}