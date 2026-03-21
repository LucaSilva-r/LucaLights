using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogHostAvalonia;
using LTEK_ULed.Code.OsuPlayer;

namespace LTEK_ULed.Controls;

public partial class ColumnMappingDialog : UserControl
{
    public ColumnMappingDialog(int keyCount, List<ColumnMapping>? existingMappings)
    {
        DataContext = new ColumnMappingDialogData(keyCount, existingMappings);
        InitializeComponent();
    }

    private void Cancel(object? sender, RoutedEventArgs e)
    {
        DialogHost.Close("OsuDialog", null);
    }

    private void Confirm(object? sender, RoutedEventArgs e)
    {
        var data = (ColumnMappingDialogData)DataContext!;
        DialogHost.Close("OsuDialog", data.Mappings);
    }
}
