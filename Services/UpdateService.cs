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
    public const string GitHubReleasesApiUrl = "https://api.github.com/repos/ziard47/voltrex-light/releases/latest";
    public const string GitHubManifestFallbackUrl = "https://raw.githubusercontent.com/ziard47/voltrex-light/main/version.json";

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

        bool foundRelease = false;
        Exception? networkException = null;

        // 1. Primary: Check directly from GitHub Releases API
        try
        {
            using var releasesRequest = new HttpRequestMessage(HttpMethod.Get, GitHubReleasesApiUrl);
            releasesRequest.Headers.Accept.ParseAdd("application/vnd.github.v3+json");

            using var releasesResponse = await _httpClient.SendAsync(releasesRequest, cancellationToken);
            if (releasesResponse.IsSuccessStatusCode)
            {
                var json = await releasesResponse.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("tag_name", out var tagProp) && tagProp.GetString() is string tag && !string.IsNullOrWhiteSpace(tag))
                {
                    result.LatestVersion = tag.TrimStart('v', 'V');
                    foundRelease = true;
                }

                if (root.TryGetProperty("published_at", out var pubProp) && pubProp.GetString() is string pubStr && DateTime.TryParse(pubStr, out var pubDate))
                {
                    result.ReleaseDate = pubDate.ToString("MMMM yyyy");
                }

                if (root.TryGetProperty("body", out var bodyProp) && bodyProp.GetString() is string body && !string.IsNullOrWhiteSpace(body))
                {
                    result.ReleaseNotes = body.Trim();
                }

                if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
                {
                    string? exeUrl = null;
                    long exeSize = 0;
                    string? zipUrl = null;
                    long zipSize = 0;

                    foreach (var asset in assetsProp.EnumerateArray())
                    {
                        if (asset.TryGetProperty("name", out var nameProp) && nameProp.GetString() is string name)
                        {
                            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                if (asset.TryGetProperty("browser_download_url", out var dlProp))
                                {
                                    exeUrl = dlProp.GetString();
                                }
                                if (asset.TryGetProperty("size", out var sizeProp))
                                {
                                    exeSize = sizeProp.GetInt64();
                                }
                                break;
                            }
                            else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && zipUrl == null)
                            {
                                if (asset.TryGetProperty("browser_download_url", out var dlProp))
                                {
                                    zipUrl = dlProp.GetString();
                                }
                                if (asset.TryGetProperty("size", out var sizeProp))
                                {
                                    zipSize = sizeProp.GetInt64();
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(exeUrl))
                    {
                        result.DownloadUrl = exeUrl;
                        result.DownloadSizeBytes = exeSize;
                    }
                    else if (!string.IsNullOrEmpty(zipUrl))
                    {
                        result.DownloadUrl = zipUrl;
                        result.DownloadSizeBytes = zipSize;
                    }
                }

                if (string.IsNullOrWhiteSpace(result.DownloadUrl) && root.TryGetProperty("html_url", out var htmlUrlProp))
                {
                    result.DownloadUrl = htmlUrlProp.GetString() ?? string.Empty;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            networkException = ex;
        }
        catch (TaskCanceledException ex)
        {
            networkException = ex;
        }
        catch (Exception)
        {
            // Non-critical, fall back to version.json
        }

        // 2. Secondary fallback: Check version.json in repository
        if (!foundRelease)
        {
            try
            {
                using var response = await _httpClient.GetAsync(GitHubManifestFallbackUrl, cancellationToken);
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
                    foundRelease = true;
                    networkException = null;
                }
                else
                {
                    // If remote manifest not yet found, app is currently up to date
                    result.IsUpdateAvailable = false;
                    Status = UpdateStatus.UpToDate;
                    networkException = null;
                }
            }
            catch (HttpRequestException ex)
            {
                networkException ??= ex;
            }
            catch (TaskCanceledException ex)
            {
                networkException ??= ex;
            }
            catch (Exception ex)
            {
                networkException ??= ex;
            }
        }

        if (networkException != null && !foundRelease)
        {
            if (networkException is TaskCanceledException)
            {
                result.ErrorMessage = "Update check was cancelled or timed out.";
            }
            else
            {
                result.ErrorMessage = "Unable to connect to update server. Please check your internet connection.";
            }
            Status = UpdateStatus.Error;
        }
        else
        {
            result.IsUpdateAvailable = IsNewerVersion(result.LatestVersion, currentVersion);
            Status = result.IsUpdateAvailable ? UpdateStatus.UpdateAvailable : UpdateStatus.UpToDate;
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

