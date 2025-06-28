using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using LTEK_ULed.Code;
using LTEK_ULed.ViewModels;
using LTEK_ULed.Views;
using System;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;

namespace LTEK_ULed;

public partial class App : Application
{
    public delegate Task CheckUpdates();
    public static CheckUpdates? checkUpdates;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

        //DisableAvaloniaDataAnnotationValidation();

        desktop.MainWindow = new MainWindow()
        {
            DataContext = new MainViewModel()
        };
        desktop.MainWindow.Closed += (sender, e) =>
        {
            LightingManager.Stop();
            PipeManager.Stop();
            desktop.Shutdown();
        };

        GCSettings.LatencyMode = GCLatencyMode.Interactive;
        base.OnFrameworkInitializationCompleted();
        
    }
}
