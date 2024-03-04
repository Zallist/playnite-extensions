using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Playnite.SDK;
using PlayniteUtilities;

namespace RomManager
{
    /// <summary>
    /// Interaction logic for DuplicateRoms.xaml
    /// </summary>
    public partial class DuplicateRoms : UserControl, INotifyPropertyChanged
    {
        public enum ComparisonCategoryType
        {
            OnlyCompareSamePlatform,
            OnlyCompareSamePlatformCategory,
            CompareAllPlatforms
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private readonly IPlayniteAPI API;
        private readonly IReadOnlyList<ComparableRom> GameComparers;

        private float similarityThreshold = 0.99f;
        private ComparisonCategoryType comparisonCategory = ComparisonCategoryType.OnlyCompareSamePlatform;

        private List<DuplicateRomGroup> duplicateRomGroups = new List<DuplicateRomGroup>();
        private List<DuplicateRom> allDuplicateRoms = new List<DuplicateRom>();

        public List<DuplicateRomGroup> DuplicateRomGroups
        {
            get => duplicateRomGroups;
            private set => SetValue(ref duplicateRomGroups, value);
        }

        public List<DuplicateRom> AllDuplicateRoms
        {
            get => allDuplicateRoms;
            private set => SetValue(ref allDuplicateRoms, value);
        }

        public List<Tuple<ComparisonCategoryType, string>> ComparisonCategoryTypes { get; }

        public float SimilarityThreshold { get => similarityThreshold; set => SetValue(ref similarityThreshold, value); }
        public ComparisonCategoryType ComparisonCategory { get => comparisonCategory; set => SetValue(ref comparisonCategory, value); }

        public ICommand RecalculateGroupsCommand { get; set; }
        public ICommand DoDeleteCommand { get; set; }
        public ICommand DoCancelCommand { get; set; }

        private DuplicateRoms()
        {
            this.ComparisonCategoryTypes = GeneralHelpers.GetEnumValues<ComparisonCategoryType>()
                .Select(e => Tuple.Create(e, GeneralHelpers.HumanizeVariable(Enum.GetName(typeof(ComparisonCategoryType), e))))
                .ToList();

            this.RecalculateGroupsCommand = new RelayCommand(RecalculateGroups);
            this.DoDeleteCommand = new RelayCommand(DoDelete);
            this.DoCancelCommand = new RelayCommand(DoCancel);

            this.DataContext = this;
            InitializeComponent();
        }

        public DuplicateRoms(IPlayniteAPI api, IReadOnlyList<ComparableRom> gameComparers)
            : this()
        {
            this.API = api;
            this.GameComparers = gameComparers;

            RecalculateGroups();
        }

        public void DoDelete()
        {
            var toDeleteRoms = AllDuplicateRoms.FindAll(rom => rom.Delete);
            var toDeleteRomsGroupedByGameId = toDeleteRoms
                .GroupBy(rom => rom.ComparableRom.GameId)
                .ToDictionary(g => g.Key, g => g.ToList());

            int romCount = toDeleteRoms.Count;
            int deletedCount = 0;

            var confirm = API.Dialogs.ShowMessage($"Are you sure you want to delete {romCount} roms? \n" +
                $"All the roms will be deleted from the Playnite database before the files themselves are deleted.", 
                "Confirm deletion", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                var started = DateTime.Now;

                var logDirectory = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Playnite\\Logs\\deleted-files\\");

                var deletedFromDatabaseLogPath = System.IO.Path.Combine(
                    logDirectory,
                    $"deleted-from-database-{started:yyyyMMddTHHmmss.FFF}.log");

                var deletedFromDiskLogPath = System.IO.Path.Combine(
                    logDirectory,
                    $"deleted-from-disk-{started:yyyyMMddTHHmmss.FFF}.log");

                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);

                var deletedFromDatabaseResult = API.Dialogs.ActivateGlobalProgress(progress =>
                {
                    using (System.IO.StreamWriter sw = new System.IO.StreamWriter(deletedFromDatabaseLogPath, true))
                    {
                        progress.CurrentProgressValue = deletedCount = 0;
                        progress.ProgressMaxValue = romCount;

                        if (progress.CancelToken.IsCancellationRequested)
                            return;

                        using (API.Database.BufferedUpdate())
                        {
                            foreach (var romGroup in toDeleteRomsGroupedByGameId)
                            {
                                if (progress.CancelToken.IsCancellationRequested)
                                    return;

                                var gameId = romGroup.Key;
                                var game = API.Database.Games.Get(gameId);

                                if (game?.Roms != null)
                                {
                                    foreach (var rom in romGroup.Value)
                                    {
                                        ++deletedCount;
                                        sw.WriteLine(rom.RomPath);
                                    }

                                    for (int i = game.Roms.Count - 1; i >= 0; i--)
                                    {
                                        if (romGroup.Value.Any(rg => StringComparer.OrdinalIgnoreCase.Equals(rg.ComparableRom.RomPath, game.Roms[i].Path)))
                                        {
                                            game.Roms.RemoveAt(i);
                                        }
                                    }

                                    if (game.Roms.Count == 0)
                                    {
                                        // We're deleting all the roms for this game
                                        API.Database.Games.Remove(game);
                                    }
                                    else
                                    {
                                        // We're only deleting a subset of roms for this game
                                        API.Database.Games.Update(game);
                                    }
                                }

                                progress.CurrentProgressValue = deletedCount;
                                progress.Text = $"Deleted {deletedCount} of {romCount} from Playnite database...";
                            }
                        }
                    }
                }, new GlobalProgressOptions("Deleting roms from Playnite database...", true) { IsIndeterminate = false });

                if (deletedFromDatabaseResult.Canceled || deletedFromDatabaseResult.Error != null)
                {
                    API.Dialogs.ShowMessage($"Deleted {deletedCount} roms from Playnite before stopping. \n" +
                        $"The files that were deleted still exist, so you can re-add them by updating your library. \n" +
                        $"Deleted file paths can be viewed at {deletedFromDatabaseLogPath}");
                    return;
                }

                var deletedFromDiskResult = API.Dialogs.ActivateGlobalProgress(progress =>
                {
                    using (System.IO.StreamWriter sw = new System.IO.StreamWriter(deletedFromDiskLogPath, true))
                    {
                        progress.CurrentProgressValue = deletedCount = 0;
                        progress.ProgressMaxValue = romCount;

                        if (progress.CancelToken.IsCancellationRequested)
                            return;

                        foreach (var rom in toDeleteRoms)
                        {
                            if (progress.CancelToken.IsCancellationRequested)
                                return;

                            if (System.IO.File.Exists(rom.RomPath))
                            {
                                sw.WriteLine(rom.RomPath);

                                System.IO.File.Delete(rom.RomPath);
                            }

                            progress.CurrentProgressValue = ++deletedCount;
                            progress.Text = $"Deleted {deletedCount} of {romCount} from disk...";
                        }
                    }
                }, new GlobalProgressOptions("Deleting roms from disk...", true) { IsIndeterminate = false });

                if (deletedFromDiskResult.Canceled || deletedFromDiskResult.Error != null)
                {
                    API.Dialogs.ShowMessage($"Deleted {deletedCount} files from disk before stopping. \n" +
                        $"These files cannot be restored anymore. \n" +
                        $"Deleted file paths can be viewed at {deletedFromDatabaseLogPath}");
                    return;
                }

                API.Dialogs.ShowMessage($"Deleted {deletedCount} files from disk. \n" +
                    $"These files cannot be restored anymore. \n" +
                    $"Deleted file paths can be viewed at {deletedFromDatabaseLogPath}");
            }
            else if (confirm == MessageBoxResult.Cancel)
            {
                return;
            }

            Window.GetWindow(this).Close();
        }

        public void DoCancel()
        {
            Window.GetWindow(this).Close();
        }

        public void RecalculateGroups()
        {
            List<DuplicateRomGroup> duplicateRomGroups = new List<DuplicateRomGroup>();

            API.Dialogs.ActivateGlobalProgress(progress =>
            {
                var gameComparerMap = GameComparers.ToDictionary(g => g.Id, g => g);

                Dictionary<Int32, DuplicateRomGroup> romPlacedIntoGroup = new Dictionary<Int32, DuplicateRomGroup>();

                progress.CurrentProgressValue = 0;
                progress.ProgressMaxValue = GameComparers.Count;

                for (int i = 0, count = GameComparers.Count; i < count; i++)
                {
                    if (progress.CancelToken.IsCancellationRequested)
                        break;

                    var rom = GameComparers[i];
                    DuplicateRom duplicate = null;
                    var hadASimilarity = false;

                    foreach (var other in rom.OtherComparableRomSimilarity)
                    {
                        if (progress.CancelToken.IsCancellationRequested)
                            break;

                        if (ComparisonCategory != ComparisonCategoryType.CompareAllPlatforms)
                        {
                            if (gameComparerMap.TryGetValue(other.Key, out var otherRom))
                            {
                                switch (ComparisonCategory)
                                {
                                    case ComparisonCategoryType.OnlyCompareSamePlatform:
                                        if (!otherRom.Game.PlatformIds.Intersect(rom.Game.PlatformIds).Any())
                                            continue;
                                        break;
                                    case ComparisonCategoryType.OnlyCompareSamePlatformCategory:
                                        if (!GetPlatformCategories(rom.Game.PlatformIds).Intersect(GetPlatformCategories(otherRom.Game.PlatformIds)).Any())
                                            continue;
                                        break;
                                    default:
                                        throw new NotImplementedException();
                                }
                            }
                        }

                        var addToGroup = false;
                        DuplicateRomGroup group = null;

                        if (float.IsNaN(other.Value) || other.Value >= SimilarityThreshold)
                        {
                            hadASimilarity = true;
                            addToGroup = romPlacedIntoGroup.TryGetValue(other.Key, out group);
                        }

                        if (addToGroup && group != null)
                        {
                            if (duplicate != null &&
                                duplicate.Group != null)
                            {
                                if (duplicate.Group != group)
                                {
                                    // We already exist in a group, so just move everything from {new group} to {existing group}
                                    for (int j = group.DuplicateRoms.Count - 1; j >= 0; j--)
                                    {
                                        DuplicateRom d = group.DuplicateRoms[j];

                                        duplicate.Group.AddRom(d);

                                        romPlacedIntoGroup[d.Id] = duplicate.Group;
                                    }
                                }
                            }
                            else
                            {
                                duplicate = new DuplicateRom(rom);
                                group.AddRom(duplicate);
                                romPlacedIntoGroup[duplicate.Id] = group;
                            }

                            if (float.IsNaN(other.Value))
                                duplicate.RomParentId = other.Key;
                        }
                    }

                    if (hadASimilarity && duplicate == null)
                    {
                        var group = romPlacedIntoGroup[rom.Id] = new DuplicateRomGroup();
                        duplicate = new DuplicateRom(rom);
                        group.AddRom(duplicate);
                        duplicateRomGroups.Add(group);
                    }

                    if (i % 100 == 0)
                        progress.CurrentProgressValue = i;
                }

                progress.Text = "Cleaning up...";
                progress.IsIndeterminate = true;

                duplicateRomGroups.RemoveAll(g => g.DuplicateRoms.Where(r => r.IsEnabled).Count() < 2);
                duplicateRomGroups = duplicateRomGroups
                    .OrderByDescending(g => g.DuplicateRoms.Count)
                    .ThenBy(g => g.DuplicateRoms[0].GameName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var group in duplicateRomGroups)
                {
                    group.DuplicateRoms = group.DuplicateRoms
                        .OrderByDescending(rom => GetPlatformOrder(rom.ComparableRom.Game.PlatformIds))
                        .ThenByDescending(rom => rom.ComparableRom.Game.ReleaseYear.GetValueOrDefault(0))
                        .ThenByDescending(rom => GetRomLanguageOrder(rom))
                        .ThenBy(rom => rom.IsEnabled ? 0 : 1)
                        .ThenByDescending(rom => rom.ComparableRom.RomPath, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
            }, new GlobalProgressOptions("Grouping duplicates...", true) { IsIndeterminate = false });

            this.DuplicateRomGroups = duplicateRomGroups;
            this.AllDuplicateRoms = duplicateRomGroups.SelectMany(g => g.DuplicateRoms).ToList();
        }

        private int GetRomLanguageOrder(DuplicateRom rom)
        {
            int score = 0;
            var romFileName = System.IO.Path.GetFileNameWithoutExtension(rom.ComparableRom.ExpandedRomPath);

            if (romFileName.IndexOf("(USA)", StringComparison.OrdinalIgnoreCase) > -1)
                score += 200;
            else if (romFileName.IndexOf("(Europe)", StringComparison.OrdinalIgnoreCase) > -1)
                score += 150;
            else if (romFileName.IndexOf("(World)", StringComparison.OrdinalIgnoreCase) > -1)
                score += 100;

            if (romFileName.IndexOf("(En", StringComparison.OrdinalIgnoreCase) > -1)
                score += 25;

            return score;
        }

        private long GetPlatformOrder(IEnumerable<Guid> platformIds)
        {
            var platforms = new HashSet<string>(
                platformIds.Select(p => API.Database.Platforms.Get(p)?.Name),
                StringComparer.OrdinalIgnoreCase);

            var maxOrder = platforms.Max(p => PlayniteUtilities.PlatformDatabase.GetHandpickedOrder(p));
            return maxOrder;
        }

        private IEnumerable<PlatformDatabase.PlatformCategory> GetPlatformCategories(IEnumerable<Guid> platformIds)
        {
            var platforms = new HashSet<string>(
                platformIds.Select(p => API.Database.Platforms.Get(p)?.Name),
                StringComparer.OrdinalIgnoreCase);

            foreach (var p in platforms)
            {
                yield return PlayniteUtilities.PlatformDatabase.GetCategory(p);
            }
        }

        #region INotifyPropertyChanged
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected void SetValue<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
        {
            property = value;
            OnPropertyChanged(propertyName);
        }
        #endregion INotifyPropertyChanged

        public class DuplicateRomGroup
        {
            public List<DuplicateRom> DuplicateRoms { get; set; } = new List<DuplicateRom>();

            public string Name => DuplicateRoms.Count > 0 ? DuplicateRoms[0].RomPath : "N/A";

            public void AddRom(DuplicateRom rom)
            {
                if (rom.Group == this)
                    return;

                if (rom.Group != null)
                {
                    foreach (var d in rom.Group.DuplicateRoms)
                    {
                        d.Similarities.Remove(rom.Id);
                        rom.Similarities.Remove(d.Id);
                    }

                    rom.Group.DuplicateRoms.Remove(rom);
                }

                rom.Group = this;

                foreach (var d in this.DuplicateRoms)
                {
                    if (rom.ComparableRom.OtherComparableRomSimilarity.TryGetValue(d.Id, out var similarity) ||
                        d.ComparableRom.OtherComparableRomSimilarity.TryGetValue(rom.Id, out similarity))
                    {
                        rom.Similarities[d.Id] = d.Similarities[rom.Id] = similarity;
                    }
                }

                rom.Group.DuplicateRoms.Add(rom);
            }

            public string CommonRomPath
            {
                get
                {
                    if (DuplicateRoms.Count < 2)
                        return "";

                    var commonRomPath = GeneralHelpers.GetLongestPathCommonPrefix(
                        DuplicateRoms.Select(rom => System.IO.Path.GetDirectoryName(rom.RomPath)));

                    if (commonRomPath.Length > 0 &&
                        !commonRomPath.EndsWith($"{System.IO.Path.DirectorySeparatorChar}") &&
                        !commonRomPath.EndsWith($"{System.IO.Path.AltDirectorySeparatorChar}"))
                    {
                        commonRomPath += $"{System.IO.Path.DirectorySeparatorChar}";
                    }

                    return commonRomPath;
                }
            }
        }

        public class DuplicateRom : ObservableObject
        {
            private bool delete = false;

            public DuplicateRom(ComparableRom comparableRom, Int32? romParentId = null)
            {
                this.ComparableRom = comparableRom;
                this.RomParentId = romParentId;
            }

            public ComparableRom ComparableRom { get; set; }
            public DuplicateRomGroup Group { get; set; } = null;
            public Int32? RomParentId { get; set; } = null;

            public bool Delete
            {
                get => delete;
                set
                {
                    SetValue(ref delete, value);
                    OnPropertyChanged(nameof(ExclusivelyKeep));

                    if (IsEnabled)
                    {
                        Group.DuplicateRoms
                            .Where(d => d.RomParentId == this.Id)
                            .ForEach(d => d.Delete = delete);
                    }
                }
            }

            public bool? ExclusivelyKeep
            {
                get
                {
                    if (Delete)
                        return false;
                    if (Group.DuplicateRoms.Any(g => g.IsEnabled && g != this && !g.Delete))
                        return null;
                    return true;
                }
                set
                {
                    if (!ExclusivelyKeep.HasValue)
                        value = true;

                    if (value == true)
                    {
                        Group.DuplicateRoms.Where(g => g.IsEnabled)
                            .ForEach(g => g.Delete = g != this);
                    }
                    else if (value == false)
                    {
                        Group.DuplicateRoms.Where(g => g.IsEnabled)
                            .ForEach(g => g.Delete = g == this);
                    }

                    OnPropertyChanged(nameof(ExclusivelyKeep));
                }
            }

            public string GroupName => Group.Name;

            public Int32 Id => ComparableRom.Id;
            public string GameName => ComparableRom.Game.Name;
            public string RomPath => ComparableRom.ExpandedRomPath;
            public string RelativeRomPath => RomPath.Replace(Group.CommonRomPath, "");
            public string Platforms => string.Join(", ", ComparableRom.Game.PlatformIds.Select(p => Playnite.SDK.API.Instance.Database.Platforms.Get(p)?.Name));
            public bool IsEnabled => !RomParentId.HasValue;

            public Dictionary<Int32, float> Similarities = new Dictionary<Int32, float>();
            public float SimilarityAverage => Similarities.Values.Where(s => !float.IsNaN(s)).Average();
            public string SimilarityPercentage => $"{SimilarityAverage:P2}";

            private static void OpenDetailsInPlaynite(DuplicateRom rom)
            {
                Playnite.SDK.API.Instance.MainView.SelectGame(rom.ComparableRom.Game.Id);
                Application.Current.MainWindow.Activate();
            }

            private static void SearchInWebBrowser(string searchTerm)
            {
                GeneralHelpers.OpenBrowser($"https://google.com/search?q={Uri.EscapeDataString(searchTerm)}");
            }

            public List<Tuple<string, ICommand>> MenuOptions
            {
                get
                {
                    var options = new List<Tuple<string, ICommand>>();

                    options.Add(Tuple.Create("View Details In Playnite", new RelayCommand(() => OpenDetailsInPlaynite(this)) as ICommand));

                    string[] searchTerms = new string[]
                    {
                        $"{this.ComparableRom.ComparisonText}",
                        $"{this.ComparableRom.ComparisonText} for the {this.Platforms}",
                        $"{this.GameName}",
                        $"{this.GameName} for the {this.Platforms}",
                        $"{System.IO.Path.GetFileName(this.RomPath)}",
                        $"{System.IO.Path.GetFileName(this.RomPath)} for the {this.Platforms}",
                    };
                    foreach (var st in searchTerms)
                        options.Add(Tuple.Create($"Search: {st}", new RelayCommand(() => SearchInWebBrowser(st)) as ICommand));

                    return options;
                }
            }
        }
    }
}
