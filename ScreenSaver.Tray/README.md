# ScreenSaver Tray Companion

A small Windows tray-resident app that sits next to the screensaver and adds four things the screensaver can't do while it's idle:

| # | Feature | What it does |
|---|---|---|
| #3 | **Desktop wallpaper mosaic** | Renders the same square-tile mosaic the screensaver shows, sets it as your actual Windows wallpaper. Refreshes every 5 minutes (configurable via tray menu). |
| #4 | **Discord Rich Presence** | When SMTC reports a track, pushes "Listening to {title} — {artist}" to your Discord profile. |
| #9 / #13 | **LAN HTTP companion** | Tiny web server at `http://localhost:9999/` showing a phone-friendly Now-Playing + searchable cover library. JSON endpoints make it scriptable too. |
| #18 | **Lock screen mosaic** | Same render → Windows lock screen, via `Windows.System.UserProfile.LockScreen`. Manual trigger from the tray menu. |

All four ride on the **same** `AlbumCoverMgr` and `NowPlayingMonitor` instances the screensaver uses — so the genre/year filter you set in AlbumCoverFinder applies everywhere automatically.

## Running it

```powershell
dotnet build ScreenSaver.Tray\ScreenSaver.Tray.csproj -c Release
ScreenSaver.Tray\bin\Release\net10.0-windows10.0.19041.0\ScreenSaver.Tray.exe
```

A blue-circle icon appears in your tray. Right-click for the menu:

- **Refresh wallpaper now** — render and apply immediately
- **Refresh lock screen now** — render and apply to the lock screen
- **Auto-refresh wallpaper** — toggle the 5-minute timer
- **Discord Rich Presence** — toggle the SMTC → Discord push
- **HTTP companion** — toggle the HTTP listener
- **Open companion in browser** — launches `http://localhost:9999/`
- **Quit**

Double-click the tray icon = "refresh wallpaper now."

## HTTP endpoints

| Endpoint | Returns |
|---|---|
| `GET /` | Browser companion page (search + cover grid). Mobile-friendly. |
| `GET /now-playing.json` | `{ playing, title, artist, album, year, genre }` |
| `GET /now-playing.png` | PNG of the SMTC current cover, or 204 if nothing playing |
| `GET /random.png` | PNG of a random cover from the filtered library |
| `GET /index.json` | Array of `{ index, artist, album, year, genre }` for every cover |
| `GET /cover/{index}.png` | PNG of the cover at that index in the filtered library |

The listener binds to **`localhost:9999`** by default, so only this machine can hit it. To allow LAN devices (phone, Steam Deck), run this once as admin:

```powershell
netsh http add urlacl url=http://+:9999/ user=Everyone
```

Then change `localhost:9999` to `+:9999` in `HttpCompanion.cs:Start()` and rebuild. (Left as a manual step on purpose — opening a port to the LAN is something you should choose explicitly.)

## Discord Rich Presence setup

1. Go to https://discord.com/developers/applications and create a new application named "ScreenSaver" (or whatever).
2. Copy the **Application ID** (numeric, 17+ digits).
3. Optionally: under **Rich Presence → Art Assets**, upload a PNG named `music` (this becomes the large icon shown in your Discord status).
4. Save the ID:

   ```powershell
   $key = "HKCU:\SOFTWARE\Demo_ScreenSaver"
   New-Item -Path $key -Force | Out-Null
   Set-ItemProperty -Path $key -Name DiscordAppId -Value "YOUR_NUMERIC_ID_HERE"
   ```

5. Restart the tray app (right-click tray icon → Quit, relaunch).

If `DiscordAppId` isn't set or is `0`, the Discord integration silently no-ops — the rest of the tray still works.

## Lock screen permissions

`LockScreen.SetImageFileAsync` works in the regular user desktop context — no admin required on consumer Windows 10/11. Group Policy can block it on managed devices; if the menu item silently does nothing, that's the culprit, and it's deliberate at the policy level.

## Wallpaper output

The rendered wallpaper BMP lives at:

```
%APPDATA%\ScreenSaver\wallpaper.bmp
```

And the lock screen JPG at:

```
%APPDATA%\ScreenSaver\lockscreen.jpg
```

Both are overwritten on each refresh. Safe to delete; the next refresh will rebuild them.

## Why a separate project?

The screensaver only runs while Windows is idle. The wallpaper, Discord presence, lock screen, and HTTP companion need to run **always**. Splitting them into a tray app means you can pick whether you want them by leaving the tray running or not — without affecting the screensaver itself.
