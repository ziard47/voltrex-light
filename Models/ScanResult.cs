using System;
using System.Collections.Generic;
using voltrex_light.Helpers;

namespace voltrex_light.Models;

/// <summary>
/// Comprehensive result of a disk or folder scan operation.
/// </summary>
public class ScanResult
{
    public Guid ScanId { get; init; } = Guid.NewGuid();
    public string RootPath { get; init; } = string.Empty;
    public FileSystemNode? RootNode { get; init; }
    public ScanStatus Status { get; init; } = ScanStatus.Completed;
    public long TotalBytesScanned { get; init; }
    public long TotalFilesCount { get; init; }
    public long TotalDirectoriesCount { get; init; }
    public int SkippedCount { get; init; }
    public IReadOnlyList<ScanWarning> Warnings { get; init; } = Array.Empty<ScanWarning>();
    public IReadOnlyList<FileSystemNode> LargestFiles { get; init; } = Array.Empty<FileSystemNode>();
    public TimeSpan ElapsedTime { get; init; }
    public bool IsPartial => Status == ScanStatus.Cancelled || Status == ScanStatus.CompletedWithWarnings || Status == ScanStatus.Failed || SkippedCount > 0;
    public string? ErrorMessage { get; init; }

    public string FormattedTotalBytes => FileSystemFormatter.FormatBytes(TotalBytesScanned);
    public string FormattedElapsedTime => ElapsedTime.TotalSeconds < 1
        ? $"{ElapsedTime.TotalMilliseconds:F0} ms"
        : $"{ElapsedTime.TotalSeconds:F1} s";

    public static ScanResult CreateFailed(string rootPath, string errorMessage, Guid scanId) => new()
    {
        ScanId = scanId,
        RootPath = rootPath,
        Status = ScanStatus.Failed,
        ErrorMessage = errorMessage
    };

    public static ScanResult CreateCancelled(string rootPath, TimeSpan elapsed, Guid scanId) => new()
    {
        ScanId = scanId,
        RootPath = rootPath,
        Status = ScanStatus.Cancelled,
        ElapsedTime = elapsed
    };
}

