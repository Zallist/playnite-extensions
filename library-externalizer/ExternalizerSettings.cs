using System.Collections.Generic;
using System.IO;
using LibraryExternalizer;
using Playnite.SDK;
using Playnite.SDK.Data;
using PlayniteUtilities;

namespace CopyToFasterDrive
{
    public class ExternalizerSettings : ObservableObject
    {
        public ExternalizerSettings()
        {
        }
    }

    public class ExternalizerSettingsViewModel : ObservableObject, ISettings
    {
        public static string SettingsFilePath = Path.Combine(
            API.Instance.Paths.ExtensionsDataPath,
            "LibraryExternalizer\\settings.json");

        private ExternalizerSettings editingClone { get; set; }

        private ExternalizerSettings settings;

        public ExternalizerSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public ExternalizerSettingsViewModel(LibraryExternalizerPlugin plugin)
        {
            var savedSettings = PlayniteApiUtilities.DeserializeFromFile<ExternalizerSettings>(SettingsFilePath);

            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new ExternalizerSettings();
            }
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            PlayniteApiUtilities.SerializeToFile(SettingsFilePath, Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}