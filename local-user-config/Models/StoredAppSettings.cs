using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace LocalUserConfig.Models
{
    public class StoredAppSettings
    {
        public Guid FilterPresetId { get; set; }
        public FilterPresetSettings FilterSettings { get; set; }
        public GroupableField Grouping { get; set; }
        public SortOrder SortOrder { get; set; }
        public SortOrderDirection SortOrderDirection { get; set; }
        public DesktopView ActiveDesktopView { get; set; }
        public FullscreenView ActiveFullscreenView { get; set; }
        public Guid? SelectedGameId { get; set; }
    }
}
