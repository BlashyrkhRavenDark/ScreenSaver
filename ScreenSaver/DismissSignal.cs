using System;
using System.Threading;

namespace ScreenSaver
{
    /// <summary>
    /// Cross-process "dismiss the screensaver" handshake over a session-local
    /// named event. The screensaver creates and listens on the event; companion
    /// processes (the Stream Deck plugin) signal it when the user presses a deck
    /// key. This is needed because the deck's media controls are SMTC API calls,
    /// not key events - the saver would otherwise never notice the interaction.
    /// Shared source: compiled into both ScreenSaver and ScreenSaver.StreamDeck.
    /// </summary>
    public static class DismissSignal
    {
        // Local\ scopes the event to the current user session.
        private const string EVENT_NAME = @"Local\AlbumArtScreenSaver.Dismiss";

        /// <summary>
        /// Saver side: create the event and invoke <paramref name="onSignaled"/>
        /// (on a thread-pool thread) the first time anyone sets it. Returns the
        /// handle - keep it referenced for the saver's lifetime.
        /// </summary>
        public static EventWaitHandle Listen(Action onSignaled)
        {
            var ev = new EventWaitHandle(false, EventResetMode.AutoReset, EVENT_NAME, out bool createdNew);
            if (!createdNew) ev.Reset(); // clear any stale signal from a previous run
            ThreadPool.RegisterWaitForSingleObject(ev, (state, timedOut) => onSignaled(),
                null, Timeout.Infinite, executeOnlyOnce: true);
            return ev;
        }

        /// <summary>Companion side: signal a running saver, if any. No-op otherwise.
        /// Opens and closes the handle per call so a dead saver's event doesn't
        /// linger signaled and instantly dismiss the next launch.</summary>
        public static void Dismiss()
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting(EVENT_NAME, out EventWaitHandle ev))
                    using (ev) ev.Set();
            }
            catch
            {
                // No saver running (or no rights) - nothing to dismiss.
            }
        }
    }
}
