using System;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using voltrex_light.Helpers;
using voltrex_light.Models;
using voltrex_light.Services;

namespace voltrex_light.ViewModels;

/// <summary>
/// ViewModel for Settings view managing appearance, visualization defaults, unit formats, scan history, and system info.
/// </summary>
public class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IScanHistoryService _scanHistoryService;

    private string _selectedTheme;
    private string _selectedVisualization;
    private string _selectedSizeUnit;

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                _ = _settingsService.SetThemeAsync(value);
            }
        }
    }

    public string SelectedVisualization
    {
        get => _selectedVisualization;
        set
        {
            if (SetProperty(ref _selectedVisualization, value))
            {
                _settingsService.Settings.DefaultVisualization = value;
                _ = _settingsService.SaveSettingsAsync();
            }
        }
    }

    public string SelectedSizeUnit
    {
        get => _selectedSizeUnit;
        set
        {
            if (SetProperty(ref _selectedSizeUnit, value))
            {
                _settingsService.Settings.SizeDisplayUnit = value;
                FileSystemFormatter.ActiveUnitFormat = value.StartsWith("Binary", StringComparison.OrdinalIgnoreCase)
                    ? SizeUnitFormat.Binary
                    : SizeUnitFormat.Standard;
                _ = _settingsService.SaveSettingsAsync();
            }
        }
    }

    public string[] AvailableThemes { get; } = { "Default", "Light", "Dark" };
    public string[] AvailableVisualizations { get; } = { "Sunburst", "Treemap" };
    public string[] AvailableSizeUnits { get; } = { "Standard (KB, MB, GB)", "Binary (KiB, MiB, GiB)" };

    public ObservableCollection<ScanHistoryItem> HistoryItems { get; } = new();
    public bool HasHistory => HistoryItems.Count > 0;

    public RelayCommand ClearHistoryCommand { get; }

    public string AppVersion => "1.0.0";
    public string DeveloperName => "mohomed Ziard | Voltrex Digital";
    public string OsVersion => RuntimeInformation.OSDescription;
    public string Architecture => RuntimeInformation.ProcessArchitecture.ToString();
    public string FrameworkVersion => RuntimeInformation.FrameworkDescription;
    public string MinimumOsRequirement => "Windows 10, version 1809 (Build 17763) or Windows 11 (Recommended: Windows 11 22H2+)";

    public SettingsViewModel(ISettingsService settingsService, IScanHistoryService scanHistoryService)
    {
        _settingsService = settingsService;
        _scanHistoryService = scanHistoryService;

        _selectedTheme = _settingsService.Settings.AppTheme;
        _selectedVisualization = _settingsService.Settings.DefaultVisualization;
        _selectedSizeUnit = _settingsService.Settings.SizeDisplayUnit.Equals("Binary", StringComparison.OrdinalIgnoreCase)
            ? "Binary (KiB, MiB, GiB)"
            : "Standard (KB, MB, GB)";

        ClearHistoryCommand = new RelayCommand(async () => await ExecuteClearHistoryAsync());

        RefreshHistory();
    }

    public void RefreshHistory()
    {
        HistoryItems.Clear();
        foreach (var item in _scanHistoryService.History)
        {
            HistoryItems.Add(item);
        }
        OnPropertyChanged(nameof(HasHistory));
    }

    private async Task ExecuteClearHistoryAsync()
    {
        await _scanHistoryService.ClearHistoryAsync();
        RefreshHistory();
    }
}
