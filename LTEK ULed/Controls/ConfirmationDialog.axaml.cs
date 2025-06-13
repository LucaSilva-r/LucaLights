using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LTEK_ULed.Controls;
using System.Collections.ObjectModel;

namespace LTEK_ULed;

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

    private readonly ObservableCollection<MenuItem> menuItems = new ObservableCollection<MenuItem>();



}