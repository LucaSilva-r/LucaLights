using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using LTEK_ULed.Code;
using System.Collections.Generic;
using System.Diagnostics;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;

namespace LTEK_ULed;

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
        Debug.WriteLine("Found Container");

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
                Rectangle temp = new Rectangle() { Margin = new Thickness(5, 5, 5, 5), Fill = new SolidColorBrush(Color.Parse("Black")), Height = 15, Width = 15 };
                LedContainer!.Children.Add(temp);
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

        Debug.WriteLine("Updating Length " + length);
        
    }

    public static readonly StyledProperty<string> SegmentNameProperty =
        AvaloniaProperty.Register<DeviceControl, string>(nameof(SegmentName), defaultValue: "Segment Name");

    public string SegmentName
    {
        get => GetValue(SegmentNameProperty);
        set => SetValue(SegmentNameProperty, value);
    }

    public static readonly StyledProperty<int> SegmentLengthProperty =
        AvaloniaProperty.Register<DeviceControl, int>(nameof(SegmentLength), defaultValue: 0);

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
    AvaloniaProperty.Register<DeviceControl, Color[]>(nameof(Leds),defaultValue: []);

    public Color[] Leds
    {
        get => GetValue(LedsProperty);
        set => SetValue(LedsProperty, value);
    }
}