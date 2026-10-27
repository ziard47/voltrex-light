using System;
using voltrex_light.Helpers;

namespace voltrex_light.Models;

/// <summary>
/// Status and throttled live metrics for active or completed disk analysis operations.
/// </summary>
public class ScanProgressInfo : ObservableObject
{
    private ScanStatus _status = ScanStatus.Idle;
    private bool _isScanning;
    private string _statusMessage = "Ready";
    private string _currentPath = string.Empty;
    private long _filesProcessed;
    private long _directoriesProcessed;
    private long _totalBytesScanned;
    private int _skippedItemsCount;
    private TimeSpan _elapsedTime = TimeSpan.Zero;
    private double _progressPercentage;

    public ScanStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                IsScanning = value == ScanStatus.Scanning || value == ScanStatus.Preparing || value == ScanStatus.Finalizing;
                OnPropertyChanged(nameof(StatusDescription));
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string CurrentPath
    {
        get => _currentPath;
        set => SetProperty(ref _currentPath, value);
    }

    public long FilesProcessed
    {
        get => _filesProcessed;
        set
        {
            if (SetProperty(ref _filesProcessed, value))
            {
                OnPropertyChanged(nameof(FormattedFilesCount));
                OnPropertyChanged(nameof(ScannedItemsCount));
                OnPropertyChanged(nameof(FormattedItemsCount));
            }
        }
    }

    public long DirectoriesProcessed
    {
        get => _directoriesProcessed;
        set
        {
            if (SetProperty(ref _directoriesProcessed, value))
            {
                OnPropertyChanged(nameof(FormattedDirectoriesCount));
                OnPropertyChanged(nameof(ScannedItemsCount));
                OnPropertyChanged(nameof(FormattedItemsCount));
            }
        }
    }

    public long ScannedItemsCount => _filesProcessed + _directoriesProcessed;

    public long TotalBytesScanned
    {
        get => _totalBytesScanned;
        set
        {
            if (SetProperty(ref _totalBytesScanned, value))
            {
                OnPropertyChanged(nameof(FormattedBytesScanned));
            }
        }
    }

    public int SkippedItemsCount
    {
        get => _skippedItemsCount;
        set
        {
            if (SetProperty(ref _skippedItemsCount, value))
            {
                OnPropertyChanged(nameof(HasWarnings));
                OnPropertyChanged(nameof(FormattedSkippedCount));
            }
        }
    }

    public bool HasWarnings => _skippedItemsCount > 0;

    public TimeSpan ElapsedTime
    {
        get => _elapsedTime;
        set
        {
            if (SetProperty(ref _elapsedTime, value))
            {
                OnPropertyChanged(nameof(FormattedElapsedTime));
            }
        }
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    public string FormattedFilesCount => FileSystemFormatter.FormatCount(FilesProcessed, "file", "files");
    public string FormattedDirectoriesCount => FileSystemFormatter.FormatCount(DirectoriesProcessed, "folder", "folders");
    public string FormattedItemsCount => FileSystemFormatter.FormatCount(ScannedItemsCount, "item", "items");
    public string FormattedBytesScanned => FileSystemFormatter.FormatBytes(TotalBytesScanned);
    public string FormattedSkippedCount => FileSystemFormatter.FormatCount(SkippedItemsCount, "skipped item", "skipped items");
    public string FormattedElapsedTime => ElapsedTime.TotalSeconds < 1
        ? $"{ElapsedTime.TotalMilliseconds:F0} ms"
        : $"{ElapsedTime.TotalSeconds:F1} s";

    public string StatusDescription => Status switch
    {
        ScanStatus.Preparing => "Preparing scan...",
        ScanStatus.Scanning => "Scanning file system...",
        ScanStatus.Finalizing => "Finalizing metrics...",
        ScanStatus.Completed => "Scan completed successfully",
        ScanStatus.CompletedWithWarnings => $"Scan completed with {SkippedItemsCount} skipped items",
        ScanStatus.Cancelled => "Scan cancelled by user",
        ScanStatus.Failed => "Scan failed",
        _ => "Ready"
    };

    public void Reset()
    {
        Status = ScanStatus.Preparing;
        StatusMessage = "Preparing scan...";
        CurrentPath = string.Empty;
        FilesProcessed = 0;
        DirectoriesProcessed = 0;
        TotalBytesScanned = 0;
        SkippedItemsCount = 0;
        ElapsedTime = TimeSpan.Zero;
        ProgressPercentage = 0.0;
    }
}

