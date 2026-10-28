using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using voltrex_light.Helpers;
using voltrex_light.Models;
using voltrex_light.Services;

namespace voltrex_light.ViewModels;

/// <summary>
/// ViewModel for Settings view managing appearance, visualization defaults, unit formats, scan history, and updates.
/// </summary>
public class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IScanHistoryService _scanHistoryService;
    private readonly IUpdateService _updateService;

    private string _selectedTheme;
    private string _selectedVisualization;
    private string _selectedSizeUnit;

    private UpdateStatus _updateStatus = UpdateStatus.Idle;
    private string _updateStatusMessage = string.Empty;
    private string _updateNewVersion = string.Empty;
    private string _updateReleaseNotes = string.Empty;
    private double _downloadProgress;
    private string _downloadProgressText = string.Empty;
    private UpdateInfo? _updateInfo;
    private string? _downloadedInstallerPath;
    private bool _isUpdateModalOpen;

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

    // Clean, technology-agnostic About Section properties
    public string AppName => "Voltrex Light";
    public string AppVersion => "1.0.1";
    public string AppDescription => "Modern, high-performance disk space analyzer and storage visualization tool for Windows.";
    public string DeveloperName => "mohomed Ziard | Voltrex Digital";

    // Check Updates System Properties
    public UpdateStatus UpdateStatus
    {
        get => _updateStatus;
        set
        {
            if (SetProperty(ref _updateStatus, value))
            {
                OnPropertyChanged(nameof(IsCheckingForUpdates));
                OnPropertyChanged(nameof(IsDownloading));
                OnPropertyChanged(nameof(IsUpdateAvailable));
                OnPropertyChanged(nameof(IsReadyToInstall));
                OnPropertyChanged(nameof(IsUpToDate));
                OnPropertyChanged(nameof(HasUpdateError));
                OnPropertyChanged(nameof(CanCheckForUpdates));
                NotifyModalProperties();
            }
        }
    }

    public string UpdateStatusMessage
    {
        get => _updateStatusMessage;
        set => SetProperty(ref _updateStatusMessage, value);
    }

    public string UpdateNewVersion
    {
        get => _updateNewVersion;
        set => SetProperty(ref _updateNewVersion, value);
    }

    public string UpdateReleaseNotes
    {
        get => _updateReleaseNotes;
        set => SetProperty(ref _updateReleaseNotes, value);
    }

    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    public string DownloadProgressText
    {
        get => _downloadProgressText;
        set => SetProperty(ref _downloadProgressText, value);
    }

    public bool IsCheckingForUpdates => UpdateStatus == UpdateStatus.Checking;
    public bool IsDownloading => UpdateStatus == UpdateStatus.Downloading;
    public bool IsUpdateAvailable => UpdateStatus == UpdateStatus.UpdateAvailable;
    public bool IsReadyToInstall => UpdateStatus == UpdateStatus.ReadyToInstall;
    public bool IsUpToDate => UpdateStatus == UpdateStatus.UpToDate;
    public bool HasUpdateError => UpdateStatus == UpdateStatus.Error;
    public bool CanCheckForUpdates => UpdateStatus != UpdateStatus.Checking && UpdateStatus != UpdateStatus.Downloading;

    // Modal UI Properties
    public bool IsUpdateModalOpen
    {
        get => _isUpdateModalOpen;
        set => SetProperty(ref _isUpdateModalOpen, value);
    }

    public string ModalTitle => UpdateStatus switch
    {
        UpdateStatus.UpdateAvailable => "Update Available",
        UpdateStatus.Downloading => "Downloading Update",
        UpdateStatus.ReadyToInstall => "Update Ready to Install",
        UpdateStatus.UpToDate => "You're Up to Date",
        UpdateStatus.Error => "Update Check",
        _ => "Voltrex Light Updates"
    };

    public string ModalSubtitle => UpdateStatus switch
    {
        UpdateStatus.UpdateAvailable => "A new version of Voltrex Light is ready to download.",
        UpdateStatus.Downloading => "Please wait while the update package is being downloaded.",
        UpdateStatus.ReadyToInstall => "Download completed. Restart now to apply updates.",
        UpdateStatus.UpToDate => "Voltrex Light is running the newest release.",
        UpdateStatus.Error => "Unable to connect to the update service.",
        _ => "Check for the latest updates and release improvements."
    };

    public string VersionTagText => UpdateStatus == UpdateStatus.UpdateAvailable
        ? $"Version {UpdateNewVersion} (Current: v{AppVersion})"
        : $"Voltrex Light v{AppVersion}";

    public string ReleaseDateText => _updateInfo != null && !string.IsNullOrEmpty(_updateInfo.ReleaseDate)
        ? $"Released {_updateInfo.ReleaseDate}"
        : $"Released {DateTime.Now:MMMM yyyy}";

    public string ModalReleaseNotes => !string.IsNullOrWhiteSpace(_updateInfo?.ReleaseNotes)
        ? _updateInfo.ReleaseNotes
        : UpdateService.DefaultCurrentReleaseNotes;

    public bool ShowModalPrimaryButton => UpdateStatus == UpdateStatus.UpdateAvailable ||
                                          UpdateStatus == UpdateStatus.ReadyToInstall ||
                                          UpdateStatus == UpdateStatus.Downloading;

    public string ModalPrimaryButtonText => UpdateStatus switch
    {
        UpdateStatus.ReadyToInstall => "Restart & Install Now",
        UpdateStatus.Downloading => "Downloading...",
        _ => "Download & Install Update"
    };

    public string ModalPrimaryButtonIcon => UpdateStatus == UpdateStatus.ReadyToInstall ? "\uE768" : "\uE896";

    public bool IsModalPrimaryButtonEnabled => UpdateStatus != UpdateStatus.Downloading;

    public AsyncRelayCommand CheckForUpdatesCommand { get; }
    public AsyncRelayCommand DownloadAndInstallCommand { get; }
    public RelayCommand InstallUpdateCommand { get; }
    public RelayCommand ModalPrimaryActionCommand { get; }
    public RelayCommand OpenReleaseNotesCommand { get; }
    public RelayCommand CloseUpdateModalCommand { get; }

    public SettingsViewModel(
        ISettingsService settingsService,
        IScanHistoryService scanHistoryService,
        IUpdateService? updateService = null)
    {
        _settingsService = settingsService;
        _scanHistoryService = scanHistoryService;
        _updateService = updateService ?? new UpdateService();

        _selectedTheme = _settingsService.Settings.AppTheme;
        _selectedVisualization = _settingsService.Settings.DefaultVisualization;
        _selectedSizeUnit = _settingsService.Settings.SizeDisplayUnit.Equals("Binary", StringComparison.OrdinalIgnoreCase)
            ? "Binary (KiB, MiB, GiB)"
            : "Standard (KB, MB, GB)";

        ClearHistoryCommand = new RelayCommand(async () => await ExecuteClearHistoryAsync());
        CheckForUpdatesCommand = new AsyncRelayCommand(ExecuteCheckForUpdatesAsync);
        DownloadAndInstallCommand = new AsyncRelayCommand(ExecuteDownloadAndInstallAsync);
        InstallUpdateCommand = new RelayCommand(ExecuteInstallUpdate);
        ModalPrimaryActionCommand = new RelayCommand(OnModalPrimaryAction);
        OpenReleaseNotesCommand = new RelayCommand(OpenReleaseNotes);
        CloseUpdateModalCommand = new RelayCommand(() => IsUpdateModalOpen = false);

        RefreshHistory();
    }

    private void NotifyModalProperties()
    {
        OnPropertyChanged(nameof(ModalTitle));
        OnPropertyChanged(nameof(ModalSubtitle));
        OnPropertyChanged(nameof(VersionTagText));
        OnPropertyChanged(nameof(ReleaseDateText));
        OnPropertyChanged(nameof(ModalReleaseNotes));
        OnPropertyChanged(nameof(ShowModalPrimaryButton));
        OnPropertyChanged(nameof(ModalPrimaryButtonText));
        OnPropertyChanged(nameof(ModalPrimaryButtonIcon));
        OnPropertyChanged(nameof(IsModalPrimaryButtonEnabled));
    }

    public void OpenReleaseNotes()
    {
        if (_updateInfo == null)
        {
            _updateInfo = new UpdateInfo
            {
                CurrentVersion = AppVersion,
                LatestVersion = AppVersion,
                ReleaseDate = DateTime.Now.ToString("MMMM yyyy"),
                ReleaseNotes = UpdateService.DefaultCurrentReleaseNotes,
                IsUpdateAvailable = false
            };
            UpdateStatus = UpdateStatus.UpToDate;
            UpdateStatusMessage = $"Voltrex Light is up to date (v{AppVersion}).";
        }
        NotifyModalProperties();
        IsUpdateModalOpen = true;
    }

    private void OnModalPrimaryAction()
    {
        if (UpdateStatus == UpdateStatus.ReadyToInstall)
        {
            ExecuteInstallUpdate();
        }
        else if (UpdateStatus == UpdateStatus.UpdateAvailable)
        {
            _ = ExecuteDownloadAndInstallAsync();
        }
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

    public async Task ExecuteCheckForUpdatesAsync()
    {
        UpdateStatus = UpdateStatus.Checking;
        UpdateStatusMessage = "Checking for updates...";

        try
        {
            _updateInfo = await _updateService.CheckForUpdatesAsync(AppVersion);
            if (_updateInfo.IsUpdateAvailable)
            {
                UpdateStatus = UpdateStatus.UpdateAvailable;
                UpdateNewVersion = _updateInfo.LatestVersion;
                UpdateReleaseNotes = string.IsNullOrWhiteSpace(_updateInfo.ReleaseNotes)
                    ? UpdateService.DefaultNewReleaseNotes
                    : _updateInfo.ReleaseNotes;
                UpdateStatusMessage = $"Version {_updateInfo.LatestVersion} is available!";
            }
            else if (!string.IsNullOrEmpty(_updateInfo.ErrorMessage))
            {
                UpdateStatus = UpdateStatus.Error;
                UpdateStatusMessage = _updateInfo.ErrorMessage;
            }
            else
            {
                UpdateStatus = UpdateStatus.UpToDate;
                UpdateStatusMessage = $"Voltrex Light is up to date (v{AppVersion}).";
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = UpdateStatus.Error;
            UpdateStatusMessage = $"Update check failed: {ex.Message}";
        }
        finally
        {
            NotifyModalProperties();
            IsUpdateModalOpen = true;
        }
    }

    public async Task ExecuteDownloadAndInstallAsync()
    {
        if (string.IsNullOrEmpty(_updateInfo?.DownloadUrl)) return;

        UpdateStatus = UpdateStatus.Downloading;
        UpdateStatusMessage = "Downloading update package...";
        DownloadProgress = 0;
        DownloadProgressText = "Connecting...";

        var progress = new Progress<UpdateDownloadProgress>(p =>
        {
            DownloadProgress = p.ProgressPercentage;
            DownloadProgressText = $"{FileSystemFormatter.FormatBytes(p.BytesReceived)} / {FileSystemFormatter.FormatBytes(p.TotalBytesToReceive)} ({p.ProgressPercentage:F0}%)";
        });

        try
        {
            string installerPath = await _updateService.DownloadUpdateAsync(_updateInfo.DownloadUrl, progress);
            _downloadedInstallerPath = installerPath;
            UpdateStatus = UpdateStatus.ReadyToInstall;
            UpdateStatusMessage = "Update downloaded successfully. Ready to install.";
        }
        catch (Exception ex)
        {
            UpdateStatus = UpdateStatus.Error;
            UpdateStatusMessage = $"Download failed: {ex.Message}";
        }
        finally
        {
            NotifyModalProperties();
        }
    }

    public void ExecuteInstallUpdate()
    {
        if (!string.IsNullOrEmpty(_downloadedInstallerPath))
        {
            UpdateStatus = UpdateStatus.Installing;
            UpdateStatusMessage = "Launching installer...";
            bool launched = _updateService.InstallUpdate(_downloadedInstallerPath);
            if (launched)
            {
                Environment.Exit(0);
            }
            else
            {
                UpdateStatus = UpdateStatus.Error;
                UpdateStatusMessage = "Failed to launch installer.";
            }
        }
    }
}

