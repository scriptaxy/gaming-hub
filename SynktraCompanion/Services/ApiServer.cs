using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using SynktraCompanion.Models;

namespace SynktraCompanion.Services;

public class ApiServer
{
    private HttpListener? _listener;
    private UdpClient? _discoveryServer;
    private CancellationTokenSource? _cts;
 private readonly GameScanner _gameScanner;
    private readonly SystemMonitor _systemMonitor;
    private List<InstalledGame> _cachedGames = [];
    private DateTime _lastGameScan = DateTime.MinValue;
  private int _port = 5000;

    public const int DiscoveryPort = 5001;
    public const string DiscoveryMessage = "SYNKTRA_DISCOVER";

    public ApiServer()
    {
        _gameScanner = new GameScanner();
  _systemMonitor = new SystemMonitor();
    }

    public async Task StartAsync(int port = 5000)
    {
   _port = port;
    _cts = new CancellationTokenSource();
    _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{port}/");

        try
        {
            _listener.Start();
 Console.WriteLine($"API Server started on port {port}");

        StartDiscoveryServer();

    _cachedGames = await _gameScanner.ScanAllGamesAsync();
      _lastGameScan = DateTime.Now;

    _ = Task.Run(() => ListenAsync(_cts.Token));
     }
        catch (Exception ex)
        {
    Console.WriteLine($"Failed to start API server: {ex.Message}");
        }
    }

    private void StartDiscoveryServer()
    {
        try
        {
            _discoveryServer = new UdpClient(DiscoveryPort);
     _discoveryServer.EnableBroadcast = true;
     _discoveryServer.Client.ReceiveTimeout = 1000;
   _ = Task.Run(DiscoveryListenAsync);
            Console.WriteLine($"Discovery server started on UDP port {DiscoveryPort}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start discovery server: {ex.Message}");
 }
    }

    private async Task DiscoveryListenAsync()
    {
      while (_cts?.IsCancellationRequested == false)
    {
     try
   {
      if (_discoveryServer == null) break;
    
          var receiveTask = _discoveryServer.ReceiveAsync();
var completedTask = await Task.WhenAny(receiveTask, Task.Delay(500, _cts.Token));
           
          if (completedTask != receiveTask || _cts.IsCancellationRequested)
    continue;
  
         var result = await receiveTask;
      var message = Encoding.UTF8.GetString(result.Buffer);

       if (message == DiscoveryMessage)
        {
         var settings = SettingsManager.Load();
      var response = new DiscoveryResponse
   {
                 Hostname = Environment.MachineName,
     Port = _port,
        RequiresAuth = !string.IsNullOrEmpty(settings.AuthToken),
        Version = "1.0.0"
      };

        var responseBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
         await _discoveryServer.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
        }
            }
      catch (OperationCanceledException)
            {
     break;
          }
            catch (ObjectDisposedException)
            {
     break;
  }
     catch { }
      }
    }

    public void Stop()
    {
        try
  {
          _cts?.Cancel();
        }
        catch { }
    
        try
        {
            _listener?.Stop();
      _listener?.Close();
        }
        catch { }
        
     try
 {
         _discoveryServer?.Close();
            _discoveryServer?.Dispose();
 }
        catch { }
      
        _listener = null;
        _discoveryServer = null;
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener?.IsListening == true)
        {
 try
     {
    var contextTask = _listener.GetContextAsync();
           var completedTask = await Task.WhenAny(contextTask, Task.Delay(500, ct));
                
                if (completedTask != contextTask || ct.IsCancellationRequested)
   continue;
 
   var context = await contextTask;
     _ = Task.Run(() => HandleRequestAsync(context), ct);
    }
       catch (OperationCanceledException)
            {
       break;
            }
         catch (HttpListenerException)
  {
                break;
            }
            catch { }
  }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
   var request = context.Request;
        var response = context.Response;

   try
     {
     var settings = SettingsManager.Load();
       if (!string.IsNullOrEmpty(settings.AuthToken))
      {
var authHeader = request.Headers["Authorization"];
        var providedToken = authHeader?.Replace("Bearer ", "");
  if (providedToken != settings.AuthToken)
           {
await SendResponse(response, 401, new { error = "Unauthorized" });
 return;
          }
            }

            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
          response.Headers.Add("Access-Control-Allow-Headers", "Authorization, Content-Type");

          if (request.HttpMethod == "OPTIONS")
    {
      response.StatusCode = 204;
          response.Close();
      return;
          }

       var path = request.Url?.AbsolutePath.ToLowerInvariant() ?? "/";
       var method = request.HttpMethod;
            object? result = null;

        if (path == "/api/status" && method == "GET")
    {
          result = GetStatus();
            }
   else if (path == "/api/games" && method == "GET")
            {
            result = await GetGames();
            }
            else if (path.StartsWith("/api/games/") && path.EndsWith("/launch") && method == "POST")
         {
 result = LaunchGame(path);
   }
            else if (path == "/api/games/close" && method == "POST")
            {
                result = CloseGame();
            }
     else if (path == "/api/system/sleep" && method == "POST")
      {
           result = SystemSleep();
       }
else if (path == "/api/system/shutdown" && method == "POST")
            {
      result = SystemShutdown(request);
            }
            else if (path == "/api/system/restart" && method == "POST")
   {
       result = SystemRestart();
         }
      else if (path == "/api/discover" && method == "GET")
 {
     result = new DiscoveryResponse
       {
        Hostname = Environment.MachineName,
   Port = _port,
         RequiresAuth = !string.IsNullOrEmpty(settings.AuthToken),
   Version = "1.0.0"
          };
        }

    if (result != null)
            {
                await SendResponse(response, 200, result);
        }
      else
     {
          await SendResponse(response, 404, new { error = "Not found" });
       }
 }
        catch (Exception ex)
        {
   try { await SendResponse(response, 500, new { error = ex.Message }); } catch { }
        }
    }

    private async Task SendResponse(HttpListenerResponse response, int statusCode, object data)
    {
 response.StatusCode = statusCode;
        response.ContentType = "application/json";

    var json = JsonConvert.SerializeObject(data);
        var buffer = Encoding.UTF8.GetBytes(json);

    response.ContentLength64 = buffer.Length;
     await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private ApiStatusResponse GetStatus()
    {
   var stats = _systemMonitor.GetCurrentStats();
        var currentGame = _systemMonitor.GetRunningGame(_cachedGames);
    var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

     return new ApiStatusResponse
        {
   Hostname = Environment.MachineName,
      CpuUsage = stats.CpuUsage,
   MemoryUsage = stats.MemoryUsage,
     GpuUsage = stats.GpuUsage,
         GpuTemp = stats.GpuTemperature,
    CurrentGame = currentGame,
    IsStreaming = false,
            Uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
        };
    }

    private async Task<List<InstalledGame>> GetGames()
    {
    if ((DateTime.Now - _lastGameScan).TotalMinutes > 5)
        {
    _cachedGames = await _gameScanner.ScanAllGamesAsync();
     _lastGameScan = DateTime.Now;
        }
 return _cachedGames;
    }

    private object LaunchGame(string path)
    {
        var parts = path.Split('/');
        if (parts.Length < 4) return new { success = false, error = "Invalid game ID" };

        var gameId = parts[3];
        var game = _cachedGames.FirstOrDefault(g => g.Id == gameId);

        if (game == null)
            return new { success = false, error = "Game not found" };

        try
    {
          if (!string.IsNullOrEmpty(game.LaunchCommand))
        {
 System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
             FileName = game.LaunchCommand,
       UseShellExecute = true
        });
            return new { success = true };
      }
     return new { success = false, error = "No launch command" };
        }
        catch (Exception ex)
        {
     return new { success = false, error = ex.Message };
        }
    }

    private object CloseGame()
    {
        var currentGame = _systemMonitor.GetRunningGame(_cachedGames);
        if (currentGame == null)
            return new { success = false, error = "No game running" };

        return new { success = true, message = "Please close the game manually" };
    }

    private object SystemSleep()
    {
     try
        {
          System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
     {
         FileName = "rundll32.exe",
      Arguments = "powrprof.dll,SetSuspendState 0,1,0",
UseShellExecute = false
    });
       return new { success = true };
 }
        catch (Exception ex)
   {
  return new { success = false, error = ex.Message };
      }
    }

    private object SystemShutdown(HttpListenerRequest request)
    {
        var queryDelay = request.QueryString["delay"];
        var delay = int.TryParse(queryDelay, out var d) ? d : 0;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
     {
       FileName = "shutdown",
            Arguments = $"/s /t {delay}",
       UseShellExecute = false
            });
    return new { success = true };
        }
        catch (Exception ex)
        {
       return new { success = false, error = ex.Message };
      }
    }

    private object SystemRestart()
    {
        try
      {
 System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
          {
    FileName = "shutdown",
                Arguments = "/r /t 0",
          UseShellExecute = false
    });
  return new { success = true };
        }
  catch (Exception ex)
      {
         return new { success = false, error = ex.Message };
   }
    }
}

public class DiscoveryResponse
{
    public string Hostname { get; set; } = string.Empty;
public int Port { get; set; }
    public bool RequiresAuth { get; set; }
    public string Version { get; set; } = "1.0.0";
}
