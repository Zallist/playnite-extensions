using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PlayniteUtilities;
using System.Windows.Media.Effects;

namespace MiscellaneousPlugin
{
    public class MiscellaneousPluginPlugin : GenericPlugin
    {
        public override Guid Id { get; } = Guid.Parse("08069769-99F8-4231-8C98-29851CA5F36F");

        public readonly IPlayniteAPI API;

        public MiscellaneousPluginPlugin(IPlayniteAPI api) : base(api)
        {
            API = api;

            Properties = new GenericPluginProperties
            {
                HasSettings = false
            };
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            EnsureOutlinesOnText();
        }

        private void EnsureOutlinesOnText()
        {

        }
    }
}