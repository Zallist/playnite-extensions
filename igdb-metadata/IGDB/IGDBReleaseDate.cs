using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IGDBMetadataPlugin.IGDB;

namespace IGDBMetadataPlugin.IGDB
{
    public class IGDBReleaseDate
    {
        public int id { get; set; }

        public long? date { get; set; }

        public ReleaseDateRegion region { get; set; }

        public IGDBPlatform platform { get; set; }

        public DateTime? Date => date.HasValue ? DateTimeOffset.FromUnixTimeSeconds(date.Value).LocalDateTime : (DateTime?)null;

        public enum ReleaseDateRegion
        {
            Europe = 1,
            North_America = 2,
            Australia = 3,
            New_Zealand = 4,
            Japan = 5,
            China = 6,
            Asia = 7,
            Worldwide = 8,
            Korea = 9,
            Brazil = 10
        }
    }
}
