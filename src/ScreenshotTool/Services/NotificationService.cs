// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — NotificationService                           ║
// ║  Shows Windows 10 toast notifications when a screenshot is      ║
// ║  captured. Includes image preview and "Open Folder" action.     ║
// ║  Uses Microsoft.Toolkit.Uwp.Notifications (works in WPF).      ║
// ╚══════════════════════════════════════════════════════════════════╝

using System.IO;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ScreenshotTool.Services;

public class NotificationService
{
    // ─── Show "Screenshot Saved" Toast ────────────────────────────
    /// <summary>
    /// Display a Windows toast notification with a thumbnail of the
    /// captured screenshot and an action button to open the folder.
    /// </summary>
    public void ShowScreenshotSaved(string filePath, string folderPath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);

            new ToastContentBuilder()
                .AddText("Screenshot Saved ✂️")
                .AddText(fileName)
                .AddInlineImage(new Uri(filePath))
                .AddButton(new ToastButton()
                    .SetContent("Open Folder")
                    .AddArgument("action", "openFolder")
                    .AddArgument("path", folderPath))
                .AddButton(new ToastButton()
                    .SetContent("Open Image")
                    .AddArgument("action", "openImage")
                    .AddArgument("path", filePath))
                .Show(toast => { toast.ExpirationTime = DateTimeOffset.Now.AddSeconds(10); });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NotificationService] Failed to show toast: {ex.Message}");
        }
    }

    // ─── Show Generic Info Toast ──────────────────────────────────
    /// <summary>
    /// Show a simple informational toast notification.
    /// </summary>
    public void ShowInfo(string title, string message)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show(toast => { toast.ExpirationTime = DateTimeOffset.Now.AddSeconds(10); });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NotificationService] Failed to show info toast: {ex.Message}");
        }
    }
}
