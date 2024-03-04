using System;
using System.Collections.Generic;
using System.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace LocalUserConfig.Models
{
    public class StoredData
    {
        private static readonly string UserConfigPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PlayniteLocalUserConfig\\stored_data.dat");

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

        private int version = 1;
        public int Version => version;

        public StoredAppSettings AppSettings { get; set; } = new StoredAppSettings();

        public Dictionary<Guid, StoredGame> Games { get; set; } = new Dictionary<Guid, StoredGame>();

        public List<FilterPreset> SavedFilterPresets { get; set; } = new List<FilterPreset>();

        public static void EnsureInstance()
        {
            if (_instance != null)
                return;

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
            AppSettings.FilterPresetId = API.MainView.GetActiveFilterPreset();
            AppSettings.FilterSettings = API.MainView.GetCurrentFilterSettings();
            AppSettings.Grouping = API.MainView.Grouping;
            AppSettings.SortOrder = API.MainView.SortOrder;
            AppSettings.SortOrderDirection = API.MainView.SortOrderDirection;
            AppSettings.ActiveDesktopView = API.MainView.ActiveDesktopView;
            AppSettings.ActiveFullscreenView = API.MainView.ActiveFullscreenView;

            AppSettings.SelectedGameId = API.MainView.SelectedGames?.FirstOrDefault()?.Id;

            if (save)
                Save();
        }

        public void ApplyStoredAppSettings()
        {
            var filterPreset = API.Database.FilterPresets.Get(AppSettings.FilterPresetId) ??
                new FilterPreset();

            if (AppSettings.FilterSettings != null)
            {
                filterPreset.GroupingOrder = AppSettings.Grouping;
                filterPreset.SortingOrder = AppSettings.SortOrder;
                filterPreset.SortingOrderDirection = AppSettings.SortOrderDirection;
                filterPreset.Settings = AppSettings.FilterSettings;

                API.MainView.ApplyFilterPreset(filterPreset);
            }

            if (AppSettings.SelectedGameId.HasValue)
                API.MainView.SelectGame(AppSettings.SelectedGameId.Value);

            API.MainView.ActiveDesktopView = AppSettings.ActiveDesktopView;
        }
    }
}