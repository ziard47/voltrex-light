using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using voltrex_light.Services;

namespace voltrex_light.Tests;

[TestClass]
public class DriveServiceTests
{
    [TestMethod]
    public async Task DriveService_GetDrivesAsync_ReturnsLocalDrivesWithActualData()
    {
        var svc = new DriveService();
        var drives = await svc.GetDrivesAsync();

        Assert.IsNotNull(drives);
        Assert.IsTrue(drives.Count > 0, "Expected at least one storage drive to be present on the host system.");

        var systemDrive = drives[0];
        Assert.IsFalse(string.IsNullOrWhiteSpace(systemDrive.Name));
        Assert.IsTrue(systemDrive.TotalSizeBytes > 0, "Storage drive total capacity must be greater than 0.");
        Assert.IsTrue(systemDrive.FreeSizeBytes >= 0, "Free space cannot be negative.");
        Assert.IsTrue(systemDrive.UsedSizeBytes >= 0, "Used space cannot be negative.");
        Assert.IsTrue(systemDrive.PercentUsed >= 0 && systemDrive.PercentUsed <= 100, "Percent used must be between 0 and 100.");
    }
}

