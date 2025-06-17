using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using LTEK_ULed.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Diagnostics;
using System.Windows.Input;

namespace LTEK_ULed.Controls;

public partial class ConfirmationDialog : UserControl
{
    public ConfirmationDialog()
    {
        InitializeComponent();

    }

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<EnumButton, string>(nameof(Description), defaultValue: "Are you sure", defaultBindingMode: Avalonia.Data.BindingMode.OneTime);

    public string Description
    {
        get { return (string)GetValue(DescriptionProperty); }
        set
        {
            SetValue(DescriptionProperty, value);
        }
    }

    private void Cancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string name = this.GetLogicalParent()!.GetLogicalParent()!.GetLogicalParent<DialogHost>()!.Identifier!;
        DialogHost.Close(name, false);
    }

    private void Confirm(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {

        string name = this.GetLogicalParent()!.GetLogicalParent()!.GetLogicalParent<DialogHost>()!.Identifier!;
        DialogHost.Close(name, true);

    }
}