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

    public MainViewModel() {

        Instance = this;

        Settings.Load();
        Settings.Save();
        PipeManager.Start();
        LightingManager.Start();

    }

}
