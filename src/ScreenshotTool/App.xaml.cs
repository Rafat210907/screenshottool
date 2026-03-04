// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — App.xaml.cs (Application Entry Point)         ║
// ║                                                                  ║
// ║  Responsibilities:                                               ║
// ║    • Single-instance enforcement via named Mutex                 ║
// ║    • System tray icon with context menu (Hardcodet)              ║
// ║    • Global hotkey registration (HotkeyService)                  ║
// ║    • Toast notification callback handling                        ║
// ║    • Orchestrates overlay → capture → save → clipboard → toast   ║
// ╚══════════════════════════════════════════════════════════════════╝

using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Toolkit.Uwp.Notifications;
using ScreenshotTool.Helpers;
using ScreenshotTool.Models;
using ScreenshotTool.Services;
using ScreenshotTool.Views;

// ─── Disambiguate WPF types from WinForms ─────────────────────────
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace ScreenshotTool;

public partial class App : Application
{
    // ─── Single Instance Mutex ────────────────────────────────────
    private Mutex? _singleInstanceMutex;
    private const string MutexName = "ScreenshotTool_SingleInstance_Mutex";

    // ─── System Tray Icon ─────────────────────────────────────────
    private TaskbarIcon? _trayIcon;

    // ─── Services ─────────────────────────────────────────────────
    private SettingsService _settingsService = null!;
    private HotkeyService _hotkeyService = null!;
    private ScreenCaptureService _captureService = null!;
    private ClipboardService _clipboardService = null!;
    private NotificationService _notificationService = null!;
    private StartupService _startupService = null!;
    private WindowEnumerationService _windowEnumService = null!;

    // ─── Settings ─────────────────────────────────────────────────
    private AppSettings _settings = null!;

    // ─── Hidden Helper Window (for hotkey message routing) ────────
    private Window? _helperWindow;

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Application Startup                                       ║
    // ╚════════════════════════════════════════════════════════════╝
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ─── 1. Single Instance Check ─────────────────────────────
        _singleInstanceMutex = new Mutex(true, MutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show(
                "Screenshot Tool is already running.\nCheck the system tray icon.",
                "Screenshot Tool", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // ─── 2. Initialize Services ──────────────────────────────
        _settingsService = new SettingsService();
        _captureService = new ScreenCaptureService();
        _clipboardService = new ClipboardService();
        _notificationService = new NotificationService();
        _startupService = new StartupService();
        _windowEnumService = new WindowEnumerationService();
        _hotkeyService = new HotkeyService();

        // ─── 3. Load Settings ─────────────────────────────────────
        _settings = _settingsService.Load();

        // ─── 4. Ensure Screenshots Folder Exists ──────────────────
        Directory.CreateDirectory(_settings.ResolvedSaveFolder);

        // ─── 5. Register Toast Notification Callbacks ─────────────
        RegisterToastCallbacks();

        // ─── 6. Create System Tray Icon ───────────────────────────
        CreateTrayIcon();

        // ─── 7. Create Hidden Helper Window & Register Hotkey ─────
        CreateHelperWindowAndRegisterHotkey();

        // ─── 8. Show startup notification ─────────────────────────
        _trayIcon?.ShowBalloonTip(
            "Screenshot Tool",
            $"Running in system tray. Press {_settings.Hotkey} to capture.",
            BalloonIcon.Info);
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  System Tray Icon Setup                                    ║
    // ╚════════════════════════════════════════════════════════════╝
    private void CreateTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            Icon = CreateTrayIconImage(),
            ToolTipText = $"Screenshot Tool ({_settings.Hotkey})",
            ContextMenu = CreateTrayContextMenu(),
        };

        // Double-click tray icon → take screenshot
        _trayIcon.TrayMouseDoubleClick += (s, e) => OnHotkeyPressed();
    }

    // ─── Tray Icon Image (from embedded resource) ─────────────────
    private static Icon CreateTrayIconImage()
    {
        // Load the embedded app.ico resource
        var iconUri = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
        var stream = Application.GetResourceStream(iconUri)?.Stream;
        if (stream != null)
        {
            return new Icon(stream);
        }

        // Fallback: create a simple icon programmatically
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        using var pen = new Pen(Color.White, 2);
        g.DrawLine(pen, 2, 2, 14, 14);
        g.DrawLine(pen, 14, 2, 2, 14);
        var hIcon = bmp.GetHicon();
        return Icon.FromHandle(hIcon);
    }

    // ─── Tray Context Menu ────────────────────────────────────────
    private System.Windows.Controls.ContextMenu CreateTrayContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        // ── Take Screenshot ──
        var takeScreenshot = new System.Windows.Controls.MenuItem
        {
            Header = $"✂ Take Screenshot ({_settings.Hotkey})",
            FontWeight = FontWeights.Bold
        };
        takeScreenshot.Click += (s, e) => OnHotkeyPressed();
        menu.Items.Add(takeScreenshot);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // ── Open Screenshots Folder ──
        var openFolder = new System.Windows.Controls.MenuItem { Header = "📁 Open Screenshots Folder" };
        openFolder.Click += (s, e) =>
        {
            var folder = _settings.ResolvedSaveFolder;
            Directory.CreateDirectory(folder);
            Process.Start("explorer.exe", folder);
        };
        menu.Items.Add(openFolder);

        // ── Settings ──
        var settingsItem = new System.Windows.Controls.MenuItem { Header = "⚙ Settings" };
        settingsItem.Click += (s, e) => ShowSettingsWindow();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        // ── Exit ──
        var exit = new System.Windows.Controls.MenuItem { Header = "❌ Exit" };
        exit.Click += (s, e) => ExitApplication();
        menu.Items.Add(exit);

        return menu;
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Helper Window & Hotkey Registration                       ║
    // ╚════════════════════════════════════════════════════════════╝
    private void CreateHelperWindowAndRegisterHotkey()
    {
        // Create a hidden window to receive WM_HOTKEY messages
        _helperWindow = new Window
        {
            Width = 0, Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Visibility = Visibility.Hidden
        };
        _helperWindow.Show();
        _helperWindow.Hide();

        // Register the global hotkey
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        bool registered = _hotkeyService.Register(_helperWindow, _settings.Hotkey);

        if (!registered)
        {
            // If Win+Shift+S failed, try Ctrl+Shift+S as fallback
            if (_settings.Hotkey.Contains("Win"))
            {
                _settings.Hotkey = "Ctrl+Shift+S";
                _settingsService.Save(_settings);
                registered = _hotkeyService.Register(_helperWindow, _settings.Hotkey);
            }

            if (!registered)
            {
                _trayIcon?.ShowBalloonTip(
                    "Screenshot Tool",
                    $"Failed to register hotkey {_settings.Hotkey}. Another app may be using it.",
                    BalloonIcon.Warning);
            }
        }
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Hotkey Pressed → Open Overlay → Capture → Save            ║
    // ╚════════════════════════════════════════════════════════════╝
    private void OnHotkeyPressed()
    {
        // Parse default snip mode from settings
        var mode = _settings.DefaultSnipMode switch
        {
            "Freeform" => SnipMode.Freeform,
            "Window" => SnipMode.Window,
            "Fullscreen" => SnipMode.Fullscreen,
            _ => SnipMode.Rectangle
        };

        // ─── Show Overlay ─────────────────────────────────────────
        var overlay = new OverlayWindow(
            _captureService, _windowEnumService,
            _settings.InkColor, mode);

        overlay.ShowDialog();

        // ─── Process Capture Result ───────────────────────────────
        if (!overlay.WasCancelled && overlay.CapturedBitmap != null)
        {
            ProcessCapture(overlay.CapturedBitmap);
        }
    }

    // ─── Save + Clipboard + Toast ─────────────────────────────────
    private void ProcessCapture(Bitmap bitmap)
    {
        try
        {
            // ── 1. Ensure save folder exists ──
            var saveFolder = _settings.ResolvedSaveFolder;
            Directory.CreateDirectory(saveFolder);

            // ── 2. Generate filename: "Screenshot YYYY-MM-DD HHMMSS.png" ──
            var fileName = BitmapHelper.GenerateFileName(_settings.ImageFormat);
            var filePath = Path.Combine(saveFolder, fileName);

            // ── 3. Save to disk ──
            BitmapHelper.SaveBitmap(bitmap, filePath, _settings.ImageFormat, _settings.JpegQuality);

            // ── 4. Copy to clipboard ──
            if (_settings.CopyToClipboard)
            {
                _clipboardService.CopyToClipboard(bitmap);
            }

            // ── 5. Show toast notification ──
            _notificationService.ShowScreenshotSaved(filePath, saveFolder);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] Capture processing failed: {ex.Message}");
            _notificationService.ShowInfo("Screenshot Tool", $"Error saving: {ex.Message}");
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Toast Notification Click Handling                          ║
    // ╚════════════════════════════════════════════════════════════╝
    private void RegisterToastCallbacks()
    {
        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            var args = ToastArguments.Parse(toastArgs.Argument);

            if (args.TryGetValue("action", out string? action))
            {
                switch (action)
                {
                    case "openFolder":
                        if (args.TryGetValue("path", out string? folderPath))
                            Process.Start("explorer.exe", folderPath);
                        break;

                    case "openImage":
                        if (args.TryGetValue("path", out string? imagePath))
                            Process.Start(new ProcessStartInfo(imagePath) { UseShellExecute = true });
                        break;
                }
            }
        };
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Settings Window                                           ║
    // ╚════════════════════════════════════════════════════════════╝
    private void ShowSettingsWindow()
    {
        var settingsWindow = new SettingsWindow(_settings, _settingsService, _startupService);
        settingsWindow.ShowDialog();

        if (settingsWindow.SettingsSaved)
        {
            // Reload settings and re-register hotkey
            _settings = _settingsService.Load();

            _hotkeyService.Unregister();
            _hotkeyService.Register(_helperWindow!, _settings.Hotkey);

            // Update tray tooltip
            if (_trayIcon != null)
            {
                _trayIcon.ToolTipText = $"Screenshot Tool ({_settings.Hotkey})";
                _trayIcon.ContextMenu = CreateTrayContextMenu();
            }
        }
    }

    // ╔════════════════════════════════════════════════════════════╗
    // ║  Application Exit & Cleanup                                ║
    // ╚════════════════════════════════════════════════════════════╝
    private void ExitApplication()
    {
        _hotkeyService.Dispose();
        _trayIcon?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIcon?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }
}
