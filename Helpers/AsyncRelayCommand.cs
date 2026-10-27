using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace voltrex_light.Helpers;

/// <summary>
/// An asynchronous command that relays execution to an async Task.
/// Prevents re-entrance while running and notifies CanExecuteChanged.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting != value)
            {
                _isExecuting = value;
                NotifyCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => !IsExecuting && (_canExecute == null || _canExecute());

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        try
        {
            IsExecuting = true;
            await _execute();
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null)) return;

        try
        {
            IsExecuting = true;
            await _execute();
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    public void RaiseCanExecuteChanged() => NotifyCanExecuteChanged();
}

/// <summary>
/// Generic asynchronous command that takes a parameter of type T.
/// </summary>
public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Predicate<T?>? _canExecute;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting != value)
            {
                _isExecuting = value;
                NotifyCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand(Func<T?, Task> execute, Predicate<T?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        if (IsExecuting) return false;
        if (_canExecute == null) return true;
        if (parameter == null && typeof(T).IsValueType) return _canExecute(default);
        return _canExecute((T?)parameter);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        try
        {
            IsExecuting = true;
            await _execute((T?)parameter);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public async Task ExecuteAsync(T? parameter)
    {
        if (!CanExecute(parameter)) return;

        try
        {
            IsExecuting = true;
            await _execute(parameter);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    public void RaiseCanExecuteChanged() => NotifyCanExecuteChanged();
}

