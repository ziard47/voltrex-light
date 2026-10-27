using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
#if !TESTING
using Windows.ApplicationModel.DataTransfer;
#endif

namespace voltrex_light.Helpers;

/// <summary>
/// Provides shell integration for native Windows properties dialogs, Explorer actions, and clipboard operations.
/// </summary>
public static class WindowsShellHelper
{
    private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
    private const int SW_SHOW = 5;

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string pFrom;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpVerb;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string lpFile;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpParameters;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPTStr)]
        public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    /// <summary>
    /// Opens the native Windows File/Folder Properties dialog.
    /// </summary>
    public static bool ShowFileProperties(string path, IntPtr hwnd = default)
    {
        if (string.IsNullOrEmpty(path)) return false;

        try
        {
            var info = new SHELLEXECUTEINFO
            {
                cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
                lpVerb = "properties",
                lpFile = path,
                nShow = SW_SHOW,
                fMask = SEE_MASK_INVOKEIDLIST,
                hwnd = hwnd
            };

            return ShellExecuteEx(ref info);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsShellHelper] ShowFileProperties error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Opens the native Windows "Open with..." dialog for the specified file.
    /// </summary>
    public static void ShowOpenWithDialog(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = $"shell32.dll,OpenAs_RunDLL \"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsShellHelper] ShowOpenWithDialog error: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens the file with its default Windows shell association.
    /// </summary>
    public static void OpenFile(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsShellHelper] OpenFile error: {ex.Message}");
        }
    }

    /// <summary>
    /// Reveals the file or folder selected in Windows Explorer.
    /// </summary>
    public static void OpenInExplorer(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            else if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsShellHelper] OpenInExplorer error: {ex.Message}");
        }
    }

    /// <summary>
    /// Copies text (such as a full path) to the Windows clipboard.
    /// </summary>
    public static void CopyToClipboard(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

#if !TESTING
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsShellHelper] CopyToClipboard error: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Moves a file or directory to the Windows Recycle Bin (can be undone).
    /// </summary>
    public static bool MoveToRecycleBin(string path, IntPtr hwnd = default)
    {
        if (string.IsNullOrEmpty(path)) return false;

        try
        {
            // Path must be double-null terminated for SHFileOperation
            var fileOp = new SHFILEOPSTRUCT
            {
                hwnd = hwnd,
                wFunc = FO_DELETE,
                pFrom = path + "\0\0",
                fFlags = FOF_ALLOWUNDO | FOF_SILENT
            };

            int ret = SHFileOperation(ref fileOp);
            return ret == 0 && !fileOp.fAnyOperationsAborted;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsShellHelper] MoveToRecycleBin failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Permanently deletes a file or directory directly from the file system.
    /// </summary>
    public static bool DeletePermanently(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                return true;
            }
            else if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsShellHelper] DeletePermanently failed: {ex.Message}");
            return false;
        }
    }
}
