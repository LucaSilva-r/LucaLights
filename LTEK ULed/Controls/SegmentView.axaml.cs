using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls.Shapes;

namespace LTEK_ULed.Controls;

public partial class SegmentView : UserControl
{

    private List<Rectangle> ledRects = new List<Rectangle>();

    public void UpdateLeds()
    {

        for (int i = 0; i < ledRects.Count && i < Leds.Length; i++)
        {
            (ledRects[i].Fill as SolidColorBrush)!.Color = Leds[i];
        }
    }

    public SegmentView()
    {
        InitializeComponent();

        UpdateLength(this.SegmentLength);

    }

    private void UpdateLength(int length)
    {

        if (LedContainer == null)
        {
            return;
        }
        if (length > ledRects.Count)
        {
            for (int i = 0; i < length - ledRects.Count; i++)
            {
                Rectangle temp = new Rectangle() { Margin = new Thickness(5, 5, 5, 5), Fill = new SolidColorBrush(Color.Parse("Red")), Height = 15, Width = 15 };

                Border border = new Border() { BorderBrush = (IBrush) new DynamicResourceExtension("PrimaryForegroundColor"), BorderThickness = new Thickness(1), };
                border.Child = temp;

                LedContainer!.Children.Add(border);
                ledRects.Add(temp);
            }
        } else if(length < ledRects.Count)
        {
            for (int i = 0; i < ledRects.Count - length; i++)
            {
                ledRects.RemoveAt(ledRects.Count - 1);
                LedContainer!.Children.RemoveAt(ledRects.Count);
            }
        }

        Debug.WriteLine("Updating Length " + length + Leds.Length);
        
    }

    public static readonly StyledProperty<string> SegmentNameProperty =
        AvaloniaProperty.Register<SegmentView, string>(nameof(SegmentName), defaultValue: "Segment Name");

    public string SegmentName
    {
        get => GetValue(SegmentNameProperty);
        set => SetValue(SegmentNameProperty, value);
    }

    public static readonly StyledProperty<int> SegmentLengthProperty =
        AvaloniaProperty.Register<SegmentView, int>(nameof(SegmentLength), defaultValue: 0);

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
    AvaloniaProperty.Register<SegmentView, Color[]>(nameof(Leds),defaultValue: []);

    public Color[] Leds
    {
        get => GetValue(LedsProperty);
        set => SetValue(LedsProperty, value);
    }
}