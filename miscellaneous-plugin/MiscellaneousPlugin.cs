using System;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;

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