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
using System.Threading.Tasks;
using System.Windows.Controls;
using PlayniteUtilities;

namespace MetadataHelper
{
    public class MissingMetadata : IDisposable
    {
        #region Enums
        [Flags]
        public enum Info : ushort
        {
            All = (1 << 16) - 1,

            Platform = 1 << 0,
            Genres = 1 << 1,
            Description = 1 << 2,
            ReleaseDate = 1 << 3,
            AgeRating = 1 << 4
        }

        [Flags]
        public enum Media : ushort
        {
            All = (1 << 16) - 1,

            Icon = 1 << 0,
            Cover = 1 << 1,
            Background = 1 << 2
        }
        #endregion

        #region Dictionary Mappings
        private static readonly Dictionary<Info, string> InfoToTag = new Dictionary<Info, string>()
        {
            { Info.AgeRating, "Missing Info: Age Rating" },
            { Info.Platform, "Missing Info: Platform" },
            { Info.Description, "Missing Info: Description" },
            { Info.Genres, "Missing Info: Genres" },
            { Info.ReleaseDate, "Missing Info: Release Date" },
        };

        private static readonly Dictionary<string, Info> TagToInfo = new Dictionary<string, Info>(
            InfoToTag.ToDictionary(k => k.Value, k => k.Key), StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<Media, string> MediaToTag = new Dictionary<Media, string>()
        {
            { Media.Icon, "Missing Media: Icon" },
            { Media.Cover, "Missing Media: Cover" },
            { Media.Background, "Missing Media: Background" },
        };

        private static readonly Dictionary<string, Media> TagToMedia = new Dictionary<string, Media>(
            MediaToTag.ToDictionary(k => k.Value, k => k.Key), StringComparer.OrdinalIgnoreCase);
        #endregion

        const string MissingMenuSection = MetadataHelperPlugin.BaseMenuSection + "Missing Info";

        public readonly IPlayniteAPI API;

        public MissingMetadata(IPlayniteAPI api)
        {
            this.API = api;

            API.Database.Games.ItemUpdated += Games_ItemUpdated;
        }

        public void Dispose()
        {
            API.Database.Games.ItemUpdated -= Games_ItemUpdated;
        }

        private GameMenuItem CreateMenuItem(string menuSection, string text, Action<GameMenuItemActionArgs> action)
        {
            return new GameMenuItem() { MenuSection = menuSection, Description = text, Action = action };
        }

        public IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            if (args.Games.Count < 1)
                yield break;

            string suffix = $"on {args.Games.Count} game{(args.Games.Count > 1 ? "s" : "")}";
            string missingInfo = $"{MissingMenuSection}|Add tags for missing info {suffix}";
            string missingMedia = $"{MissingMenuSection}|Add tags for missing media {suffix}";

            yield return CreateMenuItem(missingInfo,
                "All",
                (mArgs) => AddTagsForMissingInfo(mArgs, Info.All));

            yield return new GameMenuItem() { MenuSection = missingInfo, Description = "-" };

            foreach (var e in GeneralHelpers.GetEnumFlagValues<Info>())
            {
                yield return CreateMenuItem(missingInfo,
                    $"Just {Enum.GetName(typeof(Info), e).ToLower()}",
                    (mArgs) => AddTagsForMissingInfo(mArgs, e));
            }

            yield return CreateMenuItem(missingMedia,
                "All",
                (mArgs) => AddTagsForMissingMedia(mArgs, Media.All));

            yield return new GameMenuItem() { MenuSection = missingMedia, Description = "-" };

            foreach (var e in GeneralHelpers.GetEnumFlagValues<Media>())
            {
                yield return CreateMenuItem(missingMedia,
                    $"Just {Enum.GetName(typeof(Media), e).ToLower()}",
                    (mArgs) => AddTagsForMissingMedia(mArgs, e));
            }
        }

        #region Missing Stuff
        private void AddTagsForMissingInfo(GameMenuItemActionArgs args, Info infos)
        {
            try
            {
                var hasInfo = new ConcurrentBag<Tuple<Game, Info, bool>>();

                var progressResult = API.Dialogs.ActivateGlobalProgressWithErrorChecking((progress) =>
                {
                    foreach (var game in args.Games)
                        foreach (var e in GeneralHelpers.GetEnumFlagValues<Info>())
                            if (infos.HasFlag(e))
                                hasInfo.Add(Tuple.Create(game, e, InfoExists(game, e)));
                }, new GlobalProgressOptions("Checking missing info...") { IsIndeterminate = true, Cancelable = false });

                if (progressResult.Canceled || progressResult.Error != null || hasInfo.IsEmpty)
                    return;

                AddTagsForMissingStuff(hasInfo, InfoToTag);
            }
            catch (Exception ex)
            {
                API.Dialogs.ShowErrorMessage(ex.ToString(), "Failed to add tags for missing info");
                throw;
            }
        }

        private void AddTagsForMissingMedia(GameMenuItemActionArgs args, Media medias)
        {
            try
            {
                var hasMedia = new ConcurrentBag<Tuple<Game, Media, bool>>();

                var progressResult = API.Dialogs.ActivateGlobalProgressWithErrorChecking((progress) =>
                {
                    foreach (var game in args.Games)
                        foreach (var e in GeneralHelpers.GetEnumFlagValues<Media>())
                            if (medias.HasFlag(e))
                                hasMedia.Add(Tuple.Create(game, e, MediaExists(game, e)));
                }, new GlobalProgressOptions("Checking missing media...") { IsIndeterminate = true, Cancelable = false });

                if (progressResult.Canceled || progressResult.Error != null || hasMedia.IsEmpty)
                    return;

                AddTagsForMissingStuff(hasMedia, MediaToTag);
            }
            catch (Exception ex)
            {
                API.Dialogs.ShowErrorMessage(ex.ToString(), "Failed to add tags for missing info");
                throw;
            }
        }

        private void AddTagsForMissingStuff<TEnum>(
            IReadOnlyCollection<Tuple<Game, TEnum, bool>> hasStuff,
            Dictionary<TEnum, string> enumToTag)
        {
            int count = 0;
            var groups = hasStuff.GroupBy(g => g.Item1).ToDictionary(g => g.Key);

            API.Dialogs.ActivateGlobalProgressWithErrorChecking((progress) =>
            {
                Dictionary<TEnum, Guid> enumToTagId = new Dictionary<TEnum, Guid>();

                foreach (var e in enumToTag.Keys)
                    enumToTagId[e] = API.Database.Tags.Add(enumToTag[e]).Id;

                progress.ProgressMaxValue = groups.Count;

                HashSet<Game> gamesToUpdate = new HashSet<Game>();

                foreach (var group in groups)
                {
                    if (count % 10 == 0)
                    {
                        progress.CurrentProgressValue = count;
                        progress.Text = $"Changing tags ({count} of {groups.Count})";
                    }

                    if (progress.CancelToken.IsCancellationRequested)
                        return;

                    var game = group.Key;

                    if (game.TagIds == null)
                        game.TagIds = new List<Guid> { };

                    foreach (var tup in group.Value)
                    {
                        var tagId = enumToTagId[tup.Item2];
                        var exists = tup.Item3;

                        if (exists)
                        {
                            if (game.TagIds.Contains(tagId))
                            {
                                game.TagIds.Remove(tagId);
                                gamesToUpdate.Add(game);
                            }
                        }
                        else
                        {
                            if (!game.TagIds.Contains(tagId))
                            {
                                game.TagIds.Add(tagId);
                                gamesToUpdate.Add(game);
                            }
                        }
                    }

                    count++;
                }

                if (gamesToUpdate.Count > 0)
                {
                    count = 0;
                    progress.ProgressMaxValue = gamesToUpdate.Count;
                    progress.CurrentProgressValue = count;
                    progress.Text = $"Saving Games";

                    using (API.Database.BufferedUpdate())
                    {
                        foreach (var game in gamesToUpdate)
                        {
                            if (progress.CancelToken.IsCancellationRequested)
                                return;

                            if (count % 10 == 0)
                            {
                                progress.CurrentProgressValue = count;
                                progress.Text = $"Saving Games ({count} of {gamesToUpdate.Count}) ({game.Name})";
                            }

                            API.Database.Games.Update(game);

                            count++;
                        }
                    }
                }
            }, new GlobalProgressOptions($"Adding tags ({count} of {groups.Count})") { Cancelable = true, IsIndeterminate = false });
        }

        private bool InfoExists(Game game, Info info)
        {
            switch (info)
            {
                case Info.Platform:
                    if (game.PlatformIds != null && game.PlatformIds.Count > 0)
                        return true;
                    break;
                case Info.AgeRating:
                    if (game.AgeRatingIds != null && game.AgeRatingIds.Count > 0)
                        return true;
                    break;
                case Info.Genres:
                    if (game.GenreIds != null && game.GenreIds.Count > 0)
                        return true;
                    break;
                case Info.Description:
                    if (!string.IsNullOrWhiteSpace(game.Description))
                        return true;
                    break;
                case Info.ReleaseDate:
                    if (game.ReleaseDate.HasValue)
                        return true;
                    break;
            }

            return false;
        }

        private bool MediaExists(Game game, Media media)
        {
            switch (media)
            {
                case Media.Icon:
                    if (!string.IsNullOrWhiteSpace(game.Icon))
                    {
                        if (game.Icon.StartsWith("http"))
                            return true;
                        if (File.Exists(API.Database.GetFullFilePath(game.Icon)))
                            return true;
                    }
                    break;
                case Media.Cover:
                    if (!string.IsNullOrWhiteSpace(game.CoverImage))
                    {
                        if (game.CoverImage.StartsWith("http"))
                            return true;
                        if (File.Exists(API.Database.GetFullFilePath(game.CoverImage)))
                            return true;
                    }
                    break;
                case Media.Background:
                    if (!string.IsNullOrWhiteSpace(game.BackgroundImage))
                    {
                        if (game.BackgroundImage.StartsWith("http"))
                            return true;
                        if (File.Exists(API.Database.GetFullFilePath(game.BackgroundImage)))
                            return true;
                    }
                    break;
            }

            return false;
        }
        #endregion

        private void Games_ItemUpdated(object sender, ItemUpdatedEventArgs<Game> e)
        {
            try
            {
                Dictionary<Guid, Info> tagIdToInfo = new Dictionary<Guid, Info>();
                Dictionary<Guid, Media> tagIdToMedia = new Dictionary<Guid, Media>();

                foreach (var tag in API.Database.Tags)
                {
                    if (TagToInfo.TryGetValue(tag.Name, out var info))
                        tagIdToInfo[tag.Id] = info;
                    if (TagToMedia.TryGetValue(tag.Name, out var media))
                        tagIdToMedia[tag.Id] = media;
                }

                HashSet<Game> gamesToUpdate = new HashSet<Game>();

                foreach (var updatedItem in e.UpdatedItems)
                {
                    var game = updatedItem.NewData;

                    if (game == null || game.TagIds == null)
                        continue;

                    for (int i = game.TagIds.Count - 1; i >= 0; i--)
                    {
                        var removeTag = false;
                        var tag = game.TagIds[i];

                        if (tagIdToInfo.TryGetValue(tag, out var info) && InfoExists(game, info))
                            removeTag = true;
                        if (tagIdToMedia.TryGetValue(tag, out var media) && MediaExists(game, media))
                            removeTag = true;

                        if (removeTag)
                        {
                            game.TagIds.RemoveAt(i);
                            gamesToUpdate.Add(game);
                        }
                    }
                }


                if (gamesToUpdate.Count > 0)
                {
                    using (API.Database.BufferedUpdate())
                    {
                        foreach (var game in gamesToUpdate)
                        {
                            API.Database.Games.Update(game);
                        }
                    }
                }
            }
            catch { }
        }
    }
}