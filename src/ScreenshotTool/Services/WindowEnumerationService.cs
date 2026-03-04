// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — WindowEnumerationService                      ║
// ║  Enumerates all visible top-level windows using Win32 APIs.     ║
// ║  Used by Window Snip mode to let the user click on a window     ║
// ║  and capture it. Uses DWM extended frame bounds for accuracy.   ║
// ╚══════════════════════════════════════════════════════════════════╝

using System.Runtime.InteropServices;
using System.Text;
using ScreenshotTool.Helpers;

namespace ScreenshotTool.Services;

/// <summary>
/// Represents a visible OS window with its title and pixel-accurate bounds.
/// </summary>
public record WindowInfo(IntPtr Handle, string Title, System.Drawing.Rectangle Bounds);

public class WindowEnumerationService
{
    // ─── Enumerate All Visible Windows ────────────────────────────
    /// <summary>
    /// Returns a list of all visible, non-cloaked top-level windows
    /// with their DWM-corrected bounds (no invisible Win10 borders).
    /// </summary>
    public List<WindowInfo> GetVisibleWindows()
    {
        var windows = new List<WindowInfo>();
        var shellWindow = NativeMethods.GetShellWindow();
        var desktopWindow = NativeMethods.GetDesktopWindow();

        NativeMethods.EnumWindows((hWnd, lParam) =>
        {
            // Skip invisible windows
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;

            // Skip desktop & shell windows
            if (hWnd == shellWindow || hWnd == desktopWindow) return true;

            // Skip cloaked (hidden UWP) windows
            if (IsWindowCloaked(hWnd)) return true;

            // Skip windows with no title
            int titleLength = NativeMethods.GetWindowTextLength(hWnd);
            if (titleLength == 0) return true;

            // Get window title
            var titleBuilder = new StringBuilder(titleLength + 1);
            NativeMethods.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
            string title = titleBuilder.ToString();

            // Skip certain system windows
            if (ShouldSkipWindow(hWnd, title)) return true;

            // Get accurate bounds (DWM extended frame, not GetWindowRect)
            var bounds = GetAccurateWindowBounds(hWnd);
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                windows.Add(new WindowInfo(hWnd, title, bounds));
            }

            return true; // Continue enumeration
        }, IntPtr.Zero);

        return windows;
    }

    // ─── Find Window at Screen Coordinates ────────────────────────
    /// <summary>
    /// Find which window in the list contains the given screen point.
    /// Returns the topmost (first) match since EnumWindows returns Z-order.
    /// </summary>
    public WindowInfo? FindWindowAtPoint(List<WindowInfo> windows, int screenX, int screenY)
    {
        return windows.FirstOrDefault(w =>
            screenX >= w.Bounds.X && screenX < w.Bounds.Right &&
            screenY >= w.Bounds.Y && screenY < w.Bounds.Bottom);
    }

    // ─── Get Accurate Window Bounds (DWM) ─────────────────────────
    /// <summary>
    /// Uses DwmGetWindowAttribute with DWMWA_EXTENDED_FRAME_BOUNDS
    /// for pixel-accurate bounds on Windows 10 (excludes invisible borders).
    /// Falls back to GetWindowRect if DWM call fails.
    /// </summary>
    private static System.Drawing.Rectangle GetAccurateWindowBounds(IntPtr hWnd)
    {
        int result = NativeMethods.DwmGetWindowAttribute(
            hWnd, NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
            out NativeMethods.RECT dwmRect,
            Marshal.SizeOf<NativeMethods.RECT>());

        if (result == 0) // S_OK
        {
            return new System.Drawing.Rectangle(
                dwmRect.Left, dwmRect.Top,
                dwmRect.Width, dwmRect.Height);
        }

        // Fallback to GetWindowRect (includes invisible borders)
        NativeMethods.GetWindowRect(hWnd, out NativeMethods.RECT rect);
        return new System.Drawing.Rectangle(
            rect.Left, rect.Top, rect.Width, rect.Height);
    }

    // ─── Check if Window is Cloaked ───────────────────────────────
    /// <summary>
    /// Windows 10 hides some UWP windows by "cloaking" them.
    /// These should be excluded from enumeration.
    /// </summary>
    private static bool IsWindowCloaked(IntPtr hWnd)
    {
        int result = NativeMethods.DwmGetWindowAttribute(
            hWnd, NativeMethods.DWMWA_CLOAKED,
            out NativeMethods.RECT cloaked,
            Marshal.SizeOf<NativeMethods.RECT>());
        return result == 0 && cloaked.Left != 0;
    }

    // ─── Filter Out System / Tool Windows ─────────────────────────
    /// <summary>
    /// Skip known system windows that shouldn't be snip targets.
    /// </summary>
    private static bool ShouldSkipWindow(IntPtr hWnd, string title)
    {
        // Skip our own overlay window
        var className = new StringBuilder(256);
        NativeMethods.GetClassName(hWnd, className, className.Capacity);
        string cls = className.ToString();

        // Skip common system windows
        if (title == "Program Manager") return true;
        if (cls == "Shell_TrayWnd") return true;            // Taskbar
        if (cls == "Shell_SecondaryTrayWnd") return true;    // Secondary taskbar
        if (cls == "Windows.UI.Core.CoreWindow") return true; // Some hidden UWP windows

        return false;
    }
}
