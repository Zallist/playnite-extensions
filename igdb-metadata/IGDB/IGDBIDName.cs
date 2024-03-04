namespace IGDBMetadataPlugin.IGDB
{
    public class IGDBIDName
    {
        public int id { get; set; }
        public string name { get; set; }

        public override string ToString()
            => name;
    }
}