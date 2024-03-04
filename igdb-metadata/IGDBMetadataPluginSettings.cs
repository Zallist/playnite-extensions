using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IGDBMetadataPlugin
{
    public class IGDBMetadataPluginSettings : ObservableObject
    {
        [DontSerialize]
        private byte[] clientIdRaw = new byte[0];
        [DontSerialize]
        private byte[] clientSecretRaw = new byte[0];

        [DontSerialize]
        public string ClientId
        {
            get
            {
                if (clientIdRaw.Length == 0)
                    return string.Empty;

                var unprotected = ProtectedData.Unprotect(clientIdRaw, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(unprotected);
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    clientIdRaw = new byte[0];
                    return;
                }

                var unprotected = Encoding.UTF8.GetBytes(value);
                clientIdRaw = ProtectedData.Protect(unprotected, null, DataProtectionScope.CurrentUser);
            }
        }

        [DontSerialize]
        public string ClientSecret
        {
            get
            {
                if (clientSecretRaw.Length == 0)
                    return string.Empty;

                var unprotected = ProtectedData.Unprotect(clientSecretRaw, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(unprotected);
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    clientSecretRaw = new byte[0];
                    return;
                }

                var unprotected = Encoding.UTF8.GetBytes(value);
                clientSecretRaw = ProtectedData.Protect(unprotected, null, DataProtectionScope.CurrentUser);
            }
        }

        public string ClientIdBase64
        {
            get => Convert.ToBase64String(clientIdRaw);
            set => clientIdRaw = Convert.FromBase64String(value);
        }

        public string ClientSecretBase64
        {
            get => Convert.ToBase64String(clientSecretRaw);
            set => clientSecretRaw = Convert.FromBase64String(value);
        }

        public bool IsAuthenticationConfigured => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);
    }

    public class IGDBMetadataPluginSettingsViewModel : ObservableObject, ISettings
    {
        private readonly IGDBMetadataPlugin plugin;
        private IGDBMetadataPluginSettings editingClone { get; set; }

        private IGDBMetadataPluginSettings settings;

        public IGDBMetadataPluginSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public IGDBMetadataPluginSettingsViewModel(IGDBMetadataPlugin plugin)
        {
            // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
            this.plugin = plugin;

            // Load saved settings.
            var savedSettings = plugin.LoadPluginSettings<IGDBMetadataPluginSettings>();

            // LoadPluginSettings returns null if no saved data is available.
            if (savedSettings != null)
            {
                Settings = savedSettings;
            }
            else
            {
                Settings = new IGDBMetadataPluginSettings();
            }
        }

        public void BeginEdit()
        {
            // Code executed when settings view is opened and user starts editing values.
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            // Code executed when user decides to cancel any changes made since BeginEdit was called.
            // This method should revert any changes made to Option1 and Option2.
            Settings = editingClone;
        }

        public void EndEdit()
        {
            // Code executed when user decides to confirm changes made since BeginEdit was called.
            // This method should save settings made to Option1 and Option2.
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            // Code execute when user decides to confirm changes made since BeginEdit was called.
            // Executed before EndEdit is called and EndEdit is not called if false is returned.
            // List of errors is presented to user if verification fails.
            errors = new List<string>();
            return true;
        }
    }
}