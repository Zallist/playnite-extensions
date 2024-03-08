using System;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace LocalUserConfig.Models
{
    public class StoredAppSettings
    {
        public DesktopView ActiveDesktopView { get; set; }
        public FullscreenView ActiveFullscreenView { get; set; }
        public Guid? SelectedGameId { get; set; }
    }
}