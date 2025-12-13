using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace gaming_hub.Services
{
    public class RemotePCService
    {
     private static RemotePCService? _instance;
        private readonly HttpClient _httpClient;

        public const int DiscoveryPort = 5001;
        public const int ApiPort = 19500;
        public const string DiscoveryMessage = "SYNKTRA_DISCOVER";

        public static RemotePCService Instance => _instance ??= new RemotePCService();
    
    // Expose HttpClient for other services
    public HttpClient HttpClient => _httpClient;

  public event Action<string>? OnDiscoveryLog;

      private RemotePCService()
        {
 var handler = new HttpClientHandler
      {
          ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        }

        private void Log(string message)
     {
            Console.WriteLine(message);
  OnDiscoveryLog?.Invoke(message);
        }

        public async Task<List<DiscoveredPC>> DiscoverPCsOnNetworkAsync(int timeoutMs = 8000)
        {
  var discovered = new List<DiscoveredPC>();
     var seenIps = new HashSet<string>();

Log("Starting network discovery...");

            // Get local IP to determine subnet
var localIp = GetLocalIPAddress();
            Log($"Local IP: {localIp ?? "unknown"}");

    // Run all discovery methods in parallel
       var tasks = new List<Task>
    {
 DiscoverViaUdpAsync(discovered, seenIps, timeoutMs),
 DiscoverViaFullSubnetScanAsync(discovered, seenIps, localIp, 1500)
        };

      await Task.WhenAll(tasks);

   Log($"Discovery complete. Found {discovered.Count} PC(s)");
            return discovered;
    }

        private async Task DiscoverViaUdpAsync(List<DiscoveredPC> discovered, HashSet<string> seenIps, int timeoutMs)
   {
            try
            {
       Log("Starting UDP broadcast discovery...");
                using var udpClient = new UdpClient();
                udpClient.EnableBroadcast = true;

      var discoveryBytes = Encoding.UTF8.GetBytes(DiscoveryMessage);
       var localIp = GetLocalIPAddress();

      // Build list of broadcast addresses
      var broadcastAddresses = new List<IPEndPoint>
             {
          new(IPAddress.Broadcast, DiscoveryPort),
         new(IPAddress.Parse("255.255.255.255"), DiscoveryPort)
       };

    if (!string.IsNullOrEmpty(localIp))
     {
     var parts = localIp.Split('.');
       if (parts.Length == 4)
        {
 // Add subnet broadcast
        var subnetBroadcast = $"{parts[0]}.{parts[1]}.{parts[2]}.255";
   broadcastAddresses.Add(new IPEndPoint(IPAddress.Parse(subnetBroadcast), DiscoveryPort));
   Log($"Subnet broadcast: {subnetBroadcast}");
      }
        }

   // Send to all broadcast addresses multiple times
          for (int attempt = 0; attempt < 3; attempt++)
     {
   foreach (var endpoint in broadcastAddresses)
     {
            try
  {
              await udpClient.SendAsync(discoveryBytes, discoveryBytes.Length, endpoint);
   }
              catch { }
        }
                    await Task.Delay(200);
 }

      // Listen for responses
       var endTime = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < endTime)
            {
      try
          {
   using var cts = new CancellationTokenSource(500);
         var result = await udpClient.ReceiveAsync(cts.Token);
 var response = Encoding.UTF8.GetString(result.Buffer);
              var ip = result.RemoteEndPoint.Address.ToString();

if (!response.StartsWith("{")) continue;

     lock (seenIps)
     {
        if (seenIps.Contains(ip)) continue;
  seenIps.Add(ip);
     }

   var json = JObject.Parse(response);
       var pc = new DiscoveredPC
  {
       Hostname = json["Hostname"]?.ToString() ?? "Unknown PC",
         IpAddress = ip,
     Port = json["Port"]?.Value<int>() ?? ApiPort,
             MacAddress = json["MacAddress"]?.ToString(),
  RequiresAuth = json["RequiresAuth"]?.Value<bool>() ?? false,
  Version = json["Version"]?.ToString() ?? "1.0.0",
        SupportsVirtualController = json["SupportsVirtualController"]?.Value<bool>() ?? false,
            VirtualControllerActive = json["VirtualControllerActive"]?.Value<bool>() ?? false,
    SupportsAudio = json["SupportsAudio"]?.Value<bool>() ?? false,
     AudioStreamPort = json["AudioStreamPort"]?.Value<int>() ?? 19503
    };

          lock (discovered)
                {
   discovered.Add(pc);
          }
  Log($"UDP: Found {pc.Hostname} at {ip}:{pc.Port}");
   }
        catch (OperationCanceledException) { }
      catch { }
             }
            }
            catch (Exception ex)
      {
          Log($"UDP discovery error: {ex.Message}");
   }
 }

        private async Task DiscoverViaFullSubnetScanAsync(List<DiscoveredPC> discovered, HashSet<string> seenIps, string? localIp, int timeoutMs)
{
     if (string.IsNullOrEmpty(localIp))
{
                Log("Cannot determine local IP for subnet scan");
                return;
            }

            var parts = localIp.Split('.');
            if (parts.Length != 4)
    {
              Log("Invalid local IP format");
     return;
            }

var subnet = $"{parts[0]}.{parts[1]}.{parts[2]}";
       Log($"Scanning full subnet: {subnet}.1-254");

            // Scan ALL IPs in the subnet (1-254) in parallel batches
var allIps = Enumerable.Range(1, 254)
           .Select(i => $"{subnet}.{i}")
          .Where(ip => ip != localIp)
        .ToList();

          // Process in larger batches for speed
        var batchSize = 30;
        for (int i = 0; i < allIps.Count; i += batchSize)
    {
     var batch = allIps.Skip(i).Take(batchSize).ToList();
 var tasks = batch.Select(ip => TryDiscoverAtIpAsync(ip, ApiPort, discovered, seenIps, timeoutMs));
    await Task.WhenAll(tasks);

    // Small delay between batches to avoid overwhelming the network
    if (i + batchSize < allIps.Count)
             await Task.Delay(50);
        }
        }

  private async Task TryDiscoverAtIpAsync(string ip, int port, List<DiscoveredPC> discovered, HashSet<string> seenIps, int timeoutMs)
        {
            try
            {
    lock (seenIps)
    {
          if (seenIps.Contains(ip)) return;
          }

     using var cts = new CancellationTokenSource(timeoutMs);
   using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };

         var url = $"http://{ip}:{port}/api/discover";
            var response = await client.GetAsync(url, cts.Token);

  if (response.IsSuccessStatusCode)
    {
      var content = await response.Content.ReadAsStringAsync();
          var json = JObject.Parse(content);

           var pc = new DiscoveredPC
             {
    Hostname = json["Hostname"]?.ToString() ?? "Unknown PC",
          IpAddress = ip,
    Port = json["Port"]?.Value<int>() ?? port,
    RequiresAuth = json["RequiresAuth"]?.Value<bool>() ?? false,
        Version = json["Version"]?.ToString() ?? "1.0.0",
       SupportsVirtualController = json["SupportsVirtualController"]?.Value<bool>() ?? false,
   VirtualControllerActive = json["VirtualControllerActive"]?.Value<bool>() ?? false,
    };

          lock (seenIps)
        {
        if (!seenIps.Contains(ip))
          {
      seenIps.Add(ip);
 lock (discovered)
        {
       discovered.Add(pc);
      }
     Log($"HTTP: Found {pc.Hostname} at {ip}:{port}");
            }
        }
                }
            }
    catch { }
        }

        /// <summary>
        /// Direct connection test - use when user enters IP manually
     /// </summary>
        public async Task<DiscoveredPC?> TryConnectDirectAsync(string host, int port = 19500)
        {
          Log($"Testing direct connection to {host}:{port}...");

      try
      {
      using var cts = new CancellationTokenSource(5000);
          using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

       var url = $"http://{host}:{port}/api/discover";
    var response = await client.GetAsync(url, cts.Token);

      if (response.IsSuccessStatusCode)
                {
     var content = await response.Content.ReadAsStringAsync();
       var json = JObject.Parse(content);

      var pc = new DiscoveredPC
        {
            Hostname = json["Hostname"]?.ToString() ?? "Unknown PC",
           IpAddress = host,
       Port = json["Port"]?.Value<int>() ?? port,
           MacAddress = json["MacAddress"]?.ToString(),
           RequiresAuth = json["RequiresAuth"]?.Value<bool>() ?? false,
         Version = json["Version"]?.ToString() ?? "1.0.0",
        SupportsVirtualController = json["SupportsVirtualController"]?.Value<bool>() ?? false,
    VirtualControllerActive = json["VirtualControllerActive"]?.Value<bool>() ?? false,
    SupportsAudio = json["SupportsAudio"]?.Value<bool>() ?? false,
            AudioStreamPort = json["AudioStreamPort"]?.Value<int>() ?? 19503
 };

   Log($"Connected! Found {pc.Hostname}");
     return pc;
                }
            }
            catch (Exception ex)
     {
       Log($"Connection failed: {ex.Message}");
     }

     return null;
        }

      private string? GetLocalIPAddress()
   {
       try
            {
     // This works best on iOS - creates a UDP socket to determine local IP
       using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
         socket.Connect("8.8.8.8", 65530);
       if (socket.LocalEndPoint is IPEndPoint endPoint)
          {
  return endPoint.Address.ToString();
                }
       }
            catch { }

          try
 {
            var host = Dns.GetHostEntry(Dns.GetHostName());
      foreach (var ip in host.AddressList)
     {
        if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("127."))
   {
     return ip.ToString();
       }
   }
            }
            catch { }

            return null;
        }

        public async Task<RemotePCStatus> GetStatusAsync(string host, int port, string? authToken = null)
     {
            if (string.IsNullOrEmpty(host))
          return new RemotePCStatus { IsOnline = false };

            try
  {
   var url = $"http://{host}:{port}/api/status";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
          if (!string.IsNullOrEmpty(authToken))
     request.Headers.Add("Authorization", $"Bearer {authToken}");

                var response = await _httpClient.SendAsync(request);

       if (response.IsSuccessStatusCode)
         {
     var content = await response.Content.ReadAsStringAsync();
         var json = JObject.Parse(content);

                    return new RemotePCStatus
        {
       IsOnline = true,
     Hostname = json["Hostname"]?.ToString() ?? json["hostname"]?.ToString() ?? host,
 CpuUsage = json["CpuUsage"]?.Value<double>() ?? json["cpu_usage"]?.Value<double>() ?? 0,
  MemoryUsage = json["MemoryUsage"]?.Value<double>() ?? json["memory_usage"]?.Value<double>() ?? 0,
    GpuUsage = json["GpuUsage"]?.Value<double?>() ?? json["gpu_usage"]?.Value<double?>(),
         GpuTemperature = json["GpuTemp"]?.Value<double?>() ?? json["gpu_temp"]?.Value<double?>(),
 CurrentGame = json["CurrentGame"]?.ToString() ?? json["current_game"]?.ToString(),
      IsStreaming = json["IsStreaming"]?.Value<bool>() ?? json["is_streaming"]?.Value<bool>() ?? false,
              Uptime = json["Uptime"]?.ToString() ?? json["uptime"]?.ToString(),
      VirtualControllerConnected = json["VirtualControllerConnected"]?.Value<bool>() ?? false,
    VirtualControllerType = json["VirtualControllerType"]?.ToString() ?? string.Empty,
      InputMode = json["InputMode"]?.ToString() ?? string.Empty
       };
 }
          }
   catch (TaskCanceledException)
            {
    Console.WriteLine("Request timed out");
        }
     catch (HttpRequestException ex)
            {
        Console.WriteLine($"HTTP error: {ex.Message}");
            }
       catch (Exception ex)
       {
         Console.WriteLine($"Error: {ex.GetType().Name}: {ex.Message}");
 }

            return new RemotePCStatus { IsOnline = false };
        }

        public async Task<bool> PingAsync(string host, int port)
   {
            try
   {
     using var client = new TcpClient();
        var connectTask = client.ConnectAsync(host, port);
         var delayTask = Task.Delay(3000);
          var completedTask = await Task.WhenAny(connectTask, delayTask);

     return completedTask == connectTask && client.Connected;
     }
  catch
       {
     return false;
    }
        }

        public async Task<bool> WakeOnLanAsync(string macAddress, string? broadcastAddress = null)
   {
          try
      {
    var macBytes = ParseMacAddress(macAddress);
     if (macBytes == null) return false;

         var magicPacket = new byte[102];
     for (int i = 0; i < 6; i++)
         magicPacket[i] = 0xFF;
           for (int i = 6; i < 102; i += 6)
   Array.Copy(macBytes, 0, magicPacket, i, 6);

   using var udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;

       var endpoints = new List<IPEndPoint>
      {
          new(IPAddress.Broadcast, 9),
            new(IPAddress.Parse("255.255.255.255"), 7),
    new(IPAddress.Parse("255.255.255.255"), 9)
    };

         if (!string.IsNullOrEmpty(broadcastAddress))
   endpoints.Insert(0, new IPEndPoint(IPAddress.Parse(broadcastAddress), 9));

 foreach (var endpoint in endpoints)
         {
 try
   {
                await udpClient.SendAsync(magicPacket, magicPacket.Length, endpoint);
        }
          catch { }
        }

    return true;
            }
            catch
   {
   return false;
  }
        }

        private static byte[]? ParseMacAddress(string macAddress)
        {
    try
            {
   var cleanMac = macAddress.Replace(":", "").Replace("-", "").Replace(" ", "").ToUpper();
      if (cleanMac.Length != 12) return null;

            var bytes = new byte[6];
        for (int i = 0; i < 6; i++)
        bytes[i] = Convert.ToByte(cleanMac.Substring(i * 2, 2), 16);
        return bytes;
          }
        catch
{
             return null;
            }
     }

        public async Task<List<RemoteGame>> GetInstalledGamesAsync(string host, int port, string? authToken = null)
        {
       try
            {
                var url = $"http://{host}:{port}/api/games";
      var request = new HttpRequestMessage(HttpMethod.Get, url);
             if (!string.IsNullOrEmpty(authToken))
   request.Headers.Add("Authorization", $"Bearer {authToken}");

           var response = await _httpClient.SendAsync(request);
if (response.IsSuccessStatusCode)
      {
        var content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<List<RemoteGame>>(content) ?? [];
     }
       }
         catch { }
            return [];
      }

   public async Task<bool> LaunchGameAsync(string host, int port, string gameId, string? authToken = null)
 {
            try
 {
           var url = $"http://{host}:{port}/api/games/{gameId}/launch";
          var request = new HttpRequestMessage(HttpMethod.Post, url);
     if (!string.IsNullOrEmpty(authToken))
   request.Headers.Add("Authorization", $"Bearer {authToken}");

            var response = await _httpClient.SendAsync(request);
     return response.IsSuccessStatusCode;
 }
     catch
            {
      return false;
    }
   }

        public async Task<bool> CloseGameAsync(string host, int port, string? authToken = null)
        {
        try
        {
           var url = $"http://{host}:{port}/api/games/close";
        var request = new HttpRequestMessage(HttpMethod.Post, url);
     if (!string.IsNullOrEmpty(authToken))
         request.Headers.Add("Authorization", $"Bearer {authToken}");

         var response = await _httpClient.SendAsync(request);
       return response.IsSuccessStatusCode;
        }
            catch
            {
      return false;
        }
        }

        public async Task<bool> ShutdownAsync(string host, int port, string? authToken = null, int delaySeconds = 0)
  {
            try
      {
      var url = $"http://{host}:{port}/api/system/shutdown?delay={delaySeconds}";
     var request = new HttpRequestMessage(HttpMethod.Post, url);
     if (!string.IsNullOrEmpty(authToken))
       request.Headers.Add("Authorization", $"Bearer {authToken}");

       var response = await _httpClient.SendAsync(request);
              return response.IsSuccessStatusCode;
            }
          catch
            {
       return false;
            }
  }

        public async Task<bool> SleepAsync(string host, int port, string? authToken = null)
        {
            try
        {
      var url = $"http://{host}:{port}/api/system/sleep";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
   if (!string.IsNullOrEmpty(authToken))
        request.Headers.Add("Authorization", $"Bearer {authToken}");

    var response = await _httpClient.SendAsync(request);
             return response.IsSuccessStatusCode;
    }
            catch
            {
return false;
  }
        }

     public async Task<bool> RestartAsync(string host, int port, string? authToken = null)
    {
try
            {
  var url = $"http://{host}:{port}/api/system/restart";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
         if (!string.IsNullOrEmpty(authToken))
         request.Headers.Add("Authorization", $"Bearer {authToken}");

    var response = await _httpClient.SendAsync(request);
      return response.IsSuccessStatusCode;
   }
       catch
        {
 return false;
      }
        }
    }

    public class DiscoveredPC
    {
        public string Hostname { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 19500;
      public string? MacAddress { get; set; }
        public bool RequiresAuth { get; set; }
        public string Version { get; set; } = string.Empty;
        public bool SupportsVirtualController { get; set; }
   public bool VirtualControllerActive { get; set; }
      public bool SupportsAudio { get; set; }
        public int AudioStreamPort { get; set; } = 19503;
    }

    public class RemotePCStatus
    {
      public bool IsOnline { get; set; }
        public string Hostname { get; set; } = string.Empty;
  public string ComputerName => Hostname;
    public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double? GpuUsage { get; set; }
        public double? GpuTemperature { get; set; }
        public string? CurrentGame { get; set; }
        public bool IsStreaming { get; set; }
   public string? Uptime { get; set; }
   public bool VirtualControllerConnected { get; set; }
      public string VirtualControllerType { get; set; } = string.Empty;
        public string InputMode { get; set; } = string.Empty;
    }

    public class RemoteGame
    {
        public string Id { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
        public string? Platform { get; set; }
 public string? InstallPath { get; set; }
        public string? IconPath { get; set; }
     public bool IsRunning { get; set; }
    }
}
