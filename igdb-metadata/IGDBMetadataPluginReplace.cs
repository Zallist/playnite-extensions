using System;
using System.Collections.Generic;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace IGDBMetadataPlugin
{
    public class IGDBMetadataPluginReplace : IGDBMetadataPlugin
    {
        public override Guid Id { get; } = Guid.Parse("E2C2C488-094F-4EB8-A9FE-C4B2490DD6D8");

        public override List<MetadataField> SupportedFields { get; }

        public IGDBMetadataPluginReplace(IPlayniteAPI api) : base(api)
        {
            SupportedFields = base.SupportedFields;
            SupportedFields.Add(MetadataField.Name);
            SupportedFields.Add(MetadataField.Description);
            SupportedFields.Add(MetadataField.CommunityScore);
            SupportedFields.Add(MetadataField.CriticScore);
            SupportedFields.Add(MetadataField.ReleaseDate);

            SupportedFields.Add(MetadataField.BackgroundImage);
            SupportedFields.Add(MetadataField.CoverImage);
            SupportedFields.Add(MetadataField.Icon);
        }

        public override string Name => "IGDB Metadata (replace)";

        public override bool Supplemental => false;
    }
}