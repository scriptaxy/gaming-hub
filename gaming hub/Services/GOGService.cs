using Newtonsoft.Json.Linq;
using gaming_hub.Models;

namespace gaming_hub.Services
{
    /// <summary>
    /// GOG Galaxy integration service.
    /// Note: GOG doesn't have a public API, so this uses their internal embed API
    /// which provides limited functionality for owned games display.
    /// </summary>
    public class GOGService
    {
      private static GOGService? _instance;
  private readonly HttpClient _httpClient;

 // GOG uses OAuth but doesn't have public API - we use embed/account endpoints
        private const string GogAuthUrl = "https://auth.gog.com/auth";
        private const string GogApiUrl = "https://embed.gog.com";
     private const string GogCatalogUrl = "https://catalog.gog.com/v1";
 
        // GOG Galaxy client credentials (public)
        private const string ClientId = "46899977096215655";
        private const string ClientSecret = "9d85c43b1482497dbbce61f6e4aa173a433796eeae2ca8c5f6129f2dc4de46d9";
        private const string RedirectUri = "https://embed.gog.com/on_login_success?origin=client";

        public static GOGService Instance => _instance ??= new GOGService();

        private GOGService()
        {
            _httpClient = new HttpClient();
         _httpClient.DefaultRequestHeaders.Add("User-Agent", "GOGGalaxyClient/2.0");
        }

        public string GetAuthorizationUrl()
        {
         return $"{GogAuthUrl}?client_id={ClientId}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&response_type=code&layout=client2";
   }

        public async Task<GOGAuthResult?> ExchangeCodeForTokenAsync(string authCode)
        {
          try
            {
          var url = $"https://auth.gog.com/token?client_id={ClientId}&client_secret={ClientSecret}&grant_type=authorization_code&code={authCode}&redirect_uri={Uri.EscapeDataString(RedirectUri)}";
         
    var response = await _httpClient.GetStringAsync(url);
             var json = JObject.Parse(response);

             return new GOGAuthResult
  {
  AccessToken = json["access_token"]?.ToString() ?? "",
    RefreshToken = json["refresh_token"]?.ToString(),
       UserId = json["user_id"]?.ToString() ?? "",
        ExpiresIn = json["expires_in"]?.Value<int>() ?? 3600
            };
   }
          catch (Exception ex)
   {
      Console.WriteLine($"Error exchanging GOG code: {ex.Message}");
       return null;
    }
        }

        public async Task<GOGAuthResult?> RefreshTokenAsync(string refreshToken)
        {
 try
            {
         var url = $"https://auth.gog.com/token?client_id={ClientId}&client_secret={ClientSecret}&grant_type=refresh_token&refresh_token={refreshToken}";
            
    var response = await _httpClient.GetStringAsync(url);
          var json = JObject.Parse(response);

     return new GOGAuthResult
                {
   AccessToken = json["access_token"]?.ToString() ?? "",
    RefreshToken = json["refresh_token"]?.ToString(),
  UserId = json["user_id"]?.ToString() ?? "",
         ExpiresIn = json["expires_in"]?.Value<int>() ?? 3600
            };
  }
         catch (Exception ex)
            {
         Console.WriteLine($"Error refreshing GOG token: {ex.Message}");
         return null;
            }
 }

   public async Task<GOGProfile?> GetUserDataAsync(string accessToken)
        {
      try
            {
      var request = new HttpRequestMessage(HttpMethod.Get, $"{GogApiUrl}/userData.json");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

         var response = await _httpClient.SendAsync(request);
     var content = await response.Content.ReadAsStringAsync();

     if (response.IsSuccessStatusCode)
         {
           var json = JObject.Parse(content);
            return new GOGProfile
           {
     UserId = json["userId"]?.ToString() ?? "",
  Username = json["username"]?.ToString() ?? "GOG User",
  GalaxyUserId = json["galaxyUserId"]?.ToString(),
   Email = json["email"]?.ToString(),
    Avatar = json["avatar"]?.ToString()
          };
 }
            }
 catch (Exception ex)
         {
                Console.WriteLine($"Error getting GOG user data: {ex.Message}");
            }
    return null;
        }

        public async Task<List<Game>> GetOwnedGamesAsync(string accessToken)
     {
    var games = new List<Game>();
            try
     {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{GogApiUrl}/user/data/games");
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

  var response = await _httpClient.SendAsync(request);
      var content = await response.Content.ReadAsStringAsync();

         if (response.IsSuccessStatusCode)
 {
          var json = JObject.Parse(content);
        var owned = json["owned"] as JArray;

       if (owned != null)
        {
   // Get details for each game
         foreach (var gameId in owned.Take(100)) // Limit to prevent rate limiting
             {
   var gameDetails = await GetGameDetailsAsync(gameId.ToString());
        if (gameDetails != null)
   {
         games.Add(gameDetails);
 }
        await Task.Delay(100); // Rate limiting
}
         }
      }
       }
      catch (Exception ex)
            {
     Console.WriteLine($"Error getting GOG games: {ex.Message}");
        }
  return games;
        }

        public async Task<Game?> GetGameDetailsAsync(string gameId)
        {
    try
            {
   var response = await _httpClient.GetStringAsync($"https://api.gog.com/products/{gameId}?expand=description,screenshots,videos");
    var json = JObject.Parse(response);

           var screenshots = new List<string>();
  var screenshotsArray = json["screenshots"] as JArray;
                if (screenshotsArray != null)
 {
        foreach (var ss in screenshotsArray.Take(10))
                    {
             var url = ss["image_id"]?.ToString();
          if (!string.IsNullOrEmpty(url))
            {
screenshots.Add($"https://images.gog.com/{url}.jpg");
        }
  }
           }

          return new Game
        {
       ExternalId = gameId,
         Name = json["title"]?.ToString() ?? "Unknown",
        Description = json["description"]?["lead"]?.ToString(),
    CoverImageUrl = json["images"]?["logo2x"]?.ToString(),
         BackgroundImageUrl = json["images"]?["background"]?.ToString(),
            Platform = GamePlatform.GOG,
   Genres = string.Join(", ", (json["genres"] as JArray)?.Select(g => g["name"]?.ToString()) ?? []),
          Developers = string.Join(", ", (json["developers"] as JArray)?.Select(d => d["name"]?.ToString()) ?? []),
         Publishers = string.Join(", ", (json["publisher"] as JArray)?.Select(p => p["name"]?.ToString()) ?? []),
     Screenshots = Newtonsoft.Json.JsonConvert.SerializeObject(screenshots),
      Website = json["links"]?["product_card"]?.ToString(),
        DateAdded = DateTime.UtcNow
              };
            }
          catch (Exception ex)
            {
     Console.WriteLine($"Error getting GOG game details: {ex.Message}");
  return null;
            }
        }

     public async Task<List<Game>> SearchGamesAsync(string query)
        {
         var games = new List<Game>();
            try
      {
             var response = await _httpClient.GetStringAsync($"https://embed.gog.com/games/ajax/filtered?mediaType=game&search={Uri.EscapeDataString(query)}");
   var json = JObject.Parse(response);
           var products = json["products"] as JArray;

          if (products != null)
     {
   foreach (var item in products.Take(20))
      {
      games.Add(new Game
         {
ExternalId = item["id"]?.ToString(),
        Name = item["title"]?.ToString() ?? "Unknown",
         CoverImageUrl = "https:" + item["image"]?.ToString()?.Replace("_glx_logo", "_product_tile_256"),
  Platform = GamePlatform.GOG,
    ReleaseDate = DateTime.TryParse(item["releaseDate"]?.ToString(), out var date) ? date : null
       });
        }
     }
   }
            catch (Exception ex)
  {
                Console.WriteLine($"Error searching GOG games: {ex.Message}");
      }
            return games;
 }

     public async Task<int> SyncLibraryAsync(string accessToken)
        {
            var games = await GetOwnedGamesAsync(accessToken);
     if (games.Count > 0)
            {
         await DatabaseService.Instance.SaveGamesAsync(games);
          var userData = await DatabaseService.Instance.GetUserDataAsync();
     userData.GogLastSync = DateTime.UtcNow;
    await DatabaseService.Instance.SaveUserDataAsync(userData);
   }
            return games.Count;
        }
    }

    public class GOGAuthResult
    {
        public string AccessToken { get; set; } = string.Empty;
 public string? RefreshToken { get; set; }
     public string UserId { get; set; } = string.Empty;
      public int ExpiresIn { get; set; }
    }

    public class GOGProfile
 {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? GalaxyUserId { get; set; }
        public string? Email { get; set; }
        public string? Avatar { get; set; }
}
}
