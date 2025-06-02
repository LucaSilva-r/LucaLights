using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using LucideAvalonia;
using LucideAvalonia.Enum;
using ShadUI.Themes;

namespace ShadUI.Demo.Converters;

public static class ThemeModeConverters
{
    private static readonly Dictionary<ThemeMode, Lucide> Icons = new()
    {
        { ThemeMode.System, new Lucide { Icon = LucideIconNames.SunMoon, StrokeThickness = 1.5, Width = 18, Height = 18 } },
        { ThemeMode.Light, new Lucide { Icon = LucideIconNames.Sun, StrokeThickness = 1.5, Width = 18, Height = 18 } },
        { ThemeMode.Dark, new Lucide { Icon = LucideIconNames.Moon, StrokeThickness = 1.5, Width = 14, Height = 14 } }
    };

    public static readonly IValueConverter ToLucideIcon =
        new FuncValueConverter<ThemeMode, Lucide>(mode => Icons.TryGetValue(mode, out var icon) ? icon : Icons[0]);
}

public static class WindowStateConverters
{
    public static readonly IValueConverter IsFullScreen =
        new FuncValueConverter<WindowState, bool>(state =>
        {
            return state == WindowState.FullScreen;
        });
    
    public static readonly IValueConverter IsNotFullScreen =
        new FuncValueConverter<WindowState, bool>(state =>
        {
            return state != WindowState.FullScreen;
        });
}
