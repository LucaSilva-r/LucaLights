using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using LTEK_ULed.Code;
using LTEK_ULed.Controls;
using Microsoft.VisualBasic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace LTEK_ULed.ViewModels;

public partial class MainViewModel : ViewModelBase
{

    public static MainViewModel? Instance { get; private set; }

    public bool debug { get; set; }
    public bool lightOutput { get; set; } = true;

    public ObservableCollection<Device> devices => Settings.Instance!.Devices;
    public ObservableCollection<LightEffect> effects => Settings.Instance!.Effects;

    public Settings settings => Settings.Instance!;

    public MainViewModel()
    {

        Instance = this;
        Settings.Load();
        PipeManager.Start();
        LightingManager.Start();

        Task.Delay(3000).ContinueWith((t) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                CheckUpdate();
            });
        });
    }

    private async void CheckUpdate()
    {
        var mgr = new UpdateManager(new GithubSource("https://github.com/LucaSilva-r/LucaLights", null, false, null));

        if (!mgr.IsInstalled)
        {
            return;
        }
        // check for new version
        var newVersion = await mgr.CheckForUpdatesAsync();
        if (newVersion == null)
        {
            return; // no update available
        }

        UpdateDialog updateDialog = new UpdateDialog() { Update = newVersion };
        DialogClosingEventHandler handler = (s, e) =>
        {
            if (updateDialog.Updating)
            {
                e.Cancel(); // prevent dialog from closing while updating
            }
        };

        await DialogHost.Show(updateDialog, handler);

    }
    [RelayCommand]
    public async Task AddDevice()
    {
        object? newDevice = await DialogHost.Show(new DeviceSetup());

        lock (Settings.Lock)
        {
            if (newDevice != null)
            {
                Settings.Instance!.AddDevice((Device)newDevice);
                Settings.Save();
            }
        }
    }


    [RelayCommand]
    public async Task AddEffect()
    {
        object? newEffect = await DialogHost.Show(new EffectSetup());

        lock (Settings.Lock)
        {
            if (newEffect != null)
            {
                Settings.Instance!.AddEffect((LightEffect)newEffect);
                Settings.Save();
            }
        }

    }
}


