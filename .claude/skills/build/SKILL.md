---
name: build
description: Build the ScreenSaver solution end-to-end - solution build for the three SDK projects, then a self-contained publish of the Stream Deck plugin so it has an exe ready to install. Triggers - '/build', 'build the project', 'compile everything', 'rebuild'.
---

# Build the ScreenSaver solution

Compile all four projects (ScreenSaver, AlbumCoverFinder, ScreenSaver.Tray, ScreenSaver.StreamDeck) and verify each produced its expected output.

## When to use

User asks to build, compile, or rebuild the project. Also use proactively before any change you want to verify actually compiles.

## What to do

Run from the project root (`T:\GOOGLEDRIVE\Source.Code\ScreenSaver`):

1. **Verify .NET 10 SDK is available.**

   ```powershell
   dotnet --list-sdks
   ```

   Expect at least one `10.x.y` line. If absent, stop and tell the user to install the .NET 10 SDK from https://dotnet.microsoft.com/download.

2. **Build the solution** (covers ScreenSaver, AlbumCoverFinder, ScreenSaver.Tray):

   ```powershell
   dotnet build ScreenSaver.sln -c Release -nologo
   ```

   Tail the last ~20 lines of output. If `Build succeeded` with `0 Error(s)`, continue. If errors, stop and surface them to the user.

3. **Publish the Stream Deck plugin** (self-contained, single-file, win-x64). The sln config doesn't build this project by default because it's x64-only and the sln defaults to Any CPU:

   ```powershell
   dotnet publish ScreenSaver.StreamDeck\ScreenSaver.StreamDeck.csproj -c Release -r win-x64 --self-contained -nologo
   ```

   Tail the last ~10 lines. Verify success.

4. **Publish the screensaver as a plain framework-dependent multi-file app.** Earlier versions used `--self-contained -p:PublishSingleFile=true`, which produced a ~55 MB compressed single-file exe. Bitdefender and other AVs flagged it as a packer false-positive. We now publish framework-dependent (small exe + a few DLLs), which looks like any other .NET app and avoids the heuristic.

   ```powershell
   dotnet publish ScreenSaver\ScreenSaver.csproj -c Release -nologo
   ```

   No `-r`, no `--self-contained`, no `PublishSingleFile`. Output is `ScreenSaver.exe` plus ~5-10 small DLLs in the publish folder. Target machines need .NET 10 runtime installed (the SDK includes it).

5. **Publish the tiny screensaver launcher** (single-file, framework-dependent, no compression). This is the ~160 KB `ScreenSaver.exe` we drop into `C:\Windows\System32\ScreenSaver.scr` so the Windows screensaver picker can list us. It does nothing but read `HKCU\SOFTWARE\Demo_ScreenSaver\ScreenSaverPath` and forward args to the real screensaver.

   ```powershell
   dotnet publish ScreenSaver.Launcher\ScreenSaver.Launcher.csproj -c Release -r win-x64 --no-self-contained -nologo
   ```

   Tail the last ~5 lines. Verify success.

6. **Verify the expected outputs exist** (each project's csproj redirects bin/obj into a top-level `out\<project>\` folder to dodge a Bitdefender path-block from earlier single-file builds; the install skill consumes the `publish\` subfolder of each):

   - `out\ScreenSaver\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\ScreenSaver.exe` (**framework-dependent, multi-file**, the real screensaver, deployed under %LOCALAPPDATA%)
   - `out\ScreenSaver.Launcher\bin\Release\net10.0-windows\win-x64\publish\ScreenSaver.exe` (**framework-dependent single-file**, ~160 KB, the System32 picker stub - reads registry + forwards args)
   - `out\AlbumCoverFinder\bin\Release\net10.0-windows10.0.19041.0\publish\AlbumCoverFinder.exe`
   - `out\ScreenSaver.Tray\bin\Release\net10.0-windows10.0.19041.0\publish\ScreenSaver.Tray.exe`
   - `out\ScreenSaver.StreamDeck\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\com.blashyrkh.screensaver.exe` (Stream Deck plugin is still self-contained because Stream Deck's host process isn't .NET)

   If any are missing, surface that to the user.

7. **Report** the five output paths in a single concise message. Mention zero warnings / zero errors if true. Mention `MSB3539` "BaseIntermediateOutputPath modified after use" warnings (benign), `NU1603` "version not found, X resolved instead" warnings (benign), and any non-benign warnings.

## Notes

- **Google Drive file locks** sometimes cause `MSB4018: ...deps.json being used by another process` on rebuilds. If you hit it, delete the offending project's `out\<project>\` folder and retry once. Don't enter a sleep loop.
- **Bitdefender path-block** can surface as `MSB3021: Access to the path ...AlbumCoverFinder.exe is denied` on the OLD `<project>\bin\Release\` path. The csproj redirects already avoid that path; if you somehow see the error, clean `out\` and retry. Never tell the user to add an AV exception.
- **Working directory persists** between Bash calls in one turn; you don't need to `cd` more than once.
- Do **not** clear the cache (`dotnet nuget locals all --clear`) - it's slow and never the right fix here.
- Do **not** start any background processes; the build is fast (~3-5 seconds clean).
