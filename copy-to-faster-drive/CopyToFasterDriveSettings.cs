using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MetadataHelper;
using Playnite.SDK;
using Playnite.SDK.Data;
using PlayniteUtilities;

namespace CopyToFasterDrive
{
    public class CopyToFasterDriveSettings : ObservableObject
    {
        private bool enabled;
        private string fasterDirectoryPath;
        private long fasterDirectoryMaxSizeInBytes;

        public bool Enabled { get => enabled; set => SetValue(ref enabled, value); }
        public string FasterDirectoryPath { get => fasterDirectoryPath; set => SetValue(ref fasterDirectoryPath, value); }
        public long FasterDirectoryMaxSizeInBytes { get => fasterDirectoryMaxSizeInBytes; set => SetValue(ref fasterDirectoryMaxSizeInBytes, value); }

        public CopyToFasterDriveSettings()
        {
            enabled = true;

            fasterDirectoryPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                "Temp\\Playnite_Faster_Cache");

            // Set default size to 20GB
            fasterDirectoryMaxSizeInBytes = 20L * 1024L * 1024L * 1024L;

            this.PropertyChanged += (s, e) => { if (e.PropertyName != nameof(FolderSizeHint)) OnPropertyChanged(nameof(FolderSizeHint)); };
        }

        [DontSerialize]
        public string FolderSizeHint
        {
            get
            {
                if (!this.Enabled)
                    return "This extension is disabled.";

                var humanReadableMaxSize = GeneralHelpers.GetHumanReadableByteSize(FasterDirectoryMaxSizeInBytes);

                return $"The directory located at {FasterDirectoryPath} will never have more than {humanReadableMaxSize}. " +
                    $"Files will be deleted to make space when a game is launched.";
            }
        }
    }


    public class CopyToFasterDriveSettingsViewModel : ObservableObject, ISettings
    {
        public static string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "PlayniteExtensions\\CopyToFasterDriveSettings.json");

        private CopyToFasterDriveSettings editingClone { get; set; }

        private CopyToFasterDriveSettings settings;
        public CopyToFasterDriveSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public CopyToFasterDriveSettingsViewModel(CopyToFasterDrivePlugin plugin)
        {
            var savedSettings = PlayniteApiUtilities.DeserializeFromFile<CopyToFasterDriveSettings>(SettingsFilePath);

            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new CopyToFasterDriveSettings();
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
