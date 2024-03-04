using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;

namespace PlayniteUtilities
{
    public enum GameListSource
    {
        AllGames,
        FilteredGames,
        SelectedGames
    };

    public static class PlayniteApiUtilities
    {
        public static IEnumerable<Game> GetGameList(GameListSource source)
        {
            switch (source)
            {
                case GameListSource.AllGames:
                    return API.Instance.Database.Games;

                case GameListSource.FilteredGames:
                    return API.Instance.MainView.FilteredGames ?? new List<Game>();

                case GameListSource.SelectedGames:
                    return API.Instance.MainView.SelectedGames ?? new List<Game>();

                default:
                    throw new NotImplementedException();
            }
        }

        public static GlobalProgressResult ActivateGlobalProgressWithErrorChecking(this IDialogsFactory dialogs, Action<GlobalProgressActionArgs> progressAction, GlobalProgressOptions progressOptions)
        {
            return dialogs.ActivateGlobalProgress((progress) =>
            {
                try
                {
                    progressAction(progress);
                }
                catch (Exception ex)
                {
                    dialogs.ShowErrorMessage(ex.ToString(), "Error");
                }
            }, progressOptions);
        }

        public static bool AddTagToGame(IPlayniteAPI api, Game game, string tagName)
        {
            return AddTagToGame(api, game, api.Database.Tags.Add(tagName).Id);
        }

        public static bool AddTagToGame(IPlayniteAPI api, Game game, Guid tagId)
        {
            if (game.TagIds == null)
                game.TagIds = new List<Guid> { };

            if (!game.TagIds.Contains(tagId))
            {
                game.TagIds.Add(tagId);
                api.Database.Games.Update(game);
                return true;
            }

            return false;
        }

        public static bool RemoveTagFromGame(IPlayniteAPI api, Game game, string tagName)
        {
            if (!api.TryGetTagId(tagName, out var tagId))
                return false;

            return RemoveTagFromGame(api, game, tagId);
        }

        public static bool RemoveTagFromGame(IPlayniteAPI api, Game game, Guid tagId)
        {
            if (game.TagIds == null || game.TagIds.Count == 0)
                return false;

            if (!game.TagIds.Remove(tagId))
                return false;

            api.Database.Games.Update(game);
            return true;
        }

        public static bool TryGetTagId(this IPlayniteAPI api, string tagName, out Guid tagId, bool caseInsensitive = false)
        {
            foreach (var tag in api.Database.Tags)
            {
                if (string.Equals(tag.Name, tagName, caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                {
                    tagId = tag.Id;
                    return true;
                }
            }

            tagId = Guid.Empty;
            return false;
        }

        public static TSettings DeserializeFromFile<TSettings>(string path) where TSettings : class
        {
            if (File.Exists(path))
            {
                return Serialization.FromJsonFile<TSettings>(path);
            }

            return null;
        }

        public static void SerializeToFile<TSettings>(string path, TSettings settings) where TSettings : class
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            string contents = Serialization.ToJson(settings, formatted: false);

            var tmpNew = path + ".new";
            var tmpBak = path + ".bak";

            File.WriteAllText(tmpNew, contents);

            if (File.Exists(path))
            {
                if (File.Exists(tmpBak))
                    File.Delete(tmpBak);

                File.Move(path, tmpBak);
            }

            File.Move(tmpNew, path);

            if (File.Exists(tmpBak))
                File.Delete(tmpBak);
        }

        private class UpdateDatabaseItemActions
        {
            public Action Action;
            public Action OnComplete;
        }

        private static System.Threading.Thread updateDatabaseItemQueueThread = null;

        private static readonly System.Collections.Concurrent.ConcurrentQueue<UpdateDatabaseItemActions> updateDatabaseItemQueue =
            new System.Collections.Concurrent.ConcurrentQueue<UpdateDatabaseItemActions>();

        public static void KickUpdateDatabaseItemQueueThread(bool recreate = false)
        {
            if (updateDatabaseItemQueueThread != null && !recreate)
            {
                updateDatabaseItemQueueThread?.Interrupt();
                return;
            }

            try
            {
                updateDatabaseItemQueueThread?.Abort();
            }
            catch (Exception) { }

            updateDatabaseItemQueueThread = new System.Threading.Thread(UpdateDatabaseItemQueueThread_Worker);
            updateDatabaseItemQueueThread.Name = $"{nameof(updateDatabaseItemQueueThread)}_DateTime.Now:s";
            updateDatabaseItemQueueThread.IsBackground = true;
            updateDatabaseItemQueueThread.Start();
        }

        private static void UpdateDatabaseItemQueueThread_Worker()
        {
            UpdateDatabaseItemActions activeAction = null;

            try
            {
                while (!Environment.HasShutdownStarted)
                {
                    try
                    {
                        if (updateDatabaseItemQueue.TryDequeue(out activeAction))
                        {
                            activeAction.Action.Invoke();
                            activeAction.OnComplete?.Invoke();
                        }

                        activeAction = null;

                        System.Threading.Thread.Sleep(1000);
                    }
                    catch (System.Threading.ThreadInterruptedException) { }
                }
            }
            finally
            {
                activeAction?.OnComplete?.Invoke();
                KickUpdateDatabaseItemQueueThread(recreate: true);
            }
        }

        public static void KillUpdateDatabaseItemQueueThread()
        {
            updateDatabaseItemQueueThread?.Abort();
        }

        public static bool UpdateDatabaseWithTimeout<T>(this IItemCollection<T> db, T item, bool tryHandling = true)
            where T : DatabaseObject
        {
            System.Threading.EventWaitHandle waitHandle = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.ManualReset);

            db.BeginBufferUpdate();

            try
            {
                updateDatabaseItemQueue.Enqueue(new UpdateDatabaseItemActions()
                {
                    Action = () => db.Update(item),
                    OnComplete = () => waitHandle.Set()
                });

                KickUpdateDatabaseItemQueueThread();

                if (waitHandle.WaitOne(1000))
                    return true;

                KillUpdateDatabaseItemQueueThread();
                waitHandle.WaitOne();

                if (!tryHandling)
                    return false;

                var confirm = API.Instance.Dialogs.ShowMessage(
                    $"The {typeof(T).Name} '{item.Name}' with {nameof(item.Id)} '{item.Id}' is corrupted. Would you like to try recreating it?",
                    $"Corrupted {typeof(T).Name}",
                    MessageBoxButton.YesNo);

                if (confirm != MessageBoxResult.Yes)
                    return false;

                waitHandle.Reset();

                updateDatabaseItemQueue.Enqueue(new UpdateDatabaseItemActions()
                {
                    Action = () =>
                    {
                        db.Remove(item);
                        db.Add(item);
                    },
                    OnComplete = () => waitHandle.Set()
                });

                KickUpdateDatabaseItemQueueThread();

                if (waitHandle.WaitOne(1000))
                    return true;

                API.Instance.Dialogs.ShowMessage(
                    $"Failed to recreate the {typeof(T).Name} '{item.Name}' with {nameof(item.Id)} '{item.Id}'.",
                    $"Corrupted {typeof(T).Name}",
                    MessageBoxButton.OK);

                return false;
            }
            finally
            {
                waitHandle.Dispose();
                db.EndBufferUpdate();
            }
        }
    }
}