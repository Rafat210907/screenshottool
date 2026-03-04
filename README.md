# ScreenshotTool

A lightweight Snipping Tool replacement for Windows 10, built with WPF (.NET 8).

**Why?** The built-in Windows 10 Snipping Tool doesn't automatically save screenshots to a folder — this tool does, and it receives updates when needed.

---

<!-- ───────────────────────────────────────────────────── -->
<!-- GROUP: Features                                       -->
<!-- ───────────────────────────────────────────────────── -->

## Features

- **Multiple Snip Modes** — Rectangle, Freeform, Window, and Fullscreen capture
- **Auto-Save** — Screenshots are saved automatically to `Pictures\Screenshots` (or a custom folder)
- **Clipboard Copy** — Optionally copies every screenshot to the clipboard
- **Configurable Hotkey** — Default `Ctrl+Shift+S`; fully customizable
- **System Tray Integration** — Runs quietly in the background with a tray icon
- **Single Instance** — Prevents duplicate processes via a named Mutex
- **Toast Notifications** — Click-to-open notifications after each capture
- **Start with Windows** — Optional auto-launch at login
- **Image Format Options** — Save as PNG, JPG (with quality slider), or BMP
- **Customizable Ink Color** — Change the selection border color

---

<!-- ───────────────────────────────────────────────────── -->
<!-- GROUP: Requirements                                   -->
<!-- ───────────────────────────────────────────────────── -->

## Requirements

- Windows 10 (build 17763+) or later
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (x64)

---

<!-- ───────────────────────────────────────────────────── -->
<!-- GROUP: Build & Run                                    -->
<!-- ───────────────────────────────────────────────────── -->

## Build & Run

```bash
cd src/ScreenshotTool
dotnet build
dotnet run
```

To publish a single-file executable:

```bash
dotnet publish -c Release
```

---



<!-- ───────────────────────────────────────────────────── -->
<!-- GROUP: License                                        -->
<!-- ───────────────────────────────────────────────────── -->

## Author

**Tahsan Ahmed Rafat** — AKA *PHENOMENAL*

---

## License

This project is provided as-is. See the repository for details.
