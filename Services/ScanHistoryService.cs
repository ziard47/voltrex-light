using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using voltrex_light.Models;

namespace voltrex_light.Services;

/// <summary>
/// Manages versioned local scan history storage in %LOCALAPPDATA%\DiskScope\history.json.
/// Completely offline and private.
/// </summary>
public class ScanHistoryService : IScanHistoryService
{
    private const int CurrentVersion = 1;
    private const int MaxHistoryItems = 50;
    private readonly string _historyFilePath;
    private readonly List<ScanHistoryItem> _items = new();

    public IReadOnlyList<ScanHistoryItem> History => _items.AsReadOnly();

    public ScanHistoryService(string? customFilePath = null)
    {
        if (!string.IsNullOrEmpty(customFilePath))
        {
            _historyFilePath = customFilePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "DiskScope");
            _historyFilePath = Path.Combine(dir, "history.json");
        }
    }

    public async Task LoadHistoryAsync()
    {
        try
        {
            if (File.Exists(_historyFilePath))
            {
                var json = await File.ReadAllTextAsync(_historyFilePath);
                var container = JsonSerializer.Deserialize<HistoryContainer>(json);
                if (container != null && container.Items != null)
                {
                    _items.Clear();
                    _items.AddRange(container.Items);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScanHistoryService] Load failed: {ex.Message}");
        }
    }

    public async Task RecordScanAsync(ScanResult result)
    {
        if (result == null || string.IsNullOrWhiteSpace(result.RootPath)) return;

        var historyItem = new ScanHistoryItem
        {
            Id = result.ScanId,
            RootPath = result.RootPath,
            ScanTimestamp = DateTime.Now,
            Status = result.Status,
            TotalBytesScanned = result.TotalBytesScanned,
            TotalFilesCount = result.TotalFilesCount,
            TotalDirectoriesCount = result.TotalDirectoriesCount,
            SkippedCount = result.SkippedCount,
            IsPartial = result.IsPartial
        };

        // Insert at beginning
        _items.Insert(0, historyItem);

        // Trim to max history
        while (_items.Count > MaxHistoryItems)
        {
            _items.RemoveAt(_items.Count - 1);
        }

        await SaveHistoryAsync();
    }

    public async Task ClearHistoryAsync()
    {
        _items.Clear();
        await SaveHistoryAsync();
    }

    private async Task SaveHistoryAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var container = new HistoryContainer
            {
                Version = CurrentVersion,
                Items = _items
            };

            var json = JsonSerializer.Serialize(container, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_historyFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScanHistoryService] Save failed: {ex.Message}");
        }
    }

    private class HistoryContainer
    {
        public int Version { get; set; } = CurrentVersion;
        public List<ScanHistoryItem> Items { get; set; } = new();
    }
}
