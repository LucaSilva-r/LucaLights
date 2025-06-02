using Avalonia.Controls.Shapes;
using Avalonia.Media;
using LTEK_ULed.Code;
using LTEK_ULed.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LTEK_ULed.ViewModels;

public partial class MainViewModel : ViewModelBase
{

    public static MainViewModel? Instance { get; private set; }

    public bool debug { get; set; }
    public bool lightOutput { get; set; } = true;

    public ObservableCollection<Device> devices => Settings.Instance!.devices;

    public MainViewModel() {

        Instance = this;

        Settings.Load();
        Settings.Save();
        PipeManager.Start();
        LightingManager.Start();

    }

}
