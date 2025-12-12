using DiscordRPC;
using DiscordRPC.Logging;
using SynktraCompanion.Models;

namespace SynktraCompanion.Services;

/// <summary>
/// Discord Rich Presence integration for showing gaming activity
/// </summary>
public class DiscordPresenceService : IDisposable
{
    private static DiscordPresenceService? _instance;
    public static DiscordPresenceService Instance => _instance ??= new DiscordPresenceService();

    // Discord Application ID - You should create your own at discord.com/developers
    private const string ClientId = "1449174280666480670"; // Replace with actual Discord App ID
    
    private DiscordRpcClient? _client;
    private bool _isEnabled;
    private string? _currentGame;
    private DateTime _sessionStart;
    private bool _showGameActivity = true;
    private bool _showPCStatus = true;

    public bool IsConnected => _client?.IsInitialized == true;
    public bool IsEnabled => _isEnabled;

    private DiscordPresenceService()
    {
        _sessionStart = DateTime.UtcNow;
 }

    public void Initialize()
    {
        try
        {
  _client = new DiscordRpcClient(ClientId)
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Warning }
            };

            _client.OnReady += (sender, e) =>
            {
     Console.WriteLine($"Discord RPC connected as {e.User.Username}");
       };

   _client.OnError += (sender, e) =>
        {
    Console.WriteLine($"Discord RPC error: {e.Message}");
            };

        _client.OnConnectionFailed += (sender, e) =>
     {
            Console.WriteLine("Discord RPC connection failed - Discord may not be running");
            };

       _client.Initialize();
   _isEnabled = true;

        // Set initial presence
      UpdatePresence(null, new SystemStats());
        }
        catch (Exception ex)
        {
     Console.WriteLine($"Failed to initialize Discord RPC: {ex.Message}");
            _isEnabled = false;
     }
    }

    public void SetEnabled(bool enabled)
    {
      _isEnabled = enabled;
        
        if (!enabled)
  {
            ClearPresence();
        }
        else if (_client?.IsInitialized == true)
        {
    UpdatePresence(_currentGame, new SystemStats());
        }
 }

    public void SetShowGameActivity(bool show)
    {
        _showGameActivity = show;
        UpdatePresence(_currentGame, new SystemStats());
    }

    public void SetShowPCStatus(bool show)
    {
        _showPCStatus = show;
        UpdatePresence(_currentGame, new SystemStats());
    }

    public void UpdatePresence(string? currentGame, SystemStats stats)
    {
 if (!_isEnabled || _client?.IsInitialized != true)
 return;

        try
        {
   _currentGame = currentGame;

            var presence = new RichPresence
            {
    Timestamps = new Timestamps(_sessionStart)
    };

            if (!string.IsNullOrEmpty(currentGame) && _showGameActivity)
      {
      // Playing a game
         presence.Details = $"Playing {currentGame}";
     presence.State = _showPCStatus ? $"CPU: {stats.CpuUsage:0}% | RAM: {stats.MemoryUsage:0}%" : "Gaming";
  presence.Assets = new Assets
  {
        LargeImageKey = "gaming",
        LargeImageText = currentGame,
     SmallImageKey = "synktra",
SmallImageText = "Synktra Companion"
    };
    }
            else
  {
      // Idle / Desktop
     presence.Details = "Ready to Play";
         presence.State = _showPCStatus ? $"CPU: {stats.CpuUsage:0}% | RAM: {stats.MemoryUsage:0}%" : "Online";
       presence.Assets = new Assets
    {
            LargeImageKey = "synktra",
      LargeImageText = "Synktra Companion",
          SmallImageKey = "online",
    SmallImageText = "Online"
   };
            }

            // Add buttons
presence.Buttons = new Button[]
            {
 new Button
       {
          Label = "Get Synktra",
              Url = "https://github.com/scriptaxy/gaming-hub"
                }
            };

  _client.SetPresence(presence);
   }
        catch (Exception ex)
        {
        Console.WriteLine($"Failed to update Discord presence: {ex.Message}");
     }
    }

public void SetStreamingPresence(string? gameName, int viewerCount)
    {
   if (!_isEnabled || _client?.IsInitialized != true)
  return;

 try
        {
            var presence = new RichPresence
 {
 Details = !string.IsNullOrEmpty(gameName) ? $"Streaming {gameName}" : "Streaming Desktop",
    State = viewerCount > 0 ? $"{viewerCount} viewer(s) connected" : "Waiting for viewers",
      Timestamps = new Timestamps(_sessionStart),
        Assets = new Assets
             {
                 LargeImageKey = "streaming",
             LargeImageText = "Live Streaming",
           SmallImageKey = "synktra",
  SmallImageText = "Synktra Companion"
    },
  Buttons = new Button[]
    {
              new Button
               {
  Label = "Get Synktra",
                Url = "https://github.com/scriptaxy/gaming-hub"
          }
    }
            };

            _client.SetPresence(presence);
        }
        catch (Exception ex)
        {
   Console.WriteLine($"Failed to set streaming presence: {ex.Message}");
}
    }

    public void ClearPresence()
    {
  try
    {
            _client?.ClearPresence();
        }
        catch { }
  }

    public void Dispose()
    {
        try
        {
        _client?.ClearPresence();
   _client?.Dispose();
  }
     catch { }
        _client = null;
    }
}
