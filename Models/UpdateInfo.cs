using System;

namespace voltrex_light.Models;

/// <summary>
/// Status enumeration for application update operations.
/// </summary>
public enum UpdateStatus
{
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    ReadyToInstall,
    Installing,
    Error
}

/// <summary>
/// Represents update check results and package metadata.
/// </summary>
public class UpdateInfo
{
    public string CurrentVersion { get; set; } = "1.0.1";
    public string LatestVersion { get; set; } = "1.0.1";
    public string ReleaseDate { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public long DownloadSizeBytes { get; set; }
    public bool IsUpdateAvailable { get; set; }
    public string? ErrorMessage { get; set; }
    public string? LocalInstallerPath { get; set; }
}

/// <summary>
/// Real-time progress updates for file downloads.
/// </summary>
public class UpdateDownloadProgress
{
    public long BytesReceived { get; set; }
    public long TotalBytesToReceive { get; set; }
    public double ProgressPercentage => TotalBytesToReceive > 0 ? (double)BytesReceived / TotalBytesToReceive * 100.0 : 0.0;
}

