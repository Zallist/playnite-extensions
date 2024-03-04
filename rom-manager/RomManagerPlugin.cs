using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PlayniteUtilities;

namespace RomManager
{
    public class RomManagerPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override Guid Id { get; } = Guid.Parse("7A535507-7110-4CB3-A703-A25A7D5C6D18");

        public readonly IPlayniteAPI API;

        public const string BaseMenuSection = "Rom Manager|";

        public RomManagerPlugin(IPlayniteAPI api) : base(api)
        {
            API = api;

            Properties = new GenericPluginProperties
            {
                HasSettings = false
            };
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            var findDuplicateRomsMenuSection = $"{BaseMenuSection}Find duplicate roms|";

            yield return new MainMenuItem()
            {
                MenuSection = BaseMenuSection,
                Description = "Find duplicated roms",
                Action = (s) =>
                {
                    var window = API.Dialogs.CreateWindow(new WindowCreationOptions()
                    {
                        ShowCloseButton = true,
                        ShowMaximizeButton = false,
                        ShowMinimizeButton = false
                    });

                    var comparisonOptions = new ComparisonOptions();

                    window.Content = comparisonOptions;

                    window.Title = "Comparison Options";
                    window.SizeToContent = SizeToContent.WidthAndHeight;
                    window.ResizeMode = ResizeMode.NoResize;

                    var result = window.ShowDialog();

                    if (result == true)
                    {
                        FindDuplicateRoms(
                            comparisonOptions.SelectedUsingSourceList,
                            comparisonOptions.SelectedAgainstSourceList,
                            comparisonOptions.SelectedComparisonField);
                    }
                }
                //Action = (s) => FindDuplicateRoms(GameListSource.AllGames, GameListSource.AllGames)
            };

            yield break;
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            yield return new GameMenuItem()
            {
                MenuSection = BaseMenuSection,
                Description = "Cleanup games with missing rom files",
                Action = (s) => CleanupMissingRoms(s.Games.Select(g => g.Id).ToHashSet())
            };

            yield break;
        }

        private void CleanupMissingRoms(HashSet<Guid> gameIds)
        {
            Dictionary<Game, Tuple<Guid, string>> gameToEmulatorProfile = new Dictionary<Game, Tuple<Guid, string>>();
            Dictionary<Game, List<string>> missingGames = new Dictionary<Game, List<string>>();

            var buildingResult = API.Dialogs.ActivateGlobalProgress(progress =>
            {
                progress.ProgressMaxValue = gameIds.Count;
                progress.CurrentProgressValue = 0;

                foreach (var gameId in gameIds)
                {
                    if (progress.CancelToken.IsCancellationRequested)
                        return;

                    progress.CurrentProgressValue++;

                    var game = API.Database.Games.Get(gameId);

                    if (game?.Roms?.Count > 0)
                    {
                        var gameAction = game.GameActions.FirstOrDefault(ga => ga.Type == GameActionType.Emulator);

                        if (gameAction == null)
                            continue;

                        var emulator = API.Database.Emulators.Get(gameAction.EmulatorId);

                        if (emulator == null)
                            continue;

                        gameToEmulatorProfile[game] = Tuple.Create(gameAction.EmulatorId, gameAction.EmulatorProfileId);

                        bool anyFound = false;
                        List<string> missingPaths = new List<string>();

                        foreach (var rom in game.Roms)
                        {
                            var romPath = API.ExpandGameVariables(game, rom.Path);

                            if (System.IO.File.Exists(romPath))
                            {
                                anyFound = true;
                            }
                            else
                            {
                                missingPaths.Add(rom.Path);
                            }
                        }

                        if (!anyFound)
                        {
                            // Delete the game
                            missingGames[game] = null;
                        }
                        else if (missingPaths.Count > 0)
                        {
                            // Just delete these roms
                            if (!missingGames.TryGetValue(game, out var romPaths))
                                missingGames[game] = romPaths = new List<string>(missingPaths);
                            else if (romPaths != null)
                                romPaths.AddRange(missingPaths);
                        }
                    }
                }
            }, new GlobalProgressOptions("Cleaning up...", true) { IsIndeterminate = false });

            if (buildingResult.Canceled || buildingResult.Error != null)
                return;

            if (missingGames.Count == 0)
            {
                API.Dialogs.ShowMessage($"No missing roms found");
                return;
            }

            Dictionary<Tuple<Guid, string>, int[]> toBeDeleted = new Dictionary<Tuple<Guid, string>, int[]>();

            foreach (var gameKVP in missingGames)
            {
                var key = gameToEmulatorProfile[gameKVP.Key];

                if (!toBeDeleted.TryGetValue(key, out var counts))
                    toBeDeleted[key] = counts = new int[2] { 0, 0 };

                counts[0] += 1;
                counts[1] += gameKVP.Value == null ? gameKVP.Key.Roms.Count : gameKVP.Value.Count;
            }

            StringBuilder emulatorSpecificCounts = new StringBuilder();

            foreach (var kvp in toBeDeleted)
            {
                var emulator = API.Database.Emulators.Get(kvp.Key.Item1);
                var emulatorProfile = emulator?.GetProfile(kvp.Key.Item2);

                if (emulator == null)
                    continue;

                if (emulatorProfile != null)
                    emulatorSpecificCounts.AppendLine($"{emulator.Name} ({emulatorProfile.Name}): {kvp.Value[0]} games and {kvp.Value[1]} roms");
                else
                    emulatorSpecificCounts.AppendLine($"{emulator.Name}: {kvp.Value[0]} games and {kvp.Value[1]} roms");
            }

            var confirm = API.Dialogs.ShowMessage(
                $"{missingGames.Count} games were found to be missing roms split across a number of emulators. Continue? \n" +
                $"\n" +
                $"{emulatorSpecificCounts}",
                "Missing roms found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            API.Dialogs.ActivateGlobalProgress(progress =>
            {
                progress.ProgressMaxValue = missingGames.Count;
                progress.CurrentProgressValue = 0;

                using (API.Database.BufferedUpdate())
                {
                    foreach (var gameKVP in missingGames)
                    {
                        if (progress.CancelToken.IsCancellationRequested)
                            break;

                        progress.CurrentProgressValue++;

                        var game = gameKVP.Key;
                        var romPaths = gameKVP.Value;

                        if (romPaths == null)
                        {
                            API.Database.Games.Remove(game);
                        }
                        else
                        {
                            for (int i = game.Roms.Count - 1; i >= 0; i--)
                            {
                                GameRom rom = game.Roms[i];

                                if (romPaths.Contains(rom.Path))
                                {
                                    game.Roms.RemoveAt(i);
                                    continue;
                                }
                            }

                            API.Database.Games.Update(game);
                        }
                    }
                }
            }, new GlobalProgressOptions("Deleting...", true) { IsIndeterminate = false });
        }

        public void FindDuplicateRoms(GameListSource usingSourceList, GameListSource againstSourceList,
            RomManage.Enums.ComparisonField comparisonField)
        {
            // Loop through all games and figure out which ones are duplicates, rank them by
            // similarity for each rom Account for differences that guarantee a different file
            // ("Disc 1") Similarity is based on how close the file names are (minus any obvious
            // ignorable chunks like region or revision or version)

            var usingList = PlayniteApiUtilities.GetGameList(usingSourceList).ToDictionary(g => g.Id);
            var againstList = PlayniteApiUtilities.GetGameList(againstSourceList).ToDictionary(g => g.Id);

            var allGames = usingList.Values.ToHashSet();
            allGames.UnionWith(againstList.Values);

            var gameComparers = new ConcurrentDictionary<Int32, ComparableRom>(Environment.ProcessorCount, allGames.Count);

            API.Dialogs.ActivateGlobalProgress(progress =>
            {
                int count;

                progress.Text = "Parsing games...";
                progress.IsIndeterminate = true;

                Parallel.ForEach(allGames, new ParallelOptions() { CancellationToken = progress.CancelToken }, game =>
                {
                    foreach (var comp in ComparableRom.Parse(API, game, comparisonField))
                        gameComparers[comp.Id] = comp;
                });

                if (progress.CancelToken.IsCancellationRequested)
                    return;

                var compareUsing = gameComparers.Values.Where(c => usingList.ContainsKey(c.GameId)).ToList();
                var compareAgainst = gameComparers.Values.Where(c => againstList.ContainsKey(c.GameId)).ToList();

                progress.CurrentProgressValue = count = 0;
                progress.ProgressMaxValue = compareUsing.Count;
                progress.IsIndeterminate = false;

                UpdateComparingProgress();

                int updateProgressEveryX = Math.Max((int)Math.Floor(compareUsing.Count / 100.0), 1);

                Parallel.ForEach(compareUsing, new ParallelOptions() { CancellationToken = progress.CancelToken }, gameComparer =>
                {
                    foreach (var otherGameComparer in compareAgainst)
                        gameComparer.CompareTo(otherGameComparer);

                    if (++count % updateProgressEveryX == 0)
                        UpdateComparingProgress();
                });

                void UpdateComparingProgress()
                {
                    progress.Text = $"Comparing roms... ({count} / {compareUsing.Count})";
                    progress.CurrentProgressValue = count;
                }
            }, new GlobalProgressOptions("Finding duplicates...", true) { IsIndeterminate = true });

            var duplicateRomWindow = API.Dialogs.CreateWindow(new WindowCreationOptions()
            {
                ShowCloseButton = true,
                ShowMaximizeButton = true,
                ShowMinimizeButton = true
            });

            duplicateRomWindow.Title = "Duplicates";

            duplicateRomWindow.Content = new DuplicateRoms(API, gameComparers.Values.ToList());

            duplicateRomWindow.Show();
        }
    }
}