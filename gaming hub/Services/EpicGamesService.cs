using Newtonsoft.Json.Linq;
using gaming_hub.Models;

namespace gaming_hub.Services
{
    public class EpicGamesService
 {
     private static EpicGamesService? _instance;
        private readonly HttpClient _httpClient;

        // Epic Games OAuth endpoints
        private const string EpicAuthUrl = "https://www.epicgames.com/id/authorize";
        private const string EpicTokenUrl = "https://account-public-service-prod.ol.epicgames.com/account/api/oauth/token";
        private const string EpicAccountUrl = "https://account-public-service-prod.ol.epicgames.com/account/api/public/account";
        private const string EpicLibraryUrl = "https://library-service.live.use1a.on.epicgames.com/library/api/public/items";
        
     // Client credentials (public launcher client)
        private const string ClientId = "34a02cf8f4414e29b15921876da36f9a";
        private const string ClientSecret = "daafbccc737745039dffe53d94fc76cf";

        public static EpicGamesService Instance => _instance ??= new EpicGamesService();

        private EpicGamesService()
        {
         _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "GamingHub-iOS/1.0");
        }

        public string GetAuthorizationUrl(string redirectUri)
        {
         return $"{EpicAuthUrl}?client_id={ClientId}&response_type=code&scope=basic_profile&redirect_uri={Uri.EscapeDataString(redirectUri)}";
        }

        public async Task<EpicAuthResult?> ExchangeCodeForTokenAsync(string authCode)
        {
            try
         {
    var request = new HttpRequestMessage(HttpMethod.Post, EpicTokenUrl);
              var authHeader = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
   request.Headers.Add("Authorization", $"Basic {authHeader}");
       
     request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
      {
       { "grant_type", "authorization_code" },
 { "code", authCode }
         });

     var response = await _httpClient.SendAsync(request);
  var content = await response.Content.ReadAsStringAsync();
            
if (response.IsSuccessStatusCode)
  {
         var json = JObject.Parse(content);
     return new EpicAuthResult
    {
      AccessToken = json["access_token"]?.ToString() ?? "",
                  RefreshToken = json["refresh_token"]?.ToString(),
               AccountId = json["account_id"]?.ToString() ?? "",
  DisplayName = json["displayName"]?.ToString(),
        ExpiresIn = json["expires_in"]?.Value<int>() ?? 3600
         };
          }
 }
            catch (Exception ex)
            {
    Console.WriteLine($"Error exchanging Epic code: {ex.Message}");
            }
      return null;
        }

        public async Task<EpicAuthResult?> RefreshTokenAsync(string refreshToken)
        {
            try
   {
          var request = new HttpRequestMessage(HttpMethod.Post, EpicTokenUrl);
    var authHeader = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
      request.Headers.Add("Authorization", $"Basic {authHeader}");
      
     request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
   {
                  { "grant_type", "refresh_token" },
        { "refresh_token", refreshToken }
              });

     var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
        
         if (response.IsSuccessStatusCode)
        {
    var json = JObject.Parse(content);
        return new EpicAuthResult
   {
  AccessToken = json["access_token"]?.ToString() ?? "",
      RefreshToken = json["refresh_token"]?.ToString(),
     AccountId = json["account_id"]?.ToString() ?? "",
  DisplayName = json["displayName"]?.ToString(),
  ExpiresIn = json["expires_in"]?.Value<int>() ?? 3600
        };
           }
     }
         catch (Exception ex)
   {
          Console.WriteLine($"Error refreshing Epic token: {ex.Message}");
   }
      return null;
 }

        public async Task<EpicProfile?> GetAccountInfoAsync(string accessToken, string accountId)
   {
     try
            {
      var request = new HttpRequestMessage(HttpMethod.Get, $"{EpicAccountUrl}/{accountId}");
     request.Headers.Add("Authorization", $"Bearer {accessToken}");
         
      var response = await _httpClient.SendAsync(request);
          var content = await response.Content.ReadAsStringAsync();
                
     if (response.IsSuccessStatusCode)
  {
         var json = JObject.Parse(content);
          return new EpicProfile
      {
      AccountId = json["id"]?.ToString() ?? "",
          DisplayName = json["displayName"]?.ToString() ?? "Unknown",
                  Email = json["email"]?.ToString()
  };
                }
            }
         catch (Exception ex)
      {
        Console.WriteLine($"Error getting Epic account info: {ex.Message}");
       }
            return null;
   }

        public async Task<List<Game>> GetLibraryAsync(string accessToken)
        {
var games = new List<Game>();
   try
       {
       var request = new HttpRequestMessage(HttpMethod.Get, $"{EpicLibraryUrl}?includeMetadata=true");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        
  var response = await _httpClient.SendAsync(request);
           var content = await response.Content.ReadAsStringAsync();
     
      if (response.IsSuccessStatusCode)
         {
                    var json = JObject.Parse(content);
     var records = json["records"] as JArray;
      
      if (records != null)
              {
      foreach (var item in records)
  {
       var appName = item["appName"]?.ToString();
      var metadata = item["metadata"];
      
      games.Add(new Game
   {
   ExternalId = appName,
   Name = metadata?["title"]?.ToString() ?? item["productName"]?.ToString() ?? "Unknown",
      CoverImageUrl = GetEpicImageUrl(metadata),
     Platform = GamePlatform.Epic,
        DateAdded = DateTime.UtcNow
        });
        }
                  }
    }
     }
            catch (Exception ex)
 {
 Console.WriteLine($"Error getting Epic library: {ex.Message}");
      }
        return games;
        }

 private string? GetEpicImageUrl(JToken? metadata)
        {
       if (metadata == null) return null;
   
 var keyImages = metadata["keyImages"] as JArray;
      if (keyImages != null)
   {
                // Prefer OfferImageWide or DieselGameBoxTall
       var preferred = keyImages.FirstOrDefault(i => 
    i["type"]?.ToString() == "OfferImageWide" || 
           i["type"]?.ToString() == "DieselGameBoxTall" ||
        i["type"]?.ToString() == "Thumbnail");
      
  return preferred?["url"]?.ToString() ?? keyImages.FirstOrDefault()?["url"]?.ToString();
            }
     return null;
        }

   public async Task<int> SyncLibraryAsync(string accessToken)
        {
     var games = await GetLibraryAsync(accessToken);
            if (games.Count > 0)
   {
  await DatabaseService.Instance.SaveGamesAsync(games);
            var userData = await DatabaseService.Instance.GetUserDataAsync();
   userData.EpicLastSync = DateTime.UtcNow;
            await DatabaseService.Instance.SaveUserDataAsync(userData);
          }
            return games.Count;
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
