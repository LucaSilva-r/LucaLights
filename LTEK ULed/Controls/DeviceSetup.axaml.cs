using Avalonia.Controls;
using Avalonia.LogicalTree;
using DialogHostAvalonia;
using LTEK_ULed.Code;
using LTEK_ULed.Code.Utils;
using System;
using System.Collections.ObjectModel;

namespace LTEK_ULed.Controls;

public partial class DeviceSetup : UserControl
{

    public DeviceSetup()
    {
        string deviceName = "New Device";

        if (Settings.Instance != null)
        {
            deviceName = "New Device #" + Settings.Instance.Devices.Count;
        }
        DataContext = new Device(deviceName, "192.168.1.1", new ObservableCollection<Segment>());
        InitializeComponent();
        SetupProtocolComboBox();
    }

    public DeviceSetup(Device device)
    {
        DataContext = device;
        InitializeComponent();
        SetupProtocolComboBox();
    }

    private void SetupProtocolComboBox()
    {
        ProtocolComboBox.ItemsSource = Enum.GetValues(typeof(WledProtocol));
    }

    private void TextBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        (sender as TextBox)!.Text = (sender as TextBox)!.Text.Truncate(15,"");
    }

    private void Cancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string name = this.GetLogicalParent()!.GetLogicalParent()!.GetLogicalParent<DialogHost>()!.Identifier!;
        DialogHost.Close(name, null);
    }

    private void Confirm(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {

        string name = this.GetLogicalParent()!.GetLogicalParent()!.GetLogicalParent<DialogHost>()!.Identifier!;
        DialogHost.Close(name, DataContext);

    }
}