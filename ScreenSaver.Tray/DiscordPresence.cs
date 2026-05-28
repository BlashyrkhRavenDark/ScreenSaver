using System;
using DiscordRPC;

namespace ScreenSaver.Tray
{
    /// <summary>
    /// Pushes the currently-playing track to the local Discord client's Rich
    /// Presence slot. The user appears as "Listening to {title} - {artist}" with
    /// a generic music icon (album art would need each cover URL-hosted somewhere
    /// public, which is out of scope for v1).
    ///
    /// Discord application ID is read from HKCU\SOFTWARE\Demo_ScreenSaver\DiscordAppId.
    /// To set up: create an app at https://discord.com/developers, paste the
    /// numeric Client ID into that registry value, optionally upload an asset
    /// named "music" to the app's Rich Presence Assets so the large icon renders.
    /// </summary>
    public class DiscordPresence
    {
        private DiscordRpcClient m_oClient;

        public void Start(string appId)
        {
            Stop();
            if (string.IsNullOrWhiteSpace(appId) || appId == "0") return;
            try
            {
                m_oClient = new DiscordRpcClient(appId);
                m_oClient.Initialize();
            }
            catch
            {
                m_oClient = null;
            }
        }

        public void Stop()
        {
            try { m_oClient?.ClearPresence(); m_oClient?.Dispose(); }
            catch { }
            m_oClient = null;
        }

        public void Update(global::ScreenSaver.NowPlayingMonitor.NowPlayingInfo info)
        {
            if (m_oClient == null || info == null) return;
            try
            {
                var presence = new RichPresence
                {
                    Details = TruncateForDiscord(info.Title),
                    State = string.IsNullOrEmpty(info.Artist) ? string.Empty : ("by " + TruncateForDiscord(info.Artist)),
                    Assets = new Assets
                    {
                        LargeImageKey = "music",
                        LargeImageText = TruncateForDiscord(info.Album)
                    }
                };
                m_oClient.SetPresence(presence);
            }
            catch { }
        }

        public void Clear()
        {
            try { m_oClient?.ClearPresence(); } catch { }
        }

        /// <summary>
        /// Discord caps string fields at 128 bytes; anything longer is rejected.
        /// Long ID3 titles ("Symphony No. 9 - Mvt. III: Adagio molto e cantabile") hit it.
        /// </summary>
        private static string TruncateForDiscord(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= 128 ? s : s.Substring(0, 125) + "...";
        }
    }
}
