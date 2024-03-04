using Playnite.SDK;
using Playnite.SDK.Models;
using RomManage.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using PlayniteUtilities;

namespace RomManager
{
    public class ComparableRom
    {
        private const int SHINGLE_SIZE = 3;
        public const float MINIMUM_SIMILARITY = 0.5f;

        private ConcurrentDictionary<Int32, float> _otherComparableRomSimilarity = new ConcurrentDictionary<Int32, float>();
        public ConcurrentDictionary<Int32, float> OtherComparableRomSimilarity => _otherComparableRomSimilarity;

        public readonly Int32 Id = GeneralHelpers.GetUniqueId<Int32>(nameof(ComparableRom));

        public Guid GameId { get; private set; }
        public Game Game { get; private set; }

        public int RomIndex { get; private set; }
        public string RomName { get; private set; }
        public string RomPath { get; private set; }
        public string ExpandedRomPath { get; private set; }

        public string ComparisonText { get; private set; }

        private static readonly Regex BracketedChunk = new Regex(@"(?: \( ([^)]+) \) | \[ ([^]]+) \] | \{ ([^}]+) \} )",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
        private static readonly Regex IsPunctuation = new Regex(@"\p{P}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
        private static readonly Regex MultipleSpaces = new Regex(@"\s{2,}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
        private static readonly Regex SuperfluousWords = new Regex(@"\b(?:and|the|an|a)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        private IDictionary<string, int> _shingles = null;
        private double _shinglesNormalized = double.NaN;

        public IDictionary<string, int> Shingles
        {
            get
            {
                if (_shingles != null)
                    return _shingles;

                _shingles = GetShingles(ComparisonText);
                _shinglesNormalized = Cosine_NormalizeShingles(_shingles);

                return _shingles;
            }
        }

        public double ShinglesNormalized => double.IsNaN(_shinglesNormalized) ? Cosine_NormalizeShingles(Shingles) : _shinglesNormalized;

        public static IEnumerable<ComparableRom> Parse(IPlayniteAPI API, Game game, ComparisonField comparisonBase = ComparisonField.RomFileNameWithoutExtension)
        {
            if (game.Roms == null)
                yield break;

            for (int i = 0; i < game.Roms.Count; i++)
            {
                var rom = game.Roms[i];
                var comp = new ComparableRom
                {
                    GameId = game.Id,
                    Game = game,
                    RomIndex = i,
                    RomName = rom.Name,
                    RomPath = rom.Path,
                    ExpandedRomPath = Path.GetFullPath(API.ExpandGameVariables(game, rom.Path))
                };

                switch (comparisonBase)
                {
                    case ComparisonField.RomFileNameWithoutExtension:
                    default:
                        comp.ComparisonText = Path.GetFileNameWithoutExtension(comp.ExpandedRomPath);
                        break;
                    case ComparisonField.GameName:
                        comp.ComparisonText = game.Name;
                        break;
                    case ComparisonField.RomFullPath:
                        comp.ComparisonText = comp.ExpandedRomPath;
                        break;
                }

                comp.ComparisonText = GeneralHelpers.SanitizeName(comp.ComparisonText)
                    .ToUpper();

                yield return comp;
            }
        }

        public static Dictionary<string, int> GetShingles(string s)
        {
            var shingles = new Dictionary<string, int>();

            if (s.Length < SHINGLE_SIZE)
                s = s.PadRight(SHINGLE_SIZE, ' ');

            for (int i = 0; i < (s.Length - SHINGLE_SIZE + 1); i++)
            {
                var shingle = s.Substring(i, SHINGLE_SIZE);

                if (shingles.TryGetValue(shingle, out var old))
                {
                    shingles[shingle] = old + 1;
                }
                else
                {
                    shingles[shingle] = 1;
                }
            }

            return shingles;
        }

        private static double Cosine_NormalizeShingles(IDictionary<string, int> profile)
        {
            double agg = 0;

            foreach (var v in profile.Values)
            {
                agg += 1.0 * v * v;
            }

            return Math.Sqrt(agg);
        }

        private static double Cosine_DotProductShingles(IDictionary<string, int> profile1,
            IDictionary<string, int> profile2)
        {
            // Loop over the smallest map
            var small_profile = profile2;
            var large_profile = profile1;

            if (profile1.Count < profile2.Count)
            {
                small_profile = profile1;
                large_profile = profile2;
            }

            double agg = 0;
            foreach (var entry in small_profile)
            {
                if (!large_profile.TryGetValue(entry.Key, out var i)) continue;

                agg += 1.0 * entry.Value * i;
            }

            return agg;
        }

        private readonly Regex IsRomForSameGame = new Regex(@"(?: \p{Ps} \s* dis[ck] | \b \d+ \p{Pd} \d+ \b .* \.bs$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        public void CompareTo(ComparableRom other)
        {
            if (other == this || this._otherComparableRomSimilarity.ContainsKey(other.Id))
                return;

            float similarity;

            if (this.GameId == other.GameId &&
                IsRomForSameGame.IsMatch(this.RomPath) &&
                IsRomForSameGame.IsMatch(other.RomPath))
            {
                // They should be different discs of the same copy of the game
                similarity = float.NaN;
            }
            else
            {
                similarity = (float)(Cosine_DotProductShingles(this.Shingles, other.Shingles) /
                    (this.ShinglesNormalized * other.ShinglesNormalized));
            }

            if (float.IsNaN(similarity) || similarity >= MINIMUM_SIMILARITY)
            {
                this._otherComparableRomSimilarity[other.Id] = similarity;
                other._otherComparableRomSimilarity[this.Id] = similarity;
            }
        }

        private ComparableRom() { }
    }
}