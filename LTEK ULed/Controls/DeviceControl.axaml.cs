using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace LTEK_ULed;

public partial class DeviceControl : UserControl
{
    public DeviceControl()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<string> DeviceNameProperty =
        AvaloniaProperty.Register<DeviceControl, string>(nameof(DeviceName), defaultValue: "Name");

    public string DeviceName
    {
        get => GetValue(DeviceNameProperty);
        set => SetValue(DeviceNameProperty, value);
    }

    public static readonly StyledProperty<string> IpAddressProperty =
        AvaloniaProperty.Register<DeviceControl, string>(nameof(IpAddress), defaultValue: "Ip");

    public string IpAddress
    {
        get => GetValue(IpAddressProperty);
        set => SetValue(IpAddressProperty, value);
    }

    public static readonly StyledProperty<int> NumberOfSegmentsProperty =
        AvaloniaProperty.Register<DeviceControl, int>(nameof(NumberOfSegments), defaultValue: 0);

    public int NumberOfSegments
    {
        get => GetValue(NumberOfSegmentsProperty);
        set => SetValue(NumberOfSegmentsProperty, value);
    }

    public static readonly StyledProperty<int> NumberOfLedsProperty =
        AvaloniaProperty.Register<DeviceControl, int>(nameof(NumberOfLeds), defaultValue: 0);

    public int NumberOfLeds
    {
        get => GetValue(NumberOfLedsProperty);
        set => SetValue(NumberOfLedsProperty, value);
    }
}