<!-- ───────────────────────────────────────────────────── -->
<!--      Remove the Built-in Snipping Tool              -->
<!-- ───────────────────────────────────────────────────── -->

## Remove the Built-in Snipping Tool

> Follow these steps to fully remove the Windows 10 Snipping Tool and prevent hotkey conflicts with ScreenshotTool.

### Step 1 — Remove the App Package

Open **PowerShell as Administrator** and run:

```powershell
Get-AppxPackage -AllUsers *ScreenSketch* | Remove-AppxPackage -AllUsers
Get-AppxProvisionedPackage -Online | Where-Object {$_.DisplayName -like "*ScreenSketch*"} | Remove-AppxProvisionedPackage -Online
```

Restart your PC after this step.

### Step 2 — Disable the Win + Shift + S Overlay

Even after removal, Windows may still intercept the `Win+Shift+S` shortcut.

**Registry method (recommended)** — run in Admin PowerShell:

```powershell
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" /v DisabledHotkeys /t REG_SZ /d S /f
```

Then restart Explorer:

```powershell
taskkill /f /im explorer.exe
start explorer.exe
```

### Step 3 — Disable Print Screen Opening Snipping Tool

**Option A — Settings UI:**

1. Open **Settings > Ease of Access > Keyboard**
2. Turn **OFF** *"Use the Print Screen key to launch screen snipping"*

**Option B — Registry:**

```powershell
reg add "HKCU\Control Panel\Keyboard" /v PrintScreenKeyForSnippingEnabled /t REG_DWORD /d 0 /f
```

---