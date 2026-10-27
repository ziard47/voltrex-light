using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using voltrex_light.Models;

namespace voltrex_light.Services;

/// <summary>
/// Production update service for checking, downloading, and installing Voltrex Light updates.
/// </summary>
public class UpdateService : IUpdateService
{
    private readonly HttpClient _httpClient;
    private const string DefaultUpdateManifestUrl = "https://raw.githubusercontent.com/voltrexdigital/voltrex-light/main/version.json";

    public const string DefaultCurrentReleaseNotes =
@"✨ Highlights in Voltrex Light:
• Modern Windows 11 Fluent Design with translucent Mica backdrop.
• Unified Storage Analysis combining volume and folder inspection.
• Interactive Filelight-style Radial Sunburst and Squarified Treemap visualizations.
• Direct Windows Explorer context menu with Recycle Bin and Permanent Delete.
• Real-time throttled scanning indicators across titlebar, sidebar, and workspace.
• High-precision 64-bit disk engine with junction and reparse cycle avoidance.
• RFC 4180 CSV and hierarchical JSON scan report exports.
• 100% private, local file processing with zero telemetry.";

    public const string DefaultNewReleaseNotes =
@"✨ What's New in this Update:
• Redesigned Update Manager with interactive modal and release notes viewer.
• Compact 220px navigation sidebar matching Windows 11 standards.
• Enhanced search and filter bar layout in Largest Files.
• Improved folder properties panel alignment and item badges.
• Performance optimizations for multi-terabyte disk traversals.";

    public UpdateStatus Status { get; private set; } = UpdateStatus.Idle;
    public UpdateInfo? CurrentUpdateInfo { get; private set; }

    public UpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VoltrexLight-Updater/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        Status = UpdateStatus.Checking;

        var result = new UpdateInfo
        {
            CurrentVersion = currentVersion,
            LatestVersion = currentVersion,
            IsUpdateAvailable = false
        };

        try
        {
            // Attempt to query update manifest
            using var response = await _httpClient.GetAsync(DefaultUpdateManifestUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("version", out var verProp))
                {
                    result.LatestVersion = verProp.GetString() ?? currentVersion;
                }
                if (root.TryGetProperty("releaseDate", out var dateProp))
                {
                    result.ReleaseDate = dateProp.GetString() ?? string.Empty;
                }
                if (root.TryGetProperty("releaseNotes", out var notesProp))
                {
                    result.ReleaseNotes = notesProp.GetString() ?? string.Empty;
                }
                if (root.TryGetProperty("downloadUrl", out var urlProp))
                {
                    result.DownloadUrl = urlProp.GetString() ?? string.Empty;
                }
                if (root.TryGetProperty("downloadSize", out var sizeProp))
                {
                    result.DownloadSizeBytes = sizeProp.GetInt64();
                }

                result.IsUpdateAvailable = IsNewerVersion(result.LatestVersion, currentVersion);
                Status = result.IsUpdateAvailable ? UpdateStatus.UpdateAvailable : UpdateStatus.UpToDate;
            }
            else
            {
                // If remote manifest is not found yet, current version is up to date
                result.IsUpdateAvailable = false;
                Status = UpdateStatus.UpToDate;
            }
        }
        catch (HttpRequestException)
        {
            // Network unreachable / offline
            result.ErrorMessage = "Unable to connect to update server. Please check your internet connection.";
            Status = UpdateStatus.Error;
        }
        catch (TaskCanceledException)
        {
            result.ErrorMessage = "Update check was cancelled or timed out.";
            Status = UpdateStatus.Error;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Failed to check for updates: {ex.Message}";
            Status = UpdateStatus.Error;
        }

        if (string.IsNullOrWhiteSpace(result.ReleaseDate))
        {
            result.ReleaseDate = DateTime.Now.ToString("MMMM yyyy");
        }

        if (string.IsNullOrWhiteSpace(result.ReleaseNotes))
        {
            result.ReleaseNotes = result.IsUpdateAvailable ? DefaultNewReleaseNotes : DefaultCurrentReleaseNotes;
        }

        CurrentUpdateInfo = result;
        return result;
    }

    public async Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new ArgumentException("Invalid download URL provided.", nameof(downloadUrl));
        }

        Status = UpdateStatus.Downloading;

        var tempDir = Path.Combine(Path.GetTempPath(), "VoltrexLightUpdates");
        Directory.CreateDirectory(tempDir);

        string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            fileName = "VoltrexLight_Update.exe";
        }

        string destinationPath = Path.Combine(tempDir, fileName);

        try
        {
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalRead += bytesRead;

                progress?.Report(new UpdateDownloadProgress
                {
                    BytesReceived = totalRead,
                    TotalBytesToReceive = totalBytes
                });
            }

            if (CurrentUpdateInfo != null)
            {
                CurrentUpdateInfo.LocalInstallerPath = destinationPath;
            }

            Status = UpdateStatus.ReadyToInstall;
            return destinationPath;
        }
        catch (Exception)
        {
            Status = UpdateStatus.Error;
            throw;
        }
    }

    public bool InstallUpdate(string installerPath)
    {
        if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
        {
            return false;
        }

        try
        {
            Status = UpdateStatus.Installing;

            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            return true;
        }
        catch (Exception)
        {
            Status = UpdateStatus.Error;
            return false;
        }
    }

    /// <summary>
    /// Compares two version strings (e.g. "1.1.0" vs "1.0.0") to determine if candidate is newer.
    /// </summary>
    public static bool IsNewerVersion(string candidateVersion, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(candidateVersion)) return false;
        if (string.IsNullOrWhiteSpace(currentVersion)) return true;

        string cleanCandidate = candidateVersion.Trim().TrimStart('v', 'V');
        string cleanCurrent = currentVersion.Trim().TrimStart('v', 'V');

        if (Version.TryParse(cleanCandidate, out var candidate) && Version.TryParse(cleanCurrent, out var current))
        {
            return candidate > current;
        }

        return string.Compare(cleanCandidate, cleanCurrent, StringComparison.OrdinalIgnoreCase) > 0;
    }
}

