using IGDBMetadataPlugin.IGDB;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using PlayniteUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace IGDBMetadataPlugin
{
    public class IGDBMetadataPluginProvider : OnDemandMetadataProvider
    {
        private const string IGDB_GAMES_URL = "https://api.igdb.com/v4/games";
        private const string IGDB_IMAGE_URL = "https://images.igdb.com/igdb/image/upload/t_original/";

        private const string TWITCH_AUTH_URL = "https://id.twitch.tv/oauth2/token";

        private class IGDBDownloader
        {
            private readonly ILogger logger = LogManager.GetLogger(nameof(IGDBDownloader));
            private readonly HttpClient httpClient;

            private bool warnedAboutBadDetails = false;
            private string clientId = null;
            private string clientSecret = null;

            private string accessToken = null;
            private DateTime accessTokenExpiresAt = DateTime.MaxValue;

            public IGDBDownloader(IGDBMetadataPluginSettings settings)
            {
                this.httpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(60),
                    MaxResponseContentBufferSize = int.MaxValue - 1,
                };
            }

            public async Task SetAuth(string clientId, string clientSecret)
            {
                if (clientId == this.clientId && clientSecret == this.clientSecret)
                    return;

                this.clientId = clientId;
                this.clientSecret = clientSecret;

                this.warnedAboutBadDetails = false;
                this.accessToken = null;
                this.accessTokenExpiresAt = DateTime.MaxValue;

                await EnsureAccessToken();
            }

            private async Task EnsureAccessToken()
            {
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    if (!warnedAboutBadDetails)
                    {
                        warnedAboutBadDetails = true;
                        logger.Error("Cannot get access token if not configured.");
                        API.Instance.Dialogs.ShowErrorMessage($"You need to configure the IGDB metadata plugin's settings first.", "IGDB Metadata Plugin Downloader");
                    }

                    return;
                }

                if (accessToken != null)
                {
                    if (accessTokenExpiresAt > DateTime.Now)
                        return;

                    accessToken = null;
                    accessTokenExpiresAt = DateTime.MaxValue;
                }

                httpClient.DefaultRequestHeaders.Clear();

                var responseTask = httpClient.PostAsync(
                    TWITCH_AUTH_URL,
                    new FormUrlEncodedContent(new Dictionary<string, string>()
                    {
                        { "client_id", clientId },
                        { "client_secret", clientSecret },
                        { "grant_type", "client_credentials" }
                    })
                );

                await responseTask;

                if (responseTask.IsFaulted)
                {
                    logger.Error(responseTask.Exception, "Failed to get access token.");
                }
                else
                {
                    var response = responseTask.Result;

                    if (response.IsSuccessStatusCode)
                    {
                        var content = response.Content;
                        var resultJson = await content.ReadAsStringAsync();
                        var result = Playnite.SDK.Data.Serialization.FromJson<Dictionary<string, string>>(resultJson);

                        this.accessToken = result["access_token"];
                        this.accessTokenExpiresAt = DateTime.Now.AddSeconds(int.Parse(result["expires_in"]) - 300);

                        httpClient.DefaultRequestHeaders.Add("Client-ID", clientId);
                        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    }
                    else
                    {
                        if (!warnedAboutBadDetails)
                        {
                            warnedAboutBadDetails = true;
                            logger.Error($"Failed to get access token: {response.Content?.ReadAsStringAsync()?.Result}");
                            API.Instance.Dialogs.ShowErrorMessage($"Failed to get access token: {response.Content?.ReadAsStringAsync()?.Result}", "IGDB Metadata Plugin Downloader");
                            return;
                        }
                    }
                }
            }

            private const int MILLISECONDS_BETWEEN_REQUESTS = 250;
            private static DateTime nextAllowedRequest = DateTime.UtcNow;
            private static readonly SemaphoreSlim maxActiveRequests = new SemaphoreSlim(8, 8);

            public async Task<HttpResponseMessage> Request(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await EnsureAccessToken().ConfigureAwait(false);

                if (this.accessToken == null)
                    throw new Exception("Failed to get access token.");

                HttpResponseMessage response;

                var currentNextAllowedRequest = nextAllowedRequest;

                if (DateTime.Now < nextAllowedRequest)
                {
                    nextAllowedRequest = currentNextAllowedRequest.AddMilliseconds(MILLISECONDS_BETWEEN_REQUESTS);
                    await Task.Delay(currentNextAllowedRequest - DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    nextAllowedRequest = DateTime.UtcNow.AddMilliseconds(MILLISECONDS_BETWEEN_REQUESTS);
                }

                await maxActiveRequests.WaitAsync().ConfigureAwait(false);

                try
                {
                    response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    maxActiveRequests.Release();
                }

                if (cancellationToken.IsCancellationRequested)
                    return response;

                if ((int)response.StatusCode == 429)
                {
                    await Task.Delay(MILLISECONDS_BETWEEN_REQUESTS, cancellationToken).ConfigureAwait(false);
                    return await Request(request, cancellationToken).ConfigureAwait(false);
                }

                return response;
            }
        }

        private readonly MetadataRequestOptions options;
        private readonly IGDBMetadataPlugin plugin;
        private readonly bool supplemental;

        private static IGDBDownloader downloader = null;

        private string queryString;

        public IGDBMetadataPluginProvider(MetadataRequestOptions options, IGDBMetadataPlugin plugin, IGDBMetadataPluginSettings settings, bool supplemental)
        {
            this.options = options;
            this.plugin = plugin;
            this.supplemental = supplemental;

            if (downloader == null)
                downloader = new IGDBDownloader(settings);

            downloader.SetAuth(settings.ClientId, settings.ClientSecret).Wait();

            queryString = @"fields name,alternative_names.name,game_localizations.name,ports.name,remakes.name,remasters.name,
game_modes.name,genres.name,keywords.name,themes.name,player_perspectives.name,
platforms.name,platforms.alternative_name,platforms.abbreviation,platforms.slug,
multiplayer_modes.*,
url,websites.url,websites.category,
age_ratings.category,age_ratings.rating,
involved_companies.developer,involved_companies.publisher,involved_companies.company.name,
release_dates.date,release_dates.region,
release_dates.platform.name,release_dates.platform.alternative_name,release_dates.platform.abbreviation,release_dates.platform.slug,
collections.name;
limit 50;";

            if (!this.supplemental)
                queryString += @"fields summary,storyline,
artworks.image_id,artworks.width,artworks.height,
cover.image_id,cover.width,cover.height,
screenshots.image_id,screenshots.width,screenshots.height,
aggregated_rating,
rating;";

            // Preload the data if we're in manual mode
            if (!options.IsBackgroundDownload)
                EnsureLoaded().Wait();
        }

        public IGDBGame IGDBData = null;

        private static string EscapeForIGDBQuery(string text, bool forSearch)
        {
            text = text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");

            if (forSearch)
                text = text.Replace(";", " ");

            return text;
        }

        private async Task NetworkError(HttpResponseMessage responseMessage)
        {
            var errorText = $"A network error occured while processing game: {options.GameData.Name} ({options.GameData.Id})\n\n{responseMessage.StatusCode} ({responseMessage.ReasonPhrase})";

            try
            {
                if (responseMessage?.Content != null)
                    errorText += $": {await responseMessage.Content.ReadAsStringAsync()}";
            }
            catch (Exception)
            {
                errorText += $": Failed to read content.";
            }

            plugin.Logger.Error(errorText);
        }

        private async Task<List<IGDBGame>> GetGames(string queryFilter, CancellationToken cancellationToken)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Post, IGDB_GAMES_URL))
            {
                req.Content = new StringContent($@"{queryString} {queryFilter}");

                using (var response = await downloader.Request(req, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        await NetworkError(response).ConfigureAwait(false);
                        return new List<IGDBGame>();
                    }

                    var jsonStream = await response.Content.ReadAsStreamAsync();
                    return Playnite.SDK.Data.Serialization.FromJsonStream<List<IGDBGame>>(jsonStream);
                }
            }
        }

        private async Task EnsureGameData()
        {
            try
            {
                if (!options.IsBackgroundDownload)
                {
                    Dictionary<GenericItemOption, IGDBGame> gameMap = new Dictionary<GenericItemOption, IGDBGame>();

                    var item = plugin.PlayniteApi.Dialogs.ChooseItemWithSearch(null, (search) =>
                    {
                        if (string.IsNullOrWhiteSpace(search))
                        {
                            return new List<GenericItemOption>();
                        }

                        var games = GetGames($@"search ""{EscapeForIGDBQuery(search, true)}"";", CancellationToken.None).Result;

                        gameMap.Clear();

                        foreach (var game in games)
                        {
                            gameMap.Add(
                                new GenericItemOption(game.name, string.Join(", ", new string[0].Concat(game.platforms?.Select(p => p.name) ?? new string[0]))),
                                game);
                        }

                        return gameMap.Keys.ToList();
                    }, options.GameData.Name);

                    if (item == null || !gameMap.TryGetValue(item, out IGDBData))
                        return;
                }
                else
                {
                    var escapedName = EscapeForIGDBQuery(options.GameData.Name, true);
                    var games = await GetGames($@"search ""{escapedName}"";", CancellationToken.None).ConfigureAwait(false);

                    if (games.Count == 0)
                    {
                        escapedName = EscapeForIGDBQuery(options.GameData.Name, false);
                        games = await GetGames($@"where name ~ *""{escapedName}""* | alternative_names.name ~ *""{escapedName}""* | game_localizations.name ~ *""{escapedName}""* | ports.name ~ *""{escapedName}""* | remakes.name ~ *""{escapedName}""* | remasters.name ~ *""{escapedName}""*;",
                            CancellationToken.None).ConfigureAwait(false);
                    }

                    if (games.Count == 0)
                    {
                        escapedName = EscapeForIGDBQuery(GeneralHelpers.SanitizeName(options.GameData.Name), true);
                        games = await GetGames($@"search ""{escapedName}"";", CancellationToken.None).ConfigureAwait(false);
                    }

                    if (games.Count == 0)
                    {
                        plugin.Logger.Warn($"Failed to find game: {options.GameData.Name} ({options.GameData.Id})");
                        return;
                    }

                    var matchingIgdbGame = GetBestMatch(games, options.GameData);

                    if (matchingIgdbGame == null)
                    {
                        plugin.Logger.Warn($"Found IGDB matches but for some reason no 'best' match was found for {options.GameData.Name} ({options.GameData.Id})");
                        return;
                    }

                    IGDBData = matchingIgdbGame;
                }
            }
            catch (Exception ex)
            {
                plugin.Logger.Error(ex, $"Failed to get game: {options.GameData.Name} ({options.GameData.Id})");
                throw;
            }
        }

        #region Auto-matching
        private IGDBGame GetBestMatch(List<IGDBGame> igdbGames, Game game)
        {
            if (igdbGames.Count == 1)
                return igdbGames[0];

            HashSet<IGDBGame> matches;
            IGDBGame matched;

            IEnumerable<string> getNames(IGDBGame g)
            {
                return new string[] { g.name }
                    .Concat(g.alternative_names?.Where(n => !string.IsNullOrEmpty(n.name)).Select(n => n.name) ?? new string[0])
                    .Concat(g.game_localizations?.Where(n => !string.IsNullOrEmpty(n.name)).Select(n => n.name) ?? new string[0])
                    .Concat(g.ports?.Where(n => !string.IsNullOrEmpty(n.name)).Select(n => n.name) ?? new string[0])
                    .Concat(g.remakes?.Where(n => !string.IsNullOrEmpty(n.name)).Select(n => n.name) ?? new string[0])
                    .Concat(g.remasters?.Where(n => !string.IsNullOrEmpty(n.name)).Select(n => n.name) ?? new string[0]);
            }

            // exact name match
            var groupedGames = GroupGames(igdbGames, g => new string[] { g.name });
            if (groupedGames.TryGetValue(game.Name, out matches) &&
                GetMatchingGame(matches, game, out matched))
                return matched;

            groupedGames = GroupGames(igdbGames, getNames);
            if (groupedGames.TryGetValue(game.Name, out matches) &&
                GetMatchingGame(matches, game, out matched))
                return matched;

            // sanitize the names
            groupedGames = GroupGames(igdbGames, g => getNames(g).Select(PlayniteUtilities.GeneralHelpers.SanitizeName));
            if (groupedGames.TryGetValue(PlayniteUtilities.GeneralHelpers.SanitizeName(game.Name), out matches) &&
                GetMatchingGame(matches, game, out matched))
                return matched;

            // Check if we have a game that just has the same platform
            {
                List<IGDBGame> matchingGames = new List<IGDBGame>();
                foreach (var igdbGame in igdbGames)
                {
                    if (HasMatchingPlatform(igdbGame.platforms, game))
                        matchingGames.Add(igdbGame);
                }

                if (matchingGames.Count > 0)
                    return matchingGames[0];
            }

            return igdbGames.First();
        }

        static Dictionary<string, HashSet<IGDBGame>> GroupGames(List<IGDBGame> igdbGames, Func<IGDBGame, IEnumerable<string>> keySelector)
        {
            Dictionary<string, HashSet<IGDBGame>> grouped = new Dictionary<string, HashSet<IGDBGame>>(StringComparer.OrdinalIgnoreCase);

            foreach (var igdbGame in igdbGames)
            {
                foreach (var key in keySelector(igdbGame))
                {
                    if (!grouped.TryGetValue(key, out var l))
                        grouped[key] = l = new HashSet<IGDBGame>();

                    l.Add(igdbGame);
                }
            }

            return grouped;
        }


        static bool GetMatchingGame(ICollection<IGDBGame> igdbGames, Game game, out IGDBGame matched, bool fuzzy = true)
        {
            matched = null;

            if (igdbGames.Count == 1 && fuzzy)
            {
                matched = igdbGames.First();
                return true;
            }

            foreach (var igdbGame in igdbGames)
            {
                if (HasMatchingPlatform(igdbGame.platforms, game))
                {
                    //exactMatches++;
                    matched = igdbGame;
                    return true;
                }
            }

            // Find the match with the most likely platform
            return false;
        }

        static bool HasMatchingPlatform(IEnumerable<IGDBPlatform> platforms, Game game)
        {
            return platforms != null && platforms.Any(p => GetMatchingPlatform(p, game) != null);
        }

        static Platform GetMatchingPlatform(IGDBPlatform platform, Game game)
            => GetMatchingPlatform(platform, game.Platforms);

        static Platform GetMatchingPlatform(IGDBPlatform platform, IEnumerable<Platform> realPlatforms)
        {
            var platformNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                platform.name,
                platform.abbreviation,
                platform.slug
            };

            if (platform.alternative_name != null)
                platformNames.UnionWith(platform.alternative_name.Split(',').Select(p => p.Trim()));

            platformNames.RemoveWhere(p => string.IsNullOrEmpty(p));

            foreach (var realPlatform in realPlatforms)
            {
                foreach (var name in platformNames)
                {
                    if (GetWordRegex(realPlatform.Name)?.IsMatch(name) == true)
                        return realPlatform;
                    if (GetWordRegex(realPlatform.SpecificationId)?.IsMatch(name) == true)
                        return realPlatform;

                    if (realPlatform.Name != null && GetWordRegex(name)?.IsMatch(realPlatform.Name) == true)
                        return realPlatform;
                    if (realPlatform.SpecificationId != null && GetWordRegex(name)?.IsMatch(realPlatform.SpecificationId) == true)
                        return realPlatform;
                }
            }

            return null;
        }

        private static readonly Dictionary<string, Regex> wordRegexCache = new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);
        private static Regex GetWordRegex(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return null;

            if (!wordRegexCache.TryGetValue(word, out var regex))
                wordRegexCache[word] = regex = new Regex($@"\b{Regex.Escape(word.Trim())}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

            return regex;
        }
        #endregion

        private List<MetadataField> availableFields = null;

        public override List<MetadataField> AvailableFields
        {
            get
            {
                EnsureLoaded().Wait();

                return availableFields;
            }
        }

        private async Task EnsureLoaded()
        {
            if (availableFields == null)
            {
                await EnsureGameData().ConfigureAwait(false);
                availableFields = GetAvailableFields();
            }
        }

        private List<MetadataField> GetAvailableFields()
        {
            if (IGDBData == null)
                return new List<MetadataField>();

            var fields = new HashSet<MetadataField>();
            var args = new GetMetadataFieldArgs();

            if (!string.IsNullOrEmpty(GetName(args)))
                fields.Add(MetadataField.Name);
            if (!string.IsNullOrEmpty(GetDescription(args)))
                fields.Add(MetadataField.Description);

            if (GetCommunityScore(args).HasValue)
                fields.Add(MetadataField.CommunityScore);
            if (GetCriticScore(args).HasValue)
                fields.Add(MetadataField.CriticScore);
            if (GetReleaseDate(args).HasValue)
                fields.Add(MetadataField.ReleaseDate);

            if (IGDBData?.screenshots?.Count > 0 || IGDBData?.artworks?.Count > 0)
                fields.Add(MetadataField.BackgroundImage);
            if (IGDBData?.cover?.image_id != null)
                fields.Add(MetadataField.CoverImage);
            if (IGDBData?.artworks?.Count > 0 || IGDBData?.cover?.image_id != null)
                fields.Add(MetadataField.Icon);

            if (GetGenres(args)?.Any() == true)
                fields.Add(MetadataField.Genres);
            if (GetDevelopers(args)?.Any() == true)
                fields.Add(MetadataField.Developers);
            if (GetPublishers(args)?.Any() == true)
                fields.Add(MetadataField.Publishers);
            if (GetAgeRatings(args)?.Any() == true)
                fields.Add(MetadataField.AgeRating);
            if (GetSeries(args)?.Any() == true)
                fields.Add(MetadataField.Series);
            if (GetRegions(args)?.Any() == true)
                fields.Add(MetadataField.Region);
            if (GetPlatforms(args)?.Any() == true)
                fields.Add(MetadataField.Platform);

            if (GetLinks(args)?.Any() == true)
                fields.Add(MetadataField.Links);
            if (GetTags(args)?.Any() == true)
                fields.Add(MetadataField.Tags);
            if (GetFeatures(args)?.Any() == true)
                fields.Add(MetadataField.Features);

            fields.IntersectWith(plugin.SupportedFields);

            return fields.ToList();
        }

        public override string GetName(GetMetadataFieldArgs args)
        {
            if (string.IsNullOrEmpty(IGDBData?.name))
                return base.GetName(args);
            return IGDBData.name;
        }

        public override string GetDescription(GetMetadataFieldArgs args)
        {
            if (string.IsNullOrEmpty(IGDBData?.summary) || string.IsNullOrEmpty(IGDBData?.storyline))
                return base.GetDescription(args);

            var description = "";

            if (!string.IsNullOrEmpty(IGDBData.summary))
            {
                description += System.Net.WebUtility.HtmlEncode(IGDBData.summary);

                if (!string.IsNullOrEmpty(IGDBData.storyline))
                    description += "<br /><br /><h1>Storyline</h1>";
            }

            if (!string.IsNullOrEmpty(IGDBData.storyline))
            {
                description += System.Net.WebUtility.HtmlEncode(IGDBData.storyline);
            }

            return description;
        }

        public override int? GetCommunityScore(GetMetadataFieldArgs args)
        {
            if (IGDBData?.aggregated_rating == null)
                return base.GetCommunityScore(args);
            return (int)Math.Round(IGDBData.aggregated_rating.Value);
        }

        public override int? GetCriticScore(GetMetadataFieldArgs args)
        {
            if (IGDBData?.rating == null)
                return base.GetCriticScore(args);
            return (int)Math.Round(IGDBData.rating.Value);
        }

        public override ReleaseDate? GetReleaseDate(GetMetadataFieldArgs args)
        {
            if (IGDBData?.release_dates?.Any(r => r.date.HasValue) != true)
                return base.GetReleaseDate(args);

            foreach (var releaseDate in IGDBData.release_dates)
            {
                if (!releaseDate.date.HasValue)
                    continue;

                if (releaseDate.platform != null)
                {
                    if (GetMatchingPlatform(releaseDate.platform, options.GameData) != null)
                        return new ReleaseDate(releaseDate.Date.Value.Date);
                }

                return new ReleaseDate(releaseDate.Date.Value.Date);
            }

            foreach (var releaseDate in IGDBData.release_dates)
            {
                if (!releaseDate.date.HasValue)
                    continue;

                return new ReleaseDate(releaseDate.Date.Value.Date);
            }

            return base.GetReleaseDate(args);
        }

        public override MetadataFile GetCoverImage(GetMetadataFieldArgs args)
        {
            if (string.IsNullOrEmpty(IGDBData?.cover?.image_id))
                return base.GetCoverImage(args);

            return new MetadataFile($"{IGDB_IMAGE_URL}{IGDBData.cover.image_id}.jpg");
        }

        public override MetadataFile GetBackgroundImage(GetMetadataFieldArgs args)
        {
            if (IGDBData == null)
                return base.GetBackgroundImage(args);

            var possibleBackgrounds = new List<IGDBImage>();

            if (IGDBData.artworks?.Count > 0)
                possibleBackgrounds.AddRange(IGDBData.artworks.Where(i => !string.IsNullOrEmpty(i.image_id)));
            if (IGDBData.screenshots?.Count > 0)
                possibleBackgrounds.AddRange(IGDBData.screenshots.Where(i => !string.IsNullOrEmpty(i.image_id)));

            if (possibleBackgrounds.Count == 0)
                return base.GetBackgroundImage(args);

            if (possibleBackgrounds.Count == 1 || options.IsBackgroundDownload)
            {
                return new MetadataFile($"{IGDB_IMAGE_URL}{possibleBackgrounds[0].image_id}.jpg");
            }
            else
            {
                var imageFileOptions = possibleBackgrounds.Select(i => new ImageFileOption()
                {
                    Path = $"{IGDB_IMAGE_URL}{i.image_id}.jpg",
                }).ToList();

                var selectedFileOption = plugin.PlayniteApi.Dialogs.ChooseImageFile(imageFileOptions);

                if (selectedFileOption == null)
                    return base.GetBackgroundImage(args);

                return new MetadataFile(selectedFileOption.Path);
            }

        }

        public override MetadataFile GetIcon(GetMetadataFieldArgs args)
        {
            if (IGDBData == null)
                return base.GetIcon(args);

            var img = IGDBData.artworks?
                .OrderByDescending(s => s.height * s.width)
                .FirstOrDefault(s => s.image_id != null);

            if (img == null && IGDBData.cover?.image_id != null)
                img = IGDBData.cover;

            if (img == null)
                return base.GetIcon(args);

            return new MetadataFile($"{IGDB_IMAGE_URL}{img.image_id}.jpg");
        }

        private IEnumerable<MetadataNameProperty> Supplement(IEnumerable<string> existingValues, IEnumerable<string> values)
        {
            if (values?.Any() != true)
                return null;

            List<string> props = new List<string>();

            if (supplemental && existingValues != null)
            {
                props.AddRange(existingValues);
            }

            props.AddRange(values);

            props = props.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            return props.Select(v => new MetadataNameProperty(v));
        }

        public override IEnumerable<MetadataProperty> GetGenres(GetMetadataFieldArgs args)
        {
            if (IGDBData?.genres?.Any() != true)
                return base.GetGenres(args);

            return Supplement(
                options.GameData.Genres?.Select(g => g.Name),
                IGDBData.genres.Select(g => g.name));
        }

        public override IEnumerable<MetadataProperty> GetDevelopers(GetMetadataFieldArgs args)
        {
            if (IGDBData?.involved_companies?.Any(c => c.developer && !string.IsNullOrEmpty(c.company?.name)) != true)
                return base.GetDevelopers(args);

            return Supplement(
                options.GameData.Developers?.Select(g => g.Name),
                IGDBData.involved_companies.Where(c => c.developer && !string.IsNullOrEmpty(c.company?.name)).Select(c => c.company.name));
        }

        public override IEnumerable<MetadataProperty> GetPublishers(GetMetadataFieldArgs args)
        {
            if (IGDBData?.involved_companies?.Any(c => c.publisher && !string.IsNullOrEmpty(c.company?.name)) != true)
                return base.GetPublishers(args);

            return Supplement(
                options.GameData.Publishers?.Select(g => g.Name),
                IGDBData.involved_companies.Where(c => c.publisher && !string.IsNullOrEmpty(c.company?.name)).Select(c => c.company.name));
        }

        public override IEnumerable<MetadataProperty> GetAgeRatings(GetMetadataFieldArgs args)
        {
            if (IGDBData?.age_ratings?.Any() != true)
                return base.GetAgeRatings(args);

            return Supplement(
                options.GameData.AgeRatings?.Select(g => g.Name),
                IGDBData.age_ratings.Select(g => g.ToString()));
        }

        public override IEnumerable<MetadataProperty> GetRegions(GetMetadataFieldArgs args)
        {
            if (IGDBData?.release_dates?.Any() != true)
                return base.GetRegions(args);

            return Supplement(
                options.GameData.Regions?.Select(g => g.Name),
                IGDBData.release_dates.Select(g => GeneralHelpers.HumanizeEnum(g.region)));
        }

        public override IEnumerable<MetadataProperty> GetSeries(GetMetadataFieldArgs args)
        {
            if (IGDBData?.collections?.Any() != true)
                return base.GetSeries(args);

            return Supplement(
                options.GameData.Series?.Select(g => g.Name),
                IGDBData.collections.Select(g => g.name));
        }

        public override IEnumerable<MetadataProperty> GetPlatforms(GetMetadataFieldArgs args)
        {
            if (IGDBData?.platforms?.Any() != true)
                return base.GetPlatforms(args);

            // Try to reuse as many existing platforms as possible
            var existingPlatformsToAdd = new HashSet<string>();
            var igdbPlatforms = new HashSet<IGDBPlatform>(IGDBData.platforms);

            foreach (var igdbPlatform in IGDBData.platforms)
            {
                var existingPlatform = GetMatchingPlatform(igdbPlatform, plugin.PlayniteApi.Database.Platforms);

                if (existingPlatform != null)
                {
                    existingPlatformsToAdd.Add(existingPlatform.Name);
                    igdbPlatforms.Remove(igdbPlatform);
                }
            }

            List<string> props = new List<string>(existingPlatformsToAdd);

            props.AddRange(igdbPlatforms.Select(g => g.name));

            return Supplement(
                options.GameData.Platforms?.Select(g => g.Name),
                props);
        }

        public override IEnumerable<Link> GetLinks(GetMetadataFieldArgs args)
        {
            if (IGDBData?.websites?.Any() != true)
                return base.GetLinks(args);

            var websitesToAdd = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(IGDBData.url))
                websitesToAdd["IGDB"] = IGDBData.url;

            if (IGDBData.websites != null)
                foreach (var igdbValue in IGDBData.websites)
                {
                    var name = Enum.GetName(typeof(IGDBWebsite.WebsiteCategoryEnum), igdbValue.category_enum);

                    if (string.IsNullOrEmpty(name) || websitesToAdd.ContainsKey(name))
                        continue;

                    websitesToAdd[name] = igdbValue.url;
                }

            if (websitesToAdd.Count == 0)
                return base.GetLinks(args);

            List<Link> links = new List<Link>();

            foreach (var websiteKVP in websitesToAdd)
            {
                if (supplemental)
                {
                    if (options.GameData.Links.Any(x => StringComparer.OrdinalIgnoreCase.Equals(x.Name, websiteKVP.Key)))
                        continue;
                }

                links.Add(new Link(websiteKVP.Key, websiteKVP.Value));
            }

            if (supplemental)
                links.InsertRange(0, options.GameData.Links);

            return links;
        }

        public override IEnumerable<MetadataProperty> GetFeatures(GetMetadataFieldArgs args)
        {
            List<string> props = new List<string>();

            if (IGDBData?.game_modes?.Any() == true)
                props.AddRange(IGDBData.game_modes.Select(g => g.name));

            if (IGDBData?.multiplayer_modes?.Any() == true)
            {
                var maxMultiplayerMode = IGDBMultiplayerMode.GenerateMaxMultiplayerMode(IGDBData.multiplayer_modes);

                if (maxMultiplayerMode.campaigncoop)
                    props.Add("[Multiplayer] Co-op campaign");
                if (maxMultiplayerMode.dropin)
                    props.Add("[Multiplayer] Drop in/out");
                if (maxMultiplayerMode.splitscreen)
                    props.Add("[Multiplayer] Split-screen");
                if (maxMultiplayerMode.splitscreenonline)
                    props.Add("[Multiplayer] Online split-screen");
                if (maxMultiplayerMode.lancoop)
                    props.Add("[Multiplayer] LAN co-op");
                if (maxMultiplayerMode.offlinecoop)
                    props.Add("[Multiplayer] Offline co-op");
                if (maxMultiplayerMode.onlinecoop)
                    props.Add("[Multiplayer] Online co-op");

                if (maxMultiplayerMode.offlinemax >= 2)
                    props.Add("[Multiplayer] Offline");
                if (maxMultiplayerMode.onlinemax >= 2)
                    props.Add("[Multiplayer] Online");
                if (maxMultiplayerMode.offlinecoopmax >= 2)
                    props.Add("[Multiplayer] Offline co-op");
                if (maxMultiplayerMode.onlinecoopmax >= 2)
                    props.Add("[Multiplayer] Online co-op");
            }

            if (props.Count == 0)
                return base.GetFeatures(args);

            return Supplement(
                options.GameData.Features?.Select(g => g.Name),
                props);
        }

        public override IEnumerable<MetadataProperty> GetTags(GetMetadataFieldArgs args)
        {
            List<string> props = new List<string>();

            if (IGDBData?.keywords?.Any() == true)
                props.AddRange(IGDBData.keywords.Select(g => g.name));

            if (IGDBData?.themes?.Any() == true)
                props.AddRange(IGDBData.themes.Select(g => $"[Theme] {g.name}"));

            if (IGDBData?.player_perspectives?.Any() == true)
                props.AddRange(IGDBData.player_perspectives.Select(g => $"[Perspective] {g.name}"));

            if (IGDBData?.multiplayer_modes?.Any() == true)
            {
                var maxMultiplayerMode = IGDBMultiplayerMode.GenerateMaxMultiplayerMode(IGDBData.multiplayer_modes);

                void AddMultiplayerNumbers(string key, int max)
                {
                    if (max >= 2)
                    {
                        if (max > 2)
                        {
                            props.Add($"{key} supports 2+ players");
                            props.Add($"{key} supports up to {max} players");
                        }
                        else
                        {
                            props.Add($"{key} supports 2 players");
                        }
                    }
                }

                AddMultiplayerNumbers("[Multiplayer] Offline", maxMultiplayerMode.offlinemax);
                AddMultiplayerNumbers("[Multiplayer] Online", maxMultiplayerMode.onlinemax);
                AddMultiplayerNumbers("[Multiplayer] Offline co-op", maxMultiplayerMode.offlinecoopmax);
                AddMultiplayerNumbers("[Multiplayer] Online co-op", maxMultiplayerMode.onlinecoopmax);
            }

            if (props.Count == 0)
                return base.GetTags(args);

            return Supplement(
                options.GameData.Tags?.Select(g => g.Name),
                props);
        }
    }
}