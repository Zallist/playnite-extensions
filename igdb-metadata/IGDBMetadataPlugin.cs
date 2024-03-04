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
    public abstract class IGDBMetadataPlugin : MetadataPlugin
    {
        public ILogger Logger = LogManager.GetLogger(nameof(IGDBMetadataPlugin));

        private static IGDBMetadataPluginSettingsViewModel settings { get; set; } = null;

        public IGDBMetadataPlugin(IPlayniteAPI api) : base(api)
        {
            if (settings == null)
                settings = new IGDBMetadataPluginSettingsViewModel(this);

            Properties = new MetadataPluginProperties
            {
                HasSettings = true
            };
        }

        public override List<MetadataField> SupportedFields { get; } = new List<MetadataField>
        {
            MetadataField.AgeRating,
            MetadataField.Developers,
            MetadataField.Features,
            MetadataField.Genres,
            MetadataField.Links,
            MetadataField.Platform,
            MetadataField.Publishers,
            MetadataField.Region,
            MetadataField.Series,
            MetadataField.Tags,
        };

        public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
        {
            return new IGDBMetadataPluginProvider(options, this, settings.Settings, Supplemental);
        }

        public abstract bool Supplemental { get; }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new IGDBMetadataPluginSettingsView();
        }
    }
}