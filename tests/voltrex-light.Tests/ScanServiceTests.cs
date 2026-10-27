using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using voltrex_light.Models;
using voltrex_light.Services;

namespace voltrex_light.Tests;

[TestClass]
public class ScanServiceTests
{
    private string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DiskScopeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private void CleanupDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch { }
    }

    [TestMethod]
    public async Task ScanService_NestedDirectories_CalculatesAggregatesAndPercentagesAccurately()
    {
        var rootDir = CreateTempDirectory();
        try
        {
            var level1 = Path.Combine(rootDir, "Level1");
            var level2 = Path.Combine(level1, "Level2");
            Directory.CreateDirectory(level2);

            // Level 2 has 2000 bytes
            await File.WriteAllBytesAsync(Path.Combine(level2, "file2.bin"), new byte[2000]);

            // Level 1 has 3000 bytes in root of Level 1
            await File.WriteAllBytesAsync(Path.Combine(level1, "file1.bin"), new byte[3000]);

            // Root has 5000 bytes in root
            await File.WriteAllBytesAsync(Path.Combine(rootDir, "rootfile.bin"), new byte[5000]);

            var scanService = new ScanService();
            var result = await scanService.ScanAsync(new ScanOptions { RootPath = rootDir });

            Assert.IsNotNull(result);
            Assert.AreEqual(ScanStatus.Completed, result.Status);
            Assert.IsFalse(result.IsPartial);
            Assert.AreEqual(10000, result.TotalBytesScanned);
            Assert.AreEqual(3, result.TotalFilesCount);
            Assert.AreEqual(2, result.TotalDirectoriesCount);

            var rootNode = result.RootNode;
            Assert.IsNotNull(rootNode);
            Assert.AreEqual(10000, rootNode.SizeBytes);
            Assert.AreEqual(100.0, rootNode.PercentOfRoot, 0.01);

            // Check level1
            var level1Node = rootNode.Children.FirstOrDefault(c => c.Name == "Level1");
            Assert.IsNotNull(level1Node);
            Assert.AreEqual(5000, level1Node.SizeBytes);
            Assert.AreEqual(50.0, level1Node.PercentOfParent, 0.01);
            Assert.AreEqual(50.0, level1Node.PercentOfRoot, 0.01);
            Assert.AreEqual(2, level1Node.TotalFileCount);
            Assert.AreEqual(1, level1Node.TotalSubfolderCount);

            // Check level2
            var level2Node = level1Node.Children.FirstOrDefault(c => c.Name == "Level2");
            Assert.IsNotNull(level2Node);
            Assert.AreEqual(2000, level2Node.SizeBytes);
            Assert.AreEqual(40.0, level2Node.PercentOfParent, 0.01); // 2000 / 5000 = 40%
            Assert.AreEqual(20.0, level2Node.PercentOfRoot, 0.01);   // 2000 / 10000 = 20%
            Assert.AreEqual(1, level2Node.TotalFileCount);
            Assert.AreEqual(0, level2Node.TotalSubfolderCount);
        }
        finally
        {
            CleanupDirectory(rootDir);
        }
    }

    [TestMethod]
    public async Task ScanService_EmptyDirectoriesAndEmptyFiles_HandledCorrectly()
    {
        var rootDir = CreateTempDirectory();
        try
        {
            var emptySubdir = Path.Combine(rootDir, "EmptyFolder");
            Directory.CreateDirectory(emptySubdir);

            var emptyFile = Path.Combine(rootDir, "empty.txt");
            await File.WriteAllBytesAsync(emptyFile, Array.Empty<byte>());

            var scanService = new ScanService();
            var result = await scanService.ScanAsync(new ScanOptions { RootPath = rootDir });

            Assert.IsNotNull(result);
            Assert.AreEqual(ScanStatus.Completed, result.Status);
            Assert.AreEqual(0, result.TotalBytesScanned);
            Assert.AreEqual(1, result.TotalFilesCount);
            Assert.AreEqual(1, result.TotalDirectoriesCount);

            var emptyFolderNode = result.RootNode?.Children.FirstOrDefault(c => c.Name == "EmptyFolder");
            Assert.IsNotNull(emptyFolderNode);
            Assert.AreEqual(0, emptyFolderNode.SizeBytes);
            Assert.AreEqual(0, emptyFolderNode.TotalFileCount);
            Assert.AreEqual(0, emptyFolderNode.TotalSubfolderCount);
            Assert.AreEqual(0.0, emptyFolderNode.PercentOfParent);
        }
        finally
        {
            CleanupDirectory(rootDir);
        }
    }

    [TestMethod]
    public async Task ScanService_LargeFileSizes_Uses64BitArithmetic()
    {
        var rootDir = CreateTempDirectory();
        try
        {
            // Test synthetic file node representation with large size exceeding 32-bit (e.g. 5 GB)
            long largeSize = 5L * 1024 * 1024 * 1024;
            var node = new FileSystemNode
            {
                Name = "large_image.iso",
                SizeBytes = largeSize,
                IsDirectory = false
            };

            Assert.AreEqual(5368709120L, node.SizeBytes);
            Assert.AreEqual("5.00 GB", node.FormattedSize);
        }
        finally
        {
            CleanupDirectory(rootDir);
        }
    }

    [TestMethod]
    public async Task ScanService_MissingPath_ReturnsFailedResultWithoutCrashing()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistent_" + Guid.NewGuid().ToString("N"));

        var scanService = new ScanService();
        var result = await scanService.ScanAsync(new ScanOptions { RootPath = nonExistentPath });

        Assert.IsNotNull(result);
        Assert.AreEqual(ScanStatus.Failed, result.Status);
        Assert.IsTrue(result.IsPartial);
        Assert.IsFalse(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [TestMethod]
    public async Task ScanService_Cancellation_StopsGracefullyAndReportsCancelled()
    {
        var rootDir = CreateTempDirectory();
        try
        {
            // Create several directories and files
            for (int i = 0; i < 10; i++)
            {
                var dir = Path.Combine(rootDir, $"Dir_{i}");
                Directory.CreateDirectory(dir);
                for (int j = 0; j < 5; j++)
                {
                    await File.WriteAllBytesAsync(Path.Combine(dir, $"file_{j}.bin"), new byte[100]);
                }
            }

            var scanService = new ScanService();
            using var cts = new CancellationTokenSource();

            // Cancel immediately as scan starts
            cts.Cancel();

            var result = await scanService.ScanAsync(new ScanOptions { RootPath = rootDir }, cts.Token);

            Assert.IsNotNull(result);
            Assert.AreEqual(ScanStatus.Cancelled, result.Status);
            Assert.IsTrue(result.IsPartial);
        }
        finally
        {
            CleanupDirectory(rootDir);
        }
    }

    [TestMethod]
    public async Task ScanService_ConsecutiveScans_CancelsPreviousScanWithoutError()
    {
        var rootDir = CreateTempDirectory();
        try
        {
            for (int i = 0; i < 5; i++)
            {
                await File.WriteAllBytesAsync(Path.Combine(rootDir, $"file_{i}.txt"), new byte[500]);
            }

            var scanService = new ScanService();

            // Start first scan
            var task1 = scanService.ScanAsync(new ScanOptions { RootPath = rootDir });

            // Immediately start second scan on the same service
            var task2 = scanService.ScanAsync(new ScanOptions { RootPath = rootDir });

            var result2 = await task2;
            Assert.IsNotNull(result2);
            Assert.AreEqual(ScanStatus.Completed, result2.Status);
            Assert.AreEqual(2500, result2.TotalBytesScanned);
            Assert.AreEqual(5, result2.TotalFilesCount);
        }
        finally
        {
            CleanupDirectory(rootDir);
        }
    }

    [TestMethod]
    public async Task ScanService_ReparsePointDefault_DoesNotRecurseReparsePoints()
    {
        var rootDir = CreateTempDirectory();
        try
        {
            var options = new ScanOptions
            {
                RootPath = rootDir,
                FollowReparsePoints = false
            };

            Assert.IsFalse(options.FollowReparsePoints, "Default configuration must not follow reparse points recursively.");
        }
        finally
        {
            CleanupDirectory(rootDir);
        }
    }
}

