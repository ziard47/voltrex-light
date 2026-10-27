using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using voltrex_light.Models;
using voltrex_light.Services;
using voltrex_light.ViewModels;

namespace voltrex_light.Tests;

[TestClass]
public class OverviewViewModelTests
{
    private class MockDriveService : IDriveService
    {
        public Task<IReadOnlyList<DriveItem>> GetDrivesAsync()
        {
            var list = new List<DriveItem>
            {
                new DriveItem
                {
                    Name = @"C:\",
                    VolumeLabel = "SystemDrive",
                    TotalSizeBytes = 500L * 1024 * 1024 * 1024,
                    FreeSizeBytes = 200L * 1024 * 1024 * 1024,
                    UsedSizeBytes = 300L * 1024 * 1024 * 1024,
                    PercentUsed = 60.0,
                    IsReady = true
                },
                new DriveItem
                {
                    Name = @"D:\",
                    VolumeLabel = "DataDrive",
                    TotalSizeBytes = 1000L * 1024 * 1024 * 1024,
                    FreeSizeBytes = 800L * 1024 * 1024 * 1024,
                    UsedSizeBytes = 200L * 1024 * 1024 * 1024,
                    PercentUsed = 20.0,
                    IsReady = true
                }
            };
            return Task.FromResult<IReadOnlyList<DriveItem>>(list);
        }

        public Task<DriveItem?> GetDriveAsync(string rootPath) => Task.FromResult<DriveItem?>(null);
    }

    private class MockFolderPickerService : IFolderPickerService
    {
        public void SetWindowHandle(IntPtr windowHandle) { }
        public Task<string?> PickFolderAsync() => Task.FromResult<string?>(@"C:\Users");
    }

    private class MockNavigationService : INavigationService
    {
        public string LastNavigatedKey { get; private set; } = string.Empty;
        public object? LastNavigatedParameter { get; private set; }

        public bool CanGoBack => false;
        public string CurrentPageKey => LastNavigatedKey;

        public event EventHandler<string>? Navigated;

        public void Initialize(object frame) { }
        public bool NavigateTo(string pageKey, object? parameter = null)
        {
            LastNavigatedKey = pageKey;
            LastNavigatedParameter = parameter;
            Navigated?.Invoke(this, pageKey);
            return true;
        }

        public bool GoBack() => false;
    }

    [TestMethod]
    public async Task OverviewViewModel_LoadDrivesAsync_ComputesAggregatesCorrectly()
    {
        var driveSvc = new MockDriveService();
        var folderSvc = new MockFolderPickerService();
        var navSvc = new MockNavigationService();
        var scanSvc = new ScanService();

        var vm = new OverviewViewModel(driveSvc, folderSvc, navSvc, scanSvc);

        Assert.IsFalse(vm.HasDrives);

        await vm.LoadDrivesAsync();

        Assert.IsTrue(vm.HasDrives);
        Assert.AreEqual(2, vm.Drives.Count);
        Assert.AreEqual(1500L * 1024 * 1024 * 1024, vm.TotalCapacityBytes);
        Assert.AreEqual(500L * 1024 * 1024 * 1024, vm.TotalUsedBytes);
        Assert.AreEqual(1000L * 1024 * 1024 * 1024, vm.TotalFreeBytes);
        Assert.IsNotNull(vm.SelectedDrive);
        Assert.AreEqual(@"C:\", vm.SelectedDrive.Name);
    }

    [TestMethod]
    public async Task OverviewViewModel_ScanSelectedDriveCommand_NavigatesToDriveAnalysis()
    {
        var driveSvc = new MockDriveService();
        var folderSvc = new MockFolderPickerService();
        var navSvc = new MockNavigationService();
        var scanSvc = new ScanService();

        var vm = new OverviewViewModel(driveSvc, folderSvc, navSvc, scanSvc);
        await vm.LoadDrivesAsync();

        vm.ScanSelectedDriveCommand.Execute(null);

        Assert.AreEqual("StorageAnalysis", navSvc.LastNavigatedKey);
    }
}

