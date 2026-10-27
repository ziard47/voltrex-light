using System;
using System.Threading.Tasks;
using voltrex_light.Models;

namespace voltrex_light.Services;

/// <summary>
/// Service interface for persisting and loading application preferences and theme configuration.
/// </summary>
public interface ISettingsService
{
    AppSettings Settings { get; }
    Task InitializeAsync();
    Task SaveSettingsAsync();
    Task SetThemeAsync(string theme);
    event EventHandler<string>? ThemeChanged;
    void NotifyThemeChanged(string theme);
}

