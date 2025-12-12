using SQLite;

namespace gaming_hub.Models
{
    public class Game
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
  public string? ExternalId { get; set; }
   public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public GamePlatform Platform { get; set; }
        public DateTime? ReleaseDate { get; set; }
    public DateTime? LastPlayed { get; set; }
   public int PlaytimeMinutes { get; set; }
   public bool IsInstalled { get; set; }
   public bool IsFavorite { get; set; }
        public string? Genres { get; set; }
        public string? Developers { get; set; }
    public string? Publishers { get; set; }
        public double? MetacriticScore { get; set; }
    public double? UserRating { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.UtcNow;
     public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [Ignore]
        public string PlaytimeFormatted =>
        PlaytimeMinutes < 60 ? $"{PlaytimeMinutes}m" :
            PlaytimeMinutes < 1440 ? $"{PlaytimeMinutes / 60}h {PlaytimeMinutes % 60}m" :
$"{PlaytimeMinutes / 1440}d {(PlaytimeMinutes % 1440) / 60}h";

        [Ignore]
        public List<string> GenreList =>
      string.IsNullOrEmpty(Genres) ? [] :
   Genres.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(g => g.Trim()).ToList();

        [Ignore]
        public string PlatformName => Platform switch
   {
            GamePlatform.Steam => "Steam",
         GamePlatform.Epic => "Epic Games",
  GamePlatform.GOG => "GOG",
         GamePlatform.Origin => "EA App",
    GamePlatform.Ubisoft => "Ubisoft Connect",
            GamePlatform.Xbox => "Xbox",
  GamePlatform.PlayStation => "PlayStation",
GamePlatform.Nintendo => "Nintendo",
     GamePlatform.Manual => "Manual",
         _ => "Unknown"
        };
    }

    public enum GamePlatform
    {
  Steam = 0,
        Epic = 1,
        GOG = 2,
        Origin = 3,
    Ubisoft = 4,
        Xbox = 5,
     PlayStation = 6,
        Nintendo = 7,
        Manual = 99
    }
}
