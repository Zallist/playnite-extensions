using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CopyToFasterDrive;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace LibraryExternalizer
{
    public class LibraryExternalizerPlugin : GenericPlugin
    {
        public override Guid Id => new Guid("4f11a7f2-75bf-4834-b93e-e960240b88d9");
        public IPlayniteAPI API { get; }
        public ExternalizerSettingsViewModel SettingsViewModel { get; }
        public const string BaseMenuSection = "@Library Externalizer|";

        public LibraryExternalizerPlugin(IPlayniteAPI api) : base(api)
        {
            SettingsViewModel = new ExternalizerSettingsViewModel(this);

            API = api;

            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem()
            {
                MenuSection = BaseMenuSection + "Export",
                Description = "Export gamelist.xml",
                Action = (menuArgs) =>
                {

                }
            };

            yield return new MainMenuItem()
            {
                MenuSection = BaseMenuSection + "Export",
                Description = "-"
            };

            yield return new MainMenuItem()
            {
                MenuSection = BaseMenuSection + "Export",
                Description = "Export all enabled",
                Action = (menuArgs) =>
                {

                }
            };

            yield return new MainMenuItem()
            {
                MenuSection = BaseMenuSection + "Import",
                Description = "Import gamelist.xml",
                Action = (menuArgs) =>
                {

                }
            };
        }
    }
}
