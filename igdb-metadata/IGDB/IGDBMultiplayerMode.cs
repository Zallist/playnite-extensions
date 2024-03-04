using System.Collections.Generic;
using System.Linq;

namespace IGDBMetadataPlugin.IGDB
{
    public class IGDBMultiplayerMode
    {
        public ulong id { get; set; }
        public bool campaigncoop { get; set; }
        public bool dropin { get; set; }
        public bool splitscreen { get; set; }
        public bool splitscreenonline { get; set; }
        public bool lancoop { get; set; }
        public bool offlinecoop { get; set; }
        public bool onlinecoop { get; set; }

        public int offlinecoopmax { get; set; }
        public int onlinecoopmax { get; set; }
        public int offlinemax { get; set; }
        public int onlinemax { get; set; }

        //public IGDBPlatform platform { get; set; }

        public static IGDBMultiplayerMode GenerateMaxMultiplayerMode(IEnumerable<IGDBMultiplayerMode> modes)
        {
            return new IGDBMultiplayerMode
            {
                campaigncoop = modes.Any(m => m.campaigncoop),
                dropin = modes.Any(m => m.dropin),
                splitscreen = modes.Any(m => m.splitscreen),
                splitscreenonline = modes.Any(m => m.splitscreenonline),
                lancoop = modes.Any(m => m.lancoop),
                offlinecoop = modes.Any(m => m.offlinecoop),
                onlinecoop = modes.Any(m => m.onlinecoop),

                offlinecoopmax = modes.Max(m => m.offlinecoopmax),
                onlinecoopmax = modes.Max(m => m.onlinecoopmax),
                offlinemax = modes.Max(m => m.offlinemax),
                onlinemax = modes.Max(m => m.onlinemax)
            };
        }
    }
}