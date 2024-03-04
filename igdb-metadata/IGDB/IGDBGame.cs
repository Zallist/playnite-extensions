using System.Collections.Generic;

namespace IGDBMetadataPlugin.IGDB
{
    public class IGDBGame
    {
        public int id { get; set; }
        public string name { get; set; }
        public string url { get; set; }
        public List<IGDBIDName> genres { get; set; } = new List<IGDBIDName>();
        public List<IGDBIDName> keywords { get; set; } = new List<IGDBIDName>();
        public List<IGDBIDName> game_modes { get; set; } = new List<IGDBIDName>();
        public List<IGDBIDName> game_localizations { get; set; } = new List<IGDBIDName>();
        public List<IGDBIDName> ports { get; set; } = new List<IGDBIDName>();
        public List<IGDBIDName> remakes { get; set; } = new List<IGDBIDName>();
        public List<IGDBIDName> remasters { get; set; } = new List<IGDBIDName>();
        public List<IGDBIDName> themes { get; set; } = new List<IGDBIDName>();
        public List<IGDBIDName> player_perspectives { get; set; } = new List<IGDBIDName>();
        public List<IGDBAlternativeName> alternative_names { get; set; } = new List<IGDBAlternativeName>();
        public List<IGDBPlatform> platforms { get; set; } = new List<IGDBPlatform>();
        public List<IGDBWebsite> websites { get; set; } = new List<IGDBWebsite>();
        public List<IGDBMultiplayerMode> multiplayer_modes { get; set; } = new List<IGDBMultiplayerMode>();

        public List<IGDBAgeRating> age_ratings { get; set; } = new List<IGDBAgeRating>();
        public List<IGDBInvolvedCompany> involved_companies { get; set; } = new List<IGDBInvolvedCompany>();
        public List<IGDBReleaseDate> release_dates { get; set; } = new List<IGDBReleaseDate>();
        public List<IGDBIDName> collections { get; set; } = new List<IGDBIDName>();

        public string summary { get; set; }
        public string storyline { get; set; }
        public List<IGDBImage> artworks { get; set; } = new List<IGDBImage>();
        public IGDBImage cover { get; set; }
        public List<IGDBImage> screenshots { get; set; } = new List<IGDBImage>();
        public double? aggregated_rating { get; set; }
        public double? rating { get; set; }

        public override string ToString()
            => name;
    }
}