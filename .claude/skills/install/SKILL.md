---
name: install
description: Install the four ScreenSaver components on this machine - the .scr in System32 (requires UAC), the Stream Deck plugin in %APPDATA%\Elgato, the Tray app in %LOCALAPPDATA% with autostart, and a shortcut for AlbumCoverFinder. Triggers - '/install', 'install the screensaver', 'deploy everything', 'install on this machine'.
---

# Install the ScreenSaver components

Deploy the built outputs to where Windows / Stream Deck / the user's Startup will actually run them. No external installer - this is a project-local "make it work on this machine" skill.

## When to use

User asks to install, deploy, or "set it up on this machine." Also when they explicitly want one of: install the .scr, install the Stream Deck plugin, set up tray autostart.

## What to do

### Step 0 - Make sure the build is current

Before installing, check that the four build outputs exist and are newer than the source. If not, **invoke the `build` skill first** (do not duplicate its logic).

Required outputs (each project's csproj redirects bin/obj to `out\<project>\` to dodge a Bitdefender path-block from earlier single-file builds):

- `out\ScreenSaver\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\ScreenSaver.exe` (**framework-dependent multi-file**, deployed via SCRNSAVE.EXE registry pointer - no System32, no UAC)
- `out\AlbumCoverFinder\bin\Release\net10.0-windows10.0.19041.0\publish\AlbumCoverFinder.exe`
- `out\ScreenSaver.Tray\bin\Release\net10.0-windows10.0.19041.0\publish\ScreenSaver.Tray.exe`
- `out\ScreenSaver.StreamDeck\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\com.blashyrkh.screensaver.exe`

### Step 1 - Ask the user what they want installed

Use `AskUserQuestion` with one multi-select question:

> Which components should I install?

Options (all on by default):

- **Stream Deck plugin** (`%APPDATA%\Elgato\StreamDeck\Plugins\`, no admin, restarts Stream Deck if running)
- **Tray companion + autostart** (`%LOCALAPPDATA%\ScreenSaver\Tray\` + HKCU Run, no admin)
- **Screensaver** (`%LOCALAPPDATA%\ScreenSaver\App\`, no admin, points the `SCRNSAVE.EXE` registry value at it - runs on idle)
- **System32 picker launcher** (160 KB framework-dependent launcher at `C:\Windows\System32\ScreenSaver.scr`, **requires UAC**, restores the Windows picker entry. Optional - the screensaver still runs on idle without it.)
- **AlbumCoverFinder Start-menu shortcut** (no admin, points at the built exe in-place)

Then run each selected installer below.

### Step 2 - Stream Deck plugin install (no admin)

```powershell
$src = "out\ScreenSaver.StreamDeck\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
$pluginDir = "$env:APPDATA\Elgato\StreamDeck\Plugins\com.blashyrkh.screensaver.sdPlugin"
$sdProc = Get-Process StreamDeck -ErrorAction SilentlyContinue

if ($sdProc) {
    Stop-Process -Name StreamDeck -Force
    Start-Sleep -Seconds 2
}

New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -Recurse -Force "$src\*" $pluginDir
Copy-Item -Recurse -Force "ScreenSaver.StreamDeck\sdPlugin\*" $pluginDir

if ($sdProc) {
    Start-Process $sdProc.Path
}
```

If Stream Deck wasn't running, tell the user to launch it manually. Mention that the action shows up under **ScreenSaver -> Cover Tile**.

### Step 3 - Tray companion install + autostart (no admin)

The Tray uses framework-dependent deployment - the user already has .NET 10 SDK so the runtime is present. Copy the whole `bin/Release/<TFM>/` folder so all dependencies travel:

```powershell
$src = "out\ScreenSaver.Tray\bin\Release\net10.0-windows10.0.19041.0\publish"
$trayDir = "$env:LOCALAPPDATA\ScreenSaver\Tray"

# If the Tray is already running, kill it so we can overwrite files.
Get-Process ScreenSaver.Tray -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

New-Item -ItemType Directory -Force -Path $trayDir | Out-Null
Copy-Item -Recurse -Force "$src\*" $trayDir

# Autostart entry in HKCU (no admin needed).
$runKey = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$exePath = Join-Path $trayDir "ScreenSaver.Tray.exe"
Set-ItemProperty -Path $runKey -Name "ScreenSaverTray" -Value "`"$exePath`""

# Launch it now so the user sees the tray icon appear.
Start-Process $exePath
```

### Step 4 - Screensaver install (no admin, no System32)

Earlier versions copied a self-contained .scr to `C:\Windows\System32\`, which Bitdefender and other AVs flag as a false-positive (compressed single-file + System32 + unsigned = packer heuristic). We now deploy the screensaver to `%LOCALAPPDATA%\ScreenSaver\App\` as a plain framework-dependent .NET app and tell Windows to use it via the `SCRNSAVE.EXE` registry value. Same screensaver behavior, no UAC, no AV flags.

**Trade-off:** the screensaver itself (the heavy framework-dependent .NET app with all the cover-mosaic code) lives at `%LOCALAPPDATA%\ScreenSaver\App\` to dodge AV false positives on System32. By itself that excludes us from the Windows picker dropdown - so Step 4b below ALSO drops a tiny launcher in System32 to restore that listing.

```powershell
# 1. Kill any running screensaver so we can overwrite its files.
Get-Process ScreenSaver -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# 2. Copy the published framework-dependent app to a stable per-user location.
$src = "out\ScreenSaver\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
$appDir = "$env:LOCALAPPDATA\ScreenSaver\App"
New-Item -ItemType Directory -Force -Path $appDir | Out-Null
Copy-Item -Recurse -Force "$src\*" $appDir
$appExe = Join-Path $appDir "ScreenSaver.exe"

# 3. Point Windows at it via the legacy screensaver registry.
# NOTE: this points at the App exe as a FALLBACK so idle activation works even
# when the System32 stub isn't installed (Step 4b skipped / UAC declined). If the
# stub IS installed, Step 4b OVERRIDES this to point at the stub instead - that's
# important, because the Windows screensaver picker only recognises .scr files in
# System32: with SCRNSAVE.EXE pointing at a non-.scr exe the picker shows "(None)"
# and can wipe the setting when opened. Pointing at the stub keeps us listed/selected.
$desk = "HKCU:\Control Panel\Desktop"
Set-ItemProperty -Path $desk -Name "SCRNSAVE.EXE" -Value $appExe -Type String
# Make sure the screensaver is enabled and has a sensible timeout (10 min).
# Don't override an existing user-set timeout - only set it if unset.
$existingTimeout = (Get-ItemProperty -Path $desk -Name "ScreenSaveTimeOut" -ErrorAction SilentlyContinue).ScreenSaveTimeOut
if (-not $existingTimeout) {
    Set-ItemProperty -Path $desk -Name "ScreenSaveTimeOut" -Value "600" -Type String
}
Set-ItemProperty -Path $desk -Name "ScreenSaveActive" -Value "1" -Type String

# 4. Also write our own pointer so the tray can find the app for "Test screensaver".
$key = "HKCU:\SOFTWARE\Demo_ScreenSaver"
New-Item -Path $key -Force | Out-Null
Set-ItemProperty -Path $key -Name "ScreenSaverPath" -Value $appExe -Type String
```

### Step 4b - Picker launcher in System32 (requires UAC, but small + safe)

To restore the screensaver in the Windows Settings → Lock screen → Screen saver dropdown, drop a tiny **launcher** in System32. This is a ~160 KB framework-dependent single-file exe with no compression; all it does is read `HKCU\SOFTWARE\Demo_ScreenSaver\ScreenSaverPath` and forward command-line args to the real screensaver at `%LOCALAPPDATA%\ScreenSaver\App\ScreenSaver.exe`.

Why is this OK when the previous System32 install wasn't? The previous file was 55 MB, self-contained, single-file, compressed - Bitdefender flagged the packer pattern. This one is small, framework-dependent (no embedded runtime), uncompressed.

**Before triggering UAC, tell the user**: "Installing the screensaver picker entry requires admin (a 160 KB launcher in C:\Windows\System32\). UAC prompt incoming - approve to install, or skip to use the screensaver via idle activation + the tray's Test entry only."

If they approve:

```powershell
$src = (Resolve-Path "out\ScreenSaver.Launcher\bin\Release\net10.0-windows\win-x64\publish\ScreenSaver.exe").Path
$dst = "$env:WINDIR\System32\ScreenSaver.scr"
$cmd = "Copy-Item -Force -LiteralPath '$src' -Destination '$dst'"
Start-Process powershell -Verb RunAs -Wait -ArgumentList "-NoProfile", "-Command", $cmd
```

Verify with `Test-Path $env:WINDIR\System32\ScreenSaver.scr`. If false after the elevated call returned, the user declined UAC - mention that the screensaver still works via idle, just not in the picker.

**If the stub installed successfully, point `SCRNSAVE.EXE` at the STUB** (overriding Step 4 #3's fallback). The Windows picker only understands `.scr` files in System32; with `SCRNSAVE.EXE` pointing at the App exe it shows "(None)" and clears the setting when opened. Pointing at the stub (which forwards to `ScreenSaverPath` = the App) keeps us listed and selected, and idle activation runs the stub → App.

```powershell
$stub = "$env:WINDIR\System32\ScreenSaver.scr"
if (Test-Path $stub) {
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "SCRNSAVE.EXE" -Value $stub -Type String
}
```

If the user previously had a bigger ScreenSaver.scr in System32 from the older self-contained build, this step overwrites it with the small launcher.

### Step 3b - Deploy AlbumCoverFinder + register FinderPath (no admin)

Drop AlbumCoverFinder into the same install root the Tray uses so the Tray's "Open settings" menu item can find it. Write the absolute path to `HKCU\SOFTWARE\Demo_ScreenSaver\FinderPath` so the Tray (and anything else) reads it instead of relying on `%PATH%`. Also kill any running finder first so we can overwrite its exe.

```powershell
Get-Process AlbumCoverFinder -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$src = "out\AlbumCoverFinder\bin\Release\net10.0-windows10.0.19041.0\publish"
$finderDir = "$env:LOCALAPPDATA\ScreenSaver\Finder"
New-Item -ItemType Directory -Force -Path $finderDir | Out-Null
Copy-Item -Recurse -Force "$src\*" $finderDir

$finderExe = Join-Path $finderDir "AlbumCoverFinder.exe"
$key = "HKCU:\SOFTWARE\Demo_ScreenSaver"
New-Item -Path $key -Force | Out-Null
Set-ItemProperty -Path $key -Name "FinderPath" -Value $finderExe -Type String
```

### Step 5a - Force wallpaper auto-update OFF by default (no admin)

The wallpaper feature is opt-in. Every install explicitly writes `WallpaperEnabled = 0` so a re-deploy doesn't accidentally turn it back on for someone who previously left it at the prior default. Other features (lock screen, Discord, HTTP) keep whatever the user had configured.

```powershell
$key = "HKCU:\SOFTWARE\Demo_ScreenSaver"
New-Item -Path $key -Force | Out-Null
Set-ItemProperty -Path $key -Name "WallpaperEnabled" -Value 0 -Type DWord
```

Tell the user: "Wallpaper auto-refresh is off. Turn it on from AlbumCoverFinder -> Wallpaper tab if you want it."

### Step 5 - AlbumCoverFinder Start-menu shortcut (no admin)

Points at the deployed path from Step 3b (not the bin folder), so the shortcut keeps working even if the project tree moves.

```powershell
$exe = "$env:LOCALAPPDATA\ScreenSaver\Finder\AlbumCoverFinder.exe"
$lnkDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\ScreenSaver"
New-Item -ItemType Directory -Force -Path $lnkDir | Out-Null

$wsh = New-Object -ComObject WScript.Shell
$lnk = $wsh.CreateShortcut("$lnkDir\Album Cover Finder.lnk")
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = Split-Path $exe
$lnk.Save()
```

Confirms once saved.

### Step 6 - Report

Single concise summary message:

- Which components were installed and where
- One-line "what to do next" if anything needs the user (e.g. "Launch Stream Deck to see the Cover Tile action", "Open Windows Screensaver Settings to pick ScreenSaver and click Settings to scan your music folder")

## Notes

- **Always ask before triggering UAC.** Don't surprise the user with a prompt.
- **Stream Deck restart is destructive** if the user is in the middle of using it. Mention it before doing it; if they say no, just copy the files and tell them to restart Stream Deck when convenient.
- **Don't use `New-Item -Force` on files** - it truncates them. We only use `-Force` on directories above.
- **Don't add a Run key for the screensaver itself** - that would launch the screensaver at every login, which is not what anyone wants. The Run key is for the *tray*, not the screensaver.
- **The screensaver IS pointed at via `HKCU\Control Panel\Desktop\SCRNSAVE.EXE`** - this is the only way to register it now that we're not using System32. Don't comment out that registry write.
- If a step fails (e.g. Stream Deck path doesn't exist because the user hasn't installed Stream Deck), tell the user and continue with the remaining steps. Don't abort the whole install.
