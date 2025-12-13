using SQLite;
using gaming_hub.Models;

namespace gaming_hub.Models
{
    public class UserData
    {
     [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
    public string Username { get; set; } = string.Empty;

        // Steam Integration
        public string? SteamId { get; set; }
        public string? SteamApiKey { get; set; }
        public DateTime? SteamLastSync { get; set; }

    // Epic Games Integration
        public string? EpicAccountId { get; set; }
        public string? EpicAccessToken { get; set; }
        public DateTime? EpicTokenExpiry { get; set; }
      public DateTime? EpicLastSync { get; set; }

        // GOG Integration
 public string? GogAccessToken { get; set; }
        public DateTime? GogTokenExpiry { get; set; }
        public DateTime? GogLastSync { get; set; }

// Remote PC Settings
  public string? RemotePCHost { get; set; }
        public int RemotePCPort { get; set; } = 19500;
        public string? RemotePCAuthToken { get; set; }
        public string? RemotePCMacAddress { get; set; }
        public bool RemotePCEnabled { get; set; }

        // App Settings
        public bool DarkModeEnabled { get; set; } = true;
      public bool NotificationsEnabled { get; set; } = true;
        public bool DealAlertsEnabled { get; set; } = true;
        public double DealThreshold { get; set; } = 50;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class GameDeal
    {
      [PrimaryKey, AutoIncrement]
   public int Id { get; set; }
      public string DealId { get; set; } = string.Empty;
 public string GameName { get; set; } = string.Empty;
        public string? GameImageUrl { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string? StoreLogoUrl { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal SalePrice { get; set; }
        public int DiscountPercent { get; set; }
   public string? DealUrl { get; set; }
        public DateTime? DealEndDate { get; set; }
      public bool IsHistoricalLow { get; set; }
        public decimal? HistoricalLowPrice { get; set; }
     public double? MetacriticScore { get; set; }
        public double? SteamRating { get; set; }
        public DateTime DateFound { get; set; } = DateTime.UtcNow;

        [Ignore]
      public string SavingsText => $"Save {DiscountPercent}%";
    [Ignore]
     public string PriceText => SalePrice == 0 ? "FREE" : $"${SalePrice:F2}";
        [Ignore]
        public string OriginalPriceText => $"${OriginalPrice:F2}";
    }

    public class UpcomingRelease
    {
   [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string ExternalId { get; set; } = string.Empty;
      public string GameName { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public string? Description { get; set; }
        public DateTime ReleaseDate { get; set; }
        public bool IsExactDate { get; set; }
      public string? Platforms { get; set; }
        public string? Genres { get; set; }
        public bool IsWishlisted { get; set; }
   public bool NotifyOnRelease { get; set; }
      public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        [Ignore]
        public string ReleaseDateText => IsExactDate ? ReleaseDate.ToString("MMM dd, yyyy") : ReleaseDate.ToString("yyyy");
      [Ignore]
        public int DaysUntilRelease => (ReleaseDate.Date - DateTime.Today).Days;
        [Ignore]
        public string CountdownText => DaysUntilRelease switch
        {
            < 0 => "Released",
        0 => "Today!",
            1 => "Tomorrow",
        <= 7 => $"{DaysUntilRelease} days",
          <= 30 => $"{DaysUntilRelease / 7} weeks",
       _ => $"{DaysUntilRelease / 30} months"
        };
 }

    public class WishlistItem
    {
        [PrimaryKey, AutoIncrement]
     public int Id { get; set; }
     public string ExternalId { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public decimal? CurrentPrice { get; set; }
        public decimal? TargetPrice { get; set; }
    public decimal? LowestPrice { get; set; }
      public GamePlatform PreferredPlatform { get; set; }
        public bool NotifyOnSale { get; set; } = true;
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    }
}
