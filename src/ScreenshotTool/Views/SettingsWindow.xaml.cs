// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — SettingsWindow.xaml.cs (Code-Behind)          ║
// ║  Loads settings into UI controls, allows user to modify them,   ║
// ║  and saves changes back via SettingsService on "OK".            ║
// ╚══════════════════════════════════════════════════════════════════╝

using System.Windows;
using System.Windows.Controls;
using ScreenshotTool.Models;
using ScreenshotTool.Services;

// ─── Disambiguate WPF types from WinForms ─────────────────────────
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace ScreenshotTool.Views;

public partial class SettingsWindow : Window
{
    // ─── Dependencies ─────────────────────────────────────────────
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;

    // ─── Result: Whether settings were saved ──────────────────────
    public bool SettingsSaved { get; private set; }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Constructor — Populate UI from current settings            ║
    // ╚════════════════════════════════════════════════════════════╝
    public SettingsWindow(AppSettings settings, SettingsService settingsService, StartupService startupService)
    {
        InitializeComponent();

        _settings = settings;
        _settingsService = settingsService;
        _startupService = startupService;

        LoadSettingsToUI();
    }

    // ─── Load Settings → UI Controls ──────────────────────────────
    private void LoadSettingsToUI()
    {
        // Save folder
        TxtSaveFolder.Text = _settings.SaveFolder == "default"
            ? _settings.ResolvedSaveFolder
            : _settings.SaveFolder;

        // Hotkey dropdown
        SelectComboBoxItem(CmbHotkey, _settings.Hotkey);

        // Image format dropdown
        string formatDisplay = _settings.ImageFormat.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => "JPG (compressed)",
            "bmp" => "BMP (uncompressed)",
            _ => "PNG (lossless)"
        };
        SelectComboBoxItem(CmbFormat, formatDisplay);

        // JPEG quality
        SliderJpeg.Value = _settings.JpegQuality;
        TxtJpegValue.Text = _settings.JpegQuality.ToString();
        PnlJpegQuality.Visibility = _settings.ImageFormat.ToLowerInvariant() is "jpg" or "jpeg"
            ? Visibility.Visible : Visibility.Collapsed;

        // Ink color dropdown
        SelectComboBoxItemByTag(CmbInkColor, _settings.InkColor);

        // Checkboxes
        ChkStartup.IsChecked = _settings.StartWithWindows;
        ChkClipboard.IsChecked = _settings.CopyToClipboard;

        // Hotkey warning visibility
        UpdateHotkeyWarning();
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Event Handlers                                            ║
    // ╚════════════════════════════════════════════════════════════╝

    // ─── Browse for save folder ───────────────────────────────────
    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select screenshot save folder",
            SelectedPath = TxtSaveFolder.Text,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtSaveFolder.Text = dialog.SelectedPath;
        }
    }

    // ─── Format dropdown changed ──────────────────────────────────
    private void CmbFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PnlJpegQuality == null) return; // Designer guard

        var selected = (CmbFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        PnlJpegQuality.Visibility = selected.Contains("JPG")
            ? Visibility.Visible : Visibility.Collapsed;
    }

    // ─── JPEG quality slider changed ──────────────────────────────
    private void SliderJpeg_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtJpegValue != null)
        {
            TxtJpegValue.Text = ((int)SliderJpeg.Value).ToString();
        }
    }

    // ─── OK: Save settings ────────────────────────────────────────
    private void BtnOK_Click(object sender, RoutedEventArgs e)
    {
        // ── Read values from UI ──
        _settings.SaveFolder = TxtSaveFolder.Text;
        _settings.Hotkey = (CmbHotkey.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Ctrl+Shift+S";

        string formatText = (CmbFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        _settings.ImageFormat = formatText switch
        {
            string s when s.Contains("JPG") => "jpg",
            string s when s.Contains("BMP") => "bmp",
            _ => "png"
        };

        _settings.JpegQuality = (int)SliderJpeg.Value;

        // Ink color from tag
        var colorItem = CmbInkColor.SelectedItem as ComboBoxItem;
        _settings.InkColor = colorItem?.Tag?.ToString() ?? "#FF0000";

        _settings.StartWithWindows = ChkStartup.IsChecked == true;
        _settings.CopyToClipboard = ChkClipboard.IsChecked == true;

        // ── Apply startup registration ──
        _startupService.SetStartup(_settings.StartWithWindows);

        // ── Save to disk ──
        _settingsService.Save(_settings);
        SettingsSaved = true;

        DialogResult = true;
        Close();
    }

    // ─── Cancel: Discard changes ──────────────────────────────────
    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Helper Methods                                            ║
    // ╚════════════════════════════════════════════════════════════╝

    // ─── Select ComboBox item by content text (partial match) ─────
    private static void SelectComboBoxItem(ComboBox cmb, string value)
    {
        foreach (ComboBoxItem item in cmb.Items)
        {
            if (item.Content?.ToString()?.Contains(value, StringComparison.OrdinalIgnoreCase) == true)
            {
                cmb.SelectedItem = item;
                return;
            }
        }
        // Exact match fallback
        foreach (ComboBoxItem item in cmb.Items)
        {
            if (item.Content?.ToString() == value)
            {
                cmb.SelectedItem = item;
                return;
            }
        }
        if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
    }

    // ─── Select ComboBox item by Tag value (for ink color) ────────
    private static void SelectComboBoxItemByTag(ComboBox cmb, string tagValue)
    {
        foreach (ComboBoxItem item in cmb.Items)
        {
            if (item.Tag?.ToString()?.Equals(tagValue, StringComparison.OrdinalIgnoreCase) == true)
            {
                cmb.SelectedItem = item;
                return;
            }
        }
        if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
    }

    // ─── Show warning if Win+Shift+S is selected ─────────────────
    private void UpdateHotkeyWarning()
    {
        if (TxtHotkeyWarning == null) return;

        var selected = (CmbHotkey.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        TxtHotkeyWarning.Visibility = selected.Contains("Win+Shift+S")
            ? Visibility.Visible : Visibility.Collapsed;
    }
}
