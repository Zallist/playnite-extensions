using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CopyToFasterDrive;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PlayniteUtilities;

namespace MetadataHelper
{
    public class CopyToFasterDrivePlugin : GenericPlugin
    {
        public class StoredRom
        {
            public GameRom OriginalGameRom { get; set; }
            public string ExpandedRomPath { get; set; }
            public string NewRomPath { get; set; }
        }

        public class StoredGameData
        {
            public Guid GameId { get; set; }
            public List<StoredRom> Roms { get; set; } = new List<StoredRom>();
        }

        private static readonly ILogger logger = LogManager.GetLogger();

        public override Guid Id { get; } = Guid.Parse("d39abb82-8199-4bf5-a542-d0b4bd373521");

        public readonly CopyToFasterDriveSettingsViewModel SettingsViewModel;
        public CopyToFasterDriveSettings Settings => SettingsViewModel.Settings;

        public readonly IPlayniteAPI API;

        public ConcurrentDictionary<Guid, StoredGameData> storedGameDatae = new ConcurrentDictionary<Guid, StoredGameData>();

        public CopyToFasterDrivePlugin(IPlayniteAPI api) : base(api)
        {
            SettingsViewModel = new CopyToFasterDriveSettingsViewModel(this);

            API = api;

            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        public override void Dispose()
        {
            RevertStoredGameChanges();
            base.Dispose();
        }

        private static readonly HashSet<string> ExtensionsToLeaveAlone = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "",
            ".opt",
            ".state",
            ".sav",
            ".srm",
        };

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            var game = args.Game;

            if (!Settings.Enabled)
                return null;

            if (!Directory.Exists(Settings.FasterDirectoryPath))
                Directory.CreateDirectory(Settings.FasterDirectoryPath);

            // If we already have data, do nothing else since it could further screw up data
            if (storedGameDatae.ContainsKey(game.Id))
                return null;

            // We only handle if we have a single game action
            if (game.Roms?.Count < 1)
                return null;

            if (game.Roms?.All(r => !string.IsNullOrEmpty(r.Path)) == true)
            {
                var storedGameData = new StoredGameData() { GameId = game.Id };

                for (int i = 0; i < game.Roms.Count; i++)
                {
                    var rom = game.Roms[i];
                    var storedRom = new StoredRom() { OriginalGameRom = rom.GetCopy() };

                    storedRom.ExpandedRomPath = API.ExpandGameVariables(game, rom.Path);
                    storedRom.NewRomPath = Path.Combine(Settings.FasterDirectoryPath, Path.GetFileName(storedRom.ExpandedRomPath));

                    if (!File.Exists(storedRom.ExpandedRomPath))
                    {
                        API.Dialogs.ShowMessage(
                            $"Original rom path {rom.Path} expanded to {storedRom.ExpandedRomPath} which doesn't exist.",
                            "Could not copy rom to faster drive",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return null;
                    }

                    if (rom.Path == storedRom.NewRomPath)
                    {
                        API.Dialogs.ShowMessage(
                            $"The rom called {rom.Name} is already pointed to {storedRom.NewRomPath} which is probably wrong. Fix it.",
                            "Could not copy rom to faster drive because it's already there apparently",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return null;
                    }

                    storedGameData.Roms.Add(storedRom);
                }

                var filesToCopy = new List<FileInfo>();

                foreach (var storedRom in storedGameData.Roms)
                {
                    filesToCopy.AddRange(
                        new DirectoryInfo(Path.GetDirectoryName(storedRom.ExpandedRomPath))
                            .GetFiles($"{Path.GetFileNameWithoutExtension(storedRom.ExpandedRomPath)}.*", SearchOption.TopDirectoryOnly));
                }

                filesToCopy = filesToCopy.GroupBy(f => f.FullName).Select(g => g.First()).ToList();

                var fileTotalSize = 0L;
                var fileDestinations = new List<FileInfo>();
                var hasMissingFiles = false;

                for (int i = 0; i < filesToCopy.Count; i++)
                {
                    var file = filesToCopy[i];
                    var fileDestination = new FileInfo(Path.Combine(Settings.FasterDirectoryPath, file.Name));

                    fileTotalSize += file.Length;

                    fileDestinations.Add(fileDestination);

                    if (!fileDestination.Exists || fileDestination.Length != file.Length)
                        hasMissingFiles = true;
                }

                {
                    DriveInfo driveInfo = new DriveInfo(Settings.FasterDirectoryPath);

                    if (driveInfo.AvailableFreeSpace - fileTotalSize < (1024L * 1024L * 1024L))
                    {
                        API.Dialogs.ShowMessage(
                            $"Could not copy {game.Name} to {Settings.FasterDirectoryPath} because the drive would have less than 1GB left.",
                            "Could not copy rom to faster drive",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return null;
                    }
                }

                if (fileTotalSize > Settings.FasterDirectoryMaxSizeInBytes)
                {
                    // too big, give up
                    var sizeCheck = API.Dialogs.ShowMessage(
                        $"This game is bigger than the maximum size allowed to copy " +
                        $"({GeneralHelpers.GetHumanReadableByteSize(fileTotalSize)} > {GeneralHelpers.GetHumanReadableByteSize(Settings.FasterDirectoryMaxSizeInBytes)}). " +
                        $"\n\nWould you like to copy the files the files anyway?",
                        "Game too big",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (sizeCheck == MessageBoxResult.No)
                        return null;
                }

                // If all the rom files exist and are the right size, then do nothing
                if (hasMissingFiles)
                {
                    var copyResult = API.Dialogs.ActivateGlobalProgress((progress) =>
                    {
                        {
                            var destinationFiles = new DirectoryInfo(Settings.FasterDirectoryPath)
                                .GetFiles()
                                .Where(f => !ExtensionsToLeaveAlone.Contains(f.Extension))
                                .OrderBy(f => f.LastAccessTimeUtc)
                                .ToList();
                            var totalDestinationSize = destinationFiles.Sum(f => f.Length);

                            while (destinationFiles.Count > 0 && totalDestinationSize + fileTotalSize > Settings.FasterDirectoryMaxSizeInBytes)
                            {
                                progress.Text = "Deleting files...";
                                totalDestinationSize -= destinationFiles[0].Length;
                                destinationFiles[0].Delete();
                                destinationFiles.RemoveAt(0);
                            }
                        }

                        {
                            // Copy the rom files to the faster directory
                            for (int i = 0; i < filesToCopy.Count; i++)
                            {
                                var source = filesToCopy[i];
                                var target = fileDestinations[i];

                                progress.IsIndeterminate = false;
                                progress.Text = $"Copying file... \n" +
                                    $"({source.Name})";

                                progress.CurrentProgressValue = target.Exists ? target.Length : 0;
                                progress.ProgressMaxValue = source.Length;

                                FileUtility.CopyFileExtended(source.FullName, target.FullName,
                                    FileUtility.CopyFileFlags.COPY_FILE_NO_BUFFERING,
                                    (total, transferred) =>
                                    {
                                        progress.CurrentProgressValue = transferred;
                                        progress.ProgressMaxValue = total;
                                    },
                                    progress.CancelToken);
                            }
                        }
                    }, new GlobalProgressOptions("Copying files...", false) { Cancelable = true, IsIndeterminate = true });

                    if (copyResult.Canceled)
                        return null;
                    if (copyResult.Error != null)
                    {
                        API.Dialogs.ShowMessage(
                            $"Could not copy {game.Name} to {Settings.FasterDirectoryPath}: {copyResult.Error.Message}",
                            "Could not copy rom to faster drive",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }
                }

                // Change the rom path and store the reversion for later
                storedGameDatae.AddOrUpdate(game.Id,
                    storedGameData,
                    (gameId, existingStoredGameData) => storedGameData);

                for (int i = 0; i < storedGameData.Roms.Count; i++)
                {
                    var storedRom = storedGameData.Roms[i];
                    game.Roms[i].Path = storedRom.NewRomPath;
                }
            }

            return null;
        }

        public void RevertStoredGameChanges()
        {
            if (storedGameDatae.Count > 0)
            {
                lock (storedGameDatae)
                {
                    foreach (var gameId in new List<Guid>(storedGameDatae.Keys))
                    {
                        if (storedGameDatae.TryRemove(gameId, out var originalData))
                        {
                            var game = API.Database.Games.Get(gameId);

                            if (game == null)
                                continue;

                            // We only handle if we have a single game action
                            if (game.Roms?.Count < 1)
                                continue;

                            for (int i = 0; i < game.Roms.Count && i < originalData.Roms.Count; i++)
                            {
                                game.Roms[i].Path = originalData.Roms[i].OriginalGameRom.Path;
                            }

                            // In case it was saved, revert it
                            API.Database.Games.Update(game);
                        }
                    }
                }
            }
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
            => RevertStoredGameChanges();

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
            => RevertStoredGameChanges();

        public override void OnGameStarted(OnGameStartedEventArgs args)
            => RevertStoredGameChanges();

        public override void OnGameStartupCancelled(OnGameStartupCancelledEventArgs args)
            => RevertStoredGameChanges();

        public override void OnGameStopped(OnGameStoppedEventArgs args)
            => RevertStoredGameChanges();

        public override ISettings GetSettings(bool firstRunSettings)
            => SettingsViewModel;

        public override UserControl GetSettingsView(bool firstRunSettings)
            => new CopyToFasterDriveSettingsView();
    }
}