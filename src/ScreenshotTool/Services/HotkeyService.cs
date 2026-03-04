// ╔══════════════════════════════════════════════════════════════════╗
// ║  ScreenshotTool — HotkeyService                                 ║
// ║  Registers a global hotkey via Win32 RegisterHotKey API.        ║
// ║  Hooks into the WPF HwndSource to receive WM_HOTKEY messages.   ║
// ║  Supports Ctrl+Shift+S (default) and Win+Shift+S (opt-in).     ║
// ╚══════════════════════════════════════════════════════════════════╝

using System.Windows;
using System.Windows.Interop;
using ScreenshotTool.Helpers;

namespace ScreenshotTool.Services;

public class HotkeyService : IDisposable
{
    // ─── Constants ────────────────────────────────────────────────
    private const int HOTKEY_ID = 9000;

    // ─── State ────────────────────────────────────────────────────
    private IntPtr _windowHandle;
    private HwndSource? _source;
    private bool _isRegistered;

    // ─── Event: Fires when global hotkey is pressed ───────────────
    public event Action? HotkeyPressed;

    // ─── Register Hotkey ──────────────────────────────────────────
    /// <summary>
    /// Parse a hotkey string like "Ctrl+Shift+S" and register it globally.
    /// Must be called after the helper window has a valid HWND.
    /// </summary>
    public bool Register(Window helperWindow, string hotkeyString)
    {
        // Get window handle for message routing
        var helper = new WindowInteropHelper(helperWindow);
        _windowHandle = helper.Handle;
        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(HwndHook);

        // Parse the hotkey string into modifiers + virtual key
        ParseHotkey(hotkeyString, out uint modifiers, out uint vk);

        // Attempt registration with MOD_NOREPEAT to prevent auto-repeat spam
        _isRegistered = NativeMethods.RegisterHotKey(
            _windowHandle, HOTKEY_ID,
            modifiers | NativeMethods.MOD_NOREPEAT, vk);

        if (!_isRegistered)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[HotkeyService] Failed to register hotkey: {hotkeyString} " +
                $"(Error: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()})");
        }

        return _isRegistered;
    }

    // ─── Unregister Hotkey ────────────────────────────────────────
    /// <summary>
    /// Unregister the currently bound hotkey and remove the message hook.
    /// </summary>
    public void Unregister()
    {
        if (_isRegistered)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _isRegistered = false;
        }
        _source?.RemoveHook(HwndHook);
    }

    // ─── WndProc Hook: Listen for WM_HOTKEY ──────────────────────
    /// <summary>
    /// Intercepts Windows messages. Fires HotkeyPressed on WM_HOTKEY.
    /// </summary>
    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ─── Parse Hotkey String ──────────────────────────────────────
    /// <summary>
    /// Converts a human-readable hotkey string (e.g. "Ctrl+Shift+S")
    /// into Win32 modifier flags and a virtual key code.
    /// </summary>
    public static void ParseHotkey(string hotkeyString, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= NativeMethods.MOD_CTRL;
                    break;
                case "SHIFT":
                    modifiers |= NativeMethods.MOD_SHIFT;
                    break;
                case "ALT":
                    modifiers |= NativeMethods.MOD_ALT;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= NativeMethods.MOD_WIN;
                    break;
                case "PRINTSCREEN":
                case "PRTSC":
                case "SNAPSHOT":
                    vk = NativeMethods.VK_SNAPSHOT;
                    break;
                default:
                    // Single letter key: A-Z → virtual key code 0x41-0x5A
                    if (part.Length == 1 && char.IsLetter(part[0]))
                    {
                        vk = (uint)char.ToUpperInvariant(part[0]);
                    }
                    break;
            }
        }
    }

    // ─── Dispose ──────────────────────────────────────────────────
    public void Dispose()
    {
        Unregister();
        GC.SuppressFinalize(this);
    }
}
