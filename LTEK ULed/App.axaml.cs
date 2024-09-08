using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using LTEK_ULed.Code;
using LTEK_ULed.ViewModels;
using LTEK_ULed.Views;

namespace LTEK_ULed;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);



        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
            desktop.MainWindow.Closed += (sender, e) =>
            {
                LightingManager.Stop();
                SerialManager.Disconnect();
                PipeManager.Stop();
                desktop.Shutdown();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

}
