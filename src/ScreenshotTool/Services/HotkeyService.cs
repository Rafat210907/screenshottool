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
    /// Supports: letters A–Z, digits 0–9, F1–F24, PrintScreen, Space,
    /// Enter, Tab, Escape, Backspace, Delete, Insert, Home, End,
    /// PageUp, PageDown, arrow keys, numpad keys, and punctuation.
    /// </summary>
    public static void ParseHotkey(string hotkeyString, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var upper = part.ToUpperInvariant();
            switch (upper)
            {
                // ── Modifiers ──────────────────────────────────
                case "CTRL" or "CONTROL":
                    modifiers |= NativeMethods.MOD_CTRL;
                    break;
                case "SHIFT":
                    modifiers |= NativeMethods.MOD_SHIFT;
                    break;
                case "ALT":
                    modifiers |= NativeMethods.MOD_ALT;
                    break;
                case "WIN" or "WINDOWS":
                    modifiers |= NativeMethods.MOD_WIN;
                    break;

                // ── Print Screen ───────────────────────────────
                case "PRINTSCREEN" or "PRTSC" or "SNAPSHOT":
                    vk = NativeMethods.VK_SNAPSHOT;
                    break;

                // ── Function keys F1–F24 ───────────────────────
                case var f when f.Length >= 2 && f[0] == 'F' && int.TryParse(f[1..], out int fNum) && fNum >= 1 && fNum <= 24:
                    vk = (uint)(0x70 + (fNum - 1)); // VK_F1 = 0x70
                    break;

                // ── Digits 0–9 ─────────────────────────────────
                case var d when d.Length == 1 && char.IsDigit(d[0]):
                    vk = (uint)d[0]; // '0'=0x30 .. '9'=0x39
                    break;

                // ── Numpad digits ──────────────────────────────
                case var n when n.StartsWith("NUM") && int.TryParse(n[3..], out int nNum) && nNum >= 0 && nNum <= 9:
                    vk = (uint)(0x60 + nNum); // VK_NUMPAD0 = 0x60
                    break;

                // ── Navigation / editing keys ──────────────────
                case "SPACE":       vk = 0x20; break;
                case "ENTER":       vk = 0x0D; break;
                case "TAB":         vk = 0x09; break;
                case "ESCAPE":      vk = 0x1B; break;
                case "BACKSPACE":   vk = 0x08; break;
                case "DELETE":      vk = 0x2E; break;
                case "INSERT":      vk = 0x2D; break;
                case "HOME":        vk = 0x24; break;
                case "END":         vk = 0x23; break;
                case "PAGEUP":      vk = 0x21; break;
                case "PAGEDOWN":    vk = 0x22; break;
                case "UP":          vk = 0x26; break;
                case "DOWN":        vk = 0x28; break;
                case "LEFT":        vk = 0x25; break;
                case "RIGHT":       vk = 0x27; break;

                // ── Punctuation / OEM keys ─────────────────────
                case "`":   vk = 0xC0; break;  // VK_OEM_3
                case "-":   vk = 0xBD; break;  // VK_OEM_MINUS
                case "=":   vk = 0xBB; break;  // VK_OEM_PLUS
                case "[":   vk = 0xDB; break;  // VK_OEM_4
                case "]":   vk = 0xDD; break;  // VK_OEM_6
                case "\\":  vk = 0xDC; break;  // VK_OEM_5
                case ";":   vk = 0xBA; break;  // VK_OEM_1
                case "'":   vk = 0xDE; break;  // VK_OEM_7
                case ",":   vk = 0xBC; break;  // VK_OEM_COMMA
                case ".":   vk = 0xBE; break;  // VK_OEM_PERIOD
                case "/":   vk = 0xBF; break;  // VK_OEM_2

                default:
                    // Single letter key: A-Z → virtual key code 0x41-0x5A
                    if (upper.Length == 1 && char.IsLetter(upper[0]))
                    {
                        vk = (uint)char.ToUpperInvariant(upper[0]);
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
