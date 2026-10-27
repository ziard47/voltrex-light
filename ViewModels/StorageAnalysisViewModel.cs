using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using voltrex_light.Helpers;
using voltrex_light.Models;
using voltrex_light.Services;

namespace voltrex_light.ViewModels;

/// <summary>
/// Unified ViewModel for storage, drive, and folder analysis.
/// Supports hierarchical drill-down into subfolders, interactive breadcrumbs, sorting, filtering, and native shell commands.
/// </summary>
public class StorageAnalysisViewModel : ObservableObject
{
    private readonly IScanService _scanService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IExportService _exportService;

    private FileSystemNode? _rootNode;
    private FileSystemNode? _currentDirectoryNode;
    private FileSystemNode? _selectedNode;

    private string _filterQuery = string.Empty;
    private string _currentSortColumn = "Size";
    private bool _isSortAscending = false;

    private bool _hasWarnings;
    private string _warningsSummary = string.Empty;
    private bool _isPartialScan;
    private TimeSpan _elapsedTime = TimeSpan.Zero;
    private int _skippedItemsCount;

    public ObservableCollection<FileSystemNode> DisplayNodes { get; } = new();
    public ObservableCollection<FileSystemNode> BreadcrumbNodes { get; } = new();

    public FileSystemNode? RootNode
    {
        get => _rootNode;
        set
        {
            if (SetProperty(ref _rootNode, value))
            {
                CurrentDirectoryNode = value;
                OnPropertyChanged(nameof(HasData));
                OnPropertyChanged(nameof(TotalSizeFormatted));
                OnPropertyChanged(nameof(TotalFilesFormatted));
                OnPropertyChanged(nameof(TotalFoldersFormatted));
                OnPropertyChanged(nameof(ShowScanningOverlay));
                OnPropertyChanged(nameof(ShowScanningBanner));
                OnPropertyChanged(nameof(ShowWorkspace));
                OnPropertyChanged(nameof(ShowEmptyState));
                ExportCsvCommand.RaiseCanExecuteChanged();
                ExportJsonCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public FileSystemNode? CurrentDirectoryNode
    {
        get => _currentDirectoryNode;
        set
        {
            if (SetProperty(ref _currentDirectoryNode, value))
            {
                UpdateBreadcrumbs();
                ApplyFilterAndSort();
                OnPropertyChanged(nameof(CurrentFolderPath));
                OnPropertyChanged(nameof(CanNavigateUp));
                OnPropertyChanged(nameof(CurrentDirectoryName));
                OnPropertyChanged(nameof(CurrentDirectorySizeFormatted));
                OnPropertyChanged(nameof(CurrentDirectoryItemsCount));
                NavigateUpCommand.RaiseCanExecuteChanged();

                // By default select first item in current directory if available
                SelectedNode = _currentDirectoryNode?.Children.FirstOrDefault() ?? _currentDirectoryNode;
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
                OnPropertyChanged(nameof(SelectedNodeIsDirectory));
                OpenSelectedCommand.RaiseCanExecuteChanged();
                OpenInExplorerCommand.RaiseCanExecuteChanged();
                OpenWithCommand.RaiseCanExecuteChanged();
                ShowPropertiesCommand.RaiseCanExecuteChanged();
                CopyPathCommand.RaiseCanExecuteChanged();
                CopyFileNameCommand.RaiseCanExecuteChanged();
                ScanFromHereCommand.RaiseCanExecuteChanged();
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
    public bool SelectedNodeIsDirectory => SelectedNode?.IsDirectory ?? false;
    public bool CanNavigateUp => CurrentDirectoryNode != null && CurrentDirectoryNode.Parent != null && CurrentDirectoryNode != RootNode;

    public bool IsScanning => _scanService.Progress.IsScanning;
    public bool ShowScanningOverlay => IsScanning && !HasData;
    public bool ShowScanningBanner => IsScanning && HasData;
    public bool ShowWorkspace => HasData;
    public bool ShowEmptyState => !HasData && !IsScanning;

    public string ScanningCurrentPath => _scanService.Progress.CurrentPath;
    public string ScanningStatusMessage => _scanService.Progress.StatusMessage;
    public string ScanningItemsFormatted => _scanService.Progress.FormattedItemsCount;
    public string ScanningBytesFormatted => FileSystemFormatter.FormatBytes(_scanService.Progress.TotalBytesScanned);
    public string ScanningElapsedFormatted => _scanService.Progress.FormattedElapsedTime;

    public string CurrentFolderPath => CurrentDirectoryNode?.FullPath ?? RootNode?.FullPath ?? string.Empty;
    public string CurrentDirectoryName => CurrentDirectoryNode?.Name ?? "Storage Scope";
    public string CurrentDirectorySizeFormatted => CurrentDirectoryNode != null ? CurrentDirectoryNode.FormattedSize : "0 B";
    public int CurrentDirectoryItemsCount => CurrentDirectoryNode?.Children.Count ?? 0;

    public string TotalSizeFormatted => RootNode != null ? RootNode.FormattedSize : "0 B";
    public string TotalFilesFormatted => RootNode != null ? RootNode.TotalFileCount.ToString("N0") : "0";
    public string TotalFoldersFormatted => RootNode != null ? RootNode.TotalSubfolderCount.ToString("N0") : "0";
    public string FormattedElapsedTime => ElapsedTime.TotalSeconds < 1
        ? $"{ElapsedTime.TotalMilliseconds:F0} ms"
        : $"{ElapsedTime.TotalSeconds:F1} s";

    public RelayCommand<FileSystemNode> NavigateIntoCommand { get; }
    public RelayCommand NavigateUpCommand { get; }
    public RelayCommand<FileSystemNode> NavigateToBreadcrumbCommand { get; }
    public RelayCommand OpenSelectedCommand { get; }
    public RelayCommand OpenInExplorerCommand { get; }
    public RelayCommand OpenWithCommand { get; }
    public RelayCommand ShowPropertiesCommand { get; }
    public RelayCommand CopyPathCommand { get; }
    public RelayCommand CopyFileNameCommand { get; }
    public AsyncRelayCommand ScanFromHereCommand { get; }
    public AsyncRelayCommand PickFolderCommand { get; }
    public RelayCommand<string> SortCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand ExportJsonCommand { get; }
    public RelayCommand CancelScanCommand { get; }

    public StorageAnalysisViewModel(
        IScanService scanService,
        IFolderPickerService folderPickerService,
        IExportService? exportService = null)
    {
        _scanService = scanService;
        _folderPickerService = folderPickerService;
        _exportService = exportService ?? new ExportService();

        _scanService.ScanCompleted += OnScanCompleted;

        NavigateIntoCommand = new RelayCommand<FileSystemNode>(ExecuteNavigateInto);
        NavigateUpCommand = new RelayCommand(ExecuteNavigateUp, () => CanNavigateUp);
        NavigateToBreadcrumbCommand = new RelayCommand<FileSystemNode>(ExecuteNavigateToBreadcrumb);
        OpenSelectedCommand = new RelayCommand(ExecuteOpenSelected, () => HasSelection);
        OpenInExplorerCommand = new RelayCommand(ExecuteOpenInExplorer, () => HasSelection);
        OpenWithCommand = new RelayCommand(ExecuteOpenWith, () => HasSelection && !SelectedNodeIsDirectory);
        ShowPropertiesCommand = new RelayCommand(ExecuteShowProperties, () => HasSelection);
        CopyPathCommand = new RelayCommand(ExecuteCopyPath, () => HasSelection);
        CopyFileNameCommand = new RelayCommand(ExecuteCopyFileName, () => HasSelection);
        ScanFromHereCommand = new AsyncRelayCommand(ExecuteScanFromHereAsync, () => HasSelection && SelectedNodeIsDirectory);
        PickFolderCommand = new AsyncRelayCommand(ExecutePickFolderAsync);
        SortCommand = new RelayCommand<string>(SortByColumn);
        ExportCsvCommand = new RelayCommand(async () => await ExecuteExportAsync("csv"), () => HasData);
        ExportJsonCommand = new RelayCommand(async () => await ExecuteExportAsync("json"), () => HasData);
        CancelScanCommand = new RelayCommand(() => _scanService.CancelScan(), () => _scanService.IsScanning);

        _scanService.Progress.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ScanProgressInfo.IsScanning))
            {
                OnPropertyChanged(nameof(IsScanning));
                OnPropertyChanged(nameof(ShowScanningOverlay));
                OnPropertyChanged(nameof(ShowScanningBanner));
                OnPropertyChanged(nameof(ShowWorkspace));
                OnPropertyChanged(nameof(ShowEmptyState));
                CancelScanCommand.NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(ScanProgressInfo.CurrentPath))
            {
                OnPropertyChanged(nameof(ScanningCurrentPath));
            }
            else if (e.PropertyName == nameof(ScanProgressInfo.FilesProcessed) ||
                     e.PropertyName == nameof(ScanProgressInfo.DirectoriesProcessed))
            {
                OnPropertyChanged(nameof(ScanningItemsFormatted));
            }
            else if (e.PropertyName == nameof(ScanProgressInfo.TotalBytesScanned))
            {
                OnPropertyChanged(nameof(ScanningBytesFormatted));
            }
            else if (e.PropertyName == nameof(ScanProgressInfo.ElapsedTime))
            {
                OnPropertyChanged(nameof(ScanningElapsedFormatted));
            }
        };

        // Load existing scan result if already present
        if (_scanService.LastResult != null)
        {
            OnScanCompleted(this, _scanService.LastResult);
        }
    }

    private void OnScanCompleted(object? sender, ScanResult result)
    {
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(ShowScanningOverlay));
        OnPropertyChanged(nameof(ShowScanningBanner));
        OnPropertyChanged(nameof(ShowWorkspace));
        OnPropertyChanged(nameof(ShowEmptyState));

        if (result != null)
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
        }
    }

    public void ExecuteNavigateInto(FileSystemNode? node)
    {
        if (node != null && node.IsDirectory)
        {
            CurrentDirectoryNode = node;
        }
    }

    public void ExecuteNavigateUp()
    {
        if (CanNavigateUp && CurrentDirectoryNode?.Parent != null)
        {
            CurrentDirectoryNode = CurrentDirectoryNode.Parent;
        }
    }

    private void ExecuteNavigateToBreadcrumb(FileSystemNode? node)
    {
        if (node != null && node.IsDirectory)
        {
            CurrentDirectoryNode = node;
        }
    }

    public void ExecuteOpenSelected()
    {
        if (SelectedNode == null) return;

        if (SelectedNode.IsDirectory)
        {
            ExecuteNavigateInto(SelectedNode);
        }
        else
        {
            WindowsShellHelper.OpenFile(SelectedNode.FullPath);
        }
    }

    private void ExecuteOpenInExplorer()
    {
        if (SelectedNode != null)
        {
            WindowsShellHelper.OpenInExplorer(SelectedNode.FullPath);
        }
    }

    private void ExecuteOpenWith()
    {
        if (SelectedNode != null && !SelectedNode.IsDirectory)
        {
            WindowsShellHelper.ShowOpenWithDialog(SelectedNode.FullPath);
        }
    }

    private void ExecuteShowProperties()
    {
        if (SelectedNode != null)
        {
            WindowsShellHelper.ShowFileProperties(SelectedNode.FullPath);
        }
    }

    private void ExecuteCopyPath()
    {
        if (SelectedNode != null)
        {
            WindowsShellHelper.CopyToClipboard(SelectedNode.FullPath);
        }
    }

    private void ExecuteCopyFileName()
    {
        if (SelectedNode != null)
        {
            WindowsShellHelper.CopyToClipboard(SelectedNode.Name);
        }
    }

    private async Task ExecuteScanFromHereAsync()
    {
        if (SelectedNode != null && SelectedNode.IsDirectory)
        {
            await _scanService.ScanPathAsync(SelectedNode.FullPath);
        }
    }

    private async Task ExecutePickFolderAsync()
    {
        var folder = await _folderPickerService.PickFolderAsync();
        if (!string.IsNullOrEmpty(folder))
        {
            await _scanService.ScanPathAsync(folder);
        }
    }

    private async Task ExecuteExportAsync(string format)
    {
        var result = _scanService.LastResult;
        if (result == null && RootNode != null)
        {
            result = new ScanResult
            {
                RootPath = CurrentFolderPath,
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

    public bool RemoveNodeFromTree(FileSystemNode node)
    {
        var parent = node.Parent;
        if (parent == null) return false;

        parent.Children.Remove(node);
        DisplayNodes.Remove(node);

        // Deduct sizes and counts from ancestors
        long removedBytes = node.SizeBytes;
        long removedFiles = node.IsDirectory ? node.TotalFileCount : 1;
        long removedDirs = node.IsDirectory ? node.TotalSubfolderCount + 1 : 0;

        var curr = parent;
        while (curr != null)
        {
            curr.SizeBytes = Math.Max(0, curr.SizeBytes - removedBytes);
            curr.TotalFileCount = Math.Max(0, curr.TotalFileCount - removedFiles);
            curr.TotalSubfolderCount = Math.Max(0, curr.TotalSubfolderCount - removedDirs);
            curr = curr.Parent;
        }

        // Recompute parent percentages for siblings
        long parentSize = parent.SizeBytes;
        foreach (var c in parent.Children)
        {
            c.PercentOfParent = parentSize > 0 ? ((double)c.SizeBytes / parentSize) * 100.0 : 0.0;
        }

        if (RootNode != null && RootNode.SizeBytes > 0)
        {
            CalculateRootPercentagesRecursive(RootNode, RootNode.SizeBytes);
        }

        OnPropertyChanged(nameof(CurrentDirectorySizeFormatted));
        OnPropertyChanged(nameof(CurrentDirectoryItemsCount));
        OnPropertyChanged(nameof(TotalSizeFormatted));
        OnPropertyChanged(nameof(TotalFilesFormatted));
        OnPropertyChanged(nameof(TotalFoldersFormatted));

        SelectedNode = DisplayNodes.FirstOrDefault() ?? CurrentDirectoryNode;
        ApplyFilterAndSort();
        return true;
    }

    private static void CalculateRootPercentagesRecursive(FileSystemNode node, long rootTotalSize)
    {
        if (rootTotalSize > 0)
        {
            node.PercentOfRoot = ((double)node.SizeBytes / rootTotalSize) * 100.0;
        }
        else
        {
            node.PercentOfRoot = 0.0;
        }

        foreach (var child in node.Children)
        {
            CalculateRootPercentagesRecursive(child, rootTotalSize);
        }
    }

    private void UpdateBreadcrumbs()
    {
        BreadcrumbNodes.Clear();
        if (CurrentDirectoryNode == null) return;

        var stack = new List<FileSystemNode>();
        var curr = CurrentDirectoryNode;
        while (curr != null)
        {
            stack.Add(curr);
            if (curr == RootNode) break;
            curr = curr.Parent;
        }

        stack.Reverse();
        foreach (var item in stack)
        {
            BreadcrumbNodes.Add(item);
        }
    }

    public void SetPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        if (RootNode != null)
        {
            var match = FindNodeByPath(RootNode, path);
            if (match != null)
            {
                if (match.IsDirectory)
                {
                    CurrentDirectoryNode = match;
                }
                else
                {
                    if (match.Parent != null)
                    {
                        CurrentDirectoryNode = match.Parent;
                    }
                    SelectedNode = match;
                }
                return;
            }
        }

        // Path is not in the active scan tree; trigger a fresh scan
        _ = _scanService.ScanPathAsync(path);
    }

    private FileSystemNode? FindNodeByPath(FileSystemNode current, string path)
    {
        if (string.Equals(current.FullPath, path, StringComparison.OrdinalIgnoreCase))
            return current;

        foreach (var child in current.Children)
        {
            if (string.Equals(child.FullPath, path, StringComparison.OrdinalIgnoreCase))
                return child;

            if (path.StartsWith(child.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                var match = FindNodeByPath(child, path);
                if (match != null) return match;
            }
        }
        return null;
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
            IsSortAscending = column.Equals("Name", StringComparison.OrdinalIgnoreCase);
        }

        ApplyFilterAndSort();
    }

    public void ApplyFilterAndSort()
    {
        DisplayNodes.Clear();
        var targetNode = CurrentDirectoryNode ?? RootNode;
        if (targetNode == null) return;

        var items = targetNode.Children.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterQuery))
        {
            var query = FilterQuery.Trim();
            items = items.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     c.Extension.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        items = (CurrentSortColumn, IsSortAscending) switch
        {
            ("Name", true) => items.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            ("Name", false) => items.OrderByDescending(x => x.Name, StringComparer.OrdinalIgnoreCase),
            ("Size", true) => items.OrderBy(x => x.SizeBytes),
            ("Size", false) => items.OrderByDescending(x => x.SizeBytes),
            ("PercentParent", true) => items.OrderBy(x => x.PercentOfParent),
            ("PercentParent", false) => items.OrderByDescending(x => x.PercentOfParent),
            ("PercentRoot", true) => items.OrderBy(x => x.PercentOfRoot),
            ("PercentRoot", false) => items.OrderByDescending(x => x.PercentOfRoot),
            ("Files", true) => items.OrderBy(x => x.TotalFileCount),
            ("Files", false) => items.OrderByDescending(x => x.TotalFileCount),
            ("Folders", true) => items.OrderBy(x => x.TotalSubfolderCount),
            ("Folders", false) => items.OrderByDescending(x => x.TotalSubfolderCount),
            ("Modified", true) => items.OrderBy(x => x.LastModified),
            ("Modified", false) => items.OrderByDescending(x => x.LastModified),
            _ => items.OrderByDescending(x => x.SizeBytes)
        };

        foreach (var item in items)
        {
            DisplayNodes.Add(item);
        }
    }
}

