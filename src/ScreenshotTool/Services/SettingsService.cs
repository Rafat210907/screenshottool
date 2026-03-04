// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — SettingsService                               ║
// ║  Loads and saves AppSettings as JSON in %APPDATA%.              ║
// ║  Creates default settings file on first run.                    ║
// ╚══════════════════════════════════════════════════════════════════╝

using System.IO;
using System.Text.Json;
using ScreenshotTool.Models;

namespace ScreenshotTool.Services;

public class SettingsService
{
    // ─── File Paths ───────────────────────────────────────────────
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ScreenshotTool");

    private static readonly string SettingsFilePath = Path.Combine(
        AppDataFolder, "settings.json");

    // ─── JSON Serializer Options ──────────────────────────────────
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // ─── Load Settings ────────────────────────────────────────────
    /// <summary>
    /// Load settings from disk. Returns defaults if file doesn't exist or is corrupt.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                return settings ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to load: {ex.Message}");
        }

        // First run — create default settings
        var defaults = new AppSettings();
        Save(defaults);
        return defaults;
    }

    // ─── Save Settings ────────────────────────────────────────────
    /// <summary>
    /// Persist current settings to disk as JSON.
    /// </summary>
    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDataFolder);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Failed to save: {ex.Message}");
        }
    }
}
