namespace IGDBMetadataPlugin.IGDB
{
    public class IGDBInvolvedCompany
    {
        public int id { get; set; }
        public IGDBIDName company { get; set; }
        public bool developer { get; set; }
        public bool publisher { get; set; }
    }
}