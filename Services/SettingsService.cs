using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using voltrex_light.Models;

namespace voltrex_light.Services;

/// <summary>
/// Manages application configuration and theme persistence.
/// Safely supports both packaged MSIX and unpackaged desktop execution.
/// </summary>
public class SettingsService : ISettingsService
{
    private const string SettingsFileName = "voltrex_light_settings.json";
    private readonly string _settingsFilePath;

    public AppSettings Settings { get; private set; } = new();

    public event EventHandler<string>? ThemeChanged;

    public SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "VoltrexLight");
        _settingsFilePath = Path.Combine(dir, SettingsFileName);
    }

    public async Task InitializeAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Settings = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Could not read settings file: {ex.Message}");
        }
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to save settings: {ex.Message}");
        }
    }

    public async Task SetThemeAsync(string theme)
    {
        Settings.AppTheme = theme;
        NotifyThemeChanged(theme);
        await SaveSettingsAsync();
    }

    public void NotifyThemeChanged(string theme)
    {
        Settings.AppTheme = theme;
        ThemeChanged?.Invoke(this, theme);
    }
}
