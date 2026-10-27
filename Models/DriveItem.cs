using System;
using System.IO;
using voltrex_light.Helpers;

namespace voltrex_light.Models;

/// <summary>
/// Represents a local or connected storage drive with Windows capacity and usage metrics.
/// </summary>
public class DriveItem : ObservableObject
{
    private DateTime? _lastScanTime;
    private bool _isScanning;

    public string Name { get; set; } = string.Empty;
    public string VolumeLabel { get; set; } = string.Empty;
    public string RootDirectory { get; set; } = string.Empty;
    public DriveType DriveType { get; set; } = DriveType.Fixed;
    public string DriveFormat { get; set; } = string.Empty;
    public long TotalSizeBytes { get; set; }
    public long FreeSizeBytes { get; set; }
    public long UsedSizeBytes { get; set; }
    public double PercentUsed { get; set; }
    public bool IsReady { get; set; } = true;

    public string DisplayName => string.IsNullOrWhiteSpace(VolumeLabel)
        ? $"Local Disk ({Name.TrimEnd('\\')})"
        : $"{VolumeLabel} ({Name.TrimEnd('\\')})";

    public string FormattedTotalSize => FileSystemFormatter.FormatBytes(TotalSizeBytes);
    public string FormattedFreeSize => FileSystemFormatter.FormatBytes(FreeSizeBytes);
    public string FormattedUsedSize => FileSystemFormatter.FormatBytes(UsedSizeBytes);
    public string FormattedPercentUsed => FileSystemFormatter.FormatPercentage(PercentUsed, isRatio0To1: false);

    public string DriveTypeDescription => DriveType switch
    {
        DriveType.Fixed => "Local Fixed Disk",
        DriveType.Removable => "Removable Drive (USB)",
        DriveType.Network => "Network Drive",
        DriveType.CDRom => "Optical Disc Drive",
        DriveType.Ram => "RAM Disk",
        _ => "Unknown Storage"
    };

    public string IconGlyph => DriveType switch
    {
        DriveType.Fixed => "\uEDA2",       // Hard drive icon
        DriveType.Removable => "\uE88E",   // USB / removable
        DriveType.Network => "\uE710",     // Network drive
        DriveType.CDRom => "\uE958",       // CD drive
        _ => "\uEDA2"
    };

    public DateTime? LastScanTime
    {
        get => _lastScanTime;
        set
        {
            if (SetProperty(ref _lastScanTime, value))
            {
                OnPropertyChanged(nameof(LastScanTimeFormatted));
            }
        }
    }

    public string LastScanTimeFormatted => LastScanTime.HasValue
        ? LastScanTime.Value.ToString("g")
        : "Never scanned";

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(AnalyzeButtonText));
            }
        }
    }

    public string AnalyzeButtonText => IsScanning ? "Scanning..." : "Analyze";
}

