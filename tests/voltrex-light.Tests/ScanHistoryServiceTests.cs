using System;
using System.IO;
using System.Threading.Tasks;
using voltrex_light.Models;
using voltrex_light.Services;

namespace voltrex_light.Tests;

[TestClass]
public class ScanHistoryServiceTests
{
    private string _tempDir = null!;
    private string _historyFile = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DiskScopeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _historyFile = Path.Combine(_tempDir, "history.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [TestMethod]
    public async Task RecordScanAsync_PersistsAndLoadsHistory()
    {
        var service = new ScanHistoryService(_historyFile);

        var result = new ScanResult
        {
            RootPath = "C:\\TestDir",
            Status = ScanStatus.Completed,
            TotalBytesScanned = 1024 * 1024 * 50,
            TotalFilesCount = 42,
            TotalDirectoriesCount = 5,
            SkippedCount = 0
        };

        await service.RecordScanAsync(result);

        Assert.AreEqual(1, service.History.Count);
        Assert.AreEqual("C:\\TestDir", service.History[0].RootPath);
        Assert.AreEqual(42, service.History[0].TotalFilesCount);
        Assert.IsTrue(File.Exists(_historyFile));

        // Create a new service instance to verify persistence from disk
        var service2 = new ScanHistoryService(_historyFile);
        await service2.LoadHistoryAsync();

        Assert.AreEqual(1, service2.History.Count);
        Assert.AreEqual("C:\\TestDir", service2.History[0].RootPath);
    }

    [TestMethod]
    public async Task RecordScanAsync_LimitsToMax50Items()
    {
        var service = new ScanHistoryService(_historyFile);

        // Record 55 items
        for (int i = 1; i <= 55; i++)
        {
            await service.RecordScanAsync(new ScanResult
            {
                RootPath = $"C:\\TestDir_{i}",
                Status = ScanStatus.Completed,
                TotalFilesCount = i
            });
        }

        Assert.AreEqual(50, service.History.Count);
        // The most recently added item (55) should be first
        Assert.AreEqual("C:\\TestDir_55", service.History[0].RootPath);
        // The oldest preserved item (6) should be last
        Assert.AreEqual("C:\\TestDir_6", service.History[49].RootPath);
    }

    [TestMethod]
    public async Task ClearHistoryAsync_RemovesAllEntries()
    {
        var service = new ScanHistoryService(_historyFile);

        await service.RecordScanAsync(new ScanResult
        {
            RootPath = "C:\\Dir1",
            Status = ScanStatus.Completed
        });

        Assert.AreEqual(1, service.History.Count);

        await service.ClearHistoryAsync();

        Assert.AreEqual(0, service.History.Count);

        var service2 = new ScanHistoryService(_historyFile);
        await service2.LoadHistoryAsync();
        Assert.AreEqual(0, service2.History.Count);
    }
}

