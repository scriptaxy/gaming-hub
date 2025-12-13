using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using gaming_hub.Models;

namespace gaming_hub.Services
{
    /// <summary>
    /// IGDB (Internet Game Database) API Service
    /// Provides high-quality game data including upcoming releases, game details, and cover art
    /// API Documentation: https://api-docs.igdb.com/
    /// </summary>
    public class IGDBService
    {
    private static IGDBService? _instance;
  private readonly HttpClient _httpClient;
        
        // IGDB API credentials (get from https://dev.twitch.tv/console)
        // You need to create a Twitch application and use its Client ID/Secret
private string? _clientId;
        private string? _clientSecret;
        private string? _accessToken;
        private DateTime _tokenExpiry = DateTime.MinValue;
        
     private const string TwitchAuthUrl = "https://id.twitch.tv/oauth2/token";
        private const string IGDBBaseUrl = "https://api.igdb.com/v4";
        
        // NSFW content filter
        private static readonly HashSet<int> NsfwGenreIds = new() { 42, 45 }; // Erotic, Adult
  private static readonly HashSet<int> NsfwThemeIds = new() { 42 }; // Erotic
        
        public static IGDBService Instance => _instance ??= new IGDBService();
    
        public bool IsConfigured => !string.IsNullOrEmpty(_clientId) && !string.IsNullOrEmpty(_clientSecret);
      
  private IGDBService()
   {
            _httpClient = new HttpClient();
  _httpClient.DefaultRequestHeaders.Add("User-Agent", "Synktra-iOS/1.0");
    LoadCredentials();
        }
        
        /// <summary>
        /// Configure IGDB with Twitch credentials
        /// </summary>
        public void Configure(string clientId, string clientSecret)
      {
       _clientId = clientId;
          _clientSecret = clientSecret;
      _accessToken = null;
      _tokenExpiry = DateTime.MinValue;
         SaveCredentials();
        }

        private void LoadCredentials()
      {
            try
    {
          var credPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
   "igdb_credentials.json");

      if (File.Exists(credPath))
              {
   var json = File.ReadAllText(credPath);
   var creds = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
   if (creds != null)
            {
      _clientId = creds.GetValueOrDefault("clientId");
      _clientSecret = creds.GetValueOrDefault("clientSecret");
  }
                }
             
   // Use default credentials if not configured
         if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
   {
          _clientId = "kx7awl87enzaqeontdddbzcwepeoel";
            _clientSecret = "cczig93rp4hb4ev43e0bhmef3c5521";
      Console.WriteLine("IGDB: Using default credentials");
      }
       }
            catch (Exception ex)
    {
                Console.WriteLine($"Failed to load IGDB credentials: {ex.Message}");
      // Fallback to defaults
 _clientId = "kx7awl87enzaqeontdddbzcwepeoel";
   _clientSecret = "cczig93rp4hb4ev43e0bhmef3c5521";
            }
      }
        
      private void SaveCredentials()
        {
   try
            {
        var credPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
       "igdb_credentials.json");
       
       var creds = new Dictionary<string, string>
       {
          ["clientId"] = _clientId ?? "",
        ["clientSecret"] = _clientSecret ?? ""
       };
              
             File.WriteAllText(credPath, JsonConvert.SerializeObject(creds));
     }
     catch (Exception ex)
      {
    Console.WriteLine($"Failed to save IGDB credentials: {ex.Message}");
          }
        }
        
        /// <summary>
        /// Get OAuth2 access token from Twitch
    /// </summary>
        private async Task<bool> EnsureAccessTokenAsync()
        {
       if (!IsConfigured) return false;
            
 // Return if token is still valid
          if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
                return true;
        
        try
{
                var content = new FormUrlEncodedContent(new[]
     {
       new KeyValuePair<string, string>("client_id", _clientId!),
          new KeyValuePair<string, string>("client_secret", _clientSecret!),
         new KeyValuePair<string, string>("grant_type", "client_credentials")
                });
                
                var response = await _httpClient.PostAsync(TwitchAuthUrl, content);
      
   if (response.IsSuccessStatusCode)
     {
     var json = await response.Content.ReadAsStringAsync();
                 var tokenData = JObject.Parse(json);
              
         _accessToken = tokenData["access_token"]?.ToString();
   var expiresIn = tokenData["expires_in"]?.Value<int>() ?? 3600;
  _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60); // 1 min buffer
      
     Console.WriteLine("IGDB: Access token obtained successfully");
         return true;
    }
    
        Console.WriteLine($"IGDB: Failed to get access token: {response.StatusCode}");
    return false;
     }
         catch (Exception ex)
   {
   Console.WriteLine($"IGDB: Auth error: {ex.Message}");
    return false;
  }
        }
      
      /// <summary>
    /// Execute IGDB API query
   /// </summary>
        private async Task<JArray?> QueryAsync(string endpoint, string body)
        {
            if (!await EnsureAccessTokenAsync())
                return null;
      
            try
     {
 var request = new HttpRequestMessage(HttpMethod.Post, $"{IGDBBaseUrl}/{endpoint}")
           {
     Content = new StringContent(body)
    };
                request.Headers.Add("Client-ID", _clientId);
request.Headers.Add("Authorization", $"Bearer {_accessToken}");
     
       var response = await _httpClient.SendAsync(request);
      
          if (response.IsSuccessStatusCode)
          {
    var json = await response.Content.ReadAsStringAsync();
         return JArray.Parse(json);
             }
           
   Console.WriteLine($"IGDB query failed: {response.StatusCode}");
   return null;
    }
            catch (Exception ex)
            {
     Console.WriteLine($"IGDB query error: {ex.Message}");
        return null;
            }
      }
    
        /// <summary>
    /// Get upcoming game releases from IGDB
   /// </summary>
        public async Task<List<UpcomingRelease>> GetUpcomingReleasesAsync(int limit = 30, int daysAhead = 180)
        {
          var releases = new List<UpcomingRelease>();
    
  if (!IsConfigured)
    {
        Console.WriteLine("IGDB: Not configured, skipping");
  return releases;
}
     
         var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var futureDate = DateTimeOffset.UtcNow.AddDays(daysAhead).ToUnixTimeSeconds();
       
     // Query for upcoming games with release dates
            // Filter out NSFW content and focus on major platforms
 var query = $@"
    fields game.name, game.cover.url, game.summary, game.genres, game.themes,
game.platforms.name, game.aggregated_rating, game.hypes, game.follows,
          date, human, platform.name, region;
    where date >= {now} & date <= {futureDate} 
         & game.cover != null
     & (platform = (6,48,49,130,167,169) | platform.name ~ *""PC""* | platform.name ~ *""PlayStation""* | platform.name ~ *""Xbox""* | platform.name ~ *""Switch""*);
  sort date asc;
      limit {limit};
            ";
  
            var result = await QueryAsync("release_dates", query);
       
            if (result == null) return releases;
     
  var seenGames = new HashSet<int>();
            
            foreach (var item in result)
            {
       try
{
       var game = item["game"];
    if (game == null) continue;
    
             var gameId = game["id"]?.Value<int>() ?? 0;
      if (gameId == 0 || seenGames.Contains(gameId)) continue;
      
     var gameName = game["name"]?.ToString();
       if (string.IsNullOrEmpty(gameName)) continue;
   
         // Check NSFW genres/themes
    var genres = game["genres"] as JArray;
 var themes = game["themes"] as JArray;
                    
         if (genres?.Any(g => NsfwGenreIds.Contains(g.Value<int>())) == true) continue;
                    if (themes?.Any(t => NsfwThemeIds.Contains(t.Value<int>())) == true) continue;
        
   seenGames.Add(gameId);
  
       // Get cover URL (convert from thumbnail to full size)
     var coverUrl = game["cover"]?["url"]?.ToString();
   if (!string.IsNullOrEmpty(coverUrl))
              {
           coverUrl = "https:" + coverUrl.Replace("t_thumb", "t_cover_big");
          }
         
   // Parse release date
     var timestamp = item["date"]?.Value<long>() ?? 0;
   var releaseDate = timestamp > 0 
? DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime 
   : DateTime.Today.AddDays(30);
                    
          var humanDate = item["human"]?.ToString();
        var isExactDate = !string.IsNullOrEmpty(humanDate) && humanDate.Contains(",");
            
      // Get platform name
        var platformName = item["platform"]?["name"]?.ToString() ?? "Multiple Platforms";
          
         // Get hype/follows count
        var hypes = game["hypes"]?.Value<int>() ?? 0;
  var follows = game["follows"]?.Value<int>() ?? 0;
         var rating = game["aggregated_rating"]?.Value<double?>();
       
       var description = game["summary"]?.ToString();
   if (!string.IsNullOrEmpty(description) && description.Length > 150)
       description = description[..147] + "...";
       
               releases.Add(new UpcomingRelease
        {
             ExternalId = $"igdb_{gameId}",
   GameName = gameName,
  CoverImageUrl = coverUrl,
         ReleaseDate = releaseDate,
     IsExactDate = isExactDate,
               Description = description,
         Platforms = platformName
             });
           }
catch (Exception ex)
             {
     Console.WriteLine($"IGDB: Error parsing release: {ex.Message}");
              }
            }

            return releases.OrderBy(r => r.ReleaseDate).ToList();
      }
        
        /// <summary>
        /// Get popular/hyped upcoming games
        /// </summary>
        public async Task<List<UpcomingRelease>> GetMostAnticipatedAsync(int limit = 20)
        {
            var releases = new List<UpcomingRelease>();
          
       if (!IsConfigured) return releases;
  
      var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    
     // Query for highly anticipated games (high hype count)
    var query = $@"
         fields name, cover.url, summary, genres, themes, platforms.name, 
        aggregated_rating, hypes, follows, first_release_date;
      where first_release_date >= {now} 
        & cover != null 
             & hypes > 10;
           sort hypes desc;
        limit {limit};
      ";
  
var result = await QueryAsync("games", query);
            
            if (result == null) return releases;
         
    foreach (var game in result)
        {
                try
          {
     var gameId = game["id"]?.Value<int>() ?? 0;
        var gameName = game["name"]?.ToString();
     if (string.IsNullOrEmpty(gameName)) continue;
         
         // Check NSFW
               var genres = game["genres"] as JArray;
var themes = game["themes"] as JArray;
          if (genres?.Any(g => NsfwGenreIds.Contains(g.Value<int>())) == true) continue;
        if (themes?.Any(t => NsfwThemeIds.Contains(t.Value<int>())) == true) continue;
   
      var coverUrl = game["cover"]?["url"]?.ToString();
         if (!string.IsNullOrEmpty(coverUrl))
         coverUrl = "https:" + coverUrl.Replace("t_thumb", "t_cover_big");
   
               var timestamp = game["first_release_date"]?.Value<long>() ?? 0;
 var releaseDate = timestamp > 0
          ? DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime
          : DateTime.Today.AddMonths(6);
  
         var hypes = game["hypes"]?.Value<int>() ?? 0;
          var platforms = game["platforms"] as JArray;
         var platformNames = platforms != null
           ? string.Join(", ", platforms.Select(p => p["name"]?.ToString()).Where(n => n != null).Distinct().Take(3))
     : "TBA";
            
           releases.Add(new UpcomingRelease
                    {
         ExternalId = $"igdb_{gameId}",
         GameName = gameName,
      CoverImageUrl = coverUrl,
      ReleaseDate = releaseDate,
             IsExactDate = timestamp > 0,
         Description = $"{hypes:N0} followers hyped",
    Platforms = platformNames
      });
                }
      catch (Exception ex)
     {
         Console.WriteLine($"IGDB: Error parsing anticipated game: {ex.Message}");
}
            }
        
            return releases;
}
        
        /// <summary>
        /// Search for games
    /// </summary>
        public async Task<List<Game>> SearchGamesAsync(string query, int limit = 20)
        {
     var games = new List<Game>();
            
     if (!IsConfigured || string.IsNullOrWhiteSpace(query)) return games;
          
 var igdbQuery = $@"
           search ""{query.Replace("\"", "")}"";
                fields name, cover.url, summary, genres.name, themes, platforms.name,
    aggregated_rating, first_release_date, involved_companies.company.name;
           where cover != null;
    limit {limit};
   ";
  
      var result = await QueryAsync("games", igdbQuery);
          
          if (result == null) return games;
            
          foreach (var item in result)
  {
            try
 {
          var gameId = item["id"]?.Value<int>() ?? 0;
        var gameName = item["name"]?.ToString();
    if (string.IsNullOrEmpty(gameName)) continue;
    
    // Check NSFW themes
      var themes = item["themes"] as JArray;
          if (themes?.Any(t => NsfwThemeIds.Contains(t.Value<int>())) == true) continue;
         
var coverUrl = item["cover"]?["url"]?.ToString();
  if (!string.IsNullOrEmpty(coverUrl))
         coverUrl = "https:" + coverUrl.Replace("t_thumb", "t_cover_big");
   
    var timestamp = item["first_release_date"]?.Value<long>() ?? 0;
         var releaseDate = timestamp > 0
           ? (DateTime?)DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime
       : null;
       
   var genres = item["genres"] as JArray;
      var genreNames = genres != null
? string.Join(", ", genres.Select(g => g["name"]?.ToString()).Where(n => n != null))
         : null;
             
      var companies = item["involved_companies"] as JArray;
       var developerName = companies?.FirstOrDefault()?["company"]?["name"]?.ToString();
    
games.Add(new Game
  {
          ExternalId = $"igdb_{gameId}",
      Name = gameName,
        CoverImageUrl = coverUrl,
          Description = item["summary"]?.ToString(),
        Genres = genreNames,
             Developers = developerName,
      ReleaseDate = releaseDate,
    MetacriticScore = item["aggregated_rating"]?.Value<double?>(),
                 Platform = GamePlatform.Manual
         });
      }
            catch (Exception ex)
     {
  Console.WriteLine($"IGDB: Error parsing game: {ex.Message}");
   }
}
            
     return games;
        }
        
        /// <summary>
        /// Get detailed game information
     /// </summary>
        public async Task<Game?> GetGameDetailsAsync(int igdbId)
        {
 if (!IsConfigured) return null;
      
var query = $@"
fields name, cover.url, summary, storyline, genres.name, themes.name,
              platforms.name, aggregated_rating, rating, first_release_date,
             involved_companies.company.name, involved_companies.developer, involved_companies.publisher,
         screenshots.url, videos.video_id, websites.url, websites.category;
        where id = {igdbId};
            ";
        
            var result = await QueryAsync("games", query);
            
         if (result == null || result.Count == 0) return null;
          
            var item = result[0];
            
  try
            {
 var coverUrl = item["cover"]?["url"]?.ToString();
     if (!string.IsNullOrEmpty(coverUrl))
     coverUrl = "https:" + coverUrl.Replace("t_thumb", "t_cover_big");
     
          var timestamp = item["first_release_date"]?.Value<long>() ?? 0;
      
             var genres = item["genres"] as JArray;
                var genreNames = genres != null
       ? string.Join(", ", genres.Select(g => g["name"]?.ToString()).Where(n => n != null))
           : null;
       
 var companies = item["involved_companies"] as JArray;
    var developers = companies?
  .Where(c => c["developer"]?.Value<bool>() == true)
         .Select(c => c["company"]?["name"]?.ToString())
            .Where(n => n != null);
 var publishers = companies?
                    .Where(c => c["publisher"]?.Value<bool>() == true)
 .Select(c => c["company"]?["name"]?.ToString())
         .Where(n => n != null);
 
            var screenshots = item["screenshots"] as JArray;
 var screenshotUrls = screenshots?
   .Select(s => "https:" + s["url"]?.ToString()?.Replace("t_thumb", "t_screenshot_big"))
          .Where(u => u != null)
   .Take(10)
              .ToList();
      
       var websites = item["websites"] as JArray;
        var officialSite = websites?
          .FirstOrDefault(w => w["category"]?.Value<int>() == 1)?["url"]?.ToString();
        
        return new Game
       {
         ExternalId = $"igdb_{igdbId}",
            Name = item["name"]?.ToString() ?? "Unknown",
           CoverImageUrl = coverUrl,
       Description = item["summary"]?.ToString() ?? item["storyline"]?.ToString(),
  Genres = genreNames,
                 Developers = developers != null ? string.Join(", ", developers) : null,
     Publishers = publishers != null ? string.Join(", ", publishers) : null,
   ReleaseDate = timestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime : null,
  MetacriticScore = item["aggregated_rating"]?.Value<double?>(),
           Screenshots = screenshotUrls?.Count > 0 ? JsonConvert.SerializeObject(screenshotUrls) : null,
           Website = officialSite,
     Platform = GamePlatform.Manual
        };
 }
       catch (Exception ex)
            {
       Console.WriteLine($"IGDB: Error parsing game details: {ex.Message}");
      return null;
            }
        }
    }
}
