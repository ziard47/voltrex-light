using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using voltrex_light.Helpers;
using voltrex_light.Models;
using voltrex_light.Services;

namespace voltrex_light.ViewModels;

/// <summary>
/// Root ViewModel for Voltrex Light shell, top header, actions toolbar, sidebar drives, and global scan status.
/// </summary>
public class MainViewModel : ObservableObject
{
    private readonly IDriveService _driveService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IScanService _scanService;
    private readonly INavigationService _navigationService;

    private string _currentViewTitle = "Overview";
    private string _selectedPath = string.Empty;
    private string _searchQuery = string.Empty;

    public string CurrentViewTitle
    {
        get => _currentViewTitle;
        set => SetProperty(ref _currentViewTitle, value);
    }

    public string SelectedPath
    {
        get => _selectedPath;
        set
        {
            if (SetProperty(ref _selectedPath, value))
            {
                OnPropertyChanged(nameof(SelectedPathVisibility));
            }
        }
    }

    public Visibility SelectedPathVisibility => string.IsNullOrEmpty(SelectedPath) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ScanningVisibility => Progress.IsScanning ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IdleVisibility => Progress.IsScanning ? Visibility.Collapsed : Visibility.Visible;
    public string ScanStatusText => Progress.IsScanning
        ? $"{Progress.FormattedItemsCount} items ({FileSystemFormatter.FormatBytes(Progress.TotalBytesScanned)})"
        : string.Empty;

    public bool IsAdministrator => AdminElevationHelper.IsAdministrator;
    public Visibility AdminBadgeVisibility => IsAdministrator ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ElevateButtonVisibility => IsAdministrator ? Visibility.Collapsed : Visibility.Visible;

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public ScanProgressInfo Progress => _scanService.Progress;
    public ObservableCollection<DriveItem> SidebarDrives { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public AsyncRelayCommand ScanFolderCommand { get; }
    public AsyncRelayCommand ScanDriveCommand { get; }
    public RelayCommand CancelScanCommand { get; }
    public RelayCommand ElevateAdminCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }

    public MainViewModel(
        IDriveService driveService,
        IFolderPickerService folderPickerService,
        IScanService scanService,
        INavigationService navigationService)
    {
        _driveService = driveService;
        _folderPickerService = folderPickerService;
        _scanService = scanService;
        _navigationService = navigationService;

        RefreshCommand = new RelayCommand(OnRefresh);
        ScanFolderCommand = new AsyncRelayCommand(OnScanFolderAsync);
        ScanDriveCommand = new AsyncRelayCommand(OnScanDriveAsync);
        CancelScanCommand = new RelayCommand(() => _scanService.CancelScan(), () => _scanService.IsScanning);
        ElevateAdminCommand = new RelayCommand(() => AdminElevationHelper.RestartAsAdministrator());
        ToggleThemeCommand = new RelayCommand(OnToggleTheme);

        _scanService.Progress.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ScanProgressInfo.IsScanning))
            {
                OnPropertyChanged(nameof(ScanningVisibility));
                OnPropertyChanged(nameof(IdleVisibility));
                OnPropertyChanged(nameof(ScanStatusText));
                CancelScanCommand.NotifyCanExecuteChanged();
                UpdateSidebarDrivesScanningStatus();
            }
            else if (e.PropertyName == nameof(ScanProgressInfo.FilesProcessed) ||
                     e.PropertyName == nameof(ScanProgressInfo.DirectoriesProcessed) ||
                     e.PropertyName == nameof(ScanProgressInfo.TotalBytesScanned))
            {
                OnPropertyChanged(nameof(ScanStatusText));
            }
        };

        _navigationService.Navigated += OnNavigated;
    }

    private void UpdateSidebarDrivesScanningStatus()
    {
        bool isScanning = Progress.IsScanning;
        string currentRoot = Progress.CurrentPath;

        foreach (var drive in SidebarDrives)
        {
            if (!isScanning || string.IsNullOrEmpty(currentRoot))
            {
                drive.IsScanning = false;
            }
            else
            {
                drive.IsScanning = currentRoot.StartsWith(drive.RootDirectory, StringComparison.OrdinalIgnoreCase) ||
                                   drive.RootDirectory.StartsWith(currentRoot, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public async Task LoadSidebarDrivesAsync()
    {
        try
        {
            var drives = await _driveService.GetDrivesAsync();
            SidebarDrives.Clear();
            foreach (var d in drives)
            {
                if (d.IsReady)
                {
                    SidebarDrives.Add(d);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Failed to load sidebar drives: {ex.Message}");
        }
    }

    public async Task ScanDriveFromSidebarAsync(DriveItem drive)
    {
        if (drive == null || string.IsNullOrEmpty(drive.RootDirectory)) return;

        drive.IsScanning = true;
        SelectedPath = drive.RootDirectory;
        _navigationService.NavigateTo("StorageAnalysis");
        try
        {
            await _scanService.ScanPathAsync(drive.RootDirectory);
        }
        finally
        {
            drive.IsScanning = false;
        }
    }

    private void OnNavigated(object? sender, string pageKey)
    {
        CurrentViewTitle = pageKey switch
        {
            "Overview" => "Overview",
            "StorageAnalysis" => "Storage Analysis",
            "DriveAnalysis" => "Storage Analysis",
            "FolderAnalysis" => "Storage Analysis",
            "LargestFiles" => "Largest Files",
            "Settings" => "Settings",
            _ => pageKey
        };
    }

    private void OnToggleTheme()
    {
        var current = AppServices.SettingsService.Settings.AppTheme;
        var next = current == "Dark" ? "Light" : "Dark";
        _ = AppServices.SettingsService.SetThemeAsync(next);
    }

    private void OnRefresh()
    {
        if (_navigationService.CurrentPageKey == "Overview")
        {
            _ = LoadSidebarDrivesAsync();
            AppServices.OverviewViewModel.LoadDrivesCommand.Execute(null);
        }
        else if (_navigationService.CurrentPageKey == "StorageAnalysis")
        {
            if (AppServices.StorageAnalysisViewModel.RefreshCommand.CanExecute(null))
            {
                AppServices.StorageAnalysisViewModel.RefreshCommand.Execute(null);
            }
        }
        else if (!string.IsNullOrEmpty(SelectedPath))
        {
            _ = _scanService.ScanPathAsync(SelectedPath);
        }
    }

    private async Task OnScanFolderAsync()
    {
        var folder = await _folderPickerService.PickFolderAsync();
        if (!string.IsNullOrEmpty(folder))
        {
            SelectedPath = folder;
            _navigationService.NavigateTo("StorageAnalysis");
            await _scanService.ScanPathAsync(folder);
        }
    }

    private async Task OnScanDriveAsync()
    {
        var drives = await _driveService.GetDrivesAsync();
        if (drives.Count > 0)
        {
            var targetDrive = drives[0].RootDirectory;
            SelectedPath = targetDrive;
            _navigationService.NavigateTo("StorageAnalysis");
            await _scanService.ScanPathAsync(targetDrive);
        }
    }
}

