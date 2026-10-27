using System;
using System.Collections.Generic;
using voltrex_light.Models;
using voltrex_light.Services;
using voltrex_light.ViewModels;

namespace voltrex_light.Tests;

[TestClass]
public class LargestFilesViewModelTests
{
    private class MockScanService : IScanService
    {
        public bool IsScanning => false;
        public ScanProgressInfo Progress { get; } = new();
        public ScanResult? LastResult { get; set; }

        public event EventHandler<ScanResult>? ScanCompleted;

        public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<FileSystemNode?> ScanPathAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public void CancelScan() { }

        public void FireScanCompleted(ScanResult result)
        {
            LastResult = result;
            ScanCompleted?.Invoke(this, result);
        }
    }

    private class MockNavigationService : INavigationService
    {
        public bool CanGoBack => false;
        public string CurrentPageKey { get; set; } = string.Empty;
        public string? LastNavigatedParameter { get; private set; }

        public event EventHandler<string>? Navigated;

        public void Initialize(object frame) { }
        public bool NavigateTo(string pageKey, object? parameter = null)
        {
            CurrentPageKey = pageKey;
            LastNavigatedParameter = parameter as string;
            Navigated?.Invoke(this, pageKey);
            return true;
        }
        public bool GoBack() => false;
    }

    [TestMethod]
    public void LargestFilesViewModel_IdentifiesDriveVsFolderScope()
    {
        var mockScan = new MockScanService();
        var mockNav = new MockNavigationService();
        var vm = new LargestFilesViewModel(mockScan, mockNav);

        // Drive scan
        var driveResult = new ScanResult
        {
            RootPath = "C:\\",
            Status = ScanStatus.Completed,
            TotalFilesCount = 1000,
            TotalBytesScanned = 10_000_000
        };

        mockScan.FireScanCompleted(driveResult);
        Assert.IsTrue(vm.CurrentScopeDescription.Contains("Drive 'C:\\'"));
        Assert.IsFalse(vm.IsPartialScan);

        // Subfolder scan
        var folderResult = new ScanResult
        {
            RootPath = "C:\\Users\\TestUser\\Downloads",
            Status = ScanStatus.CompletedWithWarnings,
            SkippedCount = 3,
            TotalFilesCount = 150,
            TotalBytesScanned = 5_000_000,
            Warnings = new List<ScanWarning>
            {
                new() { Path = "C:\\Users\\TestUser\\Downloads\\Locked", WarningType = ScanWarningType.LockedFile }
            }
        };

        mockScan.FireScanCompleted(folderResult);
        Assert.IsTrue(vm.CurrentScopeDescription.Contains("Folder 'C:\\Users\\TestUser\\Downloads'"));
        Assert.IsTrue(vm.CurrentScopeDescription.Contains("not entire drive"));
        Assert.IsTrue(vm.IsPartialScan);
        Assert.IsTrue(vm.PartialScanNotice.Contains("3 items skipped"));
    }

    [TestMethod]
    public void LargestFilesViewModel_FiltersByCategoryAndSize()
    {
        var mockScan = new MockScanService();
        var mockNav = new MockNavigationService();

        var root = new FileSystemNode { Name = "Root", FullPath = "C:\\Data", IsDirectory = true };
        var videoFile = new FileSystemNode
        {
            Name = "movie.mp4",
            FullPath = "C:\\Data\\movie.mp4",
            IsDirectory = false,
            SizeBytes = 700 * 1024 * 1024L, // 700 MB
            Extension = ".mp4",
            Parent = root
        };
        var docFile = new FileSystemNode
        {
            Name = "report.pdf",
            FullPath = "C:\\Data\\report.pdf",
            IsDirectory = false,
            SizeBytes = 5 * 1024 * 1024L, // 5 MB
            Extension = ".pdf",
            Parent = root
        };
        var tinyDoc = new FileSystemNode
        {
            Name = "notes.txt",
            FullPath = "C:\\Data\\notes.txt",
            IsDirectory = false,
            SizeBytes = 200 * 1024L, // 200 KB
            Extension = ".txt",
            Parent = root
        };

        root.Children.Add(videoFile);
        root.Children.Add(docFile);
        root.Children.Add(tinyDoc);

        var result = new ScanResult
        {
            RootPath = "C:\\Data",
            RootNode = root,
            Status = ScanStatus.Completed,
            TotalFilesCount = 3,
            TotalBytesScanned = videoFile.SizeBytes + docFile.SizeBytes + tinyDoc.SizeBytes
        };

        mockScan.LastResult = result;
        var vm = new LargestFilesViewModel(mockScan, mockNav);

        // Initially all files
        Assert.AreEqual(3, vm.Files.Count);

        // Filter by category: Videos
        vm.SelectedCategory = "Videos";
        Assert.AreEqual(1, vm.Files.Count);
        Assert.AreEqual("movie.mp4", vm.Files[0].Name);

        // Filter by category: Documents
        vm.SelectedCategory = "Documents";
        Assert.AreEqual(2, vm.Files.Count);

        // Filter by size: > 10 MB (should filter out notes.txt and report.pdf)
        vm.SelectedSizeFilter = "> 10 MB";
        Assert.AreEqual(0, vm.Files.Count);

        // Clear category, keep > 10 MB (should only have video)
        vm.SelectedCategory = "All";
        Assert.AreEqual(1, vm.Files.Count);
        Assert.AreEqual("movie.mp4", vm.Files[0].Name);

        // Clear filters command
        vm.ClearFiltersCommand.Execute(null);
        Assert.AreEqual(3, vm.Files.Count);
        Assert.AreEqual("All", vm.SelectedCategory);
        Assert.AreEqual("All", vm.SelectedSizeFilter);
    }

    [TestMethod]
    public void LargestFilesViewModel_NavigatesToContainingFolder()
    {
        var mockScan = new MockScanService();
        var mockNav = new MockNavigationService();

        var root = new FileSystemNode { Name = "Root", FullPath = "C:\\Data", IsDirectory = true };
        var file = new FileSystemNode
        {
            Name = "archive.zip",
            FullPath = "C:\\Data\\Subdir\\archive.zip",
            IsDirectory = false,
            SizeBytes = 100 * 1024 * 1024L,
            Extension = ".zip",
            Parent = root
        };
        root.Children.Add(file);

        var result = new ScanResult
        {
            RootPath = "C:\\Data",
            RootNode = root,
            Status = ScanStatus.Completed,
            TotalFilesCount = 1
        };

        mockScan.LastResult = result;
        var vm = new LargestFilesViewModel(mockScan, mockNav);

        vm.SelectedFile = file;
        Assert.IsTrue(vm.NavigateToFolderCommand.CanExecute(null));

        vm.NavigateToFolderCommand.Execute(null);

        Assert.AreEqual("StorageAnalysis", mockNav.CurrentPageKey);
        Assert.AreEqual("C:\\Data\\Subdir", mockNav.LastNavigatedParameter);
    }
}
