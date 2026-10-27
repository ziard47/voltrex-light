using System;
using System.Threading.Tasks;

namespace voltrex_light.Services;

/// <summary>
/// Service interface for displaying native Windows folder selection dialog.
/// </summary>
public interface IFolderPickerService
{
    void SetWindowHandle(IntPtr windowHandle);
    Task<string?> PickFolderAsync();
}

