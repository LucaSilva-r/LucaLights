using Avalonia.Controls;
using Avalonia.LogicalTree;
using DialogHostAvalonia;
using LTEK_ULed.Code;
using LTEK_ULed.Code.Utils;
using System.Collections.ObjectModel;

namespace LTEK_ULed.Controls;

public partial class DeviceSetup : UserControl
{

    //Device? device = new Device("New Device", "192.168.1.1", new());
    public DeviceSetup()
    {
        DataContext = new Device("Test", "0.0.0.0", new ObservableCollection<Segment>());
        InitializeComponent();
    }

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
        string name = this.GetLogicalParent()!.GetLogicalParent()!.GetLogicalParent<DialogHost>()!.Identifier!;
        DialogHost.Close(name, null);
    }

    private void Confirm(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {

        string name = this.GetLogicalParent()!.GetLogicalParent()!.GetLogicalParent<DialogHost>()!.Identifier!;
        DialogHost.Close(name, DataContext);

    }
}