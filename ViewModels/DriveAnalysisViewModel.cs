using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using voltrex_light.Helpers;
using voltrex_light.Models;
using voltrex_light.Services;

namespace voltrex_light.ViewModels;

/// <summary>
/// ViewModel for Drive Analysis workspace including tree/table, sorting, warning reporting, and contextual details.
/// </summary>
public class DriveAnalysisViewModel : ObservableObject
{
    private readonly IScanService _scanService;
    private readonly IExportService _exportService;
    private FileSystemNode? _rootNode;
    private FileSystemNode? _selectedNode;
    private string _filterQuery = string.Empty;
    private string _currentDrivePath = string.Empty;

    private string _currentSortColumn = "Size";
    private bool _isSortAscending = false;

    private bool _hasWarnings;
    private string _warningsSummary = string.Empty;
    private bool _isPartialScan;
    private TimeSpan _elapsedTime = TimeSpan.Zero;
    private int _skippedItemsCount;

    public ObservableCollection<FileSystemNode> DisplayNodes { get; } = new();

    public FileSystemNode? RootNode
    {
        get => _rootNode;
        set
        {
            if (SetProperty(ref _rootNode, value))
            {
                ApplyFilterAndSort();
                OnPropertyChanged(nameof(HasData));
                OnPropertyChanged(nameof(TotalSizeFormatted));
                OnPropertyChanged(nameof(TotalFilesFormatted));
                OnPropertyChanged(nameof(TotalFoldersFormatted));
                ExportCsvCommand?.RaiseCanExecuteChanged();
                ExportJsonCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public FileSystemNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public string FilterQuery
    {
        get => _filterQuery;
        set
        {
            if (SetProperty(ref _filterQuery, value))
            {
                ApplyFilterAndSort();
            }
        }
    }

    public string CurrentDrivePath
    {
        get => _currentDrivePath;
        set => SetProperty(ref _currentDrivePath, value);
    }

    public string CurrentSortColumn
    {
        get => _currentSortColumn;
        set => SetProperty(ref _currentSortColumn, value);
    }

    public bool IsSortAscending
    {
        get => _isSortAscending;
        set => SetProperty(ref _isSortAscending, value);
    }

    public bool HasWarnings
    {
        get => _hasWarnings;
        set => SetProperty(ref _hasWarnings, value);
    }

    public string WarningsSummary
    {
        get => _warningsSummary;
        set => SetProperty(ref _warningsSummary, value);
    }

    public bool IsPartialScan
    {
        get => _isPartialScan;
        set => SetProperty(ref _isPartialScan, value);
    }

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

    public int SkippedItemsCount
    {
        get => _skippedItemsCount;
        set => SetProperty(ref _skippedItemsCount, value);
    }

    public bool HasData => RootNode != null && RootNode.Children.Count > 0;
    public bool HasSelection => SelectedNode != null;

    public string TotalSizeFormatted => RootNode != null ? RootNode.FormattedSize : "0 B";
    public string TotalFilesFormatted => RootNode != null ? RootNode.TotalFileCount.ToString("N0") : "0";
    public string TotalFoldersFormatted => RootNode != null ? RootNode.TotalSubfolderCount.ToString("N0") : "0";
    public string FormattedElapsedTime => ElapsedTime.TotalSeconds < 1
        ? $"{ElapsedTime.TotalMilliseconds:F0} ms"
        : $"{ElapsedTime.TotalSeconds:F1} s";

    public RelayCommand OpenInExplorerCommand { get; }
    public RelayCommand<FileSystemNode> SelectNodeCommand { get; }
    public RelayCommand<string> SortCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand ExportJsonCommand { get; }

    public DriveAnalysisViewModel(IScanService scanService, IExportService? exportService = null)
    {
        _scanService = scanService;
        _exportService = exportService ?? new ExportService();
        _scanService.ScanCompleted += OnScanCompleted;

        OpenInExplorerCommand = new RelayCommand(ExecuteOpenInExplorer, () => HasSelection);
        SelectNodeCommand = new RelayCommand<FileSystemNode>(node => SelectedNode = node);
        SortCommand = new RelayCommand<string>(SortByColumn);
        ExportCsvCommand = new RelayCommand(async () => await ExecuteExportAsync("csv"), () => HasData);
        ExportJsonCommand = new RelayCommand(async () => await ExecuteExportAsync("json"), () => HasData);
    }

    private async System.Threading.Tasks.Task ExecuteExportAsync(string format)
    {
        var result = _scanService.LastResult;
        if (result == null && RootNode != null)
        {
            result = new ScanResult
            {
                RootPath = CurrentDrivePath,
                RootNode = RootNode,
                TotalBytesScanned = RootNode.SizeBytes,
                TotalFilesCount = RootNode.TotalFileCount,
                TotalDirectoriesCount = RootNode.TotalSubfolderCount
            };
        }

        if (result != null)
        {
            await _exportService.ExportWithDialogAsync(result, format);
        }
    }

    private void OnScanCompleted(object? sender, ScanResult result)
    {
        if (result != null && (string.IsNullOrEmpty(CurrentDrivePath) || result.RootPath.StartsWith(CurrentDrivePath, StringComparison.OrdinalIgnoreCase) || CurrentDrivePath.StartsWith(result.RootPath, StringComparison.OrdinalIgnoreCase)))
        {
            ElapsedTime = result.ElapsedTime;
            SkippedItemsCount = result.SkippedCount;
            IsPartialScan = result.IsPartial;

            if (result.Warnings.Count > 0)
            {
                HasWarnings = true;
                var unauthCount = result.Warnings.Count(w => w.WarningType == ScanWarningType.UnauthorizedAccess);
                var reparseCount = result.Warnings.Count(w => w.WarningType == ScanWarningType.ReparsePointSkipped);

                WarningsSummary = $"Partial scan: {result.SkippedCount} items skipped ({unauthCount} access-denied system directories, {reparseCount} junction links).";
            }
            else
            {
                HasWarnings = false;
                WarningsSummary = string.Empty;
            }

            RootNode = result.RootNode;
            SelectedNode = result.RootNode?.Children.FirstOrDefault() ?? result.RootNode;
        }
    }

    public void SetPath(string path)
    {
        CurrentDrivePath = path;
    }

    public void SortByColumn(string? column)
    {
        if (string.IsNullOrEmpty(column)) return;

        if (CurrentSortColumn.Equals(column, StringComparison.OrdinalIgnoreCase))
        {
            IsSortAscending = !IsSortAscending;
        }
        else
        {
            CurrentSortColumn = column;
            // Default to descending for numbers/percentages/dates, ascending for names
            IsSortAscending = column.Equals("Name", StringComparison.OrdinalIgnoreCase);
        }

        ApplyFilterAndSort();
    }

    private void ApplyFilterAndSort()
    {
        DisplayNodes.Clear();
        if (RootNode == null) return;

        var items = RootNode.Children.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterQuery))
        {
            var query = FilterQuery.Trim();
            items = items.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        // Apply column sorting without mutating original tree order
        items = (CurrentSortColumn, IsSortAscending) switch
        {
            ("Name", true) => items.OrderBy(x => x.Name),
            ("Name", false) => items.OrderByDescending(x => x.Name),
            ("Size", true) => items.OrderBy(x => x.SizeBytes),
            ("Size", false) => items.OrderByDescending(x => x.SizeBytes),
            ("Percent", true) => items.OrderBy(x => x.PercentOfParent),
            ("Percent", false) => items.OrderByDescending(x => x.PercentOfParent),
            ("Files", true) => items.OrderBy(x => x.TotalFileCount),
            ("Files", false) => items.OrderByDescending(x => x.TotalFileCount),
            ("Modified", true) => items.OrderBy(x => x.LastModified),
            ("Modified", false) => items.OrderByDescending(x => x.LastModified),
            _ => items.OrderByDescending(x => x.SizeBytes)
        };

        foreach (var item in items)
        {
            DisplayNodes.Add(item);
        }
    }

    private void ExecuteOpenInExplorer()
    {
        if (SelectedNode == null || string.IsNullOrEmpty(SelectedNode.FullPath)) return;

        try
        {
            var path = SelectedNode.FullPath;
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            else if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DriveAnalysisViewModel] Failed to open explorer: {ex.Message}");
        }
    }
}

