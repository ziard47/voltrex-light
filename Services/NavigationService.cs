using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;

namespace voltrex_light.Services;

/// <summary>
/// Implements frame-based navigation between views with decoupled page key mappings.
/// </summary>
public class NavigationService : INavigationService
{
    private Frame? _frame;
    private readonly Dictionary<string, Type> _pages = new();
    private string _currentPageKey = string.Empty;

    public bool CanGoBack => _frame?.CanGoBack ?? false;
    public string CurrentPageKey => _currentPageKey;

    public event EventHandler<string>? Navigated;

    public void RegisterPage(string key, Type pageType)
    {
        _pages[key] = pageType;
    }

    public void Initialize(object frame)
    {
        _frame = frame as Frame ?? throw new ArgumentException("Expected Frame instance", nameof(frame));
    }

    public bool NavigateTo(string pageKey, object? parameter = null)
    {
        if (_frame == null) return false;

        if (!_pages.TryGetValue(pageKey, out var pageType))
        {
            return false;
        }

        var result = _frame.Navigate(pageType, parameter);
        if (result)
        {
            _currentPageKey = pageKey;
            Navigated?.Invoke(this, pageKey);
        }
        return result;
    }

    public bool GoBack()
    {
        if (_frame != null && _frame.CanGoBack)
        {
            _frame.GoBack();
            return true;
        }
        return false;
    }
}

