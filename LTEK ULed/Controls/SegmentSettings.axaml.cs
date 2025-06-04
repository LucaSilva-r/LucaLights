using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Core;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using LTEK_ULed.Code;
using LTEK_ULed.Converters;
using System;

namespace LTEK_ULed.Controls;

public partial class SegmentSettings : UserControl
{
    private Segment? segment;
    public SegmentSettings()
    {
        InitializeComponent();

        DataContextChanged += (e, s) =>
        {
            segment = DataContext as Segment;
            if (segment != null)
            {
                //var converter = new FlagsEnumValueConverter();
                //CabinetMapping.Items.Clear();
                //ButtonMapping.Items.Clear();
                //foreach (CabinetLight light in Enum.GetValues(typeof(CabinetLight)))
                //{
                //    if (light != CabinetLight.NONE)
                //    {
                //        Binding binding = new Binding()
                //        {
                //            Path = "cabinetMapping",
                //            Converter = converter,
                //            ConverterParameter = light,
                //            Mode = BindingMode.OneWayToSource,
                //        };
                        
                //        CheckBox checkBox = new CheckBox() { Content = light.ToString() };
                //        checkBox.Bind(CheckBox.IsCheckedProperty, binding);

                //        CabinetMapping.Items.Add(checkBox);

                //        if (segment.cabinetMapping.HasFlag(light))
                //            checkBox.IsChecked = true;
                //    }
                //}


                //foreach (GameButton button in Enum.GetValues(typeof(GameButton)))
                //{
                //    if (button != GameButton.NONE)
                //    {
                //        Binding binding = new Binding()
                //        {
                //            Path = "buttonMapping",
                //            Converter = converter,
                //            ConverterParameter = button,
                //        };

                //        CheckBox checkBox = new CheckBox() { Content = button.ToString() };
                //        //checkBox.Bind(CheckBox.IsCheckedProperty, binding);
                //        ButtonMapping.Items.Add(checkBox);
                //    }
                //}
            }
        };

    }
    
    public static readonly StyledProperty<string> SegmentNameProperty =
        AvaloniaProperty.Register<SegmentView, string>(nameof(SegmentName), defaultValue: "Segment Name", defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string SegmentName
    {
        get => GetValue(SegmentNameProperty);
        set => SetValue(SegmentNameProperty, value);
    }

    public static readonly StyledProperty<int> SegmentLengthProperty =
        AvaloniaProperty.Register<SegmentView, int>(nameof(SegmentLength), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public int SegmentLength
    {
        get => GetValue(SegmentLengthProperty);
        set => SetValue(SegmentLengthProperty, value);
    }

    private void ContextMenu_Closed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    }
}