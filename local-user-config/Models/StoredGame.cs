using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace LocalUserConfig.Models
{
    public class StoredGame
    {
        private static IPlayniteAPI API => Playnite.SDK.API.Instance;

        public Guid Id { get; set; }
        public Guid CompletionStatusId { get; set; }
        public DateTime? LastActivity { get; set; }
        public bool Hidden { get; set; }
        public bool Favorite { get; set; }
        public string Notes { get; set; }
        public ulong PlayCount { get; set; }
        public ulong Playtime { get; set; }
        public int? UserScore { get; set; }

        private StoredGame() { }
        private StoredGame(in Game game) : this()
        {
            Id = game.Id;
            UpdateStoredGame(in game);
        }

        public static StoredGame CreateStoredGame(in Game game)
            => new StoredGame(in game);

        public void UpdateStoredGame(in Game game)
        {
            CompletionStatusId = game.CompletionStatusId;
            LastActivity = game.LastActivity;
            Hidden = game.Hidden;
            Notes = game.Notes;
            PlayCount = game.PlayCount;
            Playtime = game.Playtime;
            UserScore = game.UserScore;
            Favorite = game.Favorite;
        }

        public void UpdateRealGame(ref Game game)
        {
            if (Id != game.Id)
                return;

            game.CompletionStatusId = CompletionStatusId;
            game.LastActivity = LastActivity;
            game.Hidden = Hidden;
            game.Notes = Notes;
            game.PlayCount = PlayCount;
            game.Playtime = Playtime;
            game.UserScore = UserScore;
            game.Favorite = Favorite;
        }
    }
}
