namespace IGDBMetadataPlugin.IGDB
{
    public class IGDBWebsite
    {
        public enum WebsiteCategoryEnum : int
        {
            //NULL = 0,
            Official = 1,
            Wikia = 2,
            Wikipedia = 3,
            Facebook = 4,
            Twitter = 5,
            Twitch = 6,
            Instagram = 8,
            YouTube = 9,
            //iPhone = 10,
            //iPad = 11,
            //Android = 12,
            Steam = 13,
            Reddit = 14,
            Itch = 15,
            Epic = 16,
            GOG = 17,
            Discord = 18,
        }

        public int id { get; set; }
        public string url { get; set; }
        public int category { get; set; }

        public WebsiteCategoryEnum category_enum => (WebsiteCategoryEnum)category;
    }
}