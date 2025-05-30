using Avalonia.Controls.Shapes;
using Avalonia.Media;
using LTEK_ULed.Code;
using LTEK_ULed.Views;
using System;
using System.Collections.Generic;

namespace LTEK_ULed.ViewModels;

public partial class MainViewModel : ViewModelBase
{

    public static MainViewModel? Instance { get; private set; }

    public bool debug { get; set; }

    public MainViewModel() {

        Instance = this;

        Settings.Load();

        PipeManager.Start();
        LightingManager.Start();

    }

}
