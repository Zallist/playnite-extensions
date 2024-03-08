using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace LocalUserConfig.Models
{
    public class StoredData
    {
        private static string UserNameFilePath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PlayniteData\\LocalUserConfig\\local-user-config.username");

        private static string UserConfigPath = null;

        private static IPlayniteAPI API => Playnite.SDK.API.Instance;

        private static StoredData _instance = null;

        public static StoredData Instance
        {
            get
            {
                EnsureInstance();
                return _instance;
            }
        }

        private StoredData()
        {
        }

        public int Version { get; set; } = 1;

        public StoredAppSettings AppSettings { get; set; } = new StoredAppSettings();

        public Dictionary<Guid, StoredGame> Games { get; set; } = new Dictionary<Guid, StoredGame>();

        public FilterPreset LastFilterPreset { get; set; }

        public Dictionary<Guid,FilterPreset> SavedFilterPresets { get; set; } = new Dictionary<Guid,FilterPreset>();

        public static void EnsureInstance()
        {
            if (_instance != null)
                return;

            if (UserConfigPath == null)
            {
                string userName = null;

                if (File.Exists(UserNameFilePath))
                    userName = File.ReadAllText(UserNameFilePath);

                if (string.IsNullOrWhiteSpace(userName))
                    userName = Environment.UserName;

                UserConfigPath = Path.Combine(
                    API.Paths.ExtensionsDataPath,
                    "LocalUserConfig",
                    userName,
                    "config.json");

                if (!File.Exists(UserNameFilePath))
                {
                    if (!Directory.Exists(Path.GetDirectoryName(UserNameFilePath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(UserNameFilePath));

                    File.WriteAllText(UserNameFilePath, userName);
                }
            }

            try
            {
                if (_instance == null)
                {
                    if (System.IO.File.Exists(UserConfigPath))
                    {
                        _instance = PlayniteUtilities.PlayniteApiUtilities.DeserializeFromFile<StoredData>(UserConfigPath);
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = API.Dialogs.ShowMessage(
                    $"An error occured while parsing {typeof(StoredData).FullName} from {UserConfigPath}. \n" +
                    $"Would you like to continue loading and createa new local user config? \n" +
                    $"This will cause all existing local app settings and play times to be reset. \n" +
                    ex.ToString(),
                    $"An error occured while parsing {nameof(StoredData)}.",
                    System.Windows.MessageBoxButton.YesNo);

                if (msg != System.Windows.MessageBoxResult.Yes)
                {
                    System.Windows.Application.Current.Shutdown(128);
                    throw;
                }

                if (System.IO.File.Exists(UserConfigPath))
                    System.IO.File.Copy(UserConfigPath, $"{UserConfigPath}-{DateTime.Now:yyyyMMddTHHmmss.FFF}.bak", true);
            }

            if (_instance == null)
            {
                _instance = new StoredData();
            }
        }

        public void Save()
        {
            // Make sure we always update our appsettings
            UpdateAppSettings(save: false);

            Exception exception;
            int attempts = 0;

            do
            {
                attempts++;
                exception = null;

                try
                {
                    PlayniteUtilities.PlayniteApiUtilities.SerializeToFile(UserConfigPath, this);
                }
                catch (Exception ex)
                {
                    exception = ex;
                    System.Threading.Thread.Sleep(500);
                }
            }
            while (exception != null && attempts <= 5);

            if (exception != null)
            {
                API.Dialogs.ShowErrorMessage(exception.ToString(), $"An error occured while saving {typeof(StoredData).FullName}.");
                throw exception;
            }
        }

        public void UpdateStoredGame(in Game game, bool save = true)
        {
            if (Games.TryGetValue(game.Id, out var storedGame))
            {
                storedGame.UpdateStoredGame(in game);
            }
            else
            {
                Games[game.Id] = StoredGame.CreateStoredGame(in game);
            }

            if (save)
                Save();
        }

        public void UpdateAppSettings(bool save = true)
        {
            this.LastFilterPreset = new FilterPreset()
            {
                Id = API.MainView.GetActiveFilterPreset(),
                Settings = API.MainView.GetCurrentFilterSettings(),
                GroupingOrder = API.MainView.Grouping,
                SortingOrder = API.MainView.SortOrder,
                SortingOrderDirection = API.MainView.SortOrderDirection
            };

            var actualFilterPreset = API.Database.FilterPresets.Get(this.LastFilterPreset.Id);

            if (actualFilterPreset != null)
            {
                this.LastFilterPreset.Name = actualFilterPreset.Name;
                this.LastFilterPreset.ShowInFullscreeQuickSelection = actualFilterPreset.ShowInFullscreeQuickSelection;
            }

            AppSettings.ActiveDesktopView = API.MainView.ActiveDesktopView;
            AppSettings.ActiveFullscreenView = API.MainView.ActiveFullscreenView;

            AppSettings.SelectedGameId = API.MainView.SelectedGames?.FirstOrDefault()?.Id;

            if (save)
                Save();
        }

        public void ApplySettingsOnStartup()
        {
            if (SavedFilterPresets?.Any() == true)
            {
                using (API.Database.BufferedUpdate())
                {
                    foreach (var savedFilterPresetKVP in SavedFilterPresets)
                    {
                        var savedFilterPreset = savedFilterPresetKVP.Value;
                        var existingFilterPreset = API.Database.FilterPresets.Get(savedFilterPresetKVP.Key);

                        if (existingFilterPreset != null)
                        {
                            savedFilterPreset.CopyDiffTo(existingFilterPreset);
                        }
                        else
                        {
                            API.Database.FilterPresets.Add(savedFilterPreset);
                        }
                    }
                }
            }

            if (LastFilterPreset != null)
                API.MainView.ApplyFilterPreset(LastFilterPreset);

            if (AppSettings.SelectedGameId.HasValue)
                API.MainView.SelectGame(AppSettings.SelectedGameId.Value);

            API.MainView.ActiveDesktopView = AppSettings.ActiveDesktopView;
        }

        public void UpsertFilterPreset(FilterPreset filterPreset)
        {
            if (SavedFilterPresets.TryGetValue(filterPreset.Id, out var savedFilterPreset))
                filterPreset.CopyDiffTo(savedFilterPreset);
            else
                SavedFilterPresets[filterPreset.Id] = filterPreset;

            Save();
        }

        public void DeleteFilterPreset(Guid id)
        {
            if (SavedFilterPresets.Remove(id))
                Save();
        }
    }
}