using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using voltrex_light.Models;
using voltrex_light.Services;
using voltrex_light.ViewModels;

namespace voltrex_light.Tests;

[TestClass]
public class UpdateServiceTests
{
    [TestMethod]
    public void IsNewerVersion_CorrectlyComparesSemanticVersions()
    {
        Assert.IsTrue(UpdateService.IsNewerVersion("1.1.0", "1.0.0"));
        Assert.IsTrue(UpdateService.IsNewerVersion("2.0.0", "1.9.9"));
        Assert.IsTrue(UpdateService.IsNewerVersion("1.0.1", "1.0.0"));
        Assert.IsTrue(UpdateService.IsNewerVersion("v1.2.0", "1.1.0"));
        Assert.IsTrue(UpdateService.IsNewerVersion("V2.0.0", "v1.9.0"));

        Assert.IsFalse(UpdateService.IsNewerVersion("1.0.0", "1.0.0"));
        Assert.IsFalse(UpdateService.IsNewerVersion("1.0.0", "1.1.0"));
        Assert.IsFalse(UpdateService.IsNewerVersion("1.0.0", "2.0.0"));
        Assert.IsFalse(UpdateService.IsNewerVersion("", "1.0.0"));
        Assert.IsFalse(UpdateService.IsNewerVersion(" ", "1.0.0"));
    }

    [TestMethod]
    public void UpdateService_Endpoints_PointToCorrectRepository()
    {
        Assert.AreEqual("https://api.github.com/repos/ziard47/voltrex-light/releases/latest", UpdateService.GitHubReleasesApiUrl);
        Assert.AreEqual("https://raw.githubusercontent.com/ziard47/voltrex-light/main/version.json", UpdateService.GitHubManifestFallbackUrl);
    }

    [TestMethod]
    public async Task UpdateService_CheckForUpdates_ParsesGitHubReleaseResponse()
    {
        const string mockReleaseJson = @"{
            ""tag_name"": ""v1.1.0"",
            ""name"": ""Voltrex Light v1.1.0"",
            ""published_at"": ""2026-11-01T12:00:00Z"",
            ""body"": ""- Added new disk benchmark charts\n- Performance improvements"",
            ""assets"": [
                {
                    ""name"": ""voltrex-light-setup-1.1.0.exe"",
                    ""size"": 85000000,
                    ""browser_download_url"": ""https://github.com/ziard47/voltrex-light/releases/download/v1.1.0/voltrex-light-setup-1.1.0.exe""
                }
            ]
        }";

        var handler = new MockHttpMessageHandler(mockReleaseJson);
        var httpClient = new System.Net.Http.HttpClient(handler);
        var updateService = new UpdateService(httpClient);

        var updateInfo = await updateService.CheckForUpdatesAsync("1.0.0");

        Assert.IsTrue(updateInfo.IsUpdateAvailable);
        Assert.AreEqual("1.1.0", updateInfo.LatestVersion);
        Assert.AreEqual("https://github.com/ziard47/voltrex-light/releases/download/v1.1.0/voltrex-light-setup-1.1.0.exe", updateInfo.DownloadUrl);
        Assert.AreEqual(85000000, updateInfo.DownloadSizeBytes);
        Assert.IsTrue(updateInfo.ReleaseNotes.Contains("Added new disk benchmark charts"));
    }

    [TestMethod]
    public void UpdateDownloadProgress_CalculatesPercentageCorrectly()
    {
        var progress = new UpdateDownloadProgress
        {
            BytesReceived = 50,
            TotalBytesToReceive = 100
        };

        Assert.AreEqual(50.0, progress.ProgressPercentage);

        var zeroProgress = new UpdateDownloadProgress
        {
            BytesReceived = 0,
            TotalBytesToReceive = 0
        };

        Assert.AreEqual(0.0, zeroProgress.ProgressPercentage);
    }

    [TestMethod]
    public async Task SettingsViewModel_CheckUpdates_UpdatesStateCorrectly_WhenUpdateAvailable()
    {
        var mockUpdateService = new MockUpdateService
        {
            CheckResult = new UpdateInfo
            {
                CurrentVersion = "1.0.0",
                LatestVersion = "1.1.0",
                ReleaseNotes = "New features",
                DownloadUrl = "https://example.com/update.exe",
                IsUpdateAvailable = true
            }
        };

        var settingsService = new SettingsService();
        var scanHistoryService = new ScanHistoryService();
        var vm = new SettingsViewModel(settingsService, scanHistoryService, mockUpdateService);

        Assert.AreEqual(UpdateStatus.Idle, vm.UpdateStatus);
        Assert.IsTrue(vm.CanCheckForUpdates);

        await vm.ExecuteCheckForUpdatesAsync();

        Assert.AreEqual(UpdateStatus.UpdateAvailable, vm.UpdateStatus);
        Assert.IsTrue(vm.IsUpdateAvailable);
        Assert.AreEqual("1.1.0", vm.UpdateNewVersion);
        Assert.AreEqual("New features", vm.UpdateReleaseNotes);
    }

    [TestMethod]
    public async Task SettingsViewModel_CheckUpdates_UpdatesStateCorrectly_WhenUpToDate()
    {
        var mockUpdateService = new MockUpdateService
        {
            CheckResult = new UpdateInfo
            {
                CurrentVersion = "1.0.0",
                LatestVersion = "1.0.0",
                IsUpdateAvailable = false
            }
        };

        var settingsService = new SettingsService();
        var scanHistoryService = new ScanHistoryService();
        var vm = new SettingsViewModel(settingsService, scanHistoryService, mockUpdateService);

        await vm.ExecuteCheckForUpdatesAsync();

        Assert.AreEqual(UpdateStatus.UpToDate, vm.UpdateStatus);
        Assert.IsTrue(vm.IsUpToDate);
        Assert.IsTrue(vm.UpdateStatusMessage.Contains("up to date"));
    }

    [TestMethod]
    public async Task SettingsViewModel_DownloadAndInstall_TransitionsToReadyToInstall()
    {
        var mockUpdateService = new MockUpdateService
        {
            CheckResult = new UpdateInfo
            {
                CurrentVersion = "1.0.0",
                LatestVersion = "1.1.0",
                DownloadUrl = "https://example.com/update.exe",
                IsUpdateAvailable = true
            },
            DownloadedPath = "C:\\dummy\\update.exe"
        };

        var settingsService = new SettingsService();
        var scanHistoryService = new ScanHistoryService();
        var vm = new SettingsViewModel(settingsService, scanHistoryService, mockUpdateService);

        await vm.ExecuteCheckForUpdatesAsync();
        Assert.IsTrue(vm.IsUpdateAvailable);

        await vm.ExecuteDownloadAndInstallAsync();
        Assert.AreEqual(UpdateStatus.ReadyToInstall, vm.UpdateStatus);
        Assert.IsTrue(vm.IsReadyToInstall);
    }

    [TestMethod]
    public void SettingsViewModel_UpdateModal_OpensAndClosesCorrectly()
    {
        var settingsService = new SettingsService();
        var scanHistoryService = new ScanHistoryService();
        var vm = new SettingsViewModel(settingsService, scanHistoryService);

        Assert.IsFalse(vm.IsUpdateModalOpen);

        vm.OpenReleaseNotesCommand.Execute(null);
        Assert.IsTrue(vm.IsUpdateModalOpen);
        Assert.IsFalse(string.IsNullOrWhiteSpace(vm.ModalReleaseNotes));
        Assert.AreEqual("You're Up to Date", vm.ModalTitle);

        vm.CloseUpdateModalCommand.Execute(null);
        Assert.IsFalse(vm.IsUpdateModalOpen);
    }

    [TestMethod]
    public void SettingsViewModel_AboutSection_HasCorrectCleanMetadata()
    {
        var settingsService = new SettingsService();
        var scanHistoryService = new ScanHistoryService();
        var vm = new SettingsViewModel(settingsService, scanHistoryService);

        Assert.AreEqual("Voltrex Light", vm.AppName);
        Assert.AreEqual("1.0.0", vm.AppVersion);
        Assert.AreEqual("mohomed Ziard | Voltrex Digital", vm.DeveloperName);
        Assert.IsFalse(string.IsNullOrWhiteSpace(vm.AppDescription));
    }

    private class MockUpdateService : IUpdateService
    {
        public UpdateStatus Status { get; set; } = UpdateStatus.Idle;
        public UpdateInfo? CurrentUpdateInfo { get; set; }
        public UpdateInfo CheckResult { get; set; } = new();
        public string DownloadedPath { get; set; } = "installer.exe";

        public Task<UpdateInfo> CheckForUpdatesAsync(string currentVersion, CancellationToken cancellationToken = default)
        {
            CurrentUpdateInfo = CheckResult;
            return Task.FromResult(CheckResult);
        }

        public Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(new UpdateDownloadProgress { BytesReceived = 100, TotalBytesToReceive = 100 });
            return Task.FromResult(DownloadedPath);
        }

        public bool InstallUpdate(string installerPath) => true;
    }

    private class MockHttpMessageHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly string _responseContent;

        public MockHttpMessageHandler(string responseContent)
        {
            _responseContent = responseContent;
        }

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}

