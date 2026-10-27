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
/// ViewModel for Largest Files view identifying heavy disk consumers across scanned trees.
/// Provides rich filtering by category, size threshold, text search, and safe shell navigation.
/// </summary>
public class LargestFilesViewModel : ObservableObject
{
    private readonly IScanService _scanService;
    private readonly INavigationService _navigationService;

    private FileSystemNode? _selectedFile;
    private string _filterQuery = string.Empty;
    private string _selectedCategory = "All";
    private string _selectedSizeFilter = "All";
    private string _selectedSort = "Size (Largest first)";
    private int _limit = 100;

    private string _currentScopeDescription = "No active scan";
    private bool _isPartialScan;
    private string _partialScanNotice = string.Empty;
    private bool _hasWarnings;
    private int _warningsCount;
    private long _totalScannedFiles;
    private long _totalScannedBytes;

    private readonly List<FileSystemNode> _allFiles = new();

    public ObservableCollection<FileSystemNode> Files { get; } = new();

    public FileSystemNode? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                OpenFileCommand.RaiseCanExecuteChanged();
                OpenInExplorerCommand.RaiseCanExecuteChanged();
                NavigateToFolderCommand.RaiseCanExecuteChanged();
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
                ApplyFilter();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SelectedSizeFilter
    {
        get => _selectedSizeFilter;
        set
        {
            if (SetProperty(ref _selectedSizeFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (SetProperty(ref _selectedSort, value))
            {
                ApplyFilter();
            }
        }
    }

    public int Limit
    {
        get => _limit;
        set
        {
            if (SetProperty(ref _limit, value))
            {
                ApplyFilter();
            }
        }
    }

    public string CurrentScopeDescription
    {
        get => _currentScopeDescription;
        private set => SetProperty(ref _currentScopeDescription, value);
    }

    public bool IsPartialScan
    {
        get => _isPartialScan;
        private set => SetProperty(ref _isPartialScan, value);
    }

    public string PartialScanNotice
    {
        get => _partialScanNotice;
        private set => SetProperty(ref _partialScanNotice, value);
    }

    public bool HasWarnings
    {
        get => _hasWarnings;
        private set => SetProperty(ref _hasWarnings, value);
    }

    public int WarningsCount
    {
        get => _warningsCount;
        private set => SetProperty(ref _warningsCount, value);
    }

    public long TotalScannedFiles
    {
        get => _totalScannedFiles;
        private set => SetProperty(ref _totalScannedFiles, value);
    }

    public long TotalScannedBytes
    {
        get => _totalScannedBytes;
        private set
        {
            if (SetProperty(ref _totalScannedBytes, value))
            {
                OnPropertyChanged(nameof(FormattedTotalScannedSize));
            }
        }
    }

    public string FormattedTotalScannedSize => FileSystemFormatter.FormatBytes(TotalScannedBytes);

    public bool HasFiles => _allFiles.Count > 0;
    public bool HasFilteredFiles => Files.Count > 0;
    public bool HasSelection => SelectedFile != null;

    public string NoResultsMessage
    {
        get
        {
            if (!HasFiles)
            {
                return "Perform a drive or folder scan to index and display top space consumers.";
            }
            return "No files match the active filters. Try clearing your search or adjusting thresholds.";
        }
    }

    public string[] AvailableCategories { get; } =
    {
        "All",
        "Documents",
        "Videos",
        "Audio",
        "Images",
        "Archives",
        "Executables",
        "Code",
        "Other"
    };

    public string[] AvailableSizeFilters { get; } =
    {
        "All",
        "> 1 MB",
        "> 10 MB",
        "> 50 MB",
        "> 100 MB",
        "> 500 MB",
        "> 1 GB"
    };

    public string[] AvailableSorts { get; } =
    {
        "Size (Largest first)",
        "Size (Smallest first)",
        "Name (A-Z)",
        "Name (Z-A)",
        "Modified (Newest first)",
        "Modified (Oldest first)",
        "Extension (A-Z)"
    };

    public RelayCommand OpenFileCommand { get; }
    public RelayCommand OpenInExplorerCommand { get; }
    public RelayCommand NavigateToFolderCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }

    public LargestFilesViewModel(IScanService scanService, INavigationService navigationService)
    {
        _scanService = scanService;
        _navigationService = navigationService;

        OpenFileCommand = new RelayCommand(ExecuteOpenFile, () => HasSelection);
        OpenInExplorerCommand = new RelayCommand(ExecuteOpenInExplorer, () => HasSelection);
        NavigateToFolderCommand = new RelayCommand(ExecuteNavigateToFolder, () => HasSelection);
        ClearFiltersCommand = new RelayCommand(ExecuteClearFilters);

        _scanService.ScanCompleted += OnScanCompleted;

        // If a scan was already completed before this ViewModel was instantiated, load it
        if (_scanService.LastResult != null)
        {
            OnScanCompleted(this, _scanService.LastResult);
        }
    }

    private void OnScanCompleted(object? sender, ScanResult result)
    {
        _allFiles.Clear();

        if (result.RootNode != null)
        {
            CollectFiles(result.RootNode, _allFiles);
        }
        else if (result.LargestFiles.Count > 0)
        {
            _allFiles.AddRange(result.LargestFiles);
        }

        UpdateScopeInfo(result);
        ApplyFilter();
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(NoResultsMessage));
    }

    private void UpdateScopeInfo(ScanResult result)
    {
        TotalScannedFiles = result.TotalFilesCount;
        TotalScannedBytes = result.TotalBytesScanned;
        IsPartialScan = result.IsPartial;
        WarningsCount = result.SkippedCount;
        HasWarnings = result.Warnings.Count > 0;

        string root = string.IsNullOrEmpty(result.RootPath) ? "Unknown Scope" : result.RootPath;
        bool isDriveRoot = root.Length <= 3 && root.EndsWith(":\\", StringComparison.Ordinal);

        if (isDriveRoot)
        {
            CurrentScopeDescription = $"Scope: Drive '{root}' ({TotalScannedFiles:N0} files indexed)";
        }
        else
        {
            CurrentScopeDescription = $"Scope: Folder '{root}' ({TotalScannedFiles:N0} files analyzed — not entire drive)";
        }

        if (result.IsPartial)
        {
            PartialScanNotice = $"Notice: Partial scan. {result.SkippedCount} items skipped due to Windows access permissions. Results reflect the largest accessible items.";
        }
        else
        {
            PartialScanNotice = string.Empty;
        }

        OnPropertyChanged(nameof(CurrentScopeDescription));
        OnPropertyChanged(nameof(IsPartialScan));
        OnPropertyChanged(nameof(PartialScanNotice));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(WarningsCount));
    }

    private void CollectFiles(FileSystemNode node, List<FileSystemNode> result)
    {
        if (!node.IsDirectory)
        {
            result.Add(node);
        }

        foreach (var child in node.Children)
        {
            CollectFiles(child, result);
        }
    }

    public void ApplyFilter()
    {
        Files.Clear();

        var query = _allFiles.AsEnumerable();

        // 1. Text Search (name or full path or extension)
        if (!string.IsNullOrWhiteSpace(FilterQuery))
        {
            var f = FilterQuery.Trim();
            query = query.Where(file =>
                file.Name.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                file.FullPath.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                file.Extension.Contains(f, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Category Filter
        if (!string.IsNullOrEmpty(SelectedCategory) && !SelectedCategory.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(file => MatchesCategory(file.Extension, SelectedCategory));
        }

        // 3. Minimum Size Filter
        long minBytes = SelectedSizeFilter switch
        {
            "> 1 MB" => 1024L * 1024L,
            "> 10 MB" => 10L * 1024L * 1024L,
            "> 50 MB" => 50L * 1024L * 1024L,
            "> 100 MB" => 100L * 1024L * 1024L,
            "> 500 MB" => 500L * 1024L * 1024L,
            "> 1 GB" => 1024L * 1024L * 1024L,
            _ => 0L
        };

        if (minBytes > 0)
        {
            query = query.Where(f => f.SizeBytes >= minBytes);
        }

        // 4. Sorting
        query = SelectedSort switch
        {
            "Size (Smallest first)" => query.OrderBy(f => f.SizeBytes),
            "Name (A-Z)" => query.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase),
            "Name (Z-A)" => query.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase),
            "Modified (Newest first)" => query.OrderByDescending(f => f.LastModified),
            "Modified (Oldest first)" => query.OrderBy(f => f.LastModified),
            "Extension (A-Z)" => query.OrderBy(f => f.Extension, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderByDescending(f => f.SizeBytes) // Default: Size (Largest first)
        };

        var topFiles = query.Take(Limit).ToList();

        long maxSizeBytes = topFiles.FirstOrDefault()?.SizeBytes ?? 1;
        foreach (var file in topFiles)
        {
            file.PercentOfParent = maxSizeBytes > 0 ? ((double)file.SizeBytes / maxSizeBytes) * 100.0 : 0.0;
            Files.Add(file);
        }

        OnPropertyChanged(nameof(HasFilteredFiles));
        OnPropertyChanged(nameof(NoResultsMessage));
    }

    private void ExecuteClearFilters()
    {
        FilterQuery = string.Empty;
        SelectedCategory = "All";
        SelectedSizeFilter = "All";
        SelectedSort = "Size (Largest first)";
    }

    private void ExecuteOpenFile()
    {
        if (SelectedFile == null || string.IsNullOrEmpty(SelectedFile.FullPath)) return;

        try
        {
            var path = SelectedFile.FullPath;
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LargestFilesViewModel] Open file failed: {ex.Message}");
        }
    }

    private void ExecuteOpenInExplorer()
    {
        if (SelectedFile == null || string.IsNullOrEmpty(SelectedFile.FullPath)) return;

        try
        {
            var path = SelectedFile.FullPath;
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LargestFilesViewModel] Open explorer failed: {ex.Message}");
        }
    }

    private void ExecuteNavigateToFolder()
    {
        if (SelectedFile == null || string.IsNullOrEmpty(SelectedFile.FullPath)) return;

        try
        {
            string? dir = Path.GetDirectoryName(SelectedFile.FullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                _navigationService.NavigateTo("StorageAnalysis", dir);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LargestFilesViewModel] Navigate to folder failed: {ex.Message}");
        }
    }

    private static bool MatchesCategory(string extension, string category)
    {
        if (string.IsNullOrEmpty(extension)) return category.Equals("Other", StringComparison.OrdinalIgnoreCase);

        return category switch
        {
            "Documents" => DocumentExtensions.Contains(extension),
            "Videos" => VideoExtensions.Contains(extension),
            "Audio" => AudioExtensions.Contains(extension),
            "Images" => ImageExtensions.Contains(extension),
            "Archives" => ArchiveExtensions.Contains(extension),
            "Executables" => ExecutableExtensions.Contains(extension),
            "Code" => CodeExtensions.Contains(extension),
            "Other" => !IsKnownCategory(extension),
            _ => true
        };
    }

    private static bool IsKnownCategory(string ext)
    {
        return DocumentExtensions.Contains(ext) ||
               VideoExtensions.Contains(ext) ||
               AudioExtensions.Contains(ext) ||
               ImageExtensions.Contains(ext) ||
               ArchiveExtensions.Contains(ext) ||
               ExecutableExtensions.Contains(ext) ||
               CodeExtensions.Contains(ext);
    }

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".txt", ".csv", ".md", ".rtf", ".odt", ".ods", ".odp", ".epub"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".alac", ".aiff"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".svg", ".ico", ".heic", ".raw", ".psd"
    };

    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso", ".cab", ".dmg", ".tgz", ".wim"
    };

    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".msi", ".sys", ".bin", ".appx", ".msix", ".com", ".scr"
    };

    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".cpp", ".c", ".h", ".hpp", ".js", ".ts", ".jsx", ".tsx", ".py", ".java", ".go", ".rs", ".rb", ".php", ".html", ".css", ".xml", ".json", ".yaml", ".yml", ".sql", ".sh", ".ps1", ".bat", ".cmd"
    };
}

