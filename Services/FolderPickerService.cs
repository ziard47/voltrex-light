using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace voltrex_light.Services;

/// <summary>
/// Native Windows folder selection dialog service implemented via Windows.Storage.Pickers.FolderPicker.
/// </summary>
public class FolderPickerService : IFolderPickerService
{
    private IntPtr _windowHandle = IntPtr.Zero;

    public void SetWindowHandle(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public async Task<string?> PickFolderAsync()
    {
        try
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            folderPicker.FileTypeFilter.Add("*");

            if (_windowHandle != IntPtr.Zero)
            {
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, _windowHandle);
            }

            var folder = await folderPicker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FolderPickerService] Folder picking failed: {ex.Message}");
            return null;
        }
    }
}

