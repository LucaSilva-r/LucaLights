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
    public ObservableCollection<Effect> effects => Settings.Instance!.Effects;

    public Settings settings => Settings.Instance!;

    public MainViewModel() {

        Instance = this;

        Settings.Load();
        PipeManager.Start();
        LightingManager.Start();

    }

    [RelayCommand]
    public async Task AddDevice()
    {
        object? newDevice = await DialogHost.Show(new DeviceSetup(new Device("New Device", "192.168.1.1", new())));

        if (newDevice != null)
        {
            Settings.Instance!.AddDevice((Device)newDevice);
        }
    }
}


