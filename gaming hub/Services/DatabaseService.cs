using SQLite;
using gaming_hub.Models;

namespace gaming_hub.Services
{
    public class DatabaseService
    {
  private static DatabaseService? _instance;
        private SQLiteAsyncConnection? _database;
        private readonly string _databasePath;

        public static DatabaseService Instance => _instance ??= new DatabaseService();

        private DatabaseService()
  {
      var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
     _databasePath = Path.Combine(documentsPath, "gaminghub.db3");
        }

        public async Task InitializeAsync()
        {
            if (_database != null) return;
    _database = new SQLiteAsyncConnection(_databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
            await _database.CreateTableAsync<Game>();
        await _database.CreateTableAsync<UserData>();
            await _database.CreateTableAsync<GameDeal>();
    await _database.CreateTableAsync<UpcomingRelease>();
        await _database.CreateTableAsync<WishlistItem>();

     var userCount = await _database.Table<UserData>().CountAsync();
        if (userCount == 0)
            {
    await _database.InsertAsync(new UserData { Username = "Player" });
}
        }

        // Games
    public async Task<List<Game>> GetAllGamesAsync()
  {
            await InitializeAsync();
            return await _database!.Table<Game>().OrderBy(g => g.Name).ToListAsync();
        }

        public async Task<List<Game>> GetGamesByPlatformAsync(GamePlatform platform)
      {
        await InitializeAsync();
          return await _database!.Table<Game>().Where(g => g.Platform == platform).OrderBy(g => g.Name).ToListAsync();
        }

 public async Task<List<Game>> GetFavoriteGamesAsync()
      {
            await InitializeAsync();
     return await _database!.Table<Game>().Where(g => g.IsFavorite).OrderBy(g => g.Name).ToListAsync();
        }

        public async Task<List<Game>> GetRecentlyPlayedAsync(int limit = 10)
        {
    await InitializeAsync();
            return await _database!.Table<Game>().Where(g => g.LastPlayed != null).OrderByDescending(g => g.LastPlayed).Take(limit).ToListAsync();
        }

   public async Task<Game?> GetGameByExternalIdAsync(string externalId, GamePlatform platform)
        {
          await InitializeAsync();
return await _database!.Table<Game>().FirstOrDefaultAsync(g => g.ExternalId == externalId && g.Platform == platform);
      }

        public async Task<int> SaveGameAsync(Game game)
    {
            await InitializeAsync();
            game.LastUpdated = DateTime.UtcNow;
      if (game.Id != 0)
   {
        await _database!.UpdateAsync(game);
        return game.Id;
   }
     game.DateAdded = DateTime.UtcNow;
      return await _database!.InsertAsync(game);
        }

        public async Task SaveGamesAsync(IEnumerable<Game> games)
        {
            await InitializeAsync();
            foreach (var game in games)
         {
          var existing = await GetGameByExternalIdAsync(game.ExternalId!, game.Platform);
   if (existing != null)
                {
     game.Id = existing.Id;
    game.DateAdded = existing.DateAdded;
    game.IsFavorite = existing.IsFavorite;
     }
   await SaveGameAsync(game);
            }
        }

      public async Task DeleteGameAsync(Game game)
      {
   await InitializeAsync();
   await _database!.DeleteAsync(game);
 }

        public async Task<int> GetGameCountAsync()
        {
            await InitializeAsync();
     return await _database!.Table<Game>().CountAsync();
        }

        // User Data
      public async Task<UserData> GetUserDataAsync()
   {
            await InitializeAsync();
            return await _database!.Table<UserData>().FirstOrDefaultAsync() ?? new UserData();
        }

   public async Task SaveUserDataAsync(UserData userData)
        {
  await InitializeAsync();
         userData.LastUpdated = DateTime.UtcNow;
        if (userData.Id != 0)
   await _database!.UpdateAsync(userData);
     else
         await _database!.InsertAsync(userData);
      }

        // Deals
        public async Task<List<GameDeal>> GetDealsAsync(int limit = 50)
     {
  await InitializeAsync();
    return await _database!.Table<GameDeal>().OrderByDescending(d => d.DiscountPercent).Take(limit).ToListAsync();
        }

        public async Task SaveDealsAsync(IEnumerable<GameDeal> deals)
        {
         await InitializeAsync();
            await _database!.DeleteAllAsync<GameDeal>();
            await _database.InsertAllAsync(deals);
        }

        // Upcoming Releases
        public async Task<List<UpcomingRelease>> GetUpcomingReleasesAsync()
        {
 await InitializeAsync();
   return await _database!.Table<UpcomingRelease>().Where(r => r.ReleaseDate >= DateTime.Today).OrderBy(r => r.ReleaseDate).ToListAsync();
        }

        public async Task SaveUpcomingReleasesAsync(IEnumerable<UpcomingRelease> releases)
        {
      await InitializeAsync();
  await _database!.DeleteAllAsync<UpcomingRelease>();
   await _database.InsertAllAsync(releases);
   }

        public async Task ToggleWishlistAsync(UpcomingRelease release)
      {
            await InitializeAsync();
     release.IsWishlisted = !release.IsWishlisted;
            await _database!.UpdateAsync(release);
        }

        // Wishlist
        public async Task<List<WishlistItem>> GetWishlistAsync()
        {
      await InitializeAsync();
          return await _database!.Table<WishlistItem>().OrderBy(w => w.GameName).ToListAsync();
  }

    public async Task SaveWishlistItemAsync(WishlistItem item)
        {
            await InitializeAsync();
            if (item.Id != 0)
      await _database!.UpdateAsync(item);
  else
  await _database!.InsertAsync(item);
        }

        public async Task DeleteWishlistItemAsync(WishlistItem item)
   {
await InitializeAsync();
         await _database!.DeleteAsync(item);
        }

        // Search
        public async Task<List<Game>> SearchGamesAsync(string query)
        {
       await InitializeAsync();
          query = query.ToLower();
      var allGames = await _database!.Table<Game>().ToListAsync();
            return allGames
          .Where(g => g.Name.ToLower().Contains(query) ||
         (g.Genres?.ToLower().Contains(query) ?? false) ||
          (g.Developers?.ToLower().Contains(query) ?? false))
      .OrderBy(g => g.Name)
             .ToList();
        }
    }
}
