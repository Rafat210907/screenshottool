// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — StartupService                                ║
// ║  Manages Windows startup registration via the registry key:     ║
// ║  HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run             ║
// ╚══════════════════════════════════════════════════════════════════╝

using Microsoft.Win32;
using System.Diagnostics;

namespace ScreenshotTool.Services;

public class StartupService
{
    // ─── Constants ────────────────────────────────────────────────
    private const string AppName = "ScreenshotTool";
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    // ─── Check if Startup is Enabled ──────────────────────────────
    /// <summary>
    /// Returns true if the app is registered to start with Windows.
    /// </summary>
    public bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    // ─── Enable / Disable Startup ─────────────────────────────────
    /// <summary>
    /// Add or remove the app from the Windows startup registry key.
    /// When enabled, the app launches with "--minimized" argument
    /// so it starts silently in the system tray.
    /// </summary>
    public void SetStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;

            if (enable)
            {
                // Get the path of the currently running executable
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\" --minimized");
                }
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[StartupService] Failed to set startup: {ex.Message}");
        }
    }
}
