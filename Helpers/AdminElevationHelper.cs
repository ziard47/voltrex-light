using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace voltrex_light.Helpers;

/// <summary>
/// Helper for querying and requesting Windows Administrator elevation.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AdminElevationHelper
{
    private static readonly Lazy<bool> _isAdminLazy = new(CheckIsAdministrator);

    /// <summary>
    /// Gets whether the current process is running with elevated Administrator privileges.
    /// </summary>
    public static bool IsAdministrator => _isAdminLazy.Value;

    private static bool CheckIsAdministrator()
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return false;

            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Relaunches the current application requesting Administrator privileges via UAC prompt.
    /// </summary>
    public static bool RestartAsAdministrator()
    {
        try
        {
            var mainModule = Process.GetCurrentProcess().MainModule;
            var exePath = mainModule?.FileName;

            if (string.IsNullOrEmpty(exePath))
            {
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas" // Requests elevation from Windows Shell
            };

            var process = Process.Start(startInfo);
            if (process != null)
            {
                Environment.Exit(0);
                return true;
            }
        }
        catch (Exception)
        {
            // User cancelled the UAC prompt or operation was rejected
        }

        return false;
    }
}
