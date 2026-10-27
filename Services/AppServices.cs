using System;
using voltrex_light.ViewModels;

namespace voltrex_light.Services;

/// <summary>
/// Lightweight, zero-dependency IoC Service Provider for DiskScope.
/// Configures and resolves singletons and transient services.
/// </summary>
public static class AppServices
{
    private static IDriveService? _driveService;
    private static ISettingsService? _settingsService;
    private static IScanService? _scanService;
    private static IFolderPickerService? _folderPickerService;
    private static INavigationService? _navigationService;
    private static IExportService? _exportService;
    private static IScanHistoryService? _scanHistoryService;
    private static IUpdateService? _updateService;

    public static IDriveService DriveService => _driveService ??= new DriveService();
    public static ISettingsService SettingsService => _settingsService ??= new SettingsService();
    public static IScanService ScanService
    {
        get
        {
            if (_scanService == null)
            {
                var svc = new ScanService();
                // Automatically record finished scans to history
                svc.ScanCompleted += async (s, result) =>
                {
                    if (result != null && result.Status != Models.ScanStatus.Cancelled)
                    {
                        await ScanHistoryService.RecordScanAsync(result);
                    }
                };
                _scanService = svc;
            }
            return _scanService;
        }
    }
    public static IFolderPickerService FolderPickerService => _folderPickerService ??= new FolderPickerService();
    public static INavigationService NavigationService => _navigationService ??= new NavigationService();
    public static IExportService ExportService => _exportService ??= new ExportService();
    public static IScanHistoryService ScanHistoryService => _scanHistoryService ??= new ScanHistoryService();
    public static IUpdateService UpdateService => _updateService ??= new UpdateService();

    private static MainViewModel? _mainViewModel;
    private static OverviewViewModel? _overviewViewModel;
    private static StorageAnalysisViewModel? _storageAnalysisViewModel;
    private static DriveAnalysisViewModel? _driveAnalysisViewModel;
    private static FolderAnalysisViewModel? _folderAnalysisViewModel;
    private static LargestFilesViewModel? _largestFilesViewModel;
    private static SettingsViewModel? _settingsViewModel;

    public static MainViewModel MainViewModel => _mainViewModel ??= new MainViewModel(DriveService, FolderPickerService, ScanService, NavigationService);
    public static OverviewViewModel OverviewViewModel => _overviewViewModel ??= new OverviewViewModel(DriveService, FolderPickerService, NavigationService, ScanService);
    public static StorageAnalysisViewModel StorageAnalysisViewModel => _storageAnalysisViewModel ??= new StorageAnalysisViewModel(ScanService, FolderPickerService, ExportService);
    public static DriveAnalysisViewModel DriveAnalysisViewModel => _driveAnalysisViewModel ??= new DriveAnalysisViewModel(ScanService);
    public static FolderAnalysisViewModel FolderAnalysisViewModel => _folderAnalysisViewModel ??= new FolderAnalysisViewModel(ScanService);
    public static LargestFilesViewModel LargestFilesViewModel => _largestFilesViewModel ??= new LargestFilesViewModel(ScanService, NavigationService);
    public static SettingsViewModel SettingsViewModel => _settingsViewModel ??= new SettingsViewModel(SettingsService, ScanHistoryService, UpdateService);

    /// <summary>
    /// Injects custom service implementations (e.g. for unit testing).
    /// </summary>
    public static void Configure(
        IDriveService? driveService = null,
        ISettingsService? settingsService = null,
        IScanService? scanService = null,
        IFolderPickerService? folderPickerService = null,
        INavigationService? navigationService = null,
        IExportService? exportService = null,
        IScanHistoryService? scanHistoryService = null,
        IUpdateService? updateService = null)
    {
        if (driveService != null) _driveService = driveService;
        if (settingsService != null) _settingsService = settingsService;
        if (scanService != null) _scanService = scanService;
        if (folderPickerService != null) _folderPickerService = folderPickerService;
        if (navigationService != null) _navigationService = navigationService;
        if (exportService != null) _exportService = exportService;
        if (scanHistoryService != null) _scanHistoryService = scanHistoryService;
        if (updateService != null) _updateService = updateService;
    }
}

