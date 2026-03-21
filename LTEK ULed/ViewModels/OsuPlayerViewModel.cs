using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using LTEK_ULed.Code;
using LTEK_ULed.Code.OsuPlayer;
using LTEK_ULed.Controls;

namespace LTEK_ULed.ViewModels;

public partial class OsuPlayerViewModel : ViewModelBase
{
    private readonly OsuPlayerEngine _engine = new();

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private double _currentPositionMs;

    [ObservableProperty]
    private double _totalLengthMs;

    [ObservableProperty]
    private string _statusText = "Waiting for tosu...";

    [ObservableProperty]
    private string _nowPlayingText = string.Empty;

    [ObservableProperty]
    private int _mappingKeyCount = 4;

    public string CurrentTimeFormatted => TimeSpan.FromMilliseconds(CurrentPositionMs).ToString(@"mm\:ss");
    public string TotalTimeFormatted => TimeSpan.FromMilliseconds(TotalLengthMs).ToString(@"mm\:ss");

    public OsuPlayerViewModel()
    {
        _engine.ConnectionChanged += connected =>
        {
            IsConnected = connected;
        };

        _engine.StatusChanged += status =>
        {
            StatusText = status;
        };

        _engine.NowPlayingChanged += text =>
        {
            NowPlayingText = text;
        };

        _engine.PositionChanged += (current, total) =>
        {
            CurrentPositionMs = current;
            TotalLengthMs = total;
            OnPropertyChanged(nameof(CurrentTimeFormatted));
            OnPropertyChanged(nameof(TotalTimeFormatted));
        };

        _engine.Start();
    }

    [RelayCommand]
    public async Task OpenMappingsDialog()
    {
        var keyCount = MappingKeyCount;
        var allMappings = Settings.Instance?.OsuColumnMappings ?? new Dictionary<int, List<ColumnMapping>>();
        var existing = allMappings.TryGetValue(keyCount, out var list) ? list : null;

        var dialog = new ColumnMappingDialog(keyCount, existing);
        var result = await DialogHost.Show(dialog, "OsuDialog");

        if (result is System.Collections.ObjectModel.ObservableCollection<ColumnMapping> mappings)
        {
            if (Settings.Instance == null) return;
            Settings.Instance.OsuColumnMappings[keyCount] = mappings.ToList();
            Settings.Save();
            StatusText = $"Saved {keyCount}K mappings";
        }
    }
}
