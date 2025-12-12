using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using gaming_hub.Models;

namespace gaming_hub.Services
{
    public class GameApiService
    {
   private static GameApiService? _instance;
        private readonly HttpClient _httpClient;
     private const string RawgBaseUrl = "https://api.rawg.io/api";
        private const string CheapSharkBaseUrl = "https://www.cheapshark.com/api/1.0";
        private string _rawgApiKey = "YOUR_RAWG_API_KEY";

   public static GameApiService Instance => _instance ??= new GameApiService();

        private GameApiService()
  {
   _httpClient = new HttpClient();
      _httpClient.DefaultRequestHeaders.Add("User-Agent", "GamingHub-iOS/1.0");
        }

 public void SetRawgApiKey(string apiKey) => _rawgApiKey = apiKey;

        public async Task<List<Game>> SearchGamesAsync(string query, int page = 1, int pageSize = 20)
        {
          var games = new List<Game>();
try
            {
     var url = $"{RawgBaseUrl}/games?key={_rawgApiKey}&search={Uri.EscapeDataString(query)}&page={page}&page_size={pageSize}";
   var response = await _httpClient.GetStringAsync(url);
          var json = JObject.Parse(response);
            var results = json["results"] as JArray;
         if (results != null)
    {
   foreach (var item in results)
     games.Add(ParseRawgGame(item));
            }
       }
          catch (Exception ex)
        {
 Console.WriteLine($"Error searching games: {ex.Message}");
         }
    return games;
        }

        public async Task<Game?> GetGameDetailsAsync(string rawgId)
   {
     try
     {
        var url = $"{RawgBaseUrl}/games/{rawgId}?key={_rawgApiKey}";
         var response = await _httpClient.GetStringAsync(url);
 return ParseRawgGameDetails(JObject.Parse(response));
 }
     catch (Exception ex)
         {
          Console.WriteLine($"Error getting game details: {ex.Message}");
       return null;
        }
    }

        public async Task<List<UpcomingRelease>> GetUpcomingReleasesAsync(int page = 1, int pageSize = 20)
     {
            var releases = new List<UpcomingRelease>();
   try
        {
    var today = DateTime.Today.ToString("yyyy-MM-dd");
 var futureDate = DateTime.Today.AddMonths(12).ToString("yyyy-MM-dd");
     var url = $"{RawgBaseUrl}/games?key={_rawgApiKey}&dates={today},{futureDate}&ordering=released&page={page}&page_size={pageSize}";
       var response = await _httpClient.GetStringAsync(url);
         var json = JObject.Parse(response);
  var results = json["results"] as JArray;
    if (results != null)
          {
 foreach (var item in results)
{
       var release = new UpcomingRelease
   {
      ExternalId = item["id"]?.ToString() ?? "",
       GameName = item["name"]?.ToString() ?? "Unknown",
           CoverImageUrl = item["background_image"]?.ToString(),
       IsExactDate = true
        };
           if (DateTime.TryParse(item["released"]?.ToString(), out var releaseDate))
 release.ReleaseDate = releaseDate;
        var platforms = item["platforms"] as JArray;
if (platforms != null)
       release.Platforms = string.Join(", ", platforms.Select(p => p["platform"]?["name"]?.ToString()).Where(n => n != null));
    var genres = item["genres"] as JArray;
     if (genres != null)
           release.Genres = string.Join(", ", genres.Select(g => g["name"]?.ToString()).Where(n => n != null));
releases.Add(release);
    }
           }
       }
     catch (Exception ex)
  {
         Console.WriteLine($"Error getting upcoming releases: {ex.Message}");
}
return releases;
        }

    private Game ParseRawgGame(JToken item)
        {
         var game = new Game
  {
       ExternalId = item["id"]?.ToString(),
   Name = item["name"]?.ToString() ?? "Unknown",
     CoverImageUrl = item["background_image"]?.ToString(),
          Platform = GamePlatform.Manual,
            MetacriticScore = item["metacritic"]?.Value<double?>()
            };
      if (DateTime.TryParse(item["released"]?.ToString(), out var releaseDate))
         game.ReleaseDate = releaseDate;
            var genres = item["genres"] as JArray;
          if (genres != null)
    game.Genres = string.Join(", ", genres.Select(g => g["name"]?.ToString()));
            return game;
    }

        private Game ParseRawgGameDetails(JObject json)
        {
      var game = new Game
          {
      ExternalId = json["id"]?.ToString(),
     Name = json["name"]?.ToString() ?? "Unknown",
       Description = json["description_raw"]?.ToString(),
                CoverImageUrl = json["background_image"]?.ToString(),
    BackgroundImageUrl = json["background_image_additional"]?.ToString() ?? json["background_image"]?.ToString(),
       Platform = GamePlatform.Manual,
        MetacriticScore = json["metacritic"]?.Value<double?>(),
            UserRating = json["rating"]?.Value<double?>()
            };
            if (DateTime.TryParse(json["released"]?.ToString(), out var releaseDate))
       game.ReleaseDate = releaseDate;
            var genres = json["genres"] as JArray;
    if (genres != null)
game.Genres = string.Join(", ", genres.Select(g => g["name"]?.ToString()));
            var developers = json["developers"] as JArray;
      if (developers != null)
          game.Developers = string.Join(", ", developers.Select(d => d["name"]?.ToString()));
  var publishers = json["publishers"] as JArray;
     if (publishers != null)
                game.Publishers = string.Join(", ", publishers.Select(p => p["name"]?.ToString()));
            return game;
  }

        // Game Deals (CheapShark)
        public async Task<List<GameDeal>> GetDealsAsync(int pageNumber = 0, int pageSize = 60, string? storeId = null)
        {
  var deals = new List<GameDeal>();
       try
   {
              var url = $"{CheapSharkBaseUrl}/deals?pageNumber={pageNumber}&pageSize={pageSize}&onSale=1";
       if (!string.IsNullOrEmpty(storeId))
            url += $"&storeID={storeId}";
       var response = await _httpClient.GetStringAsync(url);
          var json = JArray.Parse(response);
      foreach (var item in json)
                {
              deals.Add(new GameDeal
     {
       DealId = item["dealID"]?.ToString() ?? "",
          GameName = item["title"]?.ToString() ?? "Unknown",
  GameImageUrl = item["thumb"]?.ToString(),
   StoreName = GetStoreName(item["storeID"]?.ToString()),
         OriginalPrice = item["normalPrice"]?.Value<decimal>() ?? 0,
    SalePrice = item["salePrice"]?.Value<decimal>() ?? 0,
          DiscountPercent = (int)(item["savings"]?.Value<double>() ?? 0),
    DealUrl = $"https://www.cheapshark.com/redirect?dealID={item["dealID"]}",
     MetacriticScore = item["metacriticScore"]?.Value<double?>(),
       SteamRating = item["steamRatingPercent"]?.Value<double?>()
           });
      }
         }
            catch (Exception ex)
   {
    Console.WriteLine($"Error getting deals: {ex.Message}");
    }
            return deals;
        }

        public async Task<List<GameDeal>> SearchDealsAsync(string query)
        {
         var deals = new List<GameDeal>();
 try
     {
  var url = $"{CheapSharkBaseUrl}/deals?title={Uri.EscapeDataString(query)}&onSale=1";
       var response = await _httpClient.GetStringAsync(url);
var json = JArray.Parse(response);
      foreach (var item in json)
      {
        deals.Add(new GameDeal
    {
   DealId = item["dealID"]?.ToString() ?? "",
     GameName = item["title"]?.ToString() ?? "Unknown",
    GameImageUrl = item["thumb"]?.ToString(),
        StoreName = GetStoreName(item["storeID"]?.ToString()),
    OriginalPrice = item["normalPrice"]?.Value<decimal>() ?? 0,
     SalePrice = item["salePrice"]?.Value<decimal>() ?? 0,
   DiscountPercent = (int)(item["savings"]?.Value<double>() ?? 0),
   DealUrl = $"https://www.cheapshark.com/redirect?dealID={item["dealID"]}",
      MetacriticScore = item["metacriticScore"]?.Value<double?>(),
            SteamRating = item["steamRatingPercent"]?.Value<double?>()
    });
 }
       }
    catch (Exception ex)
            {
            Console.WriteLine($"Error searching deals: {ex.Message}");
          }
            return deals;
        }

        private static string GetStoreName(string? storeId) => storeId switch
        {
            "1" => "Steam", "2" => "GamersGate", "3" => "GreenManGaming", "7" => "GOG",
            "8" => "Origin", "11" => "Humble Store", "13" => "Uplay", "15" => "Fanatical",
 "21" => "WinGameStore", "23" => "GameBillet", "24" => "Voidu", "25" => "Epic Games",
        "27" => "Games Planet", "28" => "Games Load", "29" => "2Game", "30" => "IndieGala",
            "31" => "Blizzard", "33" => "DLGamer", "34" => "Noctre", "35" => "DreamGame",
      _ => "Unknown Store"
        };
    }
}
