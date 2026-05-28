using BarRaider.SdTools;

namespace ScreenSaver.StreamDeck
{
    /// <summary>
    /// Stream Deck plugin entry point. Stream Deck launches this executable with a
    /// set of WebSocket connection args; SDWrapper handles the handshake and routes
    /// events to action classes tagged with [PluginActionId].
    /// </summary>
    public static class Program
    {
        public static void Main(string[] args)
        {
            SDWrapper.Run(args);
        }
    }
}
