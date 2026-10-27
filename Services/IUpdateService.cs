using System;
using System.Threading;
using System.Threading.Tasks;
using voltrex_light.Models;

namespace voltrex_light.Services;

/// <summary>
/// Service interface for checking, downloading, and installing application updates.
/// </summary>
public interface IUpdateService
{
    UpdateStatus Status { get; }
    UpdateInfo? CurrentUpdateInfo { get; }

    /// <summary>
    /// Checks for available updates by comparing current version against latest remote manifest.
    /// </summary>
    Task<UpdateInfo> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the update package or installer to a local temporary location with progress reporting.
    /// </summary>
    Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Launches the downloaded installer and prepares the application for restart.
    /// </summary>
    bool InstallUpdate(string installerPath);
}

