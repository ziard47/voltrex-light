using System;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;
using voltrex_light.Models;
using voltrex_light.Services;
using voltrex_light.ViewModels;
using voltrex_light.Views;

namespace voltrex_light;

/// <summary>
/// Main application shell window for Voltrex Light.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel => AppServices.MainViewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Modern Windows 11 Titlebar Integration: extend into toolbar & set interactive drag region
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Pass window handle for native folder picker and export save picker WinRT interop
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        AppServices.FolderPickerService.SetWindowHandle(hWnd);
        AppServices.ExportService.SetWindowHandle(hWnd);

        // Customize titlebar buttons for translucent Mica blending
        CustomizeTitleBar();

        // Register navigation routes
        var navService = (NavigationService)AppServices.NavigationService;
        navService.RegisterPage("Overview", typeof(OverviewPage));
        navService.RegisterPage("StorageAnalysis", typeof(StorageAnalysisPage));
        navService.RegisterPage("DriveAnalysis", typeof(StorageAnalysisPage));
        navService.RegisterPage("FolderAnalysis", typeof(StorageAnalysisPage));
        navService.RegisterPage("LargestFiles", typeof(LargestFilesPage));
        navService.RegisterPage("Settings", typeof(SettingsPage));
        navService.Initialize(ContentFrame);

        // Theme management
        AppServices.SettingsService.ThemeChanged += OnThemeChanged;
        ApplyTheme(AppServices.SettingsService.Settings.AppTheme);

        // Navigation sync
        navService.Navigated += OnNavigated;

        // Populate sidebar drives
        ViewModel.SidebarDrives.CollectionChanged += OnSidebarDrivesChanged;
    }

    private void CustomizeTitleBar()
    {
        try
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = this.AppWindow.TitleBar;
                if (titleBar != null)
                {
                    AppTitleBar.Loaded += (s, e) => UpdateTitleBarMetrics();
                    AppTitleBar.SizeChanged += (s, e) => UpdateTitleBarMetrics();
                    UpdateTitleBarMetrics();
                }
            }
        }
        catch { }
    }

    private void UpdateTitleBarMetrics()
    {
        try
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = this.AppWindow.TitleBar;
                if (titleBar != null && AppTitleBar.XamlRoot != null)
                {
                    double scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;
                    if (scaleAdjustment > 0 && titleBar.RightInset > 0)
                    {
                        double rightInsetEffectivePixels = titleBar.RightInset / scaleAdjustment;
                        CaptionButtonsSpacer.Width = Math.Max(rightInsetEffectivePixels, 140);
                    }
                    else
                    {
                        CaptionButtonsSpacer.Width = 140;
                    }
                }
            }
        }
        catch { }
    }

    private void UpdateTitleBarForTheme(ElementTheme theme)
    {
        try
        {
            if (!AppWindowTitleBar.IsCustomizationSupported()) return;
            var titleBar = this.AppWindow.TitleBar;
            if (titleBar == null) return;

            bool isDark = theme == ElementTheme.Dark ||
                (theme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);

            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            if (isDark)
            {
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(30, 255, 255, 255);
                titleBar.ButtonPressedForegroundColor = Colors.White;
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(50, 255, 255, 255);
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(140, 255, 255, 255);
            }
            else
            {
                titleBar.ButtonForegroundColor = Colors.Black;
                titleBar.ButtonHoverForegroundColor = Colors.Black;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(30, 0, 0, 0);
                titleBar.ButtonPressedForegroundColor = Colors.Black;
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(50, 0, 0, 0);
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(140, 0, 0, 0);
            }
        }
        catch { }
    }

    private async void MainNavView_Loaded(object sender, RoutedEventArgs e)
    {
        // Select Overview by default
        MainNavView.SelectedItem = MainNavView.MenuItems[0];
        AppServices.NavigationService.NavigateTo("Overview");

        // Load sidebar drives
        await ViewModel.LoadSidebarDrivesAsync();
    }

    private void OnSidebarDrivesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Remove existing drive items
        var toRemove = MainNavView.MenuItems
            .OfType<NavigationViewItem>()
            .Where(item => item.Tag is string tag && tag.StartsWith("drive:"))
            .ToList();

        foreach (var item in toRemove)
        {
            MainNavView.MenuItems.Remove(item);
        }

        // Add current drives after DrivesSectionHeader
        int insertIndex = MainNavView.MenuItems.IndexOf(DrivesSectionHeader);
        if (insertIndex >= 0)
        {
            insertIndex++;
            foreach (var drive in ViewModel.SidebarDrives)
            {
                var driveItem = new NavigationViewItem
                {
                    Content = drive.DisplayName,
                    Tag = $"drive:{drive.RootDirectory}",
                    Icon = new FontIcon { Glyph = drive.IconGlyph }
                };

                void UpdateDriveItemState()
                {
                    if (drive.IsScanning)
                    {
                        driveItem.InfoBadge = new InfoBadge
                        {
                            Style = (Style)Application.Current.Resources["AttentionIconInfoBadgeStyle"]
                        };
                        ToolTipService.SetToolTip(driveItem, $"{drive.DisplayName}\nScanning in progress...");
                    }
                    else
                    {
                        driveItem.InfoBadge = null;
                        ToolTipService.SetToolTip(driveItem, $"{drive.DisplayName}\n{drive.FormattedFreeSize} free of {drive.FormattedTotalSize} ({drive.FormattedPercentUsed} used)\nClick to analyze drive");
                    }
                }

                UpdateDriveItemState();

                drive.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(DriveItem.IsScanning))
                    {
                        DispatcherQueue.TryEnqueue(UpdateDriveItemState);
                    }
                };

                MainNavView.MenuItems.Insert(insertIndex++, driveItem);
            }
        }
    }

    private void MainNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            AppServices.NavigationService.NavigateTo("Settings");
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            if (tag.StartsWith("drive:"))
            {
                string drivePath = tag.Substring("drive:".Length);
                var drive = ViewModel.SidebarDrives.FirstOrDefault(d => string.Equals(d.RootDirectory, drivePath, StringComparison.OrdinalIgnoreCase));
                if (drive != null)
                {
                    _ = ViewModel.ScanDriveFromSidebarAsync(drive);
                }
                else
                {
                    ViewModel.SelectedPath = drivePath;
                    AppServices.NavigationService.NavigateTo("StorageAnalysis", drivePath);
                }
                return;
            }

            AppServices.NavigationService.NavigateTo(tag);
        }
    }

    private void OnNavigated(object? sender, string pageKey)
    {
        if (pageKey == "Settings")
        {
            MainNavView.SelectedItem = MainNavView.SettingsItem;
            return;
        }

        string targetTag = (pageKey == "DriveAnalysis" || pageKey == "FolderAnalysis") ? "StorageAnalysis" : pageKey;

        foreach (var menuItem in MainNavView.MenuItems)
        {
            if (menuItem is NavigationViewItem nvi && (string)nvi.Tag == targetTag)
            {
                MainNavView.SelectedItem = nvi;
                break;
            }
        }
    }

    private void OnThemeChanged(object? sender, string theme)
    {
        ApplyTheme(theme);
    }

    private void ApplyTheme(string theme)
    {
        var elementTheme = theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        RootGrid.RequestedTheme = elementTheme;
        UpdateTitleBarForTheme(elementTheme);
    }
}

