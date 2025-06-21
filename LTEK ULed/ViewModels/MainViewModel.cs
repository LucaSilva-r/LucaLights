using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using LTEK_ULed.Code;
using LTEK_ULed.Controls;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

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


