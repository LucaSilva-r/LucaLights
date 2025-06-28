using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using LTEK_ULed.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Velopack;

namespace LTEK_ULed.Controls;

public partial class UpdateDialog : UserControl
{
    private UpdateManager _manager;
    private UpdateInfo _updateInfo;
    private CancellationTokenSource tokenSource = new CancellationTokenSource();

    public UpdateDialog()
    {
        InitializeComponent();
    }

    public UpdateDialog(UpdateManager manager, UpdateInfo updateInfo)
    {
        _manager = manager;
        _updateInfo = updateInfo;
        InitializeComponent();

        Description = "A new version of LucaLights is available\n\n" + updateInfo.TargetFullRelease.Version.Release + "\n\nDo you want to update?";
    }

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<UpdateDialog, string>(nameof(Description), defaultValue: "A new version of LucaLights is available:\n\n0.0.0.0\n\nDo you want to update?", defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private string Description
    {
        get { return (string)GetValue(DescriptionProperty); }
        set
        {
            SetValue(DescriptionProperty, value);
        }
    }

    public static readonly StyledProperty<int> ProgressProperty =
        AvaloniaProperty.Register<UpdateDialog, int>(nameof(Progress), defaultValue: 0, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private int Progress
    {
        get { return GetValue(ProgressProperty); }
        set
        {
            SetValue(ProgressProperty, value);
        }
    }

    public static readonly StyledProperty<bool> UpdatingProperty =
        AvaloniaProperty.Register<UpdateDialog, bool>(nameof(Updating), defaultValue: false, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private bool Updating
    {
        get { return GetValue(UpdatingProperty); }
        set
        {
            SetValue(UpdatingProperty, value);
        }
    }

    private void Cancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string name = this.GetLogicalParent()!.GetLogicalParent()!.GetLogicalParent<DialogHost>()!.Identifier!;
        tokenSource.Cancel();
        DialogHost.Close(name, false);
    }

    private async void Confirm(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if(sender is Button button)
        {
            button.IsEnabled = false;
        }
        Updating = true;

        await _manager.DownloadUpdatesAsync(_updateInfo, (e) => Progress = e, tokenSource.Token);

        await Task.Delay(1000);

        Description = "Applying updates and restarting...";

        await Task.Delay(2000);

        _manager.ApplyUpdatesAndRestart(_updateInfo);
    }
}