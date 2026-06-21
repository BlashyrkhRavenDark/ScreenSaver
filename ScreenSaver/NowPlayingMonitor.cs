using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace ScreenSaver
{
    /// <summary>
    /// Subscribes to Windows System Media Transport Controls (SMTC) and reports
    /// what's currently playing across Spotify, Apple Music, Foobar, VLC, browsers,
    /// or anything else that publishes to the OS media session.
    ///
    /// Threading model: WinRT fires events on a thread-pool thread; this class
    /// captures the SynchronizationContext at Start time and marshals all public
    /// events back to it (so subscribers can touch WinForms controls directly).
    /// </summary>
    public class NowPlayingMonitor : IDisposable
    {
        public class NowPlayingInfo
        {
            public string Title;
            public string Artist;
            public string Album;
            public Image Cover;
            // Source of this info: true = iTunes (COM poll / IPC file), false = SMTC
            // (Spotify, Apple Music, browsers, ...). The Stream Deck plugin mirrors
            // iTunes only; the screensaver shows whatever is playing regardless.
            public bool FromItunes;
        }

        /// <summary>
        /// Optional callback to swap SMTC's low-res thumbnail for a high-res cached cover.
        /// Receives an "artist+album" key (built with <see cref="BuildKey"/>) and returns the
        /// matching Image, or null if not in the cache.
        /// </summary>
        private readonly Func<string, Image> m_oCoverLookup;
        // Optional callback to persist a cover that was NOT in the local cache (e.g.
        // extracted from iTunes' artwork cache via COM) so it joins the mosaic. Only
        // the iTunes-polling writer (the Tray) wires this up.
        private readonly Action<string, string, Image> m_oCoverPersist;
        private GlobalSystemMediaTransportControlsSessionManager m_oManager;
        private GlobalSystemMediaTransportControlsSession m_oCurrentSession;
        private SynchronizationContext m_oUiCtx;
        private bool m_bDisposed;
        private bool m_bStarted;

        // Last reported state from each source. The effective Current is iTunes
        // when present (because iTunes for Windows doesn't publish to SMTC and is
        // what the user specifically cares about), SMTC otherwise.
        private NowPlayingInfo m_oLastSmtcInfo;
        private NowPlayingInfo m_oLastItunesInfo;

        // iTunes COM polling - iTunes for Windows is a legacy Win32 app that
        // doesn't push to SMTC, so the polling instance (one per machine - the
        // Tray) late-binds to its IDispatch and polls every couple seconds.
        // Non-polling instances (screensaver, Stream Deck plugin) watch a shared
        // file the polling instance writes, so iTunes' STA is hit by ONE process.
        private readonly bool m_bPollItunes;
        private long m_iLastItunesTrackId = -1;
        private System.Threading.Timer m_oItunesPollTimer;
        private FileSystemWatcher m_oItunesWatcher;
        private const int ITUNES_POLL_INTERVAL_MS = 2000;

        public event Action<NowPlayingInfo> Updated;
        public event Action Cleared;

        public NowPlayingInfo Current { get; private set; }

        /// <param name="coverLookup">Optional callback to swap SMTC's low-res thumbnail for a high-res cached cover.</param>
        /// <param name="pollItunes">
        /// If true, the monitor polls iTunes COM directly and publishes its data
        /// to a shared file (nowplaying.json / nowplaying.png) under %LOCALAPPDATA%.
        /// If false (default), the monitor instead watches that file and applies
        /// updates as if it had polled iTunes itself. ONLY ONE process per machine
        /// should set this true - the Tray is the designated owner; the screensaver
        /// and Stream Deck plugin both leave it false.
        /// </param>
        /// <param name="coverPersist">Optional callback (artist, album, cover) to save a
        /// cover that wasn't in the local cache - used to capture iTunes-cache-only art
        /// into the mosaic. Only meaningful alongside pollItunes = true.</param>
        public NowPlayingMonitor(Func<string, Image> coverLookup = null, bool pollItunes = false,
                                 Action<string, string, Image> coverPersist = null)
        {
            m_oCoverLookup = coverLookup;
            m_bPollItunes = pollItunes;
            m_oCoverPersist = coverPersist;
        }

        /// <summary>
        /// Shared directory used for the iTunes IPC files. Created on first use.
        /// </summary>
        private static string SharedDir
        {
            get
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ScreenSaver");
                try { System.IO.Directory.CreateDirectory(dir); } catch { }
                return dir;
            }
        }

        private static string NowPlayingJsonPath => System.IO.Path.Combine(SharedDir, "nowplaying.json");
        private static string NowPlayingPngPath  => System.IO.Path.Combine(SharedDir, "nowplaying.png");

        /// <summary>
        /// Canonical "artist+album" key used by cover caches. Matches the format used
        /// by AlbumCoverMgr.BuildKey so the two can share a single dictionary.
        /// </summary>
        public static string BuildKey(string artist, string album)
        {
            return (artist ?? string.Empty) + (album ?? string.Empty);
        }

        public async Task StartAsync()
        {
            if (m_bStarted) return;
            m_bStarted = true;
            m_oUiCtx = SynchronizationContext.Current ?? new SynchronizationContext();
            try
            {
                m_oManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                m_oManager.CurrentSessionChanged += OnCurrentSessionChanged;
                BindToSession(m_oManager.GetCurrentSession());
                await RefreshAsync();
            }
            catch
            {
                // SMTC not available on this OS / blocked: stay silent, the iTunes
                // poll below is still our other shot at "now playing" data.
            }

            TryStartItunesPolling();
        }

        private void TryStartItunesPolling()
        {
            if (!m_bPollItunes)
            {
                // Reader role: watch the file the polling process publishes.
                // Reading is cheap and doesn't touch iTunes COM at all.
                StartItunesFileWatcher();
                return;
            }

            // Writer role: poll iTunes via COM - but NEVER launch it. We only attach
            // when the user already has iTunes open (see PollItunes), and we release
            // the COM object after every poll, so we never keep iTunes alive, never
            // resurrect it after the user closes it, and never block its quit
            // ("a script is using it"). The timer's first tick attaches if iTunes is
            // already running; later ticks pick it up whenever the user opens it.
            m_oItunesPollTimer = new System.Threading.Timer(_ => PollItunes(), null, 0, ITUNES_POLL_INTERVAL_MS);
        }

        /// <summary>
        /// Reader-side: watch %LOCALAPPDATA%\ScreenSaver\nowplaying.json for changes
        /// and apply them as iTunes updates. The Tray writes that file; this
        /// process never touches iTunes COM directly.
        /// </summary>
        private void StartItunesFileWatcher()
        {
            try
            {
                string dir = SharedDir;
                string file = System.IO.Path.GetFileName(NowPlayingJsonPath);
                m_oItunesWatcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
                };
                m_oItunesWatcher.Changed += (s, e) => ReadAndApplyItunesFile();
                m_oItunesWatcher.Created += (s, e) => ReadAndApplyItunesFile();
                m_oItunesWatcher.Deleted += (s, e) => ApplyItunesUpdate(null);
                m_oItunesWatcher.EnableRaisingEvents = true;
            }
            catch { /* watcher creation can fail on weird FS configurations - silently skip */ }
            // Seed with whatever is currently on disk (the Tray may have written it
            // before we started, e.g. the screensaver kicks in after an idle while
            // the Tray was already running).
            ReadAndApplyItunesFile();
        }

        private void ReadAndApplyItunesFile()
        {
            try
            {
                string path = NowPlayingJsonPath;
                if (!System.IO.File.Exists(path)) { ApplyItunesUpdate(null); return; }
                string content;
                try { content = System.IO.File.ReadAllText(path); }
                catch { return; /* mid-write, try again on next watcher event */ }
                if (string.IsNullOrWhiteSpace(content) || content == "{}") { ApplyItunesUpdate(null); return; }

                using (var doc = JsonDocument.Parse(content))
                {
                    var r = doc.RootElement;
                    string title  = r.TryGetProperty("title",  out var p1) ? p1.GetString() : string.Empty;
                    string artist = r.TryGetProperty("artist", out var p2) ? p2.GetString() : string.Empty;
                    string album  = r.TryGetProperty("album",  out var p3) ? p3.GetString() : string.Empty;
                    if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(artist) && string.IsNullOrEmpty(album))
                    {
                        ApplyItunesUpdate(null);
                        return;
                    }

                    // Prefer the local cached cover; fall back to the PNG the Tray
                    // wrote alongside the JSON (which is iTunes' embedded artwork).
                    Image cover = m_oCoverLookup != null ? m_oCoverLookup(BuildKey(artist, album)) : null;
                    if (cover == null && System.IO.File.Exists(NowPlayingPngPath))
                    {
                        try
                        {
                            using (var fs = new System.IO.FileStream(NowPlayingPngPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read))
                                cover = new Bitmap(fs);
                        }
                        catch { /* mid-write or stale - skip */ }
                    }

                    ApplyItunesUpdate(new NowPlayingInfo
                    {
                        Title = title,
                        Artist = artist,
                        Album = album,
                        Cover = cover
                    });
                }
            }
            catch
            {
                // Malformed JSON or other transient error - leave previous state untouched.
            }
        }

        private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            BindToSession(sender.GetCurrentSession());
            _ = RefreshAsync();
        }

        private void BindToSession(GlobalSystemMediaTransportControlsSession session)
        {
            if (m_oCurrentSession != null)
            {
                try
                {
                    m_oCurrentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    m_oCurrentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                }
                catch { /* session may already be gone */ }
            }
            m_oCurrentSession = session;
            if (m_oCurrentSession != null)
            {
                m_oCurrentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                m_oCurrentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
            }
            else
            {
                ApplySmtcUpdate(null);
            }
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            _ = RefreshAsync();
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            _ = RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            if (m_oCurrentSession == null) { ApplySmtcUpdate(null); return; }

            GlobalSystemMediaTransportControlsSessionPlaybackInfo playback;
            try { playback = m_oCurrentSession.GetPlaybackInfo(); }
            catch { ApplySmtcUpdate(null); return; }

            // Only treat as "active" when something is actually playing - paused/stopped
            // states should fall back to the mosaic instead of locking the focal tile.
            if (playback.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                ApplySmtcUpdate(null);
                return;
            }

            GlobalSystemMediaTransportControlsSessionMediaProperties props;
            try { props = await m_oCurrentSession.TryGetMediaPropertiesAsync(); }
            catch { ApplySmtcUpdate(null); return; }

            if (props == null) { ApplySmtcUpdate(null); return; }

            string title = props.Title ?? string.Empty;
            string artist = !string.IsNullOrEmpty(props.AlbumArtist) ? props.AlbumArtist : (props.Artist ?? string.Empty);
            string album = props.AlbumTitle ?? string.Empty;

            // Prefer the local high-res cached cover; fall back to SMTC's own thumbnail.
            Image cover = m_oCoverLookup != null ? m_oCoverLookup(BuildKey(artist, album)) : null;
            if (cover == null && props.Thumbnail != null)
            {
                try { cover = await LoadThumbnailAsync(props.Thumbnail); }
                catch { cover = null; }
            }

            ApplySmtcUpdate(new NowPlayingInfo
            {
                Title = title,
                Artist = artist,
                Album = album,
                Cover = cover
            });
        }

        private void PollItunes()
        {
            // Attach to iTunes ONLY if it is already running, via the COM Running
            // Object Table (GetActiveObject) - which NEVER launches it. CreateInstance/
            // CoCreate would START iTunes when it's absent; worse, during iTunes'
            // graceful shutdown the process LINGERS in the task list while its COM
            // object is already revoked, so a process-list gate + CreateInstance
            // relaunches it via DCOM on the next poll - that was the "iTunes keeps
            // coming back" bug (its parent showed up as svchost/DcomLaunch). An ROT
            // lookup reports a half-closed iTunes as gone instead of resurrecting it.
            dynamic itunes = TryGetRunningItunes();
            if (itunes == null)
            {
                if (m_iLastItunesTrackId != -1)
                {
                    m_iLastItunesTrackId = -1;
                    WriteNowPlayingFile(null);
                }
                ApplyItunesUpdate(null);
                return;
            }

            // Released again in 'finally' so we hold no automation reference between
            // polls (that reference is what makes iTunes refuse to quit).
            dynamic track = null;
            try
            {
                // iTunes PlayerState: 0=stopped, 1=playing, 2=paused, 3=ff, 4=rew.
                int playerState = (int)itunes.PlayerState;
                if (playerState == 2)
                {
                    // Paused. Keep the last cover on the deck / focal tile.
                    return;
                }
                if (playerState != 1)
                {
                    if (m_iLastItunesTrackId != -1)
                    {
                        m_iLastItunesTrackId = -1;
                        WriteNowPlayingFile(null);
                    }
                    ApplyItunesUpdate(null);
                    return;
                }

                track = itunes.CurrentTrack;
                if (track == null) { ApplyItunesUpdate(null); return; }

                // Cheap change detection: TrackDatabaseID is a single int property.
                long trackId;
                try { trackId = (long)(int)track.TrackDatabaseID; }
                catch { trackId = 0; } // some iTunes builds expose it differently

                if (trackId != 0 && trackId == m_iLastItunesTrackId && m_oLastItunesInfo != null)
                    return;

                // Track changed (or we don't have a stable ID). Read the metadata.
                string artist = (string)track.Artist ?? string.Empty;
                string album  = (string)track.Album  ?? string.Empty;
                string title  = (string)track.Name   ?? string.Empty;

                // Cache-first: only ask iTunes to export its artwork when we don't
                // already have the cover locally (scanned libraries hit cache).
                Image cover = m_oCoverLookup != null ? m_oCoverLookup(BuildKey(artist, album)) : null;
                bool bFromCache = cover != null;
                if (cover == null) cover = ExtractItunesArtwork(track);

                if (!bFromCache && cover != null)
                {
                    try { m_oCoverPersist?.Invoke(artist, album, cover); } catch { /* non-fatal */ }
                }

                var info = new NowPlayingInfo
                {
                    Title = title,
                    Artist = artist,
                    Album = album,
                    Cover = cover
                };
                m_iLastItunesTrackId = trackId;

                // Publish for the screensaver and SD plugin. PNG-then-JSON ordering
                // handled inside WriteNowPlayingFile.
                WriteNowPlayingFile(info);
                ApplyItunesUpdate(info);
            }
            catch
            {
                m_iLastItunesTrackId = -1;
                WriteNowPlayingFile(null);
                ApplyItunesUpdate(null);
            }
            finally
            {
                // Release every RCW each poll. This is what lets the user quit iTunes
                // freely (no lingering automation client holding it) and guarantees we
                // never keep it alive.
                if (track != null) { try { System.Runtime.InteropServices.Marshal.ReleaseComObject(track); } catch { } }
                try { System.Runtime.InteropServices.Marshal.ReleaseComObject(itunes); } catch { }
            }
        }

        /// <summary>
        /// Returns the already-running iTunes Application via the COM Running Object
        /// Table, or null if iTunes isn't running. Unlike CreateInstance/CoCreate this
        /// NEVER launches iTunes - so closing iTunes makes it stay closed, and a
        /// half-shut-down iTunes (process lingering, COM object already revoked) is
        /// reported as gone rather than being relaunched.
        /// </summary>
        private static dynamic TryGetRunningItunes()
        {
            try
            {
                if (CLSIDFromProgID("iTunes.Application", out Guid clsid) != 0) return null;
                if (GetActiveObject(ref clsid, IntPtr.Zero, out object obj) != 0) return null;
                return obj;
            }
            catch { return null; }
        }

        [System.Runtime.InteropServices.DllImport("ole32.dll")]
        private static extern int CLSIDFromProgID(
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string lpszProgID,
            out Guid pclsid);

        [System.Runtime.InteropServices.DllImport("oleaut32.dll")]
        private static extern int GetActiveObject(
            ref Guid rclsid, IntPtr pvReserved,
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.IUnknown)] out object ppunk);

        /// <summary>
        /// Writer-side IPC: atomically swap in a new nowplaying.json + nowplaying.png
        /// describing the current iTunes track. Reader-side FSWatchers in other
        /// processes pick this up. Null clears both files.
        /// </summary>
        private static void WriteNowPlayingFile(NowPlayingInfo info)
        {
            try
            {
                string json = NowPlayingJsonPath;
                string png = NowPlayingPngPath;
                if (info == null)
                {
                    System.IO.File.WriteAllText(json, "{}");
                    try { if (System.IO.File.Exists(png)) System.IO.File.Delete(png); } catch { }
                    return;
                }

                // ORDER MATTERS: write the cover PNG FIRST, then the JSON.
                // Reader processes watch ONLY nowplaying.json and read the PNG
                // alongside it. If the JSON (new track) landed before the PNG, a
                // reader without a local cover cache (the Stream Deck plugin) would
                // pair the new metadata with the still-stale PNG - rendering each
                // tile one track behind. Writing the PNG first guarantees the cover
                // already matches by the time the JSON-change event fires.
                if (info.Cover != null)
                {
                    string pngTmp = png + ".tmp";
                    using (var bmp = new Bitmap(info.Cover))
                        bmp.Save(pngTmp, System.Drawing.Imaging.ImageFormat.Png);
                    try { System.IO.File.Delete(png); } catch { }
                    System.IO.File.Move(pngTmp, png);
                }
                else
                {
                    try { if (System.IO.File.Exists(png)) System.IO.File.Delete(png); } catch { }
                }

                // JSON last - its change event is the reader's wake-up, and the PNG
                // above is now already current. Temp-then-replace so no partial read.
                string payload = JsonSerializer.Serialize(new
                {
                    title  = info.Title  ?? string.Empty,
                    artist = info.Artist ?? string.Empty,
                    album  = info.Album  ?? string.Empty
                });
                string jsonTmp = json + ".tmp";
                System.IO.File.WriteAllText(jsonTmp, payload);
                try { System.IO.File.Delete(json); } catch { }
                System.IO.File.Move(jsonTmp, json);
            }
            catch
            {
                // IPC publish failures are non-fatal - the polling process still
                // serves its in-process subscribers.
            }
        }

        private static Image ExtractItunesArtwork(dynamic track)
        {
            string tmpPath = null;
            dynamic artworks = null;
            dynamic art = null;
            try
            {
                artworks = track.Artwork;
                if (artworks == null || (int)artworks.Count == 0) return null;
                art = artworks[1]; // iTunes uses 1-based indexing
                tmpPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "itunes_cover_" + Guid.NewGuid().ToString("N") + ".jpg");
                art.SaveArtworkToFile(tmpPath);
                using (var fs = new System.IO.FileStream(tmpPath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    return new Bitmap(fs);
            }
            catch { return null; }
            finally
            {
                // Release the artwork RCWs too - any held iTunes sub-object blocks quit.
                if (art != null) { try { System.Runtime.InteropServices.Marshal.ReleaseComObject(art); } catch { } }
                if (artworks != null) { try { System.Runtime.InteropServices.Marshal.ReleaseComObject(artworks); } catch { } }
                try { if (tmpPath != null) System.IO.File.Delete(tmpPath); } catch { }
            }
        }

        private void ApplySmtcUpdate(NowPlayingInfo info)
        {
            if (info != null) info.FromItunes = false;
            m_oLastSmtcInfo = info;
            RecomputeAndRaise();
        }

        private void ApplyItunesUpdate(NowPlayingInfo info)
        {
            if (info != null) info.FromItunes = true;
            m_oLastItunesInfo = info;
            RecomputeAndRaise();
        }

        /// <summary>
        /// iTunes wins when it has something; SMTC fills in otherwise. Pushes the
        /// resulting Updated/Cleared event onto the captured UI sync context.
        /// </summary>
        private void RecomputeAndRaise()
        {
            var effective = m_oLastItunesInfo ?? m_oLastSmtcInfo;
            Current = effective;
            if (effective != null)
            {
                var h = Updated;
                if (h == null || m_oUiCtx == null) return;
                m_oUiCtx.Post(_ => h(effective), null);
            }
            else
            {
                var h = Cleared;
                if (h == null || m_oUiCtx == null) return;
                m_oUiCtx.Post(_ => h(), null);
            }
        }

        private static async Task<Image> LoadThumbnailAsync(IRandomAccessStreamReference streamRef)
        {
            using (IRandomAccessStreamWithContentType stream = await streamRef.OpenReadAsync())
            {
                using (var ms = new MemoryStream())
                {
                    var netStream = stream.AsStreamForRead();
                    await netStream.CopyToAsync(ms);
                    ms.Position = 0;
                    return new Bitmap(ms);
                }
            }
        }

        /// <summary>
        /// Toggle play/pause on the currently bound SMTC session. Returns true if the
        /// session accepted the command. Apps that don't implement the control return false.
        /// </summary>
        public async Task<bool> TryTogglePlayPauseAsync()
        {
            var s = m_oCurrentSession;
            if (s == null) return false;
            try { return await s.TryTogglePlayPauseAsync(); }
            catch { return false; }
        }

        public async Task<bool> TrySkipNextAsync()
        {
            var s = m_oCurrentSession;
            if (s == null) return false;
            try { return await s.TrySkipNextAsync(); }
            catch { return false; }
        }

        public async Task<bool> TrySkipPreviousAsync()
        {
            var s = m_oCurrentSession;
            if (s == null) return false;
            try { return await s.TrySkipPreviousAsync(); }
            catch { return false; }
        }

        public async Task<bool> TryStopAsync()
        {
            var s = m_oCurrentSession;
            if (s == null) return false;
            try { return await s.TryStopAsync(); }
            catch { return false; }
        }

        public void Dispose()
        {
            if (m_bDisposed) return;
            m_bDisposed = true;
            try
            {
                if (m_oCurrentSession != null)
                {
                    m_oCurrentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                    m_oCurrentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                }
                if (m_oManager != null)
                    m_oManager.CurrentSessionChanged -= OnCurrentSessionChanged;
                if (m_oItunesPollTimer != null) { m_oItunesPollTimer.Dispose(); m_oItunesPollTimer = null; }
                if (m_oItunesWatcher != null) { m_oItunesWatcher.EnableRaisingEvents = false; m_oItunesWatcher.Dispose(); m_oItunesWatcher = null; }
            }
            catch { /* tearing down */ }
        }
    }
}
