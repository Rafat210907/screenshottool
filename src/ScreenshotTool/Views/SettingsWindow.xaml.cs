// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — SettingsWindow.xaml.cs (Code-Behind)          ║
// ║  Loads settings into UI controls, allows user to modify them,   ║
// ║  and saves changes back via SettingsService on "OK".            ║
// ╚══════════════════════════════════════════════════════════════════╝

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenshotTool.Models;
using ScreenshotTool.Services;

// ─── Disambiguate WPF types from WinForms ─────────────────────────
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ScreenshotTool.Views;

public partial class SettingsWindow : Window
{
    // ─── Dependencies ─────────────────────────────────────────────
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;

    // ─── Result: Whether settings were saved ──────────────────────
    public bool SettingsSaved { get; private set; }

    // ─── Custom Hotkey State ──────────────────────────────────────
    private string _capturedHotkey = string.Empty;

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

        // Hotkey textbox
        _capturedHotkey = _settings.Hotkey;
        TxtHotkey.Text = _settings.Hotkey;

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

    // ─── Hotkey TextBox: Capture key combination ──────────────────
    private void TxtHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true; // prevent normal TextBox input

        // Get the actual key (resolve system keys like Alt)
        var key = (e.Key == Key.System) ? e.SystemKey : e.Key;

        // Ignore lone modifier presses — wait for a real key
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            return;
        }

        // Build modifier string
        var parts = new List<string>();
        var mods = Keyboard.Modifiers;

        if (mods.HasFlag(ModifierKeys.Windows))
            parts.Add("Win");
        if (mods.HasFlag(ModifierKeys.Control))
            parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");

        // Convert the key to a readable name
        string keyName = KeyToString(key);
        if (string.IsNullOrEmpty(keyName)) return;

        parts.Add(keyName);

        _capturedHotkey = string.Join("+", parts);
        TxtHotkey.Text = _capturedHotkey;

        UpdateHotkeyWarning();
    }

    private void TxtHotkey_GotFocus(object sender, RoutedEventArgs e)
    {
        TxtHotkey.BorderBrush = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6699FF"));
        TxtHotkeyHint.Text = "Listening... press your desired key combination";
    }

    private void TxtHotkey_LostFocus(object sender, RoutedEventArgs e)
    {
        TxtHotkey.BorderBrush = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#555555"));
        TxtHotkeyHint.Text = "Press a key combination (e.g. Ctrl+Shift+S)";
    }

    private void BtnResetHotkey_Click(object sender, RoutedEventArgs e)
    {
        _capturedHotkey = "Ctrl+Shift+S";
        TxtHotkey.Text = _capturedHotkey;
        UpdateHotkeyWarning();
    }

    /// <summary>
    /// Converts a WPF Key enum value to a human-readable string
    /// matching the format expected by HotkeyService.ParseHotkey.
    /// </summary>
    private static string KeyToString(Key key)
    {
        return key switch
        {
            // Letters
            >= Key.A and <= Key.Z => key.ToString(),
            // Number row
            >= Key.D0 and <= Key.D9 => key.ToString()[1..], // "D0" → "0"
            // Numpad
            >= Key.NumPad0 and <= Key.NumPad9 => "Num" + key.ToString().Replace("NumPad", ""),
            // Function keys
            >= Key.F1 and <= Key.F24 => key.ToString(),
            // Special keys
            Key.PrintScreen or Key.Snapshot => "PrintScreen",
            Key.Space => "Space",
            Key.Enter or Key.Return => "Enter",
            Key.Escape => "Escape",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.OemTilde => "`",
            Key.OemMinus => "-",
            Key.OemPlus => "=",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemSemicolon => ";",
            Key.OemQuotes => "'",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            _ => string.Empty
        };
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
        _settings.Hotkey = _capturedHotkey;

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

        TxtHotkeyWarning.Visibility = _capturedHotkey.Contains("Win+Shift+S")
            ? Visibility.Visible : Visibility.Collapsed;
    }
}
