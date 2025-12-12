using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using gaming_hub.Models;
using System.Web;

namespace gaming_hub.Services
{
    public class SteamService
    {
        private static SteamService? _instance;
    private readonly HttpClient _httpClient;
        
        // Steam API endpoints
        private const string SteamApiBaseUrl = "https://api.steampowered.com";
     private const string SteamStoreBaseUrl = "https://store.steampowered.com/api";
    private const string SteamCdnBaseUrl = "https://steamcdn-a.akamaihd.net/steam/apps";
   
        // Steam OpenID endpoints
     private const string SteamOpenIdUrl = "https://steamcommunity.com/openid/login";

        public static SteamService Instance => _instance ??= new SteamService();

        private SteamService()
 {
 _httpClient = new HttpClient();
   _httpClient.DefaultRequestHeaders.Add("User-Agent", "Synktra-iOS/1.0");
        }

        // ==================== Steam OpenID Authentication ====================

        /// <summary>
    /// Get the Steam OpenID login URL for OAuth-like authentication
        /// </summary>
        public string GetOpenIdLoginUrl(string returnUrl)
        {
            var parameters = new Dictionary<string, string>
            {
        ["openid.ns"] = "http://specs.openid.net/auth/2.0",
    ["openid.mode"] = "checkid_setup",
              ["openid.return_to"] = returnUrl,
        ["openid.realm"] = GetRealm(returnUrl),
          ["openid.identity"] = "http://specs.openid.net/auth/2.0/identifier_select",
          ["openid.claimed_id"] = "http://specs.openid.net/auth/2.0/identifier_select"
            };

   var queryString = string.Join("&", parameters.Select(p => 
       $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    
  return $"{SteamOpenIdUrl}?{queryString}";
  }

      /// <summary>
    /// Verify Steam OpenID response and extract Steam ID
        /// </summary>
        public async Task<SteamOpenIdResult?> VerifyOpenIdResponseAsync(string responseUrl)
        {
  try
     {
 var uri = new Uri(responseUrl);
 var query = HttpUtility.ParseQueryString(uri.Query);

    // Check if authentication was successful
      var mode = query["openid.mode"];
     if (mode != "id_res")
       return null;

    // Extract Steam ID from claimed_id
    var claimedId = query["openid.claimed_id"];
        if (string.IsNullOrEmpty(claimedId))
         return null;

         // Steam ID is the last part of the URL: https://steamcommunity.com/openid/id/76561198xxxxxxxx
     var steamId = claimedId.Split('/').LastOrDefault();
                if (string.IsNullOrEmpty(steamId) || !long.TryParse(steamId, out _))
        return null;

              // Verify the response with Steam (recommended for security)
                var isValid = await VerifyWithSteamAsync(query);
     if (!isValid)
           return null;

        return new SteamOpenIdResult
        {
             SteamId = steamId,
    Success = true
       };
            }
            catch (Exception ex)
            {
      Console.WriteLine($"Error verifying Steam OpenID: {ex.Message}");
   return null;
            }
        }

        private async Task<bool> VerifyWithSteamAsync(System.Collections.Specialized.NameValueCollection query)
        {
  try
   {
        var verifyParams = new Dictionary<string, string>
         {
     ["openid.ns"] = query["openid.ns"] ?? "",
         ["openid.mode"] = "check_authentication",
       ["openid.op_endpoint"] = query["openid.op_endpoint"] ?? "",
  ["openid.claimed_id"] = query["openid.claimed_id"] ?? "",
        ["openid.identity"] = query["openid.identity"] ?? "",
             ["openid.return_to"] = query["openid.return_to"] ?? "",
  ["openid.response_nonce"] = query["openid.response_nonce"] ?? "",
       ["openid.assoc_handle"] = query["openid.assoc_handle"] ?? "",
           ["openid.signed"] = query["openid.signed"] ?? "",
              ["openid.sig"] = query["openid.sig"] ?? ""
     };

          var content = new FormUrlEncodedContent(verifyParams);
       var response = await _httpClient.PostAsync(SteamOpenIdUrl, content);
           var result = await response.Content.ReadAsStringAsync();

      return result.Contains("is_valid:true");
  }
            catch
        {
                return false;
            }
   }

        private static string GetRealm(string returnUrl)
        {
       var uri = new Uri(returnUrl);
        return $"{uri.Scheme}://{uri.Host}";
        }

     // ==================== Steam Web API (requires API key) ====================

        public async Task<List<Game>> GetOwnedGamesAsync(string steamId, string apiKey)
      {
var games = new List<Game>();
 try
 {
              var url = $"{SteamApiBaseUrl}/IPlayerService/GetOwnedGames/v1/?key={apiKey}&steamid={steamId}&include_appinfo=true&include_played_free_games=true&format=json";
         var response = await _httpClient.GetStringAsync(url);
     var json = JObject.Parse(response);
                var gameArray = json["response"]?["games"] as JArray;
           
  if (gameArray != null)
       {
   foreach (var item in gameArray)
        {
   var appId = item["appid"]?.ToString();
        var playtime = item["playtime_forever"]?.Value<int>() ?? 0;
var lastPlayed = item["rtime_last_played"]?.Value<long?>() ?? 0;
      
      games.Add(new Game
       {
    ExternalId = appId,
           Name = item["name"]?.ToString() ?? "Unknown Game",
              CoverImageUrl = $"{SteamCdnBaseUrl}/{appId}/header.jpg",
   BackgroundImageUrl = $"{SteamCdnBaseUrl}/{appId}/page_bg_generated_v6b.jpg",
        Platform = GamePlatform.Steam,
         PlaytimeMinutes = playtime,
               LastPlayed = lastPlayed > 0 ? DateTimeOffset.FromUnixTimeSeconds(lastPlayed).DateTime : null
         });
       }
    }
 }
        catch (Exception ex)
            {
       Console.WriteLine($"Error getting Steam games: {ex.Message}");
      }
    return games;
   }

        public async Task<Game?> GetGameDetailsAsync(string appId)
        {
try
        {
         var url = $"{SteamStoreBaseUrl}/appdetails?appids={appId}";
         var response = await _httpClient.GetStringAsync(url);
   var json = JObject.Parse(response);
     var gameData = json[appId]?["data"];
                
 if (gameData == null) return null;

      var game = new Game
        {
   ExternalId = appId,
       Name = gameData["name"]?.ToString() ?? "Unknown",
      Description = gameData["short_description"]?.ToString(),
  CoverImageUrl = gameData["header_image"]?.ToString(),
 BackgroundImageUrl = gameData["background"]?.ToString(),
Platform = GamePlatform.Steam,
    Website = gameData["website"]?.ToString()
      };
      
     var releaseDate = gameData["release_date"];
                if (releaseDate != null && releaseDate["coming_soon"]?.Value<bool>() == false)
                {
      if (DateTime.TryParse(releaseDate["date"]?.ToString(), out var date))
          game.ReleaseDate = date;
          }
         
      var genres = gameData["genres"] as JArray;
          if (genres != null)
 game.Genres = string.Join(", ", genres.Select(g => g["description"]?.ToString()));
   
             var developers = gameData["developers"] as JArray;
                if (developers != null)
                 game.Developers = string.Join(", ", developers.Select(d => d.ToString()));
  
   var publishers = gameData["publishers"] as JArray;
         if (publishers != null)
       game.Publishers = string.Join(", ", publishers.Select(p => p.ToString()));
   
    var metacritic = gameData["metacritic"];
             if (metacritic != null)
        game.MetacriticScore = metacritic["score"]?.Value<double?>();

  // Screenshots
          var screenshots = gameData["screenshots"] as JArray;
      if (screenshots != null)
             {
    var screenshotUrls = screenshots
        .Select(s => s["path_full"]?.ToString())
          .Where(s => s != null)
            .Take(10)
          .ToList();
             if (screenshotUrls.Count > 0)
 game.Screenshots = JsonConvert.SerializeObject(screenshotUrls);
}

    return game;
            }
   catch (Exception ex)
          {
  Console.WriteLine($"Error getting Steam game details: {ex.Message}");
    return null;
     }
}

        public async Task<List<Game>> GetRecentlyPlayedAsync(string steamId, string apiKey, int count = 10)
  {
            var games = new List<Game>();
          try
   {
   var url = $"{SteamApiBaseUrl}/IPlayerService/GetRecentlyPlayedGames/v1/?key={apiKey}&steamid={steamId}&count={count}&format=json";
  var response = await _httpClient.GetStringAsync(url);
    var json = JObject.Parse(response);
         var gameArray = json["response"]?["games"] as JArray;
          
       if (gameArray != null)
          {
   foreach (var item in gameArray)
          {
          var appId = item["appid"]?.ToString();
  games.Add(new Game
          {
       ExternalId = appId,
   Name = item["name"]?.ToString() ?? "Unknown Game",
  CoverImageUrl = $"{SteamCdnBaseUrl}/{appId}/header.jpg",
     Platform = GamePlatform.Steam,
              PlaytimeMinutes = item["playtime_forever"]?.Value<int>() ?? 0
    });
  }
       }
  }
     catch (Exception ex)
        {
   Console.WriteLine($"Error getting recently played: {ex.Message}");
  }
            return games;
   }

     public async Task<SteamProfile?> GetPlayerSummaryAsync(string steamId, string apiKey)
        {
       try
            {
 var url = $"{SteamApiBaseUrl}/ISteamUser/GetPlayerSummaries/v2/?key={apiKey}&steamids={steamId}&format=json";
   var response = await _httpClient.GetStringAsync(url);
      var json = JObject.Parse(response);
          var players = json["response"]?["players"] as JArray;
                
      if (players?.Count > 0)
          {
           var player = players[0];
               return new SteamProfile
 {
      SteamId = player["steamid"]?.ToString() ?? "",
   PersonaName = player["personaname"]?.ToString() ?? "Unknown",
         AvatarUrl = player["avatarfull"]?.ToString(),
     ProfileUrl = player["profileurl"]?.ToString(),
          IsOnline = player["personastate"]?.Value<int>() > 0,
            CurrentGame = player["gameextrainfo"]?.ToString()
     };
        }
            }
      catch (Exception ex)
       {
             Console.WriteLine($"Error getting player summary: {ex.Message}");
            }
            return null;
        }

        /// <summary>
     /// Get player summary using only Steam ID (no API key required for public profiles)
  /// Falls back to basic info if profile is private
        /// </summary>
        public async Task<SteamProfile?> GetPlayerSummaryPublicAsync(string steamId)
     {
        try
            {
     // Try to get public profile info from Steam Community
     var url = $"https://steamcommunity.com/profiles/{steamId}/?xml=1";
       var response = await _httpClient.GetStringAsync(url);
              
 // Parse basic XML response
    if (response.Contains("<steamID64>"))
      {
     var personaMatch = System.Text.RegularExpressions.Regex.Match(response, @"<steamID><!\[CDATA\[(.*?)\]\]></steamID>");
        var avatarMatch = System.Text.RegularExpressions.Regex.Match(response, @"<avatarFull><!\[CDATA\[(.*?)\]\]></avatarFull>");
     var onlineMatch = System.Text.RegularExpressions.Regex.Match(response, @"<onlineState>(.*?)</onlineState>");
             
      return new SteamProfile
                {
               SteamId = steamId,
          PersonaName = personaMatch.Success ? personaMatch.Groups[1].Value : "Steam User",
             AvatarUrl = avatarMatch.Success ? avatarMatch.Groups[1].Value : null,
         ProfileUrl = $"https://steamcommunity.com/profiles/{steamId}",
     IsOnline = onlineMatch.Success && onlineMatch.Groups[1].Value == "online"
 };
    }
     }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting public profile: {ex.Message}");
     }
       
  // Return basic profile if we couldn't get details
       return new SteamProfile
            {
       SteamId = steamId,
        PersonaName = "Steam User",
         ProfileUrl = $"https://steamcommunity.com/profiles/{steamId}"
   };
        }

        public async Task<List<WishlistItem>> GetWishlistAsync(string steamId)
   {
    var wishlist = new List<WishlistItem>();
   try
            {
    var page = 0;
        while (true)
          {
  var url = $"https://store.steampowered.com/wishlist/profiles/{steamId}/wishlistdata/?p={page}";
                  var response = await _httpClient.GetStringAsync(url);
            
       if (string.IsNullOrEmpty(response) || response == "[]" || response == "{}") break;
        
    var json = JObject.Parse(response);
      if (json.Count == 0) break;

 foreach (var prop in json.Properties())
           {
       var appId = prop.Name;
      var item = prop.Value;
     
            var wishlistItem = new WishlistItem
    {
     ExternalId = appId,
     GameName = item["name"]?.ToString() ?? "Unknown",
  CoverImageUrl = $"{SteamCdnBaseUrl}/{appId}/header.jpg",
        PreferredPlatform = GamePlatform.Steam
  };
  
         var subs = item["subs"] as JArray;
            if (subs?.Count > 0)
               wishlistItem.CurrentPrice = subs[0]["price"]?.Value<decimal>() / 100;
          
        wishlist.Add(wishlistItem);
          }
   
       page++;
if (page > 10) break;
     }
      }
 catch (Exception ex)
            {
              Console.WriteLine($"Error getting wishlist: {ex.Message}");
        }
            return wishlist;
        }

        public async Task<string?> ResolveVanityUrlAsync(string vanityUrl, string apiKey)
        {
            try
 {
           var url = $"{SteamApiBaseUrl}/ISteamUser/ResolveVanityURL/v1/?key={apiKey}&vanityurl={vanityUrl}";
   var response = await _httpClient.GetStringAsync(url);
       var json = JObject.Parse(response);
    
          if (json["response"]?["success"]?.Value<int>() == 1)
             return json["response"]?["steamid"]?.ToString();
            }
   catch (Exception ex)
            {
 Console.WriteLine($"Error resolving vanity URL: {ex.Message}");
}
            return null;
        }

        public async Task<int> SyncLibraryAsync(string steamId, string apiKey)
        {
       var games = await GetOwnedGamesAsync(steamId, apiKey);
        if (games.Count > 0)
      {
          await DatabaseService.Instance.SaveGamesAsync(games);
    var userData = await DatabaseService.Instance.GetUserDataAsync();
    userData.SteamLastSync = DateTime.UtcNow;
  await DatabaseService.Instance.SaveUserDataAsync(userData);
            }
   return games.Count;
        }

      /// <summary>
     /// Sync library using only Steam ID (limited - only works for public profiles with public game details)
        /// </summary>
        public async Task<int> SyncLibraryPublicAsync(string steamId)
        {
 // Note: Without API key, we can only get limited info from public profiles
  // The full game list requires an API key
            // For now, we'll just sync wishlist which is often public
    var wishlist = await GetWishlistAsync(steamId);
            
      var games = wishlist.Select(w => new Game
   {
     ExternalId = w.ExternalId,
          Name = w.GameName,
       CoverImageUrl = w.CoverImageUrl,
   Platform = GamePlatform.Steam,
         IsWishlisted = true,
      DateAdded = DateTime.UtcNow
        }).ToList();

       if (games.Count > 0)
        {
   await DatabaseService.Instance.SaveGamesAsync(games);
     var userData = await DatabaseService.Instance.GetUserDataAsync();
                userData.SteamLastSync = DateTime.UtcNow;
         await DatabaseService.Instance.SaveUserDataAsync(userData);
      }
    
            return games.Count;
        }
    }

    public class SteamProfile
    {
        public string SteamId { get; set; } = string.Empty;
        public string PersonaName { get; set; } = string.Empty;
 public string? AvatarUrl { get; set; }
        public string? ProfileUrl { get; set; }
        public bool IsOnline { get; set; }
public string? CurrentGame { get; set; }
    }

    public class SteamOpenIdResult
    {
        public string SteamId { get; set; } = string.Empty;
        public bool Success { get; set; }
  }
}
