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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PlayniteUtilities;

namespace KidMode
{
    public class KidModePlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override Guid Id { get; } = Guid.Parse("3CB8A002-F125-4AA1-8DE3-3702EFB7094E");

        public readonly IPlayniteAPI API;

        public const string BaseMenuSection = "Kid Mode|";
        public const string Tag_KidModeAny = "[Kidmode]: Any";

        public readonly string KidModeConfigFilePath;

        private Guid Tag_KidModeAnyId;

        private TopPanelItem TopPanelItem_KidModeIsEnabled;
        private TopPanelItem TopPanelItem_KidModeIsDisabled;

        public KidModePlugin(IPlayniteAPI api) : base(api)
        {
            API = api;

            KidModeConfigFilePath = Path.Combine(
                API.Paths.ExtensionsDataPath,
                "KidMode",
                "kid-mode-tagged.json");

            Properties = new GenericPluginProperties
            {
                HasSettings = false
            };

            API.Database.Tags.ItemCollectionChanged += Tags_ItemCollectionChanged;
            API.Database.Tags.ItemUpdated += Tags_ItemUpdated;

            API.Database.Games.ItemCollectionChanged += Games_ItemCollectionChanged;

            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

            TopPanelItem_KidModeIsEnabled = new TopPanelItem()
            {
                Title = "Kid Mode is enabled",
                Activated = DisableKidModeAskingForPass,
                Icon = Path.Combine(Path.GetDirectoryName(assemblyLocation), "kid-mode-enabled.png")
            };

            TopPanelItem_KidModeIsDisabled = new TopPanelItem()
            {
                Title = "Kid Mode is disabled",
                Activated = EnableKidModeAskingForPass,
                Icon = Path.Combine(Path.GetDirectoryName(assemblyLocation), "kid-mode-disabled.png")
            };

            IsKidModeEnabled = File.Exists(_kidModeFilePath);

            if (!IsKidModeEnabled && Environment.GetCommandLineArgs().Contains("--kidmode"))
                EnableKidMode("1111");

            // Fail-safe
            if (IsKidModeEnabled && Environment.GetCommandLineArgs().Contains("--disable-kidmode"))
                DisableKidMode();
        }

        public override void Dispose()
        {
            API.Database.Tags.ItemCollectionChanged -= Tags_ItemCollectionChanged;
            API.Database.Tags.ItemUpdated -= Tags_ItemUpdated;

            base.Dispose();
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            if (IsKidModeEnabled)
                yield break;

            if (args.Games.Any(g => !IsGameKidModeFriendly(g)))
            {
                yield return new GameMenuItem()
                {
                    MenuSection = BaseMenuSection,
                    Description = "Enable for kid mode",
                    Action = (mArgs) => ToggleGameKidModeFriendly(true, mArgs.Games)
                };
            }

            if (args.Games.Any(g => IsGameKidModeFriendly(g)))
            {
                yield return new GameMenuItem()
                {
                    MenuSection = BaseMenuSection,
                    Description = "Disable for kid mode",
                    Action = (mArgs) => ToggleGameKidModeFriendly(false, mArgs.Games)
                };
            }
        }

        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return TopPanelItem_KidModeIsEnabled;
            yield return TopPanelItem_KidModeIsDisabled;
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            EnsureKidModeTags();

            ReadConfig();
            ApplyConfig();

            CompositionTarget.Rendering += OnFrameRendered;
        }

        private void Tags_ItemCollectionChanged(object sender, ItemCollectionChangedEventArgs<Tag> e)
            => EnsureKidModeTags();

        private void Tags_ItemUpdated(object sender, ItemUpdatedEventArgs<Tag> e)
            => EnsureKidModeTags();

        #region Ensure KidMode Enforced
        private void Games_ItemCollectionChanged(object sender, ItemCollectionChangedEventArgs<Game> e)
        {
            if (!IsKidModeEnabled)
                return;

            if (e.RemovedItems.Count > 0)
            {
                API.Database.Games.Add(e.RemovedItems);

                API.Dialogs.ShowMessage("Kid mode is enabled, games cannot be deleted", "Game deletion not allowed", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }

        public override void OnGameSelected(OnGameSelectedEventArgs args)
        {
            if (!IsKidModeEnabled)
                return;

            if (args.NewValue == null)
                return;

            var changed = false;

            for (int i = args.NewValue.Count - 1; i >= 0; i--)
            {
                var game = args.NewValue[i];

                if (!IsGameKidModeFriendly(game))
                {
                    args.NewValue.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                API.MainView.SelectGames(args.NewValue.Select(g => g.Id));
            }
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (!IsKidModeEnabled)
                return;

            if (IsGameKidModeFriendly(args.Game))
                return;

            args.CancelStartup = true;
        }

        private void OnFrameRendered(object sender, EventArgs e)
        {
            if (!IsKidModeEnabled)
                return;

            // Ensure filter is set to kidmode only
            var filterSettings = API.MainView.GetCurrentFilterSettings();
            var updated = false;

            if (filterSettings.Tag == null)
            {
                updated = true;
                filterSettings.Tag = new IdItemFilterItemProperties(Tag_KidModeAnyId);
            }
            else if (filterSettings.Tag.Ids == null)
            {
                updated = true;
                filterSettings.Tag.Ids = new List<Guid> { Tag_KidModeAnyId };
            }
            else if (!filterSettings.Tag.Ids.Contains(Tag_KidModeAnyId))
            {
                updated = true;
                filterSettings.Tag.Ids.Add(Tag_KidModeAnyId);
            }

            if (!filterSettings.UseAndFilteringStyle)
            {
                // Ensure that we can't bypass by just picking another tag
                updated = true;
                filterSettings.UseAndFilteringStyle = true;
            }

            if (updated)
            {
                FilterPreset filterPreset = new FilterPreset()
                {
                    Id = Guid.Empty,
                    Name = "Kid Mode",
                    Settings = filterSettings,
                    GroupingOrder = API.MainView.Grouping,
                    ShowInFullscreeQuickSelection = false,
                    SortingOrder = API.MainView.SortOrder,
                    SortingOrderDirection = API.MainView.SortOrderDirection
                };

                API.MainView.ApplyFilterPreset(filterPreset);
            }

            // And now check if there's any windows open that shouldn't be and close them
            for (int i = Application.Current.Windows.Count - 1; i >= 0; i--)
            {
                var window = Application.Current.Windows[i];

                if (window == Application.Current.MainWindow)
                    continue;

                if (!window.IsVisible)
                    continue;

                var windowType = window.GetType();

#if DEBUG
                if (windowType.FullName.IndexOf(".VisualStudio.", StringComparison.OrdinalIgnoreCase) != -1)
                    continue;
#endif

                if (AllowedWindowTypes.Contains(windowType.FullName))
                    continue;


                if (!window.IsInitialized || !window.IsLoaded)
                    continue;

                window.Visibility = Visibility.Hidden;
                window.Hide();

                // Do it on a thread since we need to wait for it to "activate"
                new System.Threading.Thread(() =>
                {
                    System.Threading.Thread.Sleep(100);

                    try
                    {
                        window.Dispatcher.Invoke(() =>
                        {
                            try { window.DialogResult = false; } catch (Exception) { }
                            try { window.Close(); } catch (Exception) { }
                        });
                    }
                    catch (Exception) { }
                }).Start();
            }
        }

        public static HashSet<string> AllowedWindowTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Playnite.DesktopApp.Windows.MainWindow",
            "Playnite.DesktopApp.Windows.MessageBoxWindow",
            "Playnite.DesktopApp.Windows.ProgressWindow",
            "Playnite.DesktopApp.Windows.RandomGameSelectWindow",
            "Playnite.DesktopApp.Windows.CrashHandlerWindow",
            "Playnite.FullscreenApp.Windows.MainWindow",
            "Playnite.FullscreenApp.Windows.MainMenuWindow",
            "Playnite.FullscreenApp.Windows.MessageBoxWindow",
            "Playnite.FullscreenApp.Windows.ProgressWindow",
            "Playnite.FullscreenApp.Windows.RandomGameSelectWindow",
            "Playnite.FullscreenApp.Windows.CrashWindow",

        };
        #endregion

        #region Kid Mode Enable / Disable stuff
        private static readonly string _kidModeFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "playnite-kidmode");

        private bool _kidModeEnabled;

        public bool IsKidModeEnabled
        {
            get => _kidModeEnabled;
            set
            {
                _kidModeEnabled = value;

                TopPanelItem_KidModeIsEnabled.Visible = _kidModeEnabled;
                TopPanelItem_KidModeIsDisabled.Visible = !_kidModeEnabled;
            }
        }

        public void EnableKidMode(string pass)
        {
            // SHA512 Hash the pass then store it
            var hash = System.Security.Cryptography.SHA512.Create().ComputeHash(Encoding.UTF8.GetBytes(pass));
            File.WriteAllBytes(_kidModeFilePath, hash);

            IsKidModeEnabled = true;
        }

        public void DisableKidMode()
        {
            if (File.Exists(_kidModeFilePath))
                File.Delete(_kidModeFilePath);

            IsKidModeEnabled = false;

            var filterPreset = API.Database.FilterPresets.First();
            API.MainView.ApplyFilterPreset(filterPreset);
        }

        public void EnableKidModeAskingForPass()
        {
            var result = API.Dialogs.SelectString("Enter the passcode that will be used to disable kid mode in the future", "Enable kid mode", "1111");

            if (!result.Result)
                return;

            EnableKidMode(result.SelectedString);
        }

        public void DisableKidModeAskingForPass()
        {
            byte[] contents = new byte[0];

            if (File.Exists(_kidModeFilePath))
                contents = File.ReadAllBytes(_kidModeFilePath);

            var result = API.Dialogs.SelectString("Enter the passcode to disable kid mode", "Disable kid mode", "");

            if (!result.Result)
                return;

            var hash = System.Security.Cryptography.SHA512.Create().ComputeHash(Encoding.UTF8.GetBytes(result.SelectedString));

            if (contents.SequenceEqual(hash))
            {
                DisableKidMode();
            }
            else
            {
                API.Dialogs.ShowMessage("Incorrect passcode entered");
            }
        }
        #endregion

        #region Tagging
        public void ToggleGameKidModeFriendly(bool setIsKidModeFriendly, IReadOnlyCollection<Game> games)
        {
            if (IsKidModeEnabled)
                return;

            EnsureKidModeTags();

            using (API.Database.BufferedUpdate())
            {
                foreach (var game in games)
                {
                    if (game.TagIds == null)
                        game.TagIds = new List<Guid>();

                    bool tagAlreadyExists = game.TagIds.Contains(Tag_KidModeAnyId);
                    bool gameUpdated = false;

                    if (setIsKidModeFriendly)
                    {
                        if (!tagAlreadyExists)
                        {
                            game.TagIds.Add(Tag_KidModeAnyId);
                            gameUpdated = true;
                        }

                        GameToKidMode[game.Id] = Tag_KidModeAny;
                    }
                    else
                    {
                        if (tagAlreadyExists)
                        {
                            game.TagIds.Remove(Tag_KidModeAnyId);
                            gameUpdated = true;
                        }

                        GameToKidMode.Remove(game.Id);
                    }

                    if (gameUpdated)
                        API.Database.Games.Update(game);
                }
            }

            WriteConfig();
        }

        public bool IsGameKidModeFriendly(Game game)
        {
            return GameToKidMode.TryGetValue(game.Id, out var kidMode) &&
                kidMode == Tag_KidModeAny;
        }

        public void EnsureKidModeTags()
        {
            if (!API.TryGetTagId(Tag_KidModeAny, out Tag_KidModeAnyId))
                Tag_KidModeAnyId = API.Database.Tags.Add(Tag_KidModeAny).Id;
        }
        #endregion

        #region Config
        private Dictionary<Guid, string> GameToKidMode = new Dictionary<Guid, string>();

        private void ReadConfig()
        {
            GameToKidMode = PlayniteApiUtilities.DeserializeFromFile<Dictionary<Guid, string>>(KidModeConfigFilePath) ??
                new Dictionary<Guid, string>();
        }

        private void WriteConfig()
        {
            PlayniteApiUtilities.SerializeToFile(KidModeConfigFilePath, GameToKidMode);
        }

        private void ApplyConfig()
        {
            if (GameToKidMode.Count > 0)
            {
                foreach (var game in API.Database.Games)
                {
                    if (GameToKidMode.TryGetValue(game.Id, out var kidMode))
                    {
                        if (game.TagIds == null)
                            game.TagIds = new List<Guid>();

                        if (kidMode == Tag_KidModeAny && !game.TagIds.Contains(Tag_KidModeAnyId))
                            game.TagIds.Add(Tag_KidModeAnyId);
                    }
                    else
                    {
                        if (game.TagIds?.Contains(Tag_KidModeAnyId) == true)
                            game.TagIds?.Remove(Tag_KidModeAnyId);
                    }
                }
            }
        }
        #endregion

    }
}