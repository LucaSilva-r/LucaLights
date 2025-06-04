using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using LTEK_ULed.Code;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace LTEK_ULed.Controls;

public partial class EnumButton : UserControl
{
    public enum ButtonLabelStyles { Indexes, Values, Names, FixedText }


    public EnumButton() : base()
    {
        InitializeComponent();
        menu.ItemsSource = menuItems;
        buttonLabel.Text = ButtonLabel;
    }


    /// <summary>Called when the context menu closes. Update the source bound to EnumValue.
    /// </summary>
    private void Menu_Closed(object? sender, RoutedEventArgs e)
    {
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        // IsOpen will always be false here, but IsVisible will give us the menu state at the time the button was clicked.
        if (!menu.IsVisible) menu.Open();
        Debug.WriteLine("Open Menu");
    }


    public static readonly StyledProperty<int> EnumValueProperty =
        AvaloniaProperty.Register<EnumButton, int>(nameof(EnumValue), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public int EnumValue
    {
        get => GetValue(EnumValueProperty);
        set
        {
            if (menuItems.Count == 0)
                return;
            SetValue(EnumValueProperty, value);
            foreach (var item in menuItems) //This is redundant when item.Click triggered it and no overlapping values exist.
                item.IsChecked = (value & (int)item.Tag) == (int)item.Tag;
            //buttonLabel.Text = ButtonLabel;
        }
    }


    /// <summary>Sets the [Flags]enum type to be presented in the dropdown menu.
    /// </summary>
    public Type ChoicesSource
    {
        set
        {
            if (!value.IsEnum ||
                value.GetCustomAttributes(typeof(FlagsAttribute), false).Length == 0)
                throw new ArgumentException($"Type '{value.Name}' is not an enum with the [Flags] attribute.", nameof(ChoicesSource));
            if (menuItems.Count == 0)
            {
                // Create a MenuItem for each defined value of the enum
                foreach (var val in Enum.GetValues(value))
                {
                    var menuitem = new MenuItem()
                    {
                        Header = ((Enum)val).GetDescription(),
                        IsEnabled = true,
                        StaysOpenOnClick = true,
                        Tag = Enum.ToObject(value, val)
                    };
                    menuitem.Click += Menuitem_Click;
                    menuItems.Add(menuitem);
                }
                // In an expandable grid row, EnumValue gets set before ChoicesSource is set.
                // Repeat the assignment now to get the new menuItems appropriately checked and the button label updated.
                if (EnumValue != 0)
                    EnumValue = (int)GetValue(EnumValueProperty); //This is intentionally a redundant assignment.
            }
            else
                throw new InvalidOperationException("Cannot redefine ChoicesSource.");
        }
    }

    private void Menuitem_Click(object? sender, RoutedEventArgs e)
    {
        //Check?.Invoke(sender, e); //Call any added Check handler.
        // Update EnumValue
        var item = sender as MenuItem;
        SetValue(EnumValueProperty, item!.IsChecked ? EnumValue | (int)item!.Tag! : EnumValue & ~(int)item.Tag!);
        // Update label
        //buttonLabel.Text = ButtonLabel;
        e.Handled = true;
    }

    //public string ButtonLabel
    //{
    //    get
    //    {
    //        var checkedItems = menuItems.Where(x => x.IsChecked);
    //        if (checkedItems.Count() == 0)
    //            return EmptySelectionLabel;
    //        if (ButtonLabelStyle == ButtonLabelStyles.FixedText)
    //            return fixedButtonLabel;
    //        {
    //            IEnumerable<string> terms;
    //            var delim = ", ";
    //            switch (ButtonLabelStyle)
    //            {
    //                case ButtonLabelStyles.Values:
    //                    terms = checkedItems.Select(y => ((int)y.Tag).ToString());
    //                    break;
    //                case ButtonLabelStyles.Names:
    //                    terms = checkedItems.Select(y => ((Enum)y.Tag).GetDescription());
    //                    delim = Environment.NewLine;
    //                    break;
    //                default: //"Indexes" is the default
    //                    terms = checkedItems.Select(y => (menuItems.IndexOf(y) + 1).ToString());
    //                    break;
    //            }
    //            return string.Join(delim, terms);
    //        }
    //    }
    //    set
    //    {
    //        fixedButtonLabel = value;
    //    }
    //}


    public static readonly StyledProperty<ButtonLabelStyles> ButtonLabelStyleProperty =
        AvaloniaProperty.Register<EnumButton, ButtonLabelStyles>(nameof(ButtonLabelStyle), defaultValue: ButtonLabelStyles.Indexes, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public ButtonLabelStyles ButtonLabelStyle
    {
        get { return (ButtonLabelStyles)GetValue(ButtonLabelStyleProperty); }
        set
        {
            SetValue(ButtonLabelStyleProperty, value);
            //buttonLabel.Text = ButtonLabel;
        }
    }

    public static readonly StyledProperty<string> EmptySelectionLabelProperty =
        AvaloniaProperty.Register<EnumButton, string>(nameof(EmptySelectionLabel), defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public string EmptySelectionLabel
    {
        get { return (string)GetValue(EmptySelectionLabelProperty); }
        set { SetValue(EmptySelectionLabelProperty, value); }
    }


    public static readonly StyledProperty<string> ButtonLabelProperty =
    AvaloniaProperty.Register<EnumButton, string>(nameof(ButtonLabel), defaultValue: "Button", defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public string ButtonLabel
    {
        get { return (string)GetValue(ButtonLabelProperty); }
        set
        {
            buttonLabel.Text = value;
            SetValue(ButtonLabelProperty, value);
        }
    }

    private readonly ObservableCollection<MenuItem> menuItems = new ObservableCollection<MenuItem>();


}