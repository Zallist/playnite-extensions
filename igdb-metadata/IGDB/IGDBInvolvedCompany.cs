using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IGDBMetadataPlugin.IGDB;

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
