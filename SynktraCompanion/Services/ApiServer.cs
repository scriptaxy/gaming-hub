using System.Net;
using System.Net.Sockets;
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
    private readonly AudioStreamService _audioStreamService;
    private List<InstalledGame> _cachedGames = [];
    private DateTime _lastGameScan = DateTime.MinValue;
 private int _port = 19500;
    private bool _isRunning;
 private string? _lastError;

    public const int DiscoveryPort = 5001;
    public const int StreamWsPort = 19501;
    public const int StreamUdpPort = 19502;
    public const int AudioStreamPort = 19503;
    public const string DiscoveryMessage = "SYNKTRA_DISCOVER";

    public LowLatencyStreamService StreamService => _streamService;
    public AudioStreamService AudioService => _audioStreamService;
    public bool IsRunning => _isRunning;
    public string? LastError => _lastError;
    public int Port => _port;

public ApiServer()
    {
        _gameScanner = new GameScanner();
        _systemMonitor = new SystemMonitor();
        _streamService = new LowLatencyStreamService();
        _audioStreamService = new AudioStreamService();
    }

    public async Task StartAsync(int port = 19500)
    {
        _port = port;
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();

        var actualPort = await FindAvailablePortAsync(port);
        _port = actualPort;
        
        Console.WriteLine($"[ApiServer] Requested port: {port}, Actual port: {_port}");

     _listener.Prefixes.Add($"http://*:{_port}/");

        try
        {
            _listener.Start();
     _isRunning = true;
   _lastError = null;
       Console.WriteLine($"API Server started on port {_port}");

       StartDiscoveryServer();

        // Apply GPU priority for better streaming performance
            ApplyGpuPriority();

     var settings = SettingsManager.Load();
     _streamService.SetQuality(settings.StreamQuality);
            _streamService.SetTargetFps(settings.StreamFps);
            _streamService.SetResolution(settings.StreamWidth, settings.StreamHeight);
    await _streamService.StartAsync(StreamWsPort, StreamUdpPort);
      Console.WriteLine($"Video stream server started on WS:{StreamWsPort}, UDP:{StreamUdpPort}");

            // Start audio streaming
            await _audioStreamService.StartAsync(AudioStreamPort);
      Console.WriteLine($"Audio stream server started on port {AudioStreamPort}");

    // Initialize virtual controller
       InputSimulator.Instance.InitializeVirtualController();
 var controllerStatus = InputSimulator.Instance.GetControllerStatus();
  if (controllerStatus.IsConnected)
            {
      Console.WriteLine("Virtual Xbox 360 controller connected - games will recognize controller input");
    }
            else if (!controllerStatus.IsViGEmInstalled)
  {
      Console.WriteLine("ViGEmBus not installed - using keyboard/mouse fallback for gamepad input");
                Console.WriteLine("  Install ViGEmBus for full controller support: https://github.com/ViGEm/ViGEmBus/releases");
            }

    _cachedGames = await _gameScanner.ScanAllGamesAsync();
       _lastGameScan = DateTime.Now;

            _ = Task.Run(() => ListenAsync(_cts.Token));
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
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

    /// <summary>
    /// Apply GPU priority settings for better streaming performance during gaming
    /// </summary>
    private void ApplyGpuPriority()
    {
        try
    {
            // Set process priority to high for better performance
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            currentProcess.PriorityClass = System.Diagnostics.ProcessPriorityClass.High;
            Console.WriteLine("Process priority set to High");

   // Try to set GPU priority via registry (requires admin)
            try
            {
        var exePath = Environment.ProcessPath ?? currentProcess.MainModule?.FileName;
     if (!string.IsNullOrEmpty(exePath))
     {
          var exeName = Path.GetFileName(exePath);
      var keyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\" + exeName + @"\PerfOptions";
      
          using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath);
   if (key != null)
   {
    // GPU Priority: 8 = High
  key.SetValue("GpuPriority", 8, Microsoft.Win32.RegistryValueKind.DWord);
       // CPU Priority: 3 = High  
    key.SetValue("CpuPriorityClass", 3, Microsoft.Win32.RegistryValueKind.DWord);
  // IO Priority: 3 = High
    key.SetValue("IoPriority", 3, Microsoft.Win32.RegistryValueKind.DWord);
       Console.WriteLine("GPU/CPU/IO priority set in registry");
  }
      }
            }
     catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Note: Run as admin to set GPU priority in registry");
            }
        catch (Exception ex)
     {
      Console.WriteLine($"GPU priority registry warning: {ex.Message}");
       }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Priority settings warning: {ex.Message}");
        }
    }

    private async Task<int> FindAvailablePortAsync(int preferredPort)
    {
        if (IsPortAvailable(preferredPort))
         return preferredPort;

        Console.WriteLine($"Port {preferredPort} is in use, finding alternative...");

        for (int port = 19500; port < 19600; port++)
        {
    if (port != preferredPort && IsPortAvailable(port))
            {
           Console.WriteLine($"Using port: {port}");
  return port;
            }
        }

     return preferredPort;
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
         var controllerStatus = InputSimulator.Instance.GetControllerStatus();
   var response = new DiscoveryResponse
    {
   Hostname = Environment.MachineName,
   Port = _port,
   StreamWsPort = StreamWsPort,
     StreamUdpPort = StreamUdpPort,
  AudioStreamPort = AudioStreamPort,
    RequiresAuth = !string.IsNullOrEmpty(settings.AuthToken),
      Version = "1.0.0",
    SupportsStreaming = true,
 SupportsLowLatency = true,
  SupportsAudio = true,
   SupportsVirtualController = controllerStatus.IsViGEmInstalled,
   VirtualControllerActive = controllerStatus.IsConnected
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
 
        // Release controller inputs and cleanup
      InputSimulator.Instance.Shutdown();
        
        try { _cts?.Cancel(); } catch { }
     try { _listener?.Stop(); _listener?.Close(); } catch { }
        try { _discoveryServer?.Close(); _discoveryServer?.Dispose(); } catch { }
        try { _streamService.Stop(); } catch { }
        try { _audioStreamService.Stop(); } catch { }

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
   catch (HttpListenerException ex) when (ex.ErrorCode == 995) { break; }
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
    result = GetStatus();
            else if (path == "/api/games" && method == "GET")
 result = await GetGames();
    else if (path.StartsWith("/api/games/") && path.EndsWith("/launch") && method == "POST")
        result = LaunchGame(path);
            else if (path == "/api/games/close" && method == "POST")
 result = CloseGame();
            else if (path == "/api/system/sleep" && method == "POST")
       result = SystemSleep();
            else if (path == "/api/system/shutdown" && method == "POST")
         result = SystemShutdown(request);
     else if (path == "/api/system/restart" && method == "POST")
       result = SystemRestart();
   else if (path == "/api/discover" && method == "GET")
        {
            var controllerStatus = InputSimulator.Instance.GetControllerStatus();
   result = new DiscoveryResponse
  {
      Hostname = Environment.MachineName,
      Port = _port,
 StreamWsPort = StreamWsPort,
StreamUdpPort = StreamUdpPort,
         AudioStreamPort = AudioStreamPort,
   RequiresAuth = !string.IsNullOrEmpty(settings.AuthToken),
  Version = "1.0.0",
    SupportsStreaming = true,
      SupportsLowLatency = true,
    SupportsAudio = true,
        SupportsVirtualController = controllerStatus.IsViGEmInstalled,
              VirtualControllerActive = controllerStatus.IsConnected
            };
        }
         else if (path == "/api/stream/start" && method == "POST")
     result = await StartStream(request);
  else if (path == "/api/stream/stop" && method == "POST")
        result = StopStream();
            else if (path == "/api/stream/status" && method == "GET")
      result = GetStreamStatus();
      else if (path == "/api/stream/config" && method == "POST")
     result = await SetStreamConfig(request);
       // New virtual controller endpoints
else if (path == "/api/controller/status" && method == "GET")
        result = GetControllerStatus();
  else if (path == "/api/controller/enable" && method == "POST")
      result = EnableVirtualController();
      else if (path == "/api/controller/disable" && method == "POST")
                result = DisableVirtualController();
  else if (path == "/api/controller/reconnect" && method == "POST")
         result = ReconnectVirtualController();
  else if (path == "/api/controller/install" && method == "POST")
  result = await InstallViGEmDriverAsync();

  if (result != null)
        await SendResponse(response, 200, result);
        else
        await SendResponse(response, 404, new { error = "Not found" });
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
     var controllerStatus = InputSimulator.Instance.GetControllerStatus();

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
         Uptime = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m",
        // Virtual controller status
      VirtualControllerConnected = controllerStatus.IsConnected,
            VirtualControllerType = controllerStatus.ControllerType,
            InputMode = InputSimulator.Instance.InputModeDescription
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

            // Ensure virtual controller is connected when streaming starts
var controllerStatus = InputSimulator.Instance.GetControllerStatus();
    if (controllerStatus.IsViGEmInstalled && !controllerStatus.IsConnected)
   {
    VirtualControllerService.Instance.Connect();
   }

    return new
            {
                success = true,
                wsPort = StreamWsPort,
   udpPort = StreamUdpPort,
           message = "Connect to WebSocket or UDP to start receiving frames",
          virtualController = new
       {
       available = controllerStatus.IsViGEmInstalled,
       connected = VirtualControllerService.Instance.IsConnected,
                 type = controllerStatus.ControllerType
     }
     };
        }
     catch (Exception ex)
        {
            return new { success = false, error = ex.Message };
        }
    }

    private object StopStream()
    {
        InputSimulator.Instance.ReleaseAllKeys();
        return new { success = true, message = "Input released" };
    }

    private object GetStreamStatus()
    {
        var controllerStatus = InputSimulator.Instance.GetControllerStatus();
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
            fps = _streamService.CurrentFps,
            // Include controller info in stream status
            controller = new
   {
 mode = InputSimulator.Instance.InputModeDescription,
  vigemInstalled = controllerStatus.IsViGEmInstalled,
     virtualControllerConnected = controllerStatus.IsConnected,
      controllerType = controllerStatus.ControllerType
 }
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

    #region Virtual Controller Endpoints

    /// <summary>
    /// Get current virtual controller status
    /// </summary>
    private object GetControllerStatus()
    {
        var status = InputSimulator.Instance.GetControllerStatus();
        return new
    {
     vigemInstalled = status.IsViGEmInstalled,
    vigemInstallUrl = "https://github.com/ViGEm/ViGEmBus/releases",
     canAutoInstall = status.CanAutoInstall,
  isInstallingDriver = status.IsInstallingDriver,
     connected = status.IsConnected,
       enabled = status.IsEnabled,
  controllerType = status.ControllerType,
  inputMode = InputSimulator.Instance.InputModeDescription,
            error = status.LastError,
 message = status.IsConnected 
 ? "Virtual Xbox 360 controller active - games will detect controller input"
                : status.IsViGEmInstalled 
         ? "Virtual controller available but not connected"
     : "ViGEmBus driver not installed - click Install to set up automatically"
        };
    }

    /// <summary>
    /// Enable virtual controller mode
    /// </summary>
    private object EnableVirtualController()
{
        InputSimulator.Instance.UseVirtualController = true;
      
        if (VirtualControllerService.Instance.IsConnected)
        {
            return new { success = true, message = "Virtual controller enabled and connected" };
        }
  else if (VirtualControllerService.Instance.IsViGEmAvailable)
        {
            var connected = VirtualControllerService.Instance.Connect();
    return new 
       { 
              success = connected, 
            message = connected 
                ? "Virtual controller connected" 
      : $"Failed to connect: {VirtualControllerService.Instance.LastError}"
 };
        }
        else
        {
         return new 
            { 
        success = false, 
                message = "ViGEmBus driver not installed",
           canAutoInstall = true,
    installEndpoint = "/api/controller/install"
            };
        }
    }

    /// <summary>
    /// Disable virtual controller and use keyboard/mouse fallback
    /// </summary>
    private object DisableVirtualController()
    {
      InputSimulator.Instance.UseVirtualController = false;
    return new 
        { 
success = true, 
      message = "Virtual controller disabled - using keyboard/mouse fallback",
    inputMode = InputSimulator.Instance.InputModeDescription
        };
    }

    /// <summary>
    /// Attempt to reconnect the virtual controller
    /// </summary>
    private object ReconnectVirtualController()
 {
        VirtualControllerService.Instance.Disconnect();
        
        if (VirtualControllerService.Instance.Connect())
        {
    return new { success = true, message = "Virtual controller reconnected" };
        }
        else
      {
          return new 
        { 
    success = false, 
             error = VirtualControllerService.Instance.LastError,
    message = "Failed to reconnect virtual controller"
};
      }
    }

    /// <summary>
    /// Install ViGEmBus driver automatically
    /// </summary>
  private async Task<object> InstallViGEmDriverAsync()
    {
        if (VirtualControllerService.Instance.IsViGEmAvailable)
{
      return new { success = true, message = "ViGEmBus is already installed!", alreadyInstalled = true };
    }

        if (VirtualControllerService.Instance.IsInstallingDriver)
        {
     return new { success = false, message = "Installation already in progress...", installing = true };
   }

        var installMessages = new List<string>();
        VirtualControllerService.Instance.OnInstallProgress += msg => installMessages.Add(msg);

        try
        {
            var result = await VirtualControllerService.Instance.InstallViGEmBusAsync();
            
  return new 
            { 
     success = result, 
        message = result 
           ? "ViGEmBus driver installed successfully! Virtual controller is now available."
      : VirtualControllerService.Instance.LastError ?? "Installation failed",
    installed = result,
             controllerConnected = VirtualControllerService.Instance.IsConnected,
      log = installMessages
   };
        }
        finally
        {
            VirtualControllerService.Instance.OnInstallProgress -= msg => installMessages.Add(msg);
        }
    }

    #endregion
}

public class DiscoveryResponse
{
    public string Hostname { get; set; } = string.Empty;
    public int Port { get; set; }
    public int StreamWsPort { get; set; } = 19501;
    public int StreamUdpPort { get; set; } = 19502;
    public int AudioStreamPort { get; set; } = 19503;
  public bool RequiresAuth { get; set; }
    public string Version { get; set; } = "1.0.0";
    public bool SupportsStreaming { get; set; }
    public bool SupportsLowLatency { get; set; }
    public bool SupportsAudio { get; set; }
    public bool SupportsVirtualController { get; set; }
    public bool VirtualControllerActive { get; set; }
}
