// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — ClipboardService                              ║
// ║  Copies captured screenshots to the Windows clipboard.          ║
// ║  Must be called on the STA (UI) thread.                         ║
// ╚══════════════════════════════════════════════════════════════════╝

using System.Drawing;
using System.Windows;
using ScreenshotTool.Helpers;

// ─── Disambiguate WPF Clipboard from WinForms ───────────────────
using Clipboard = System.Windows.Clipboard;

namespace ScreenshotTool.Services;

public class ClipboardService
{
    // ─── Copy Bitmap to Clipboard ─────────────────────────────────
    /// <summary>
    /// Copy a System.Drawing.Bitmap to the WPF clipboard as a BitmapSource.
    /// Must be called on the UI/STA thread (use Dispatcher.Invoke if needed).
    /// </summary>
    public void CopyToClipboard(Bitmap bitmap)
    {
        try
        {
            var bitmapSource = BitmapHelper.ToBitmapSource(bitmap);
            bitmapSource.Freeze(); // Required for cross-thread clipboard access
            Clipboard.SetImage(bitmapSource);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ClipboardService] Failed to copy to clipboard: {ex.Message}");
        }
    }
}
