using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using gaming_hub.Models;

namespace gaming_hub.Services
{
    public class GameApiService
    {
        private static GameApiService? _instance;
        private readonly HttpClient _httpClient;

        private const string CheapSharkBaseUrl = "https://www.cheapshark.com/api/1.0";
  private const string SteamStoreBaseUrl = "https://store.steampowered.com/api";

        // NSFW/Adult content keywords to filter out
        private static readonly HashSet<string> NsfwKeywords = new(StringComparer.OrdinalIgnoreCase)
 {
      "hentai", "adult", "nsfw", "erotic", "xxx", "porn", "nude", "naked",
      "sex", "sexual", "lewd", "ecchi", "18+", "mature", "uncensored",
          "strip", "sexy", "waifu", "anime girl", "dating sim", "romance",
   "visual novel", "otome", "bishoujo", "galge", "eroge", "nukige",
            "tentacle", "succubus", "harem", "yuri", "yaoi", "futanari",
         "bondage", "bdsm", "fetish", "kinky", "naughty", "seductive",
            "love hotel", "hot spring", "beach girls", "swimsuit", "bikini",
            "maid", "bunny girl", "catgirl", "fox girl", "demon girl",
            "milf", "loli", "shota", "incest", "sister", "mother",
  "girlfriend", "wife", "breeding", "pregnancy", "impregnation"
   };

        // Blocked publishers/developers known for adult content
        private static readonly HashSet<string> NsfwPublishers = new(StringComparer.OrdinalIgnoreCase)
     {
            "Kagura Games", "Denpasoft", "MangaGamer", "JAST USA", "Nutaku",
            "Fakku", "Sekai Project", "Shiravune", "NekoNyan", "Frontwing",
"Winged Cloud", "Dharker Studio", "Playmeow Games", "Critical Bliss"
        };

        public static GameApiService Instance => _instance ??= new GameApiService();

        private GameApiService()
        {
     _httpClient = new HttpClient();
       _httpClient.DefaultRequestHeaders.Add("User-Agent", "Synktra-iOS/1.0");
        }

/// <summary>
   /// Check if a game name or description contains NSFW keywords
        /// </summary>
      private static bool IsNsfw(string? gameName, string? description = null, string? publisher = null)
        {
  if (!string.IsNullOrEmpty(gameName))
          {
       var nameLower = gameName.ToLowerInvariant();
      if (NsfwKeywords.Any(keyword => nameLower.Contains(keyword)))
       return true;
            }

          if (!string.IsNullOrEmpty(description))
  {
        var descLower = description.ToLowerInvariant();
      if (NsfwKeywords.Any(keyword => descLower.Contains(keyword)))
              return true;
     }

  if (!string.IsNullOrEmpty(publisher))
        {
        if (NsfwPublishers.Any(p => publisher.Contains(p, StringComparison.OrdinalIgnoreCase)))
         return true;
    }

            return false;
    }

        /// <summary>
   /// Check if game is likely a quality mainstream game
    /// </summary>
        private static bool IsQualityGame(string? gameName, double? metacritic, double? steamRating)
        {
            if (string.IsNullOrEmpty(gameName)) return false;
 
       // Filter out games with very short names (often asset flips)
         if (gameName.Length < 3) return false;
 
            // If it has a metacritic score, it's likely reviewed/mainstream
            if (metacritic.HasValue && metacritic > 0) return true;
       
        // If it has good Steam reviews, it's likely legitimate
            if (steamRating.HasValue && steamRating > 50) return true;
            
            return true; // Allow by default if no red flags
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
     var deals = await SearchDealsAsync(query);

       foreach (var deal in deals)
    {
        if (seenGames.Contains(deal.GameName)) continue;
  if (IsNsfw(deal.GameName)) continue;
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
        /// Get upcoming game releases - focuses on mainstream games only
        /// </summary>
        public async Task<List<UpcomingRelease>> GetUpcomingReleasesAsync(int page = 1, int pageSize = 20)
        {
            var releases = new List<UpcomingRelease>();
      var seenGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

       // Source 1: Epic Games upcoming free games (most reliable, curated)
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
       var description = item["description"]?.ToString();
     var seller = item["seller"]?["name"]?.ToString();
  
         if (string.IsNullOrEmpty(title) || seenGames.Contains(title)) continue;
         if (IsNsfw(title, description, seller)) continue;

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
          Description = $"Free {startDate.ToLocalTime():MMM d}",
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

            // Source 2: Steam featured coming soon (curated by Valve)
      if (releases.Count < pageSize)
     {
            try
     {
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
     if (IsNsfw(name)) continue;

   seenGames.Add(name);
      var appId = item["id"]?.ToString();
        var finalPrice = item["final_price"]?.Value<int>() ?? 0;
       var priceText = finalPrice > 0 ? $"${finalPrice / 100.0:F2}" : "TBA";

       releases.Add(new UpcomingRelease
           {
            ExternalId = appId ?? "",
 GameName = name,
     CoverImageUrl = item["large_capsule_image"]?.ToString() ??
       $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
         ReleaseDate = DateTime.Today.AddDays(14),
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

            // Source 3: Steam top wishlisted (popular anticipated games)
    if (releases.Count < pageSize)
   {
            try
           {
           var wishlistUrl = "https://store.steampowered.com/api/featuredcategories";
            var response = await _httpClient.GetStringAsync(wishlistUrl);
    var json = JObject.Parse(response);

         var topSellers = json["top_sellers"]?["items"] as JArray;
  if (topSellers != null)
          {
      foreach (var item in topSellers)
              {
   var name = item["name"]?.ToString();
        if (string.IsNullOrEmpty(name) || seenGames.Contains(name)) continue;
     if (IsNsfw(name)) continue;

       // Only add if it looks like a real game
    var discountPercent = item["discount_percent"]?.Value<int>() ?? 0;
            if (discountPercent > 0) // On sale = already released, skip
        continue;

                seenGames.Add(name);
    var appId = item["id"]?.ToString();

         releases.Add(new UpcomingRelease
   {
           ExternalId = appId ?? "",
    GameName = name,
       CoverImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
          ReleaseDate = DateTime.Today.AddDays(30),
         IsExactDate = false,
            Description = "Popular",
            Platforms = "Steam"
            });

      if (releases.Count >= pageSize) break;
                }
               }
          }
             catch (Exception ex)
          {
       Console.WriteLine($"Steam wishlist failed: {ex.Message}");
         }
            }

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
            var url = $"{CheapSharkBaseUrl}/deals?upperPrice=0&pageSize=20";
      var response = await _httpClient.GetStringAsync(url);
        var json = JArray.Parse(response);

   foreach (var item in json)
        {
    var gameName = item["title"]?.ToString() ?? "Unknown";
              if (seenGames.Contains(gameName)) continue;
   if (IsNsfw(gameName)) continue;
  
          var metacritic = item["metacriticScore"]?.Value<double?>();
 var steamRating = item["steamRatingPercent"]?.Value<double?>();
 if (!IsQualityGame(gameName, metacritic, steamRating)) continue;
         
            seenGames.Add(gameName);

     games.Add(new Game
       {
     ExternalId = item["gameID"]?.ToString(),
              Name = gameName,
            CoverImageUrl = item["thumb"]?.ToString(),
           Platform = GamePlatform.Manual,
     MetacriticScore = metacritic,
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
    var gameName = item["title"]?.ToString() ?? "Unknown";
  if (IsNsfw(gameName)) continue;
     
     var metacritic = item["metacriticScore"]?.Value<double?>();
       var steamRating = item["steamRatingPercent"]?.Value<double?>();
            if (!IsQualityGame(gameName, metacritic, steamRating)) continue;

           deals.Add(new GameDeal
    {
      DealId = item["dealID"]?.ToString() ?? "",
       GameName = gameName,
         GameImageUrl = item["thumb"]?.ToString(),
           StoreName = GetStoreName(item["storeID"]?.ToString()),
   OriginalPrice = item["normalPrice"]?.Value<decimal>() ?? 0,
     SalePrice = item["salePrice"]?.Value<decimal>() ?? 0,
      DiscountPercent = (int)(item["savings"]?.Value<double>() ?? 0),
     DealUrl = $"https://www.cheapshark.com/redirect?dealID={item["dealID"]}",
   MetacriticScore = metacritic,
            SteamRating = steamRating
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
   var gameName = item["title"]?.ToString() ?? "Unknown";
       if (IsNsfw(gameName)) continue;

deals.Add(new GameDeal
        {
          DealId = item["dealID"]?.ToString() ?? "",
        GameName = gameName,
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
