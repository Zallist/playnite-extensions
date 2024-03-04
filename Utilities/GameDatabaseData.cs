using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PlayniteUtilities
{
    public static class PlatformDatabase
    {
        public enum PlatformCategory
        {
            Console,
            LegacyHandheld,
            ModernHandheld
        }

        public class PlatformInformation
        {
            public readonly string[] Names;
            public readonly DateTime ReleaseDate;
            public readonly PlatformCategory Category;
            public int OrderNumber;

            public PlatformInformation(
                string[] names,
                DateTime releaseDate,
                PlatformCategory category = PlatformCategory.Console)
            {
                Names = names;
                ReleaseDate = releaseDate;
                Category = category;
            }
        }

        public static Dictionary<string, PlatformInformation> PlatformInformationDictionary = new Dictionary<string, PlatformInformation>(StringComparer.OrdinalIgnoreCase);

        static PlatformDatabase()
        {
            var allPlatformsWithHandpickedOrder = new List<PlatformInformation>()
            {
                new PlatformInformation(new string[] { "Nintendo Game Boy", "Game Boy", "GB" }, DateTime.Parse("1989-01-01"), PlatformCategory.LegacyHandheld),
                new PlatformInformation(new string[] { "Sega Game Gear", "Game Gear" }, DateTime.Parse("1991-01-01"), PlatformCategory.LegacyHandheld),
                new PlatformInformation(new string[] { "Virtual Boy" }, DateTime.Parse("1995-07-21"), PlatformCategory.LegacyHandheld),
                new PlatformInformation(new string[] { "Nintendo Game Boy Color", "Game Boy Color", "GBC" }, DateTime.Parse("1998-01-01"), PlatformCategory.LegacyHandheld),

                new PlatformInformation(new string[] { "Atari 2600" }, DateTime.Parse("1977-09-11")),
                new PlatformInformation(new string[] { "Atari 5200" }, DateTime.Parse("1982-11-01")),
                new PlatformInformation(new string[] { "Sega SG-1000", "SG1000" }, DateTime.Parse("1983-07-15")),
                new PlatformInformation(new string[] { "Nintendo Entertainment System", "NES", "Famicom", "Family Computer" }, DateTime.Parse("1983-07-15")),
                new PlatformInformation(new string[] { "Master System", "Sega Mark III", "Sega Mark 3" }, DateTime.Parse("1985-10-20")),
                new PlatformInformation(new string[] { "Nintendo Family Computer Disk System", "Family Computer Disk System", "FDS", "Famicom Disk System" }, DateTime.Parse("1986-02-21")),
                new PlatformInformation(new string[] { "Atari 7800" }, DateTime.Parse("1986-05-01")),
                new PlatformInformation(new string[] { "Sega Genesis", "Sega Mega Drive", "Sega Genesis/Megadrive" }, DateTime.Parse("1988-10-29")),

                new PlatformInformation(new string[] { "Nintendo Game Boy Advance", "Game Boy Advance", "GBA" }, DateTime.Parse("2001-01-01"), PlatformCategory.LegacyHandheld),

                new PlatformInformation(new string[] { "Super Nintendo Entertainment System", "Super Famicom", "Nintendo SNES" }, DateTime.Parse("1990-11-21")),
                new PlatformInformation(new string[] { "Sega CD", "Mega CD" }, DateTime.Parse("1991-12-12")),
                new PlatformInformation(new string[] { "Sega Pico" }, DateTime.Parse("1993-06-26")),
                new PlatformInformation(new string[] { "Satellaview", "Satella" }, DateTime.Parse("1995-04-23")),
                new PlatformInformation(new string[] { "Atari Jaguar" }, DateTime.Parse("1993-11-23")),
                new PlatformInformation(new string[] { "Sega 32X" }, DateTime.Parse("1994-11-21")),
                new PlatformInformation(new string[] { "Sega Saturn" }, DateTime.Parse("1994-11-22")),
                new PlatformInformation(new string[] { "Atari Jaguar CD" }, DateTime.Parse("1995-09-21")),
                new PlatformInformation(new string[] { "Nintendo 64", "N64" }, DateTime.Parse("1996-06-23")),

                new PlatformInformation(new string[] { "Sony PlayStation", "PSX" }, DateTime.Parse("1994-12-03")),

                new PlatformInformation(new string[] { "Nintendo 64DD", "N64DD" }, DateTime.Parse("1999-12-01")),

                new PlatformInformation(new string[] { "Nintendo DS", "DS", "NDS" }, DateTime.Parse("2004-01-01"), PlatformCategory.ModernHandheld),
                new PlatformInformation(new string[] { "Sony Playstation Portable", "PlayStation Portable", "Sony PSP", "PSP", "Sony PSP Mini", "Sony PSP Minis" }, DateTime.Parse("2005-01-01"), PlatformCategory.ModernHandheld),

                new PlatformInformation(new string[] { "Sega Dreamcast" }, DateTime.Parse("1998-11-27")),

                new PlatformInformation(new string[] { "Nintendo 3DS", "3DS" }, DateTime.Parse("2011-02-26"), PlatformCategory.ModernHandheld),

                new PlatformInformation(new string[] { "Sony Playstation 2", "PlayStation 2", "PS2" }, DateTime.Parse("2000-03-04")),
                new PlatformInformation(new string[] { "Nintendo Gamecube", "Gamecube", "GC" }, DateTime.Parse("2001-09-14")),
                new PlatformInformation(new string[] { "Microsoft Xbox" }, DateTime.Parse("2001-11-15")),

                new PlatformInformation(new string[] { "Sony PlayStation Vita", "PlayStation Vita", "PSV", "PS Vita" }, DateTime.Parse("2012-01-01"), PlatformCategory.ModernHandheld),

                new PlatformInformation(new string[] { "Nintendo Wii" }, DateTime.Parse("2006-11-19")),

                new PlatformInformation(new string[] { "Microsoft Xbox 360", "Xbox 360", "X360" }, DateTime.Parse("2005-11-22")),
                new PlatformInformation(new string[] { "Sony Playstation 3", "PlayStation 3", "PS3" }, DateTime.Parse("2006-11-11")),

                new PlatformInformation(new string[] { "Nintendo Wii U", "Wii U" }, DateTime.Parse("2012-11-18")),
                new PlatformInformation(new string[] { "Sony Playstation 4", "PlayStation 4", "PS4" }, DateTime.Parse("2013-11-15")),
                new PlatformInformation(new string[] { "Microsoft Xbox One", "Xbox One", "Xbone", "Xbox One X" }, DateTime.Parse("2013-11-22")),
                new PlatformInformation(new string[] { "Nintendo Switch" }, DateTime.Parse("2017-03-03")),
                new PlatformInformation(new string[] { "Microsoft Xbox Series X", "Microsoft Xbox Series S", "Xbox Series X", "Xbox Series S", "Xbox Series" }, DateTime.Parse("2020-11-10")),
                new PlatformInformation(new string[] { "Sony Playstation 5", "PlayStation 5", "PS5" }, DateTime.Parse("2020-11-12")),
            };

            for (int index = 0; index < allPlatformsWithHandpickedOrder.Count; index++)
            {
                allPlatformsWithHandpickedOrder[index].OrderNumber = index;

                var keys = allPlatformsWithHandpickedOrder[index].Names;

                foreach (var key in keys)
                {
                    HashSet<string> keysToCreate = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        key,
                        key.Replace(" ", "")
                    };

                    foreach (Match wordMatch in Regex.Matches(key, @"\b(?<word>\S+)\b"))
                        keysToCreate.Add(wordMatch.Groups["word"].Value);

                    foreach (var keyToCreate in keysToCreate)
                    {
                        PlatformInformationDictionary[keyToCreate] = allPlatformsWithHandpickedOrder[index];
                    }
                }
            }
        }

        public static bool TryGetPlatformInformation(string platform, out PlatformInformation info)
        {
            if (PlatformInformationDictionary.TryGetValue(platform, out info))
                return true;

            return false;
        }

        public static DateTime GetReleaseDate(string platform)
        {
            if (TryGetPlatformInformation(platform, out var info))
                return info.ReleaseDate;
            return DateTime.MinValue;
        }

        public static int GetHandpickedOrder(string platform)
        {
            if (TryGetPlatformInformation(platform, out var info))
                return info.OrderNumber;
            return -1;
        }

        public static PlatformCategory GetCategory(string platform)
        {
            if (TryGetPlatformInformation(platform, out var info))
                return info.Category;
            return PlatformCategory.Console;
        }
    }
}