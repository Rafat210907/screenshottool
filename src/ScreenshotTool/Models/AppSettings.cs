// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — AppSettings Model                             ║
// ║  Stores all user-configurable settings as a JSON-serializable   ║
// ║  POCO. Saved to %APPDATA%\ScreenshotTool\settings.json          ║
// ╚══════════════════════════════════════════════════════════════════╝

using System.IO;
using System.Text.Json.Serialization;

namespace ScreenshotTool.Models;

public class AppSettings
{
    // ─── Save Location ─────────────────────────────────────────────
    /// <summary>
    /// Folder path to auto-save screenshots. "default" = Pictures\Screenshots.
    /// </summary>
    public string SaveFolder { get; set; } = "default";

    // ─── Hotkey Binding ────────────────────────────────────────────
    /// <summary>
    /// Global hotkey string, e.g. "Ctrl+Shift+S" or "Win+Shift+S".
    /// </summary>
    public string Hotkey { get; set; } = "Ctrl+Shift+S";

    // ─── Image Output ──────────────────────────────────────────────
    /// <summary>
    /// Image format: "png", "jpg", or "bmp".
    /// </summary>
    public string ImageFormat { get; set; } = "png";

    /// <summary>
    /// JPEG quality level (1–100). Only used when ImageFormat = "jpg".
    /// </summary>
    public int JpegQuality { get; set; } = 90;

    // ─── Clipboard ─────────────────────────────────────────────────
    /// <summary>
    /// Whether to copy every screenshot to the clipboard automatically.
    /// </summary>
    public bool CopyToClipboard { get; set; } = true;

    // ─── Startup ───────────────────────────────────────────────────
    /// <summary>
    /// Whether to launch the app at Windows startup (writes to registry).
    /// </summary>
    public bool StartWithWindows { get; set; } = true;

    // ─── Appearance ────────────────────────────────────────────────
    /// <summary>
    /// Ink color for the selection border (hex, e.g. "#FFFFFF" = white).
    /// </summary>
    public string InkColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Default snip mode when overlay opens: Rectangle, Freeform, Window, Fullscreen.
    /// </summary>
    public string DefaultSnipMode { get; set; } = "Rectangle";

    // ─── Computed Properties (not serialized) ──────────────────────
    /// <summary>
    /// Returns the actual filesystem path for the save folder.
    /// Falls back to Pictures\Screenshots when SaveFolder is "default".
    /// </summary>
    [JsonIgnore]
    public string ResolvedSaveFolder
    {
        get
        {
            if (SaveFolder == "default" || string.IsNullOrWhiteSpace(SaveFolder))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Screenshots");
            }
            return SaveFolder;
        }
    }
}
