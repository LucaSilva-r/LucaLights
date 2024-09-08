using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Diagnostics;

namespace LTEK_ULed;

public partial class PadSettings : Window
{
    public PadSettings()
    {
        InitializeComponent();
    }


    private void GameType_Opened(object sender, System.EventArgs e)
    {
        ComboBox? box = sender as ComboBox;

        Debug.WriteLine(box?.Name);
    }
}