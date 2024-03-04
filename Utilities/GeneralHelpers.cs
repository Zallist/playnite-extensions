using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PlayniteUtilities
{
    public static class GeneralHelpers
    {
        public static T[] GetEnumValues<T>()
            where T : Enum
        {
            return (T[])Enum.GetValues(typeof(T));
        }

        public static T[] GetEnumFlagValues<T>()
            where T : Enum
        {
            // Get all the enum values which are a flag (equal to 1 << x)
            return ((T[])Enum.GetValues(typeof(T))).Where(x => (Convert.ToInt64(x) & (Convert.ToInt64(x) - 1)) == 0).ToArray();
        }

        public static string GetHumanReadableByteSize(long bytes)
        {
            var sizes = new string[] { "B", "KB", "MB", "GB", "TB" };

            var index = 0;
            var dblByte = (double)bytes;

            while (dblByte >= 1024 && index < sizes.Length - 1)
            {
                dblByte /= 1024;
                index++;
            }

            return string.Format("{0:0.##} {1}", dblByte, sizes[index]);
        }

        private static readonly char[] PathSeparators = new char[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar };
        public static string GetLongestPathCommonPrefix(IEnumerable<string> paths)
        {
            var count = paths.Count();

            if (paths == null || count == 0)
                return null;

            if (count == 1)
                return paths.First();

            var allSplittedPaths = paths.Select(p => p.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries)).ToList();

            var minPathLength = allSplittedPaths.Min(a => a.Length);
            var pathIndex = 0;
            for (pathIndex = 0; pathIndex < minPathLength; pathIndex++)
            {
                var reference = allSplittedPaths[0][pathIndex];

                if (allSplittedPaths.Any(a => !StringComparer.Ordinal.Equals(a[pathIndex], reference)))
                    break;
            }

            // Can't use Path.Combine because of the volume separator not working right
            return string.Join($"{System.IO.Path.DirectorySeparatorChar}", allSplittedPaths[0].Take(pathIndex));
        }

        public static void OpenBrowser(string url)
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                Process.Start("xdg-open", url);
            }
            else if (Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }

        public static T GetUniqueId<T>(string key)
            where T : struct, IComparable
        {
            return IdGenerator<T>.GetNextId(key);
        }


        private static readonly System.Text.RegularExpressions.Regex HumanizeVariableRegexSplitter = 
            new System.Text.RegularExpressions.Regex(@"
(?= \p{Lu}\p{Ll} )
|
(?<= \p{Ll} ) (?= \p{Lu} )
|
(?= \p{P} ) \p{P} (?<= \p{P} )", 
                System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace);
        public static string HumanizeVariable(string variable)
        {
            // This is a PascalCase and camelCase and snake_case and kebab-case
            return string.Join(" ", HumanizeVariableRegexSplitter.Split(variable).Select(x => x.Trim())).Trim();
        }


        private static readonly Regex BracketedChunk = new Regex(@"(?: \( ([^)]+) \) | \[ ([^]]+) \] | \{ ([^}]+) \} )",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
        private static readonly Regex PunctuationToRemove = new Regex(@"\p{P}|\p{So}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
        private static readonly Regex PunctuationToTranspose = new Regex(@"\p{S}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
        private static readonly Regex MultipleSpaces = new Regex(@"\s{2,}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
        private static readonly Regex SuperfluousWords = new Regex(@"\b(?:and|the|an|a|der|das|die)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
        private static readonly Regex NumbersToRomanNumerals = new Regex(@"\b\d{1,2}\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        static readonly IEnumerable<KeyValuePair<int, string>> RomanNumeralMap = new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(1000, "M"),
            new KeyValuePair<int, string>(900, "CM"),
            new KeyValuePair<int, string>(500, "D"),
            new KeyValuePair<int, string>(400, "CD"),
            new KeyValuePair<int, string>(100, "C"),
            new KeyValuePair<int, string>(90, "XC"),
            new KeyValuePair<int, string>(50, "L"),
            new KeyValuePair<int, string>(40, "XL"),
            new KeyValuePair<int, string>(10, "X"),
            new KeyValuePair<int, string>(9, "IX"),
            new KeyValuePair<int, string>(5, "V"),
            new KeyValuePair<int, string>(4, "IV"),
            new KeyValuePair<int, string>(1, "I")
        };

        public static string ToRomanNumeral(int number)
        {
            var retVal = new StringBuilder(5);

            foreach (var kvp in RomanNumeralMap)
            {
                while (number >= kvp.Key)
                {
                    number -= kvp.Key;
                    retVal.Append(kvp.Value);
                }
            }

            return retVal.ToString();
        }

        public static string RomanifyNumber(Match match)
        {
            if (!int.TryParse(match.Value, out int n))
                return match.Value;

            return ToRomanNumeral(n);
        }

        public static string SanitizeName(string n)
        {
            n = BracketedChunk.Replace(n, "");

            var superfluousWordsRemoved = SuperfluousWords.Replace(n.Replace("&", "and"), "");
            if (superfluousWordsRemoved.Length > 0)
                n = superfluousWordsRemoved;

            n = PunctuationToRemove.Replace(n, "");
            n = PunctuationToTranspose.Replace(n, "+");
            n = MultipleSpaces.Replace(n, " ");
            n = NumbersToRomanNumerals.Replace(n, RomanifyNumber);
            n = n.Trim();

            return n;
        }

        public static string HumanizeEnum<T>(T value)
            where T : Enum
        {
            var name = Enum.GetName(typeof(T), value);

            if (name == null)
                return "";

            return HumanizeVariable(name);
        }
    }

    public static class IdGenerator<T>
        where T : struct, IComparable
    {
        public static ConcurrentDictionary<string, T> KeyToCurrentValue = new ConcurrentDictionary<string, T>();

        public static T GetNextId(string key)
        {
            return KeyToCurrentValue.AddOrUpdate(key, default(T), (k, v) =>
            {
                object @object;

                unchecked
                {
                    switch (v)
                    {
                        case Byte @byte:
                            @object = ++@byte;
                            break;
                        case Int16 @short:
                            @object = ++@short;
                            break;
                        case Int32 @int:
                            @object = ++@int;
                            break;
                        case Int64 @long:
                            @object = ++@long;
                            break;
                        case UInt16 @ushort:
                            @object = ++@ushort;
                            break;
                        case UInt32 @uint:
                            @object = ++@uint;
                            break;
                        case UInt64 @ulong:
                            @object = ++@ulong;
                            break;
                        case SByte @sbyte:
                            @object = ++@sbyte;
                            break;
                        case Single @float:
                            @object = ++@float;
                            break;
                        case Double @double:
                            @object = ++@double;
                            break;
                        case Decimal @decimal:
                            @object = ++@decimal;
                            break;
                        case Char @char:
                            @object = ++@char;
                            break;
                        case DateTime @date:
                            @object = @date.AddTicks(1);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }

                return (T)@object;
            });
        }
    }

    public static class FileUtility
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CopyFileEx(string lpExistingFileName, string lpNewFileName,
           CopyProgressRoutine lpProgressRoutine, IntPtr lpData, ref Int32 pbCancel,
           CopyFileFlags dwCopyFlags);

        delegate CopyProgressResult CopyProgressRoutine(
            long TotalFileSize,
            long TotalBytesTransferred,
            long StreamSize,
            long StreamBytesTransferred,
            uint dwStreamNumber,
            CopyProgressCallbackReason dwCallbackReason,
            IntPtr hSourceFile,
            IntPtr hDestinationFile,
            IntPtr lpData);

        public delegate void ReportCopyProgress(long total, long transferred);

        enum CopyProgressResult : uint
        {
            PROGRESS_CONTINUE = 0,
            PROGRESS_CANCEL = 1,
            PROGRESS_STOP = 2,
            PROGRESS_QUIET = 3
        }

        enum CopyProgressCallbackReason : uint
        {
            CALLBACK_CHUNK_FINISHED = 0x00000000,
            CALLBACK_STREAM_SWITCH = 0x00000001
        }

        [Flags]
        public enum CopyFileFlags : uint
        {
            NONE = 0x00000000,
            /// <summary>
            /// The copy operation fails immediately if the target file already exists. 
            /// </summary>
            COPY_FILE_FAIL_IF_EXISTS = 0x00000001,
            /// <summary>
            /// Progress of the copy is tracked in the target file in case the copy fails. 
            /// The failed copy can be restarted at a later time by specifying the same values for lpExistingFileName and lpNewFileName as those used in the call that failed. 
            /// This can significantly slow down the copy operation as the new file may be flushed multiple times during the copy operation. 
            /// </summary>
            COPY_FILE_RESTARTABLE = 0x00000002,
            /// <summary>
            /// The file is copied and the original file is opened for write access. 
            /// </summary>
            COPY_FILE_OPEN_SOURCE_FOR_WRITE = 0x00000004,
            /// <summary>
            /// An attempt to copy an encrypted file will succeed even if the destination copy cannot be encrypted. 
            /// </summary>
            COPY_FILE_ALLOW_DECRYPTED_DESTINATION = 0x00000008,
            /// <summary>
            /// If the source file is a symbolic link, the destination file is also a symbolic link pointing to the same file that the source symbolic link is pointing to. 
            /// </summary>
            COPY_FILE_COPY_SYMLINK = 0x00000800,
            /// <summary>
            /// The copy operation is performed using unbuffered I/O, bypassing system I/O cache resources. Recommended for very large file transfers. 
            /// </summary>
            COPY_FILE_NO_BUFFERING = 0x00001000,
            /// <summary>
            /// Request the underlying transfer channel compress the data during the copy operation. 
            /// The request may not be supported for all mediums, in which case it is ignored. 
            /// The compression attributes and parameters (computational complexity, memory usage) are not configurable through this API, and are subject to change between different OS releases.
            /// <br />This flag was introduced in Windows 10, version 1903 and Windows Server 2022. 
            /// On Windows 10, the flag is supported for files residing on SMB shares, where the negotiated SMB protocol version is SMB v3.1.1 or greater.
            /// </summary>
            COPY_FILE_REQUEST_COMPRESSED_TRAFFIC = 0x10000000
        }

        public static void CopyFileExtended(string oldFile, string newFile, CopyFileFlags copyFileFlags = CopyFileFlags.NONE, ReportCopyProgress reportCopyProgress = null, CancellationToken cancellationToken = default)
        {
            int pbCancel = 0;

            CopyFileEx(oldFile, newFile, new CopyProgressRoutine((long total, long transferred, long streamSize, long StreamByteTrans, uint dwStreamNumber, CopyProgressCallbackReason reason, IntPtr hSourceFile, IntPtr hDestinationFile, IntPtr lpData) =>
            {
                reportCopyProgress?.Invoke(total, transferred);

                if (cancellationToken.IsCancellationRequested)
                    return CopyProgressResult.PROGRESS_CANCEL;

                return CopyProgressResult.PROGRESS_CONTINUE;
            }), IntPtr.Zero, ref pbCancel, copyFileFlags);
        }
    }
}
