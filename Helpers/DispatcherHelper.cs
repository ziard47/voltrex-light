using System;

namespace voltrex_light.Helpers;

/// <summary>
/// Thread-safe dispatcher helper for marshaling operations onto the WinUI 3 UI thread.
/// Safely falls back to synchronous invocation in headless test environments.
/// </summary>
public static class DispatcherHelper
{
#if !TESTING
    private static Microsoft.UI.Dispatching.DispatcherQueue? _uiDispatcherQueue;

    public static void Initialize(Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue)
    {
        _uiDispatcherQueue = dispatcherQueue;
    }

    public static bool HasThreadAccess => _uiDispatcherQueue == null || _uiDispatcherQueue.HasThreadAccess;

    public static void ExecuteOnUIThread(Action action)
    {
        if (action == null) return;

        if (_uiDispatcherQueue == null || _uiDispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _uiDispatcherQueue.TryEnqueue(() => action());
        }
    }
#else
    public static bool HasThreadAccess => true;

    public static void ExecuteOnUIThread(Action action)
    {
        action?.Invoke();
    }
#endif
}

