using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using voltrex_light.Models;
using voltrex_light.Services;
using voltrex_light.ViewModels;

namespace voltrex_light.Tests;

[TestClass]
public class ScanSortingTests
{
    private class DummyScanService : IScanService
    {
        public bool IsScanning => false;
        public ScanProgressInfo Progress { get; } = new();
        public ScanResult? LastResult { get; }
        public event EventHandler<ScanResult>? ScanCompleted { add { } remove { } }

        public System.Threading.Tasks.Task<ScanResult> ScanAsync(ScanOptions options, System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult(new ScanResult());

        public System.Threading.Tasks.Task<FileSystemNode?> ScanPathAsync(string rootPath, System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.FromResult<FileSystemNode?>(null);

        public void CancelScan() { }
    }

    private FileSystemNode CreateTestHierarchy()
    {
        var root = new FileSystemNode
        {
            Name = "Root",
            FullPath = @"C:\Root",
            IsDirectory = true,
            SizeBytes = 10000,
            TotalFileCount = 10
        };

        var childA = new FileSystemNode
        {
            Name = "Alpha",
            FullPath = @"C:\Root\Alpha",
            IsDirectory = true,
            SizeBytes = 1000,
            TotalFileCount = 2,
            LastModified = new DateTime(2025, 1, 1),
            Parent = root
        };

        var childB = new FileSystemNode
        {
            Name = "Beta",
            FullPath = @"C:\Root\Beta",
            IsDirectory = true,
            SizeBytes = 6000,
            TotalFileCount = 5,
            LastModified = new DateTime(2025, 3, 1),
            Parent = root
        };

        var childC = new FileSystemNode
        {
            Name = "Gamma",
            FullPath = @"C:\Root\Gamma",
            IsDirectory = true,
            SizeBytes = 3000,
            TotalFileCount = 3,
            LastModified = new DateTime(2025, 2, 1),
            Parent = root
        };

        root.Children.Add(childA);
        root.Children.Add(childB);
        root.Children.Add(childC);

        return root;
    }

    [TestMethod]
    public void FolderAnalysisViewModel_SortBySizeDescending_OrdersProperlyWithoutMutatingTotals()
    {
        var scanSvc = new DummyScanService();
        var vm = new FolderAnalysisViewModel(scanSvc);

        var root = CreateTestHierarchy();
        vm.RootNode = root;

        // Default sort is Size descending
        Assert.AreEqual(3, vm.DisplayNodes.Count);
        Assert.AreEqual("Beta", vm.DisplayNodes[0].Name);  // 6000
        Assert.AreEqual("Gamma", vm.DisplayNodes[1].Name); // 3000
        Assert.AreEqual("Alpha", vm.DisplayNodes[2].Name); // 1000

        // Hierarchy and sizes must remain intact
        Assert.AreEqual(10000, root.SizeBytes);
        Assert.AreEqual(root, vm.DisplayNodes[0].Parent);
    }

    [TestMethod]
    public void FolderAnalysisViewModel_SortByNameAscending_OrdersAlphabetically()
    {
        var scanSvc = new DummyScanService();
        var vm = new FolderAnalysisViewModel(scanSvc);

        var root = CreateTestHierarchy();
        vm.RootNode = root;

        vm.SortByColumn("Name");

        Assert.AreEqual("Alpha", vm.DisplayNodes[0].Name);
        Assert.AreEqual("Beta", vm.DisplayNodes[1].Name);
        Assert.AreEqual("Gamma", vm.DisplayNodes[2].Name);

        // Toggle to descending
        vm.SortByColumn("Name");
        Assert.AreEqual("Gamma", vm.DisplayNodes[0].Name);
        Assert.AreEqual("Beta", vm.DisplayNodes[1].Name);
        Assert.AreEqual("Alpha", vm.DisplayNodes[2].Name);
    }

    [TestMethod]
    public void FolderAnalysisViewModel_SortByFilesAndModified_SortsCorrectly()
    {
        var scanSvc = new DummyScanService();
        var vm = new FolderAnalysisViewModel(scanSvc);

        var root = CreateTestHierarchy();
        vm.RootNode = root;

        // Sort by Files descending
        vm.SortByColumn("Files");
        Assert.AreEqual("Beta", vm.DisplayNodes[0].Name);  // 5 files
        Assert.AreEqual("Gamma", vm.DisplayNodes[1].Name); // 3 files
        Assert.AreEqual("Alpha", vm.DisplayNodes[2].Name); // 2 files

        // Sort by Modified descending
        vm.SortByColumn("Modified");
        Assert.AreEqual("Beta", vm.DisplayNodes[0].Name);  // March 2025
        Assert.AreEqual("Gamma", vm.DisplayNodes[1].Name); // February 2025
        Assert.AreEqual("Alpha", vm.DisplayNodes[2].Name); // January 2025
    }

    [TestMethod]
    public void DriveAnalysisViewModel_Sorting_PreservesOriginalRootTotals()
    {
        var scanSvc = new DummyScanService();
        var vm = new DriveAnalysisViewModel(scanSvc);

        var root = CreateTestHierarchy();
        vm.RootNode = root;

        vm.SortByColumn("Size");
        Assert.AreEqual(10000, vm.RootNode.SizeBytes);
        Assert.AreEqual(10, vm.RootNode.TotalFileCount);

        vm.SortByColumn("Name");
        Assert.AreEqual(10000, vm.RootNode.SizeBytes);
        Assert.AreEqual(10, vm.RootNode.TotalFileCount);
    }
}
