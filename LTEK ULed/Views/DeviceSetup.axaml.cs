using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LTEK_ULed.Code;
using System.Diagnostics;

namespace LTEK_ULed;

public partial class DeviceSetup : Window
{
    public DeviceSetup()
    {
        InitializeComponent();
    }


    private void TextBox_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
    {
        (sender as TextBox)!.Text = (sender as TextBox)!.Text.Truncate(15,"");
    }
}