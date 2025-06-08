using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls.Shapes;
using LTEK_ULed.Code;

namespace LTEK_ULed.Controls;

public partial class SegmentView : UserControl
{

    private List<Rectangle> ledRects = new List<Rectangle>();

    Segment? segment;

    public void UpdateLeds()
    {

        if (segment!= null)
        {
            for (int i = 0; i < ledRects.Count && i < segment?.leds.Length; i++)
            {
                (ledRects[i].Fill as SolidColorBrush)!.Color = segment!.leds[i];
            }
        }

    }

    public SegmentView()
    {
        InitializeComponent();

        DataContextChanged += (e, s) =>
        {
            segment = DataContext as Segment;
            if (segment != null)
            {
                UpdateLength(segment!.length);
            }
        };
    }

    private void UpdateLength(int length)
    {

        if (LedContainer == null)
        {
            return;
        }
        if (length > ledRects.Count)
        {
            int difference = length - ledRects.Count;
            for (int i = 0; i < difference; i++)
            {
                Rectangle temp = new Rectangle() {Name = i.ToString(),  Margin = new Thickness(5, 5, 5, 5), Fill = new SolidColorBrush(Color.Parse("Red")), Height = 15, Width = 15 };

                Border border = new Border() { BorderThickness = new Thickness(1), };
                border.Bind(BorderBrushProperty, new DynamicResourceExtension("BorderCardBorderBrush"));
                border.Padding = new Thickness(0);
                border.Child = temp;

                LedContainer!.Children.Add(border);
                ledRects.Add(temp);

            }

        }
        else if (length < ledRects.Count)
        {
            int difference = ledRects.Count - length;

            for (int i = 0; i < difference; i++)
            {
                ledRects.RemoveAt(ledRects.Count - 1);
                LedContainer!.Children.RemoveAt(ledRects.Count);
            }
        }
    }

    public static readonly StyledProperty<string> SegmentNameProperty =
        AvaloniaProperty.Register<SegmentView, string>(nameof(SegmentName), defaultValue: "Segment Name", defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public string SegmentName
    {
        get => GetValue(SegmentNameProperty);
        set => SetValue(SegmentNameProperty, value);
    }

    public static readonly StyledProperty<int> SegmentLengthProperty =
        AvaloniaProperty.Register<SegmentView, int>(nameof(SegmentLength), defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public int SegmentLength
    {
        get
        {
            var length = GetValue(SegmentLengthProperty);
            UpdateLength(length);
            return length;
        }

        set
        {
            SetValue(SegmentLengthProperty, value);
        }
    }

    public static readonly StyledProperty<Color[]> LedsProperty =
    AvaloniaProperty.Register<SegmentView, Color[]>(nameof(Leds), defaultValue: [], defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

    public Color[] Leds
    {
        get => GetValue(LedsProperty);
        set => SetValue(LedsProperty, value);
    }
}