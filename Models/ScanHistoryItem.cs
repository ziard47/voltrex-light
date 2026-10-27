using System;
using System.IO;
using voltrex_light.Helpers;

namespace voltrex_light.Models;

/// <summary>
/// Historical record of a previous disk or folder scan.
/// </summary>
public class ScanHistoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RootPath { get; set; } = string.Empty;
    public DateTime ScanTimestamp { get; set; } = DateTime.Now;
    public ScanStatus Status { get; set; } = ScanStatus.Completed;
    public long TotalBytesScanned { get; set; }
    public long TotalFilesCount { get; set; }
    public long TotalDirectoriesCount { get; set; }
    public int SkippedCount { get; set; }
    public bool IsPartial { get; set; }

    public string FormattedTotalBytes => FileSystemFormatter.FormatBytes(TotalBytesScanned);
    public string FormattedTimestamp => ScanTimestamp.ToString("g");

    public bool PathExists => Directory.Exists(RootPath) || File.Exists(RootPath);

    public string StatusBadge => Status switch
    {
        ScanStatus.Completed => "Completed",
        ScanStatus.CompletedWithWarnings => "Warnings",
        ScanStatus.Cancelled => "Cancelled",
        ScanStatus.Failed => "Failed",
        _ => "Archived"
    };
}

