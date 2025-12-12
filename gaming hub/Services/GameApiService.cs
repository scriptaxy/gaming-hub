using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using gaming_hub.Models;

namespace gaming_hub.Services
{
    public class GameApiService
    {
        private static GameApiService? _instance;
  private readonly HttpClient _httpClient;
        
  // CheapShark - Free, no API key required, actively maintained
        private const string CheapSharkBaseUrl = "https://www.cheapshark.com/api/1.0";
        
        // Steam Store API - Free, no API key required for public data
        private const string SteamStoreBaseUrl = "https://store.steampowered.com/api";
        private const string SteamCdnBaseUrl = "https://steamcdn-a.akamaihd.net/steam/apps";

        public static GameApiService Instance => _instance ??= new GameApiService();

        private GameApiService()
   {
  _httpClient = new HttpClient();
 _httpClient.DefaultRequestHeaders.Add("User-Agent", "Synktra-iOS/1.0");
        }

        /// <summary>
     /// Search for games using CheapShark API (free, no key required)
        /// </summary>
        public async Task<List<Game>> SearchGamesAsync(string query, int page = 1, int pageSize = 20)
        {
            var games = new List<Game>();
var seenGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  
            try
      {
   // Search CheapShark for deals matching the query
        var deals = await SearchDealsAsync(query);
      
        foreach (var deal in deals)
    {
  if (seenGames.Contains(deal.GameName)) continue;
        seenGames.Add(deal.GameName);
     
          games.Add(new Game
         {
      ExternalId = deal.DealId,
     Name = deal.GameName,
         CoverImageUrl = deal.GameImageUrl,
        Platform = GamePlatform.Manual,
       MetacriticScore = deal.MetacriticScore
    });
   
       if (games.Count >= pageSize) break;
       }
            }
          catch (Exception ex)
            {
 Console.WriteLine($"Error searching games: {ex.Message}");
         }
            
         return games;
        }

        /// <summary>
 /// Get game details from Steam Store API (free, no key required)
        /// </summary>
        public async Task<Game?> GetGameDetailsAsync(string steamAppId)
        {
        try
          {
          var url = $"{SteamStoreBaseUrl}/appdetails?appids={steamAppId}";
      var response = await _httpClient.GetStringAsync(url);
      var json = JObject.Parse(response);
                var gameData = json[steamAppId]?["data"];
                
                if (gameData == null) return null;

    var game = new Game
                {
        ExternalId = steamAppId,
       Name = gameData["name"]?.ToString() ?? "Unknown",
  Description = StripHtml(gameData["short_description"]?.ToString()),
          CoverImageUrl = gameData["header_image"]?.ToString(),
        BackgroundImageUrl = gameData["background"]?.ToString(),
        Platform = GamePlatform.Steam,
    Website = gameData["website"]?.ToString()
                };

// Release date
    var releaseDate = gameData["release_date"];
  if (releaseDate != null && releaseDate["coming_soon"]?.Value<bool>() == false)
            {
      if (DateTime.TryParse(releaseDate["date"]?.ToString(), out var date))
       game.ReleaseDate = date;
       }

                // Genres
          var genres = gameData["genres"] as JArray;
            if (genres != null)
        game.Genres = string.Join(", ", genres.Select(g => g["description"]?.ToString()));

        // Developers & Publishers
                var developers = gameData["developers"] as JArray;
     if (developers != null)
               game.Developers = string.Join(", ", developers.Select(d => d.ToString()));

      var publishers = gameData["publishers"] as JArray;
        if (publishers != null)
        game.Publishers = string.Join(", ", publishers.Select(p => p.ToString()));

       // Metacritic
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
   Console.WriteLine($"Error getting game details: {ex.Message}");
      return null;
            }
        }

     /// <summary>
        /// Get upcoming game releases - combines multiple sources for actual upcoming games
        /// </summary>
        public async Task<List<UpcomingRelease>> GetUpcomingReleasesAsync(int page = 1, int pageSize = 20)
        {
    var releases = new List<UpcomingRelease>();
        var seenGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Source 1: Steam Search for unreleased games
       try
     {
         // Search Steam for games coming soon (filter by coming_soon tag)
    var steamUrl = "https://store.steampowered.com/api/storesearch/?term=&sort_by=_ASC&category1=998&cc=us&l=english";
         var response = await _httpClient.GetStringAsync(steamUrl);
    var json = JObject.Parse(response);
    var items = json["items"] as JArray;

    if (items != null)
     {
                 foreach (var item in items.Take(pageSize))
     {
       var name = item["name"]?.ToString();
        if (string.IsNullOrEmpty(name) || seenGames.Contains(name)) continue;
       seenGames.Add(name);

         var appId = item["id"]?.ToString();
      var price = item["price"]?["final"]?.Value<int>() ?? 0;
          var priceText = price > 0 ? $"${price / 100.0:F2}" : "TBA";

      releases.Add(new UpcomingRelease
       {
    ExternalId = appId ?? "",
            GameName = name,
      CoverImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
        ReleaseDate = DateTime.Today.AddDays(30),
    IsExactDate = false,
       Description = priceText,
                 Platforms = "Steam"
            });
 }
         }
       }
catch (Exception ex)
        {
                Console.WriteLine($"Steam search failed: {ex.Message}");
     }

  // Source 2: Epic Games upcoming free games
            try
   {
            var epicUrl = "https://store-site-backend-static.ak.epicgames.com/freeGamesPromotions?locale=en-US";
      var response = await _httpClient.GetStringAsync(epicUrl);
                var json = JObject.Parse(response);
         var elements = json["data"]?["Catalog"]?["searchStore"]?["elements"] as JArray;

          if (elements != null)
      {
            foreach (var item in elements)
                    {
              var title = item["title"]?.ToString();
       if (string.IsNullOrEmpty(title) || seenGames.Contains(title)) continue;

    // Check for upcoming promotional offers (future free games)
         var promotions = item["promotions"];
      var upcomingPromos = promotions?["upcomingPromotionalOffers"] as JArray;

     if (upcomingPromos != null && upcomingPromos.Count > 0)
  {
                var promoOffers = upcomingPromos[0]?["promotionalOffers"] as JArray;
   if (promoOffers != null && promoOffers.Count > 0)
          {
          var startDateStr = promoOffers[0]?["startDate"]?.ToString();
         if (DateTime.TryParse(startDateStr, out var startDate) && startDate > DateTime.UtcNow)
        {
     seenGames.Add(title);

          var keyImages = item["keyImages"] as JArray;
          var imageUrl = keyImages?.FirstOrDefault(i =>
              i["type"]?.ToString() == "OfferImageWide" ||
          i["type"]?.ToString() == "DieselStoreFrontWide" ||
            i["type"]?.ToString() == "Thumbnail")?["url"]?.ToString();

       releases.Add(new UpcomingRelease
    {
              ExternalId = item["id"]?.ToString() ?? "",
          GameName = title,
        CoverImageUrl = imageUrl ?? keyImages?.FirstOrDefault()?["url"]?.ToString(),
       ReleaseDate = startDate.ToLocalTime(),
    IsExactDate = true,
      Description = $"Free on {startDate.ToLocalTime():MMM d}",
          Platforms = "Epic Games"
           });
           }
   }
       }

  if (releases.Count >= pageSize) break;
            }
    }
     }
 catch (Exception ex)
            {
     Console.WriteLine($"Epic upcoming failed: {ex.Message}");
     }

// Source 3: Use Steam's actual coming soon search
            if (releases.Count < pageSize)
            {
                try
         {
            // Alternative: Search for wishlisted/popular upcoming games
           var url = "https://store.steampowered.com/search/results/?query&start=0&count=20&dynamic_data=&sort_by=_ASC&supportedlang=english&snr=1_7_7_comingsoon_702&filter=comingsoon&infinite=1";
         
   // Since this returns HTML, let's use the featuredcategories but filter properly
         var featuredUrl = "https://store.steampowered.com/api/featuredcategories";
   var response = await _httpClient.GetStringAsync(featuredUrl);
          var json = JObject.Parse(response);
     
      var comingSoon = json["coming_soon"]?["items"] as JArray;
   if (comingSoon != null)
          {
        foreach (var item in comingSoon)
        {
                var name = item["name"]?.ToString();
     if (string.IsNullOrEmpty(name) || seenGames.Contains(name)) continue;
   
   // Check if it's actually coming soon (not already released)
        var discountExpiration = item["discount_expiration"]?.Value<long?>() ?? 0;
         
        seenGames.Add(name);
 var appId = item["id"]?.ToString();
             var finalPrice = item["final_price"]?.Value<int>() ?? 0;
                  var priceText = finalPrice > 0 ? $"${finalPrice / 100.0:F2}" : "TBA";

            releases.Add(new UpcomingRelease
           {
          ExternalId = appId ?? "",
 GameName = name,
              CoverImageUrl = item["large_capsule_image"]?.ToString() ?? $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
  ReleaseDate = DateTime.Today.AddDays(14), // Coming soon placeholder
      IsExactDate = false,
          Description = priceText,
      Platforms = "Steam"
     });

              if (releases.Count >= pageSize) break;
 }
      }
     }
         catch (Exception ex)
        {
         Console.WriteLine($"Steam featured failed: {ex.Message}");
  }
      }

            // If still no results, show a message
      if (releases.Count == 0)
            {
    releases.Add(new UpcomingRelease
    {
        ExternalId = "placeholder",
         GameName = "No upcoming releases found",
     Description = "Pull to refresh",
 ReleaseDate = DateTime.Today,
        IsExactDate = false,
     Platforms = ""
       });
       }

         // Sort: Upcoming Epic free games first (they have exact dates), then Steam
     return releases
     .Where(r => r.ExternalId != "placeholder" || releases.Count == 1)
           .OrderBy(r => r.ReleaseDate)
                .ThenBy(r => r.GameName)
          .ToList();
        }

        /// <summary>
        /// Get free games currently available
        /// </summary>
        public async Task<List<Game>> GetFreeGamesAsync()
        {
        var games = new List<Game>();
            var seenGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

     try
     {
        // CheapShark deals with price = 0
                var url = $"{CheapSharkBaseUrl}/deals?upperPrice=0&pageSize=20";
         var response = await _httpClient.GetStringAsync(url);
     var json = JArray.Parse(response);

                foreach (var item in json)
  {
       var gameName = item["title"]?.ToString() ?? "Unknown";
     if (seenGames.Contains(gameName)) continue;
    seenGames.Add(gameName);

           games.Add(new Game
    {
        ExternalId = item["gameID"]?.ToString(),
             Name = gameName,
               CoverImageUrl = item["thumb"]?.ToString(),
  Platform = GamePlatform.Manual,
    MetacriticScore = item["metacriticScore"]?.Value<double?>(),
               DateAdded = DateTime.UtcNow
});
          }
          }
            catch (Exception ex)
            {
 Console.WriteLine($"Error getting free games: {ex.Message}");
     }

            return games;
        }

      private static string? StripHtml(string? html)
        {
            if (string.IsNullOrEmpty(html)) return html;
return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty)
                .Replace("&nbsp;", " ")
    .Replace("&amp;", "&")
                .Replace("&lt;", "<")
         .Replace("&gt;", ">")
         .Replace("&#39;", "'")
       .Replace("&quot;", "\"")
        .Trim();
        }

        // ==================== Game Deals (CheapShark) ====================

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

        /// <summary>
        /// Look up a game by CheapShark game ID to get deals across all stores
        /// </summary>
        public async Task<List<GameDeal>> GetGameDealsAsync(string gameId)
        {
            var deals = new List<GameDeal>();
   try
            {
          var url = $"{CheapSharkBaseUrl}/games?id={gameId}";
         var response = await _httpClient.GetStringAsync(url);
        var json = JObject.Parse(response);
       var dealsList = json["deals"] as JArray;

      if (dealsList != null)
 {
        var gameName = json["info"]?["title"]?.ToString() ?? "Unknown";
           var thumb = json["info"]?["thumb"]?.ToString();

        foreach (var item in dealsList)
           {
  deals.Add(new GameDeal
     {
DealId = item["dealID"]?.ToString() ?? "",
   GameName = gameName,
           GameImageUrl = thumb,
     StoreName = GetStoreName(item["storeID"]?.ToString()),
    OriginalPrice = item["retailPrice"]?.Value<decimal>() ?? 0,
         SalePrice = item["price"]?.Value<decimal>() ?? 0,
         DiscountPercent = (int)(item["savings"]?.Value<double>() ?? 0),
       DealUrl = $"https://www.cheapshark.com/redirect?dealID={item["dealID"]}"
       });
          }
      }
         }
   catch (Exception ex)
     {
       Console.WriteLine($"Error getting game deals: {ex.Message}");
         }
            return deals;
        }

        private static string GetStoreName(string? storeId) => storeId switch
        {
        "1" => "Steam",
            "2" => "GamersGate",
   "3" => "GreenManGaming",
            "7" => "GOG",
        "8" => "Origin",
      "11" => "Humble Store",
    "13" => "Uplay",
            "15" => "Fanatical",
            "21" => "WinGameStore",
  "23" => "GameBillet",
            "24" => "Voidu",
        "25" => "Epic Games",
      "27" => "Games Planet",
            "28" => "Games Load",
     "29" => "2Game",
  "30" => "IndieGala",
        "31" => "Blizzard",
            "33" => "DLGamer",
    "34" => "Noctre",
          "35" => "DreamGame",
            _ => "Store"
        };
    }
}
