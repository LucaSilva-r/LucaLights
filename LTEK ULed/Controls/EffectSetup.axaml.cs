using Avalonia.Controls;
using Avalonia.LogicalTree;
using DialogHostAvalonia;
using LTEK_ULed.Code;
using LTEK_ULed.Code.Utils;
using System;
using System.Linq;

namespace LTEK_ULed.Controls;

public partial class EffectSetup : UserControl
{

    //Device? device = new Device("New Device", "192.168.1.1", new());
    public EffectSetup()
    {

        int randomNumber = Random.Shared.Next();
        if (Settings.Instance != null)
        {
            while (Settings.Instance.Effects.FirstOrDefault(n => n!.GroupId == randomNumber,null) != null)
            {
                randomNumber = Random.Shared.Next();
            }
        }

        DataContext = new Effect("New Effect", 0, 0, randomNumber);
        
        InitializeComponent();
    }

    public EffectSetup(Effect effect)
    {
        DataContext = effect;

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