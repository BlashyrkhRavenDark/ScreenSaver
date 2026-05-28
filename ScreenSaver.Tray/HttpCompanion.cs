using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AlbumCoverFinder;

namespace ScreenSaver.Tray
{
    /// <summary>
    /// LAN companion HTTP server on :9999.
    /// Endpoints:
    ///   GET  /                    - browser companion page (search + grid)
    ///   GET  /now-playing.png     - PNG of the current SMTC focal cover (or 204)
    ///   GET  /now-playing.json    - { title, artist, album, year, genre }
    ///   GET  /random.png          - random cover from the (filtered) library
    ///   GET  /index.json          - array of { key, artist, album, year, genre }
    ///   GET  /cover/{n}.png       - the cover for index n (matches the cache layout)
    /// </summary>
    public class HttpCompanion
    {
        private readonly AlbumCoverMgr m_oCover;
        private readonly global::ScreenSaver.NowPlayingMonitor m_oMonitor;
        private HttpListener m_oListener;
        private CancellationTokenSource m_oCts;

        public HttpCompanion(AlbumCoverMgr cover, global::ScreenSaver.NowPlayingMonitor monitor)
        {
            m_oCover = cover;
            m_oMonitor = monitor;
        }

        public void Start()
        {
            if (m_oListener != null && m_oListener.IsListening) return;
            m_oListener = new HttpListener();
            // Loop-back only by default to avoid the netsh urlacl admin requirement.
            // Users who want LAN access can switch to http://+:9999/ after running
            // netsh once (documented in the tray README).
            m_oListener.Prefixes.Add("http://localhost:9999/");
            try { m_oListener.Start(); }
            catch { m_oListener = null; return; }

            m_oCts = new CancellationTokenSource();
            _ = Task.Run(() => AcceptLoop(m_oCts.Token));
        }

        public void Stop()
        {
            try { m_oCts?.Cancel(); } catch { }
            try { m_oListener?.Stop(); m_oListener?.Close(); } catch { }
            m_oListener = null;
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && m_oListener != null && m_oListener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await m_oListener.GetContextAsync(); }
                catch { break; }
                _ = Task.Run(() => HandleSafe(ctx));
            }
        }

        private async Task HandleSafe(HttpListenerContext ctx)
        {
            try { await HandleAsync(ctx); }
            catch { try { ctx.Response.StatusCode = 500; } catch { } }
            try { ctx.Response.Close(); } catch { }
        }

        private async Task HandleAsync(HttpListenerContext ctx)
        {
            string path = ctx.Request.Url.AbsolutePath.ToLowerInvariant();
            switch (path)
            {
                case "/":
                case "/index.html":
                    await ServeStaticAsync(ctx, "index.html", "text/html; charset=utf-8");
                    return;
                case "/now-playing.png":
                    ServeNowPlayingPng(ctx);
                    return;
                case "/now-playing.json":
                    ServeNowPlayingJson(ctx);
                    return;
                case "/random.png":
                    ServeRandomPng(ctx);
                    return;
                case "/index.json":
                    ServeIndexJson(ctx);
                    return;
                case "/hide":
                    if (ctx.Request.HttpMethod == "POST") { HideByIndex(ctx); return; }
                    ctx.Response.StatusCode = 405;
                    return;
                default:
                    if (path.StartsWith("/cover/") && path.EndsWith(".png"))
                    {
                        ServeCoverByIndex(ctx, path);
                        return;
                    }
                    ctx.Response.StatusCode = 404;
                    return;
            }
        }

        /// <summary>
        /// Adds the cover at the given filtered-pool index to the blocklist.
        /// Same effect as right-clicking the tile in the screensaver. Index is
        /// resolved against the live filtered pool, so the caller should re-fetch
        /// /index.json after a hide - the indices renumber.
        /// </summary>
        private void HideByIndex(HttpListenerContext ctx)
        {
            string indexStr = ctx.Request.QueryString["index"];
            if (!int.TryParse(indexStr, out int idx)) { ctx.Response.StatusCode = 400; return; }
            var pool = m_oCover.GetFilteredPool();
            if (idx < 0 || idx >= pool.Count) { ctx.Response.StatusCode = 404; return; }
            string key = pool[idx];
            if (m_oCover.Blocklist == null) { ctx.Response.StatusCode = 500; return; }
            m_oCover.Blocklist.Block(key);
            ctx.Response.StatusCode = 204;
        }

        private void ServeNowPlayingPng(HttpListenerContext ctx)
        {
            var cur = m_oMonitor.Current;
            if (cur == null || cur.Cover == null) { ctx.Response.StatusCode = 204; return; }
            WriteImage(ctx, cur.Cover, "image/png");
        }

        private void ServeNowPlayingJson(HttpListenerContext ctx)
        {
            var cur = m_oMonitor.Current;
            if (cur == null)
            {
                WriteJson(ctx, new { playing = false });
                return;
            }
            var key = AlbumCoverMgr.BuildKey(cur.Artist, cur.Album);
            var meta = m_oCover.GetMetadata(key);
            WriteJson(ctx, new
            {
                playing = true,
                title = cur.Title,
                artist = cur.Artist,
                album = cur.Album,
                year = meta.Year,
                genre = meta.Genre
            });
        }

        private void ServeRandomPng(HttpListenerContext ctx)
        {
            var img = m_oCover.GetRandomPicture();
            if (img == null) { ctx.Response.StatusCode = 204; return; }
            WriteImage(ctx, img, "image/png");
        }

        private void ServeIndexJson(HttpListenerContext ctx)
        {
            var pool = m_oCover.GetFilteredPool();
            var list = new List<object>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                string key = pool[i];
                var meta = m_oCover.GetMetadata(key);
                list.Add(new
                {
                    index = i,
                    artist = meta.Artist,
                    album = meta.Album,
                    year = meta.Year,
                    genre = meta.Genre
                });
            }
            WriteJson(ctx, list);
        }

        private void ServeCoverByIndex(HttpListenerContext ctx, string lowercasePath)
        {
            // /cover/12.png -> index 12 within the filtered pool
            string numPart = lowercasePath.Substring("/cover/".Length);
            numPart = numPart.Substring(0, numPart.Length - ".png".Length);
            if (!int.TryParse(numPart, out int idx)) { ctx.Response.StatusCode = 400; return; }

            var pool = m_oCover.GetFilteredPool();
            if (idx < 0 || idx >= pool.Count) { ctx.Response.StatusCode = 404; return; }
            var img = m_oCover.GetPictureByKey(pool[idx]);
            if (img == null) { ctx.Response.StatusCode = 404; return; }
            WriteImage(ctx, img, "image/png");
        }

        private async Task ServeStaticAsync(HttpListenerContext ctx, string filename, string mime)
        {
            try
            {
                string dir = Path.Combine(AppContext.BaseDirectory, "www");
                string full = Path.Combine(dir, filename);
                if (!File.Exists(full)) { ctx.Response.StatusCode = 404; return; }
                byte[] data = await File.ReadAllBytesAsync(full);
                ctx.Response.ContentType = mime;
                ctx.Response.ContentLength64 = data.Length;
                await ctx.Response.OutputStream.WriteAsync(data, 0, data.Length);
            }
            catch
            {
                ctx.Response.StatusCode = 500;
            }
        }

        private static void WriteImage(HttpListenerContext ctx, Image img, string mime)
        {
            try
            {
                ctx.Response.ContentType = mime;
                using (var ms = new MemoryStream())
                {
                    img.Save(ms, ImageFormat.Png);
                    byte[] data = ms.ToArray();
                    ctx.Response.ContentLength64 = data.Length;
                    ctx.Response.OutputStream.Write(data, 0, data.Length);
                }
            }
            catch
            {
                ctx.Response.StatusCode = 500;
            }
        }

        private static void WriteJson(HttpListenerContext ctx, object obj)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.ContentLength64 = data.Length;
                ctx.Response.OutputStream.Write(data, 0, data.Length);
            }
            catch
            {
                ctx.Response.StatusCode = 500;
            }
        }
    }
}
