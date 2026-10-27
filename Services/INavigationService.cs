using System;

namespace voltrex_light.Services;

/// <summary>
/// Navigation service for transitioning between pages within the main frame.
/// </summary>
public interface INavigationService
{
    void Initialize(object frame);
    bool NavigateTo(string pageKey, object? parameter = null);
    bool GoBack();
    bool CanGoBack { get; }
    string CurrentPageKey { get; }
    event EventHandler<string>? Navigated;
}

