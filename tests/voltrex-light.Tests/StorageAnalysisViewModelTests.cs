using System;
using System.Collections.Generic;
using voltrex_light.Models;
using voltrex_light.Services;
using voltrex_light.ViewModels;

namespace voltrex_light.Tests;

[TestClass]
public class StorageAnalysisViewModelTests
{
    private class MockScanService : IScanService
    {
        public bool IsScanning => Progress.IsScanning;
        public ScanProgressInfo Progress { get; } = new();
        public ScanResult? LastResult { get; set; }
        public string? LastScannedPath { get; private set; }

        public event EventHandler<ScanResult>? ScanCompleted;

        public Task<ScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken = default)
        {
            LastScannedPath = options.RootPath;
            return Task.FromResult(LastResult ?? new ScanResult());
        }

        public Task<FileSystemNode?> ScanPathAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            LastScannedPath = rootPath;
            return Task.FromResult(LastResult?.RootNode);
        }

        public void CancelScan() { }

        public void FireScanCompleted(ScanResult result)
        {
            LastResult = result;
            ScanCompleted?.Invoke(this, result);
        }
    }

    private class MockFolderPicker : IFolderPickerService
    {
        public void SetWindowHandle(IntPtr windowHandle) { }
        public Task<string?> PickFolderAsync() => Task.FromResult<string?>("C:\\Selected");
    }

    [TestMethod]
    public void StorageAnalysisViewModel_DrillDownAndUp_UpdatesStateCorrectly()
    {
        var mockScan = new MockScanService();
        var mockPicker = new MockFolderPicker();

        var root = new FileSystemNode
        {
            Name = "C:\\",
            FullPath = "C:\\",
            IsDirectory = true,
            SizeBytes = 10_000_000
        };

        var subFolder = new FileSystemNode
        {
            Name = "Users",
            FullPath = "C:\\Users",
            IsDirectory = true,
            SizeBytes = 6_000_000,
            Parent = root
        };

        var nestedFolder = new FileSystemNode
        {
            Name = "Alex",
            FullPath = "C:\\Users\\Alex",
            IsDirectory = true,
            SizeBytes = 4_000_000,
            Parent = subFolder
        };

        var fileInAlex = new FileSystemNode
        {
            Name = "document.docx",
            FullPath = "C:\\Users\\Alex\\document.docx",
            IsDirectory = false,
            SizeBytes = 2_000_000,
            Parent = nestedFolder
        };

        nestedFolder.Children.Add(fileInAlex);
        subFolder.Children.Add(nestedFolder);
        root.Children.Add(subFolder);

        var result = new ScanResult
        {
            RootPath = "C:\\",
            RootNode = root,
            Status = ScanStatus.Completed,
            TotalBytesScanned = root.SizeBytes,
            TotalFilesCount = 1,
            TotalDirectoriesCount = 2
        };

        mockScan.LastResult = result;
        var vm = new StorageAnalysisViewModel(mockScan, mockPicker);

        // Initial state at root
        Assert.AreEqual(root, vm.CurrentDirectoryNode);
        Assert.AreEqual(1, vm.DisplayNodes.Count);
        Assert.AreEqual("Users", vm.DisplayNodes[0].Name);
        Assert.IsFalse(vm.CanNavigateUp);
        Assert.AreEqual(1, vm.BreadcrumbNodes.Count);

        // Drill down into "Users"
        vm.ExecuteNavigateInto(subFolder);
        Assert.AreEqual(subFolder, vm.CurrentDirectoryNode);
        Assert.AreEqual(1, vm.DisplayNodes.Count);
        Assert.AreEqual("Alex", vm.DisplayNodes[0].Name);
        Assert.IsTrue(vm.CanNavigateUp);
        Assert.AreEqual(2, vm.BreadcrumbNodes.Count);

        // Drill down into "Alex"
        vm.ExecuteNavigateInto(nestedFolder);
        Assert.AreEqual(nestedFolder, vm.CurrentDirectoryNode);
        Assert.AreEqual(1, vm.DisplayNodes.Count);
        Assert.AreEqual("document.docx", vm.DisplayNodes[0].Name);
        Assert.IsTrue(vm.CanNavigateUp);
        Assert.AreEqual(3, vm.BreadcrumbNodes.Count);

        // Navigate Up
        vm.ExecuteNavigateUp();
        Assert.AreEqual(subFolder, vm.CurrentDirectoryNode);
        Assert.AreEqual(1, vm.DisplayNodes.Count);
        Assert.AreEqual("Alex", vm.DisplayNodes[0].Name);

        // Breadcrumb jump to root
        vm.NavigateToBreadcrumbCommand.Execute(root);
        Assert.AreEqual(root, vm.CurrentDirectoryNode);
        Assert.IsFalse(vm.CanNavigateUp);
        Assert.AreEqual(1, vm.BreadcrumbNodes.Count);
    }

    [TestMethod]
    public void StorageAnalysisViewModel_SetPath_SelectsDeepChild()
    {
        var mockScan = new MockScanService();
        var mockPicker = new MockFolderPicker();

        var root = new FileSystemNode { Name = "C:\\", FullPath = "C:\\", IsDirectory = true };
        var folderA = new FileSystemNode { Name = "FolderA", FullPath = "C:\\FolderA", IsDirectory = true, Parent = root };
        var folderB = new FileSystemNode { Name = "FolderB", FullPath = "C:\\FolderA\\FolderB", IsDirectory = true, Parent = folderA };
        folderA.Children.Add(folderB);
        root.Children.Add(folderA);

        mockScan.LastResult = new ScanResult { RootPath = "C:\\", RootNode = root };
        var vm = new StorageAnalysisViewModel(mockScan, mockPicker);

        vm.SetPath("C:\\FolderA\\FolderB");
        Assert.AreEqual(folderB, vm.CurrentDirectoryNode);
    }

    [TestMethod]
    public void StorageAnalysisViewModel_RemoveNodeFromTree_UpdatesSizesAndHierarchy()
    {
        var mockScan = new MockScanService();
        var mockPicker = new MockFolderPicker();

        var root = new FileSystemNode { Name = "C:\\", FullPath = "C:\\", IsDirectory = true, SizeBytes = 1000, TotalFileCount = 2 };
        var fileA = new FileSystemNode { Name = "fileA.txt", FullPath = "C:\\fileA.txt", IsDirectory = false, SizeBytes = 400, Parent = root };
        var fileB = new FileSystemNode { Name = "fileB.txt", FullPath = "C:\\fileB.txt", IsDirectory = false, SizeBytes = 600, Parent = root };
        root.Children.Add(fileA);
        root.Children.Add(fileB);

        mockScan.LastResult = new ScanResult { RootPath = "C:\\", RootNode = root, TotalBytesScanned = 1000, TotalFilesCount = 2 };
        var vm = new StorageAnalysisViewModel(mockScan, mockPicker);

        Assert.AreEqual(2, vm.DisplayNodes.Count);
        Assert.AreEqual(1000, root.SizeBytes);

        // Delete fileA
        bool removed = vm.RemoveNodeFromTree(fileA);
        Assert.IsTrue(removed);
        Assert.AreEqual(1, vm.DisplayNodes.Count);
        Assert.AreEqual("fileB.txt", vm.DisplayNodes[0].Name);
        Assert.AreEqual(600, root.SizeBytes);
        Assert.AreEqual(1, root.TotalFileCount);
        Assert.AreEqual(100.0, fileB.PercentOfParent, 0.01);
    }

    [TestMethod]
    public async Task StorageAnalysisViewModel_RefreshCommand_RescansCurrentTarget()
    {
        var mockScan = new MockScanService();
        var mockPicker = new MockFolderPicker();

        var root = new FileSystemNode { Name = "C:\\", FullPath = "C:\\", IsDirectory = true, SizeBytes = 500 };
        root.Children.Add(new FileSystemNode { Name = "test.txt", FullPath = "C:\\test.txt", IsDirectory = false, SizeBytes = 500, Parent = root });

        mockScan.LastResult = new ScanResult { RootPath = "C:\\", RootNode = root };
        var vm = new StorageAnalysisViewModel(mockScan, mockPicker);

        Assert.IsTrue(vm.CanRefresh);
        Assert.IsTrue(vm.RefreshCommand.CanExecute(null));

        await vm.RefreshCommand.ExecuteAsync();

        Assert.AreEqual("C:\\", mockScan.LastScannedPath);
    }

    [TestMethod]
    public void StorageAnalysisViewModel_ScanningFlags_UpdateAccurately()
    {
        var mockScan = new MockScanService();
        var mockPicker = new MockFolderPicker();
        var vm = new StorageAnalysisViewModel(mockScan, mockPicker);

        // Initially no data and idle
        Assert.IsFalse(vm.IsScanning);
        Assert.IsFalse(vm.ShowScanningOverlay);
        Assert.IsFalse(vm.ShowScanningBanner);
        Assert.IsFalse(vm.ShowWorkspace);
        Assert.IsTrue(vm.ShowEmptyState);

        // Start scanning initial target
        mockScan.Progress.Status = ScanStatus.Scanning;
        Assert.IsTrue(vm.IsScanning);
        Assert.IsTrue(vm.ShowScanningOverlay);
        Assert.IsFalse(vm.ShowScanningBanner);
        Assert.IsFalse(vm.ShowWorkspace);
        Assert.IsFalse(vm.ShowEmptyState);

        // Scan completes
        var root = new FileSystemNode { Name = "D:\\", FullPath = "D:\\", IsDirectory = true, SizeBytes = 1000 };
        root.Children.Add(new FileSystemNode { Name = "item.txt", FullPath = "D:\\item.txt", IsDirectory = false, SizeBytes = 1000, Parent = root });
        mockScan.Progress.Status = ScanStatus.Completed;
        mockScan.FireScanCompleted(new ScanResult { RootPath = "D:\\", RootNode = root, Status = ScanStatus.Completed });

        Assert.IsFalse(vm.IsScanning);
        Assert.IsFalse(vm.ShowScanningOverlay);
        Assert.IsFalse(vm.ShowScanningBanner);
        Assert.IsTrue(vm.ShowWorkspace);
        Assert.IsFalse(vm.ShowEmptyState);

        // Trigger rescan with existing data loaded
        mockScan.Progress.Status = ScanStatus.Scanning;
        Assert.IsTrue(vm.IsScanning);
        Assert.IsFalse(vm.ShowScanningOverlay);
        Assert.IsTrue(vm.ShowScanningBanner); // Top banner active
        Assert.IsTrue(vm.ShowWorkspace);      // Workspace remains visible
        Assert.IsFalse(vm.ShowEmptyState);
    }

    [TestMethod]
    public void StorageAnalysisViewModel_CancelledScan_PreservesExistingData()
    {
        var mockScan = new MockScanService();
        var mockPicker = new MockFolderPicker();

        var root = new FileSystemNode { Name = "C:\\", FullPath = "C:\\", IsDirectory = true, SizeBytes = 1000 };
        root.Children.Add(new FileSystemNode { Name = "item.txt", FullPath = "C:\\item.txt", IsDirectory = false, SizeBytes = 1000, Parent = root });
        mockScan.LastResult = new ScanResult { RootPath = "C:\\", RootNode = root, Status = ScanStatus.Completed };

        var vm = new StorageAnalysisViewModel(mockScan, mockPicker);
        Assert.AreEqual(root, vm.RootNode);
        Assert.IsTrue(vm.HasData);

        // Simulate cancellation of an active or duplicate scan
        mockScan.Progress.Status = ScanStatus.Cancelled;
        mockScan.FireScanCompleted(new ScanResult { RootPath = "C:\\", RootNode = null, Status = ScanStatus.Cancelled });

        // RootNode and data must be preserved, not wiped out
        Assert.AreEqual(root, vm.RootNode);
        Assert.IsTrue(vm.HasData);
    }
}

