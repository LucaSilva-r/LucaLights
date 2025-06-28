using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
using Velopack.Sources;

namespace LTEK_ULed.Controls;

public partial class UpdateDialog : UserControl
{

    private CancellationTokenSource tokenSource = new CancellationTokenSource();


    public UpdateDialog()
    {
        InitializeComponent();

    }

    public static readonly StyledProperty<UpdateInfo> UpdateInfoProperty =
    AvaloniaProperty.Register<UpdateDialog, UpdateInfo>(nameof(Update), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public UpdateInfo Update
    {
        get { return GetValue(UpdateInfoProperty); }
        set
        {
            SetValue(UpdateInfoProperty, value);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Description = "A new version of LucaLights is available\n\n V" +
            Update.TargetFullRelease.Version.Major + "." +
            Update.TargetFullRelease.Version.Minor + "." +
            Update.TargetFullRelease.Version.Patch +
            "\n\nDo you want to update?";
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

    public bool Updating
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
        if (sender is Button button)
        {
            button.IsEnabled = false;
        }

        Updating = true;

        var mgr = new UpdateManager(new GithubSource("https://github.com/LucaSilva-r/LucaLights", null, false, null));

        await mgr.DownloadUpdatesAsync(Update, (e) => Dispatcher.UIThread.Invoke(() => Progress = e), tokenSource.Token);

        await Task.Delay(1000);

        Description = "Applying updates and restarting...";

        await Task.Delay(2000);

        mgr.ApplyUpdatesAndRestart(Update);
    }
}