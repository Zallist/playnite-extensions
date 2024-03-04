using System;
using System.Collections.Generic;
using LocalUserConfig.Models;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace LocalUserConfig
{
    public class LocalUserConfigPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override Guid Id { get; } = Guid.Parse("B5E720C2-96E9-4037-80A1-848FF11C32C2");

        public readonly IPlayniteAPI API;

        private bool started = false;

        public LocalUserConfigPlugin(IPlayniteAPI api) : base(api)
        {
            API = api;

            Properties = new GenericPluginProperties
            {
                HasSettings = false
            };

            API.Database.Games.ItemUpdated += Games_ItemUpdated;
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            StoredData.EnsureInstance();
            StoredData.Instance.ApplyStoredAppSettings();

            List<Guid> missingGuids = new List<Guid>();

            //using (API.Database.BufferedUpdate())
            {
                foreach (var kvp in StoredData.Instance.Games)
                {
                    var game = API.Database.Games.Get(kvp.Key);

                    if (game != null)
                    {
                        kvp.Value.UpdateRealGame(ref game);
                        //PlayniteApiUtilities.UpdateDatabaseWithTimeout(API.Database.Games, game);
                    }
                    else
                    {
                        missingGuids.Add(kvp.Key);
                    }
                }
            }

            if (missingGuids.Count > 0)
                missingGuids.ForEach(x => StoredData.Instance.Games.Remove(x));

            started = true;
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            if (!started)
                return;

            StoredData.Instance.UpdateAppSettings(save: false);

            // Update all stored info about games
            foreach (var gameId in StoredData.Instance.Games.Keys)
            {
                var game = API.Database.Games.Get(gameId);
                if (game != null)
                    StoredData.Instance.UpdateStoredGame(game, save: false);
            }

            StoredData.Instance.Save();
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            if (!started)
                return;

            StoredData.Instance.UpdateStoredGame(args.Game, save: true);
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (!started)
                return;

            StoredData.Instance.UpdateStoredGame(args.Game, save: true);
        }

        private void Games_ItemUpdated(object sender, ItemUpdatedEventArgs<Game> e)
        {
            if (!started)
                return;

            foreach (var u in e.UpdatedItems)
                if (u.NewData != null)
                    StoredData.Instance.UpdateStoredGame(u.NewData, save: false);

            StoredData.Instance.Save();
        }
    }
}