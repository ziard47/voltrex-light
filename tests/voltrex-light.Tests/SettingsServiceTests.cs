using Microsoft.VisualStudio.TestTools.UnitTesting;
using voltrex_light.Services;

namespace voltrex_light.Tests;

[TestClass]
public class SettingsServiceTests
{
    [TestMethod]
    public void SettingsService_DefaultThemeIsDefault()
    {
        var svc = new SettingsService();
        Assert.AreEqual("Default", svc.Settings.AppTheme);
    }

    [TestMethod]
    public void SettingsService_NotifyThemeChanged_FiresEvent()
    {
        var svc = new SettingsService();
        string? receivedTheme = null;
        svc.ThemeChanged += (s, theme) => receivedTheme = theme;

        svc.NotifyThemeChanged("Dark");

        Assert.AreEqual("Dark", receivedTheme);
        Assert.AreEqual("Dark", svc.Settings.AppTheme);
    }
}

