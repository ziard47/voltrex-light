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
        InitializeComponent();

        UnhandledException += (sender, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Voltrex Light UnhandledException] {e.Message}");
        };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await AppServices.SettingsService.InitializeAsync();

        _window = new MainWindow();
        _window.Activate();
    }
}

