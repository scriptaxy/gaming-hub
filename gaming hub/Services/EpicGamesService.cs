using Newtonsoft.Json.Linq;
using gaming_hub.Models;

namespace gaming_hub.Services
{
    public class EpicGamesService
 {
        private static EpicGamesService? _instance;
     private readonly HttpClient _httpClient;

  // Epic Games Store GraphQL endpoint (public, no auth required for catalog)
        private const string EpicGraphqlUrl = "https://graphql.epicgames.com/graphql";
        private const string EpicStoreUrl = "https://store-site-backend-static.ak.epicgames.com/freeGamesPromotions";
        
   // Note: Epic Games doesn't have a public OAuth API for third-party apps
        // Users need to manually add their Epic games or use the store browser
        // The auth flow below is for reference but requires Epic Partner registration
        private const string EpicAuthUrl = "https://www.epicgames.com/id/authorize";
   private const string EpicTokenUrl = "https://account-public-service-prod.ol.epicgames.com/account/api/oauth/token";
        
    public static EpicGamesService Instance => _instance ??= new EpicGamesService();

        private EpicGamesService()
        {
       _httpClient = new HttpClient();
      _httpClient.DefaultRequestHeaders.Add("User-Agent", "Synktra-iOS/1.0");
        }

 public string GetAuthorizationUrl(string redirectUri)
 {
        // Note: This requires a registered Epic application
   // Users should manually add Epic games instead
     return $"{EpicAuthUrl}?response_type=code&scope=basic_profile&redirect_uri={Uri.EscapeDataString(redirectUri)}";
        }

  public async Task<EpicAuthResult?> ExchangeCodeForTokenAsync(string authCode)
   {
            // Epic requires registered application credentials
            // Return null to show error - users should add games manually
       Console.WriteLine("Epic Games OAuth requires a registered application. Please add games manually.");
    return null;
        }

        public async Task<EpicAuthResult?> RefreshTokenAsync(string refreshToken)
   {
    return null;
        }

  public async Task<EpicProfile?> GetAccountInfoAsync(string accessToken, string accountId)
{
         // Without valid OAuth, we can't get account info
      // Return a placeholder if we have stored account info
   if (!string.IsNullOrEmpty(accountId))
            {
         return new EpicProfile
       {
         AccountId = accountId,
   DisplayName = "Epic User",
    Email = null
 };
     }
    return null;
        }

      /// <summary>
        /// Get free games currently available on Epic Games Store (no auth required)
        /// </summary>
    public async Task<List<Game>> GetFreeGamesAsync()
   {
       var games = new List<Game>();
    try
     {
       var response = await _httpClient.GetStringAsync(EpicStoreUrl);
           var json = JObject.Parse(response);
    var elements = json["data"]?["Catalog"]?["searchStore"]?["elements"] as JArray;
  
     if (elements != null)
   {
          foreach (var item in elements)
     {
     var title = item["title"]?.ToString();
       var currentPrice = item["price"]?["totalPrice"]?["discountPrice"]?.Value<int>() ?? -1;
       
  // Only include actually free games
    if (currentPrice == 0 && !string.IsNullOrEmpty(title))
       {
       var keyImages = item["keyImages"] as JArray;
 var imageUrl = keyImages?.FirstOrDefault(i => 
         i["type"]?.ToString() == "OfferImageWide" ||
            i["type"]?.ToString() == "DieselStoreFrontWide" ||
       i["type"]?.ToString() == "Thumbnail")?["url"]?.ToString();
 
   games.Add(new Game
        {
          ExternalId = item["id"]?.ToString(),
  Name = title,
 Description = item["description"]?.ToString(),
        CoverImageUrl = imageUrl ?? keyImages?.FirstOrDefault()?["url"]?.ToString(),
          Platform = GamePlatform.Epic,
        DateAdded = DateTime.UtcNow
      });
       }
        }
  }
 }
       catch (Exception ex)
 {
         Console.WriteLine($"Error getting free Epic games: {ex.Message}");
      }
          return games;
   }

   /// <summary>
    /// Search Epic Games Store catalog (no auth required)
        /// </summary>
        public async Task<List<Game>> SearchCatalogAsync(string query)
        {
            var games = new List<Game>();
      try
   {
  // Use the Epic Store search API instead of GraphQL
          var searchUrl = $"https://store-site-backend-static-ipv4.ak.epicgames.com/freeGamesPromotions?locale=en-US&country=US&allowCountries=US";
        var response = await _httpClient.GetStringAsync(searchUrl);
   var json = JObject.Parse(response);
                var elements = json["data"]?["Catalog"]?["searchStore"]?["elements"] as JArray;

  var searchLower = query.ToLowerInvariant();

    if (elements != null)
    {
     foreach (var item in elements)
  {
  var title = item["title"]?.ToString();
     if (string.IsNullOrEmpty(title)) continue;

            // Filter by search query
         if (!title.ToLowerInvariant().Contains(searchLower)) continue;

        var keyImages = item["keyImages"] as JArray;
       var imageUrl = keyImages?.FirstOrDefault(i =>
 i["type"]?.ToString() == "OfferImageWide" ||
   i["type"]?.ToString() == "DieselStoreFrontWide" ||
        i["type"]?.ToString() == "Thumbnail")?["url"]?.ToString();

        games.Add(new Game
                    {
        ExternalId = item["id"]?.ToString(),
  Name = title,
   Description = item["description"]?.ToString(),
       CoverImageUrl = imageUrl ?? keyImages?.FirstOrDefault()?["url"]?.ToString(),
      Platform = GamePlatform.Epic,
        DateAdded = DateTime.UtcNow
       });
}
          }

        // If no results from promotions, try searching CheapShark for Epic games
             if (games.Count == 0)
     {
      var cheapSharkUrl = $"https://www.cheapshark.com/api/1.0/deals?title={Uri.EscapeDataString(query)}&storeID=25&pageSize=20";
       var csResponse = await _httpClient.GetStringAsync(cheapSharkUrl);
           var csJson = JArray.Parse(csResponse);

      foreach (var item in csJson)
          {
        var title = item["title"]?.ToString();
   if (string.IsNullOrEmpty(title)) continue;

    games.Add(new Game
            {
          ExternalId = item["gameID"]?.ToString(),
      Name = title,
         CoverImageUrl = item["thumb"]?.ToString(),
    Platform = GamePlatform.Epic,
  DateAdded = DateTime.UtcNow
             });
        }
             }
         }
  catch (Exception ex)
            {
          Console.WriteLine($"Error searching Epic catalog: {ex.Message}");
         }
   return games;
     }

   public async Task<List<Game>> GetLibraryAsync(string accessToken)
      {
// Without valid OAuth, return empty list
  // Suggest users to manually add their Epic games
     return [];
        }

        public async Task<int> SyncLibraryAsync(string accessToken)
        {
// Epic requires registered OAuth - can't sync library automatically
            // Instead, sync free games as a demonstration
         var freeGames = await GetFreeGamesAsync();
    if (freeGames.Count > 0)
    {
           await DatabaseService.Instance.SaveGamesAsync(freeGames);
      var userData = await DatabaseService.Instance.GetUserDataAsync();
  userData.EpicLastSync = DateTime.UtcNow;
      await DatabaseService.Instance.SaveUserDataAsync(userData);
        }
  return freeGames.Count;
        }
  }

    public class EpicAuthResult
    {
     public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public string AccountId { get; set; } = string.Empty;
  public string? DisplayName { get; set; }
    public int ExpiresIn { get; set; }
    }

    public class EpicProfile
    {
        public string AccountId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
    }
}
