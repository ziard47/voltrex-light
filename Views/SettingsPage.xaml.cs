using Microsoft.UI.Xaml.Controls;
using voltrex_light.Services;
using voltrex_light.ViewModels;

namespace voltrex_light.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel => AppServices.SettingsViewModel;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (s, e) => ViewModel.RefreshHistory();
    }
}

