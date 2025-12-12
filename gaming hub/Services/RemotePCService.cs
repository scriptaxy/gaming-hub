using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
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
   public const int ApiPort = 5000;
  public const string DiscoveryMessage = "SYNKTRA_DISCOVER";

        public static RemotePCService Instance => _instance ??= new RemotePCService();

        private RemotePCService()
      {
     var handler = new HttpClientHandler
        {
ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
   }

        /// <summary>
        /// Discover PCs on the network using multiple methods
        /// </summary>
public async Task<List<DiscoveredPC>> DiscoverPCsOnNetworkAsync(int timeoutMs = 5000)
        {
       var discovered = new List<DiscoveredPC>();
            var seenIps = new HashSet<string>();

         // Method 1: UDP Broadcast discovery
            var udpTask = DiscoverViaUdpAsync(discovered, seenIps, timeoutMs);
  
            // Method 2: HTTP scan of common subnet IPs (fallback for iOS)
  var scanTask = DiscoverViaHttpScanAsync(discovered, seenIps, 2000);

       await Task.WhenAll(udpTask, scanTask);

    return discovered;
  }

   /// <summary>
        /// UDP broadcast discovery
  /// </summary>
        private async Task DiscoverViaUdpAsync(List<DiscoveredPC> discovered, HashSet<string> seenIps, int timeoutMs)
{
            try
            {
    using var udpClient = new UdpClient();
           udpClient.EnableBroadcast = true;
          udpClient.Client.ReceiveTimeout = 1000;

var discoveryBytes = Encoding.UTF8.GetBytes(DiscoveryMessage);

   // Get local IP to determine subnet
     var localIp = GetLocalIPAddress();
      var broadcastAddresses = new List<IPEndPoint>
          {
new IPEndPoint(IPAddress.Broadcast, DiscoveryPort),
          new IPEndPoint(IPAddress.Parse("255.255.255.255"), DiscoveryPort)
        };

    // Add subnet-specific broadcast (e.g., 192.168.1.255)
    if (!string.IsNullOrEmpty(localIp))
  {
   var parts = localIp.Split('.');
    if (parts.Length == 4)
 {
      var subnetBroadcast = $"{parts[0]}.{parts[1]}.{parts[2]}.255";
  broadcastAddresses.Add(new IPEndPoint(IPAddress.Parse(subnetBroadcast), DiscoveryPort));
      Console.WriteLine($"Local IP: {localIp}, Subnet broadcast: {subnetBroadcast}");
  }
     }

     // Send discovery to all broadcast addresses
     foreach (var endpoint in broadcastAddresses)
  {
      try
 {
    await udpClient.SendAsync(discoveryBytes, discoveryBytes.Length, endpoint);
      Console.WriteLine($"UDP Discovery sent to {endpoint}");
                    }
   catch (Exception ex)
  {
Console.WriteLine($"Failed to send UDP to {endpoint}: {ex.Message}");
           }
      }

  // Listen for responses
      await ListenForUdpResponsesAsync(udpClient, discovered, seenIps, timeoutMs);
            }
            catch (Exception ex)
 {
   Console.WriteLine($"UDP Discovery error: {ex.Message}");
   }
        }

        /// <summary>
 /// Listen for UDP discovery responses
        /// </summary>
   private async Task ListenForUdpResponsesAsync(UdpClient udpClient, List<DiscoveredPC> discovered, HashSet<string> seenIps, int timeoutMs)
{
            var endTime = DateTime.Now.AddMilliseconds(timeoutMs);

  while (DateTime.Now < endTime)
    {
   try
   {
   using var cts = new CancellationTokenSource(500);
   
          try
          {
#if NET6_0_OR_GREATER
   var result = await udpClient.ReceiveAsync(cts.Token);
#else
      var receiveTask = udpClient.ReceiveAsync();
         var completedTask = await Task.WhenAny(receiveTask, Task.Delay(500));
  if (completedTask != receiveTask) continue;
 var result = await receiveTask;
#endif
     var response = Encoding.UTF8.GetString(result.Buffer);
         var ip = result.RemoteEndPoint.Address.ToString();

              Console.WriteLine($"UDP Response from {ip}: {response}");

   if (!response.StartsWith("{")) continue; // Not JSON

        lock (seenIps)
   {
     if (seenIps.Contains(ip)) continue;
                }

         var json = JObject.Parse(response);
         var pc = new DiscoveredPC
   {
  Hostname = json["Hostname"]?.ToString() ?? "Unknown PC",
       IpAddress = ip,
                Port = json["Port"]?.Value<int>() ?? ApiPort,
       RequiresAuth = json["RequiresAuth"]?.Value<bool>() ?? false,
              Version = json["Version"]?.ToString() ?? "1.0.0"
 };

              lock (seenIps)
          {
      if (!seenIps.Contains(ip))
       {
      seenIps.Add(ip);
    discovered.Add(pc);
          Console.WriteLine($"Discovered via UDP: {pc.Hostname} at {pc.IpAddress}:{pc.Port}");
                 }
  }
      }
     catch (OperationCanceledException) { }
  }
    catch (Exception ex)
         {
              Console.WriteLine($"UDP receive error: {ex.Message}");
         }
     }
      }

        /// <summary>
        /// HTTP scan fallback - scans common IPs on the subnet
        /// </summary>
        private async Task DiscoverViaHttpScanAsync(List<DiscoveredPC> discovered, HashSet<string> seenIps, int timeoutMs)
        {
            try
            {
         var localIp = GetLocalIPAddress();
         if (string.IsNullOrEmpty(localIp)) return;

        var parts = localIp.Split('.');
     if (parts.Length != 4) return;

                var subnet = $"{parts[0]}.{parts[1]}.{parts[2]}";
                Console.WriteLine($"Scanning subnet: {subnet}.x");

    // Scan common IPs (1-30, 100-110, 200-210) to find PCs quickly
        var ipsToScan = new List<string>();
     for (int i = 1; i <= 30; i++) ipsToScan.Add($"{subnet}.{i}");
          for (int i = 100; i <= 110; i++) ipsToScan.Add($"{subnet}.{i}");
    for (int i = 200; i <= 210; i++) ipsToScan.Add($"{subnet}.{i}");

         // Remove our own IP
  ipsToScan.Remove(localIp);

      // Scan in parallel batches
      var batchSize = 10;
        for (int i = 0; i < ipsToScan.Count; i += batchSize)
      {
        var batch = ipsToScan.Skip(i).Take(batchSize);
              var tasks = batch.Select(ip => TryDiscoverAtIpAsync(ip, ApiPort, discovered, seenIps, timeoutMs));
             await Task.WhenAll(tasks);
      }
    }
    catch (Exception ex)
            {
      Console.WriteLine($"HTTP scan error: {ex.Message}");
     }
     }

        /// <summary>
        /// Try to discover a Synktra Companion at a specific IP
        /// </summary>
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
  Version = json["Version"]?.ToString() ?? "1.0.0"
             };

    lock (seenIps)
   {
     if (!seenIps.Contains(ip))
          {
     seenIps.Add(ip);
      discovered.Add(pc);
     Console.WriteLine($"Discovered via HTTP: {pc.Hostname} at {pc.IpAddress}:{pc.Port}");
    }
      }
      }
}
        catch (TaskCanceledException) { }
            catch (HttpRequestException) { }
            catch (Exception) { }
        }

        /// <summary>
        /// Get the local IP address of this device
        /// </summary>
    private string? GetLocalIPAddress()
        {
       try
         {
      // Try to get IP by connecting to a public address (doesn't actually connect)
   using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
   socket.Connect("8.8.8.8", 65530);
 if (socket.LocalEndPoint is IPEndPoint endPoint)
    {
           return endPoint.Address.ToString();
         }
          }
            catch { }

            // Fallback: enumerate interfaces
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
                Console.WriteLine($"Fetching status from: {url}");

           var request = new HttpRequestMessage(HttpMethod.Get, url);
    if (!string.IsNullOrEmpty(authToken))
         request.Headers.Add("Authorization", $"Bearer {authToken}");

         var response = await _httpClient.SendAsync(request);
    Console.WriteLine($"Response status: {response.StatusCode}");

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
            Uptime = json["Uptime"]?.ToString() ?? json["uptime"]?.ToString()
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
  var completedTask = await Task.When.Any(connectTask, Task.Delay(3000));

          if (completedTask == connectTask && client.Connected)
       {
   Console.WriteLine($"Successfully connected to {host}:{port}");
     return true;
                }

                Console.WriteLine($"Connection to {host}:{port} timed out");
     return false;
  }
         catch (Exception ex)
            {
     Console.WriteLine($"Ping failed: {ex.Message}");
       return false;
 }
        }

        public async Task<bool> WakeOnLanAsync(string macAddress, string? broadcastAddress = null)
        {
     try
       {
        var macBytes = ParseMacAddress(macAddress);
    if (macBytes == null)
   {
        Console.WriteLine($"Invalid MAC address: {macAddress}");
          return false;
      }

  var magicPacket = new byte[102];
       for (int i = 0; i < 6; i++)
   magicPacket[i] = 0xFF;
                for (int i = 6; i < 102; i += 6)
 Array.Copy(macBytes, 0, magicPacket, i, 6);

using var udpClient = new UdpClient();
  udpClient.EnableBroadcast = true;

 var endpoints = new List<IPEndPoint>();

         if (!string.IsNullOrEmpty(broadcastAddress))
       endpoints.Add(new IPEndPoint(IPAddress.Parse(broadcastAddress), 9));

       endpoints.Add(new IPEndPoint(IPAddress.Broadcast, 9));
         endpoints.Add(new IPEndPoint(IPAddress.Parse("255.255.255.255"), 7));
      endpoints.Add(new IPEndPoint(IPAddress.Parse("255.255.255.255"), 9));

      foreach (var endpoint in endpoints)
  {
       try
          {
        await udpClient.SendAsync(magicPacket, magicPacket.Length, endpoint);
        Console.WriteLine($"WOL packet sent to {endpoint}");
        }
  catch (Exception ex)
   {
       Console.WriteLine($"Failed to send WOL to {endpoint}: {ex.Message}");
          }
          }

          return true;
            }
            catch (Exception ex)
      {
        Console.WriteLine($"Error sending WOL packet: {ex.Message}");
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
            catch (Exception ex)
      {
         Console.WriteLine($"Error getting installed games: {ex.Message}");
  }
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
            catch (Exception ex)
        {
Console.WriteLine($"Error launching game: {ex.Message}");
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
         catch (Exception ex)
            {
             Console.WriteLine($"Error closing game: {ex.Message}");
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
            catch (Exception ex)
    {
  Console.WriteLine($"Error shutting down PC: {ex.Message}");
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
            catch (Exception ex)
  {
  Console.WriteLine($"Error putting PC to sleep: {ex.Message}");
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
      catch (Exception ex)
      {
  Console.WriteLine($"Error restarting PC: {ex.Message}");
    return false;
            }
        }
  }

    public class DiscoveredPC
    {
     public string Hostname { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
   public int Port { get; set; } = 5000;
    public bool RequiresAuth { get; set; }
        public string Version { get; set; } = string.Empty;
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
