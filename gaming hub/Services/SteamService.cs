using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using gaming_hub.Models;

namespace gaming_hub.Services
{
    public class SteamService
    {
    private static SteamService? _instance;
        private readonly HttpClient _httpClient;
   private const string SteamApiBaseUrl = "https://api.steampowered.com";
        private const string SteamStoreBaseUrl = "https://store.steampowered.com/api";
     private const string SteamCdnBaseUrl = "https://steamcdn-a.akamaihd.net/steam/apps";

        public static SteamService Instance => _instance ??= new SteamService();

     private SteamService()
        {
_httpClient = new HttpClient();
 _httpClient.DefaultRequestHeaders.Add("User-Agent", "GamingHub-iOS/1.0");
      }

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
  Platform = GamePlatform.Steam
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
}
