using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace IGDBMetadataPlugin
{
    public class IGDBMetadataPluginSupplement : IGDBMetadataPlugin
    {
        public override Guid Id { get; } = Guid.Parse("e0a0c910-c966-495c-98d0-bc1ad7b1a83f");

        public IGDBMetadataPluginSupplement(IPlayniteAPI api) : base(api)
        {
        }

        public override string Name => "IGDB Metadata (supplement)";

        public override bool Supplemental => true;
    }
}