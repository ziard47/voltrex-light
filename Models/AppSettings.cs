using System;
using voltrex_light.Helpers;

namespace voltrex_light.Models;

/// <summary>
/// User settings configuration for DiskScope.
/// </summary>
public class AppSettings : ObservableObject
{
    private string _appTheme = "Default"; // "Default", "Light", "Dark"
    private string _defaultVisualization = "Sunburst"; // "Sunburst", "Treemap"
    private string _sizeDisplayUnit = "Standard"; // "Standard" (KB, MB, GB), "Binary" (KiB, MiB, GiB)
    private bool _showSystemFiles = false;
    private int _largestFilesCount = 100;
    private string _lastScannedPath = string.Empty;

    public string AppTheme
    {
        get => _appTheme;
        set => SetProperty(ref _appTheme, value);
    }

    public string DefaultVisualization
    {
        get => _defaultVisualization;
        set => SetProperty(ref _defaultVisualization, value);
    }

    public string SizeDisplayUnit
    {
        get => _sizeDisplayUnit;
        set => SetProperty(ref _sizeDisplayUnit, value);
    }

    public bool ShowSystemFiles
    {
        get => _showSystemFiles;
        set => SetProperty(ref _showSystemFiles, value);
    }

    public int LargestFilesCount
    {
        get => _largestFilesCount;
        set => SetProperty(ref _largestFilesCount, value);
    }

    public string LastScannedPath
    {
        get => _lastScannedPath;
        set => SetProperty(ref _lastScannedPath, value);
    }
}

