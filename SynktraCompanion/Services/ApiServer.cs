using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using System.IO;
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
    private readonly LowLatencyStreamService _streamService;
    private List<InstalledGame> _cachedGames = [];
    private DateTime _lastGameScan = DateTime.MinValue;
    private int _port = 5050;
    private bool _isRunning;
    private string? _lastError;

    public const int DiscoveryPort = 5001;
    public const int StreamWsPort = 19501;
    public const int StreamUdpPort = 19502;
    public const string DiscoveryMessage = "SYNKTRA_DISCOVER";

// Expose stream service for UI updates
    public LowLatencyStreamService StreamService => _streamService;
    public bool IsRunning => _isRunning;
    public string? LastError => _lastError;
    public int Port => _port;

    public ApiServer()
    {
        _gameScanner = new GameScanner();
        _systemMonitor = new SystemMonitor();
   _streamService = new LowLatencyStreamService();
    }

    public async Task StartAsync(int port = 19500)
    {
        _port = port;
        _cts = new CancellationTokenSource();
    _listener = new HttpListener();

        // Try to find an available port if the requested one is in use
        var actualPort = await FindAvailablePortAsync(port);
        _port = actualPort;

        _listener.Prefixes.Add($"http://*:{_port}/");

 try
      {
  _listener.Start();
    _isRunning = true;
      _lastError = null;
       Console.WriteLine($"API Server started on port {_port}");

            // Start discovery server
            StartDiscoveryServer();

 // Start the stream service (it only sends frames when clients connect)
          var settings = SettingsManager.Load();
       _streamService.SetQuality(settings.StreamQuality);
            _streamService.SetTargetFps(settings.StreamFps);
          _streamService.SetResolution(settings.StreamWidth, settings.StreamHeight);
        await _streamService.StartAsync(StreamWsPort, StreamUdpPort);
            Console.WriteLine($"Stream server started on WS:{StreamWsPort}, UDP:{StreamUdpPort}");

            // Scan games
            _cachedGames = await _gameScanner.ScanAllGamesAsync();
  _lastGameScan = DateTime.Now;

// Start listening for HTTP requests
        _ = Task.Run(() => ListenAsync(_cts.Token));
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5) // Access denied
     {
        _lastError = $"Access denied on port {_port}. Run as administrator or use a port above 1024.";
            _isRunning = false;
   Console.WriteLine(_lastError);
}
        catch (Exception ex)
    {
  _lastError = $"Failed to start API server: {ex.Message}";
    _isRunning = false;
       Console.WriteLine(_lastError);
        }
    }

    private async Task<int> FindAvailablePortAsync(int preferredPort)
    {
        // Check if preferred port is available
        if (IsPortAvailable(preferredPort))
  return preferredPort;

  Console.WriteLine($"Port {preferredPort} is in use, finding alternative...");

        // Try alternative ports in the 19000 range
        for (int port = 19500; port < 19600; port++)
        {
            if (IsPortAvailable(port))
{
          Console.WriteLine($"Using port: {port}");
        return port;
         }
        }

        return preferredPort; // Fall back and let it fail with a proper error
    }

    private static bool IsPortAvailable(int port)
    {
        try
      {
var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
      listener.Stop();
     return true;
        }
        catch
        {
            return false;
   }
    }

    private void StartDiscoveryServer()
    {
     try
    {
            _discoveryServer = new UdpClient();
 _discoveryServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
  _discoveryServer.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
            _discoveryServer.EnableBroadcast = true;

 _ = Task.Run(DiscoveryListenAsync);

            Console.WriteLine($"Discovery server started on UDP port {DiscoveryPort}");
            foreach (var ip in GetLocalIPAddresses())
         {
 Console.WriteLine($"  Listening on: {ip}:{DiscoveryPort}");
       }
    }
        catch (Exception ex)
        {
         Console.WriteLine($"Failed to start discovery server: {ex.Message}");
        }
    }

    private static List<string> GetLocalIPAddresses()
    {
        var ips = new List<string>();
        try
        {
  var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
     {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
     {
        ips.Add(ip.ToString());
                }
}
        }
        catch { }
        return ips;
    }

    private async Task DiscoveryListenAsync()
  {
        Console.WriteLine("Discovery listener started, waiting for broadcasts...");

        while (_cts?.IsCancellationRequested == false)
        {
     try
   {
 if (_discoveryServer == null) break;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
  timeoutCts.CancelAfter(1000);

       try
    {
  var result = await _discoveryServer.ReceiveAsync(timeoutCts.Token);
         var message = Encoding.UTF8.GetString(result.Buffer);

         Console.WriteLine($"Received UDP from {result.RemoteEndPoint}: {message}");

   if (message == DiscoveryMessage || message.Contains("SYNKTRA"))
      {
                var settings = SettingsManager.Load();
         var response = new DiscoveryResponse
             {
        Hostname = Environment.MachineName,
   Port = _port,
           StreamWsPort = StreamWsPort,
StreamUdpPort = StreamUdpPort,
             RequiresAuth = !string.IsNullOrEmpty(settings.AuthToken),
      Version = "1.0.0",
         SupportsStreaming = true,
        SupportsLowLatency = true
};

   var responseBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
               await _discoveryServer.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
           Console.WriteLine($"Discovery response sent to {result.RemoteEndPoint}");
         }
 }
       catch (OperationCanceledException) { }
            }
    catch (OperationCanceledException) { break; }
 catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
      Console.WriteLine($"Discovery error: {ex.Message}");
            }
 }

    Console.WriteLine("Discovery listener stopped");
    }

  public void Stop()
    {
        _isRunning = false;
        try { _cts?.Cancel(); } catch { }
   try { _listener?.Stop(); _listener?.Close(); } catch { }
        try { _discoveryServer?.Close(); _discoveryServer?.Dispose(); } catch { }
        try { _streamService.Stop(); } catch { }

   _listener = null;
      _discoveryServer = null;
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        Console.WriteLine("HTTP listener started, waiting for requests...");
        
        while (!ct.IsCancellationRequested && _listener?.IsListening == true)
        {
       try
            {
        var context = await _listener.GetContextAsync();
       Console.WriteLine($"Received request: {context.Request.HttpMethod} {context.Request.Url}");
_ = Task.Run(() => HandleRequestAsync(context), ct);
            }
            catch (OperationCanceledException) { break; }
        catch (HttpListenerException ex) when (ex.ErrorCode == 995) { break; } // Cancelled
            catch (ObjectDisposedException) { break; }
          catch (Exception ex)
      {
            Console.WriteLine($"HTTP listener error: {ex.Message}");
            }
     }
        
        Console.WriteLine("HTTP listener stopped");
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
    var settings = SettingsManager.Load();

        var path = request.Url?.AbsolutePath.ToLowerInvariant() ?? "/";

     // Skip auth for discovery and status endpoints
          if (path != "/api/discover" && path != "/api/status" && !string.IsNullOrEmpty(settings.AuthToken))
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
     StreamWsPort = StreamWsPort,
          StreamUdpPort = StreamUdpPort,
      RequiresAuth = !string.IsNullOrEmpty(settings.AuthToken),
        Version = "1.0.0",
       SupportsStreaming = true,
            SupportsLowLatency = true
   };
    }
       else if (path == "/api/stream/start" && method == "POST")
            {
 result = await StartStream(request);
            }
       else if (path == "/api/stream/stop" && method == "POST")
 {
      result = StopStream();
         }
            else if (path == "/api/stream/status" && method == "GET")
    {
       result = GetStreamStatus();
      }
else if (path == "/api/stream/config" && method == "POST")
   {
       result = await SetStreamConfig(request);
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
   IsStreaming = _streamService.ClientCount > 0,
            StreamClients = _streamService.ClientCount,
       StreamLatencyMs = _streamService.TotalLatency,
     StreamFps = _streamService.CurrentFps,
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

    private async Task<object> StartStream(HttpListenerRequest request)
    {
        try
        {
  // Stream server is always running, just return the ports
    // Actual streaming only happens when clients connect
if (request.HasEntityBody)
      {
          using var reader = new StreamReader(request.InputStream);
   var body = await reader.ReadToEndAsync();
        var config = JsonConvert.DeserializeAnonymousType(body, new { quality = 40, fps = 60, width = 1280, height = 720 });
        if (config != null)
          {
    _streamService.SetQuality(config.quality);
    _streamService.SetTargetFps(config.fps);
   _streamService.SetResolution(config.width, config.height);
          }
    }

return new { 
          success = true, 
        wsPort = StreamWsPort, 
    udpPort = StreamUdpPort,
       message = "Connect to WebSocket or UDP to start receiving frames" 
       };
        }
   catch (Exception ex)
        {
return new { success = false, error = ex.Message };
        }
    }

    private object StopStream()
    {
    // Release any held input keys
        InputSimulator.Instance.ReleaseAllKeys();
      return new { success = true, message = "Input released" };
    }

    private object GetStreamStatus()
  {
        return new
        {
            isStreaming = _streamService.ClientCount > 0,
       serverReady = _streamService.IsStreaming,
    clientCount = _streamService.ClientCount,
       wsPort = StreamWsPort,
      udpPort = StreamUdpPort,
    latency = new
         {
           captureMs = _streamService.CaptureLatency,
          encodeMs = _streamService.EncodeLatency,
                totalMs = _streamService.TotalLatency
      },
fps = _streamService.CurrentFps
        };
    }

    private async Task<object> SetStreamConfig(HttpListenerRequest request)
    {
      try
        {
 using var reader = new StreamReader(request.InputStream);
            var body = await reader.ReadToEndAsync();
            var config = JsonConvert.DeserializeAnonymousType(body, new { quality = 40, fps = 60, width = 1280, height = 720 });

            if (config != null)
            {
           _streamService.SetQuality(config.quality);
                _streamService.SetTargetFps(config.fps);
                _streamService.SetResolution(config.width, config.height);
            }

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
    public int StreamWsPort { get; set; } = 19501;
    public int StreamUdpPort { get; set; } = 19502;
    public bool RequiresAuth { get; set; }
  public string Version { get; set; } = "1.0.0";
    public bool SupportsStreaming { get; set; }
    public bool SupportsLowLatency { get; set; }
}
