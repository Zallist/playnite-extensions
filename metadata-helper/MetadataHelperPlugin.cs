using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using PlayniteUtilities;

namespace MetadataHelper
{
    public class MetadataHelperPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override Guid Id { get; } = Guid.Parse("F3643CCF-2EE9-4352-B2D4-B555060D42D7");

        public readonly IPlayniteAPI API;

        public const string BaseMenuSection = "Metadata Helper|";

        private MissingMetadata missingMetadata;

        public MetadataHelperPlugin(IPlayniteAPI api) : base(api)
        {
            API = api;

            Properties = new GenericPluginProperties
            {
                HasSettings = false
            };

            missingMetadata = new MissingMetadata(api);
        }

        public override void Dispose()
        {
            missingMetadata?.Dispose();

            base.Dispose();
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            foreach (var item in missingMetadata.GetGameMenuItems(args))
                yield return item;

            yield return new GameMenuItem() { MenuSection = BaseMenuSection, Description = "Find corrupted entries", Action = FindCorruptedEntries };
        }

        private void FindCorruptedEntries(GameMenuItemActionArgs args)
        {
            using (API.Database.BufferedUpdate())
            {
                var gamesToCheck = new List<Game>(args.Games);
                var corruptedGames = new List<Game>();

                var progressResult = API.Dialogs.ActivateGlobalProgressWithErrorChecking((progress) =>
                {
                    progress.ProgressMaxValue = gamesToCheck.Count;

                    for (int i = 0; i < gamesToCheck.Count; i++)
                    {
                        if (progress.CancelToken.IsCancellationRequested)
                            break;

                        Game game = gamesToCheck[i];

                        progress.CurrentProgressValue = i;
                        progress.Text = $"Checking game ({i}/{gamesToCheck.Count}) ({game.Name})";

                        if (!PlayniteApiUtilities.UpdateDatabaseWithTimeout(API.Database.Games, game, false))
                            corruptedGames.Add(game);
                    }
                }, new GlobalProgressOptions("Checking games...") { IsIndeterminate = false, Cancelable = true });

                if (corruptedGames.Count > 0)
                {
                    foreach (var game in corruptedGames)
                    {
                        PlayniteApiUtilities.UpdateDatabaseWithTimeout(API.Database.Games, game, true);
                    }
                }
                else
                {
                    API.Dialogs.ShowMessage("No corrupted games found", "No corrupted games found");
                }
            }
        }
    }
}