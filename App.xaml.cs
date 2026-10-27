using System;
using Microsoft.UI.Xaml;
using voltrex_light.Services;

namespace voltrex_light;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var msg = e.ExceptionObject?.ToString() ?? "Unknown AppDomain exception";
            try
            {
                var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoltrexLight", "crash.txt");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.WriteAllText(logPath, msg);
                System.IO.File.WriteAllText("crash.txt", msg);
            }
            catch { }
        };

        UnhandledException += (sender, e) =>
        {
            var msg = e.Exception?.ToString() ?? e.Message;
            try
            {
                var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoltrexLight", "crash.txt");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.WriteAllText(logPath, msg);
                System.IO.File.WriteAllText("crash.txt", msg);
            }
            catch { }
            System.Diagnostics.Debug.WriteLine($"[Voltrex Light UnhandledException] {e.Message}");
        };

        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await AppServices.SettingsService.InitializeAsync();

            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            try
            {
                var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoltrexLight", "crash.txt");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.WriteAllText(logPath, ex.ToString());
                System.IO.File.WriteAllText("crash.txt", ex.ToString());
            }
            catch { }
            throw;
        }
    }
}

