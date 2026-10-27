using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using voltrex_light.Services;
using voltrex_light.ViewModels;

namespace voltrex_light.Views;

public sealed partial class OverviewPage : Page
{
    public OverviewViewModel ViewModel => AppServices.OverviewViewModel;

    public OverviewPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.HasDrives)
        {
            await ViewModel.LoadDrivesAsync();
        }
        AdjustCardLayout(ActualWidth);
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        AdjustCardLayout(e.NewSize.Width);
    }

    private void AdjustCardLayout(double width)
    {
        if (MetricsGrid == null || CardTotal == null || CardUsed == null || CardFree == null || CardActions == null)
            return;

        if (width >= 860)
        {
            // 4 columns x 1 row
            Grid.SetRow(CardTotal, 0); Grid.SetColumn(CardTotal, 0);
            Grid.SetRow(CardUsed, 0); Grid.SetColumn(CardUsed, 1);
            Grid.SetRow(CardFree, 0); Grid.SetColumn(CardFree, 2);
            Grid.SetRow(CardActions, 0); Grid.SetColumn(CardActions, 3);

            Grid.SetColumnSpan(CardTotal, 1);
            Grid.SetColumnSpan(CardUsed, 1);
            Grid.SetColumnSpan(CardFree, 1);
            Grid.SetColumnSpan(CardActions, 1);
        }
        else if (width >= 480)
        {
            // 2 columns x 2 rows
            Grid.SetRow(CardTotal, 0); Grid.SetColumn(CardTotal, 0);
            Grid.SetRow(CardUsed, 0); Grid.SetColumn(CardUsed, 2); // use columns 0,1 for left, 2,3 for right
            Grid.SetRow(CardFree, 1); Grid.SetColumn(CardFree, 0);
            Grid.SetRow(CardActions, 1); Grid.SetColumn(CardActions, 2);

            Grid.SetColumnSpan(CardTotal, 2);
            Grid.SetColumnSpan(CardUsed, 2);
            Grid.SetColumnSpan(CardFree, 2);
            Grid.SetColumnSpan(CardActions, 2);
        }
        else
        {
            // 1 column x 4 rows
            Grid.SetRow(CardTotal, 0); Grid.SetColumn(CardTotal, 0);
            Grid.SetRow(CardUsed, 1); Grid.SetColumn(CardUsed, 0);
            Grid.SetRow(CardFree, 2); Grid.SetColumn(CardFree, 0);
            Grid.SetRow(CardActions, 3); Grid.SetColumn(CardActions, 0);

            Grid.SetColumnSpan(CardTotal, 4);
            Grid.SetColumnSpan(CardUsed, 4);
            Grid.SetColumnSpan(CardFree, 4);
            Grid.SetColumnSpan(CardActions, 4);
        }
    }
}

