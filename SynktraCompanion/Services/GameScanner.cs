using System.IO;
using Microsoft.Win32;
using SynktraCompanion.Models;

namespace SynktraCompanion.Services;

public class GameScanner
{
    public async Task<List<InstalledGame>> ScanAllGamesAsync()
    {
        var games = new List<InstalledGame>();
 
        await Task.Run(() =>
        {
            games.AddRange(ScanSteamGames());
   games.AddRange(ScanEpicGames());
    games.AddRange(ScanGOGGames());
     games.AddRange(ScanOtherGames());
        });
 
        return games.OrderBy(g => g.Name).ToList();
    }

    private List<InstalledGame> ScanSteamGames()
    {
    var games = new List<InstalledGame>();
        
        try
   {
            // Find Steam installation
            var steamPath = Registry.GetValue(
         @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", 
     "InstallPath", null) as string;
            
   if (string.IsNullOrEmpty(steamPath))
   {
steamPath = Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", 
   "InstallPath", null) as string;
            }

            if (string.IsNullOrEmpty(steamPath)) return games;

            // Read libraryfolders.vdf to find all library locations
     var libraryFolders = new List<string> { steamPath };
            var libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
       
   if (File.Exists(libraryFile))
            {
           var content = File.ReadAllText(libraryFile);
     // Parse VDF for additional library paths
        var matches = System.Text.RegularExpressions.Regex.Matches(
          content, @"""path""\s*""([^""]+)""");
          foreach (System.Text.RegularExpressions.Match match in matches)
                {
               var path = match.Groups[1].Value.Replace(@"\\", @"\");
       if (!libraryFolders.Contains(path))
      libraryFolders.Add(path);
   }
        }

            // Scan each library for games
    foreach (var library in libraryFolders)
   {
    var appsPath = Path.Combine(library, "steamapps");
  if (!Directory.Exists(appsPath)) continue;

       var manifestFiles = Directory.GetFiles(appsPath, "appmanifest_*.acf");
        foreach (var manifest in manifestFiles)
     {
        try
      {
  var manifestContent = File.ReadAllText(manifest);
       var appId = ExtractVdfValue(manifestContent, "appid");
             var name = ExtractVdfValue(manifestContent, "name");
         var installDir = ExtractVdfValue(manifestContent, "installdir");

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(appId)) continue;

            // Skip tools, DLC, etc
       if (name.Contains("Proton") || name.Contains("Steamworks") ||
             name.Contains("Redistributable") || name.Contains("SDK"))
         continue;

          var gamePath = Path.Combine(appsPath, "common", installDir ?? name);
       
 games.Add(new InstalledGame
   {
 Id = $"steam_{appId}",
 Name = name,
           Platform = "Steam",
   InstallPath = gamePath,
   LaunchCommand = $"steam://rungameid/{appId}"
    });
      }
     catch { }
     }
          }
        }
      catch (Exception ex)
        {
        Console.WriteLine($"Error scanning Steam games: {ex.Message}");
        }

        return games;
    }

    private List<InstalledGame> ScanEpicGames()
    {
        var games = new List<InstalledGame>();
        
        try
        {
 var manifestsPath = Path.Combine(
       Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
          "Epic", "EpicGamesLauncher", "Data", "Manifests");

            if (!Directory.Exists(manifestsPath)) return games;

foreach (var file in Directory.GetFiles(manifestsPath, "*.item"))
            {
                try
         {
        var json = File.ReadAllText(file);
         var doc = System.Text.Json.JsonDocument.Parse(json);
     var root = doc.RootElement;

       var name = root.GetProperty("DisplayName").GetString();
         var installPath = root.GetProperty("InstallLocation").GetString();
          var appName = root.GetProperty("AppName").GetString();
   var catalogId = root.TryGetProperty("CatalogItemId", out var catProp) 
   ? catProp.GetString() : null;

    if (string.IsNullOrEmpty(name)) continue;

         games.Add(new InstalledGame
  {
             Id = $"epic_{appName ?? catalogId ?? Guid.NewGuid().ToString()}",
   Name = name,
           Platform = "Epic",
  InstallPath = installPath,
      LaunchCommand = $"com.epicgames.launcher://apps/{appName}?action=launch&silent=true"
       });
    }
     catch { }
}
  }
        catch (Exception ex)
    {
         Console.WriteLine($"Error scanning Epic games: {ex.Message}");
        }

   return games;
    }

    private List<InstalledGame> ScanGOGGames()
    {
        var games = new List<InstalledGame>();
        
     try
        {
            // Check GOG Galaxy database
            var dbPath = Path.Combine(
  Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
             "GOG.com", "Galaxy", "storage", "galaxy-2.0.db");

  if (!File.Exists(dbPath))
            {
   // Alternative: check registry for installed GOG games
   using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games");
       if (key == null) return games;

   foreach (var subKeyName in key.GetSubKeyNames())
   {
       try
         {
  using var gameKey = key.OpenSubKey(subKeyName);
    if (gameKey == null) continue;

    var name = gameKey.GetValue("GAMENAME") as string;
                   var path = gameKey.GetValue("PATH") as string;
               var gameId = gameKey.GetValue("gameID") as string;

     if (string.IsNullOrEmpty(name)) continue;

 games.Add(new InstalledGame
   {
  Id = $"gog_{gameId ?? subKeyName}",
          Name = name,
 Platform = "GOG",
          InstallPath = path,
        LaunchCommand = $"goggalaxy://runGame/{gameId ?? subKeyName}"
         });
             }
        catch { }
                }
            }
      }
        catch (Exception ex)
        {
  Console.WriteLine($"Error scanning GOG games: {ex.Message}");
   }

        return games;
    }

    private List<InstalledGame> ScanOtherGames()
    {
        var games = new List<InstalledGame>();
        
        try
        {
        // Scan common game directories
        var commonPaths = new[]
          {
      @"C:\Program Files\",
        @"C:\Program Files (x86)\",
      @"D:\Games\",
     @"E:\Games\",
           Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)),
  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86))
  };

    // Check Ubisoft Connect
      var ubisoftPath = Path.Combine(
     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
          "Ubisoft", "Ubisoft Game Launcher", "games");
 
            if (Directory.Exists(ubisoftPath))
 {
     foreach (var dir in Directory.GetDirectories(ubisoftPath))
     {
           var name = Path.GetFileName(dir);
     var exe = Directory.GetFiles(dir, "*.exe", SearchOption.TopDirectoryOnly)
           .FirstOrDefault(f => !f.Contains("unins") && !f.Contains("setup"));
        
          if (exe != null)
              {
              games.Add(new InstalledGame
  {
   Id = $"ubisoft_{name.GetHashCode()}",
    Name = name,
       Platform = "Ubisoft",
        InstallPath = dir,
  LaunchCommand = exe
             });
   }
  }
     }

            // Check EA App/Origin
   using var eaKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Electronic Arts");
 if (eaKey != null)
       {
  foreach (var subKeyName in eaKey.GetSubKeyNames())
  {
      try
 {
       using var gameKey = eaKey.OpenSubKey(subKeyName);
      var installDir = gameKey?.GetValue("Install Dir") as string;
            
    if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
          {
           games.Add(new InstalledGame
    {
        Id = $"ea_{subKeyName.GetHashCode()}",
             Name = subKeyName,
          Platform = "EA",
     InstallPath = installDir,
                LaunchCommand = $"origin://launchgame/{subKeyName}"
          });
  }
            }
    catch { }
       }
  }
        }
        catch (Exception ex)
        {
       Console.WriteLine($"Error scanning other games: {ex.Message}");
 }

        return games;
    }

    private string? ExtractVdfValue(string content, string key)
    {
        var pattern = $@"""{key}""\s*""([^""]+)""";
    var match = System.Text.RegularExpressions.Regex.Match(content, pattern, 
       System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
}
