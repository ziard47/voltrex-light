using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using voltrex_light.Helpers;
using voltrex_light.Models;
using voltrex_light.Services;

namespace voltrex_light.ViewModels;

/// <summary>
/// ViewModel for Overview dashboard displaying live Windows storage drive capacity, usage metrics, and quick actions.
/// </summary>
public class OverviewViewModel : ObservableObject
{
    private readonly IDriveService _driveService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly INavigationService _navigationService;
    private readonly IScanService _scanService;

    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private DriveItem? _selectedDrive;

    private long _totalCapacityBytes;
    private long _totalUsedBytes;
    private long _totalFreeBytes;
    private double _overallPercentUsed;

    public ObservableCollection<DriveItem> Drives { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasDrives => Drives.Count > 0;

    public DriveItem? SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            if (SetProperty(ref _selectedDrive, value))
            {
                ScanSelectedDriveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public long TotalCapacityBytes
    {
        get => _totalCapacityBytes;
        set
        {
            if (SetProperty(ref _totalCapacityBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotalCapacity));
            }
        }
    }

    public long TotalUsedBytes
    {
        get => _totalUsedBytes;
        set
        {
            if (SetProperty(ref _totalUsedBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotalUsed));
            }
        }
    }

    public long TotalFreeBytes
    {
        get => _totalFreeBytes;
        set
        {
            if (SetProperty(ref _totalFreeBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotalFree));
            }
        }
    }

    public double OverallPercentUsed
    {
        get => _overallPercentUsed;
        set
        {
            if (SetProperty(ref _overallPercentUsed, value))
            {
                OnPropertyChanged(nameof(FormattedOverallPercentUsed));
            }
        }
    }

    public string FormattedTotalCapacity => FileSystemFormatter.FormatBytes(TotalCapacityBytes);
    public string FormattedTotalUsed => FileSystemFormatter.FormatBytes(TotalUsedBytes);
    public string FormattedTotalFree => FileSystemFormatter.FormatBytes(TotalFreeBytes);
    public string FormattedOverallPercentUsed => FileSystemFormatter.FormatPercentage(OverallPercentUsed, isRatio0To1: false);

    public AsyncRelayCommand LoadDrivesCommand { get; }
    public AsyncRelayCommand<DriveItem> ScanDriveCommand { get; }
    public RelayCommand ScanSelectedDriveCommand { get; }
    public AsyncRelayCommand ScanFolderQuickCommand { get; }
    public RelayCommand<DriveItem> OpenInExplorerCommand { get; }

    public OverviewViewModel(
        IDriveService driveService,
        IFolderPickerService folderPickerService,
        INavigationService navigationService,
        IScanService scanService)
    {
        _driveService = driveService;
        _folderPickerService = folderPickerService;
        _navigationService = navigationService;
        _scanService = scanService;

        LoadDrivesCommand = new AsyncRelayCommand(LoadDrivesAsync);
        ScanDriveCommand = new AsyncRelayCommand<DriveItem>(ExecuteScanDriveAsync);
        ScanSelectedDriveCommand = new RelayCommand(
            () => { if (SelectedDrive != null) _ = ExecuteScanDriveAsync(SelectedDrive); },
            () => SelectedDrive != null);
        ScanFolderQuickCommand = new AsyncRelayCommand(ExecuteScanFolderQuickAsync);
        OpenInExplorerCommand = new RelayCommand<DriveItem>(ExecuteOpenInExplorer);
    }

    public async Task LoadDrivesAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var drives = await _driveService.GetDrivesAsync();
            Drives.Clear();

            long totalCap = 0;
            long totalUsed = 0;
            long totalFree = 0;

            foreach (var d in drives)
            {
                Drives.Add(d);
                if (d.IsReady)
                {
                    totalCap += d.TotalSizeBytes;
                    totalUsed += d.UsedSizeBytes;
                    totalFree += d.FreeSizeBytes;
                }
            }

            TotalCapacityBytes = totalCap;
            TotalUsedBytes = totalUsed;
            TotalFreeBytes = totalFree;
            OverallPercentUsed = totalCap > 0 ? ((double)totalUsed / totalCap) * 100.0 : 0.0;

            if (SelectedDrive == null && Drives.Count > 0)
            {
                SelectedDrive = Drives[0];
            }

            OnPropertyChanged(nameof(HasDrives));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to retrieve drives: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecuteScanDriveAsync(DriveItem? drive)
    {
        if (drive == null || !drive.IsReady) return;

        drive.LastScanTime = DateTime.Now;
        drive.IsScanning = true;
        _navigationService.NavigateTo("StorageAnalysis", drive.RootDirectory);
        try
        {
            await _scanService.ScanPathAsync(drive.RootDirectory);
        }
        finally
        {
            drive.IsScanning = false;
        }
    }

    private async Task ExecuteScanFolderQuickAsync()
    {
        var folder = await _folderPickerService.PickFolderAsync();
        if (!string.IsNullOrEmpty(folder))
        {
            _navigationService.NavigateTo("StorageAnalysis", folder);
            await _scanService.ScanPathAsync(folder);
        }
    }

    private void ExecuteOpenInExplorer(DriveItem? drive)
    {
        if (drive != null && !string.IsNullOrEmpty(drive.RootDirectory))
        {
            WindowsShellHelper.OpenInExplorer(drive.RootDirectory);
        }
    }
}

