using LTEK_ULed.Code;
using System.Collections.ObjectModel;

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

}
