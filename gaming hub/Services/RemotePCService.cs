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
        public const string DiscoveryMessage = "SYNKTRA_DISCOVER";

        public static RemotePCService Instance => _instance ??= new RemotePCService();

        private RemotePCService()
        {
            var handler = new HttpClientHandler
            {
                // Allow self-signed certificates for local network
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        }

        public async Task<List<DiscoveredPC>> DiscoverPCsOnNetworkAsync(int timeoutMs = 3000)
      {
            var discovered = new List<DiscoveredPC>();
            
            try
            {
         using var udpClient = new UdpClient();
    udpClient.EnableBroadcast = true;
          
   var discoveryBytes = Encoding.UTF8.GetBytes(DiscoveryMessage);
     
      // Send to broadcast addresses
      var broadcastEndpoints = new[]
      {
  new IPEndPoint(IPAddress.Broadcast, DiscoveryPort),
        new IPEndPoint(IPAddress.Parse("255.255.255.255"), DiscoveryPort)
    };
    
         foreach (var endpoint in broadcastEndpoints)
        {
       try
 {
         await udpClient.SendAsync(discoveryBytes, discoveryBytes.Length, endpoint);
      Console.WriteLine($"Discovery sent to {endpoint}");
         }
        catch (Exception ex)
          {
      Console.WriteLine($"Failed to send discovery to {endpoint}: {ex.Message}");
       }
                }
     
                // Listen for responses
        var listenTask = ListenForDiscoveryResponsesAsync(udpClient, discovered, timeoutMs);
        await listenTask;
         }
            catch (Exception ex)
            {
 Console.WriteLine($"Discovery error: {ex.Message}");
    }
  
        return discovered;
 }
  
   private async Task ListenForDiscoveryResponsesAsync(UdpClient udpClient, List<DiscoveredPC> discovered, int timeoutMs)
     {
 var endTime = DateTime.Now.AddMilliseconds(timeoutMs);
   
  while (DateTime.Now < endTime)
            {
     try
    {
        var receiveTask = udpClient.ReceiveAsync();
         var delayTask = Task.Delay(500);
 
      var completedTask = await Task.WhenAny(receiveTask, delayTask);
             
          if (completedTask == receiveTask && receiveTask.IsCompletedSuccessfully)
       {
            var result = await receiveTask;
           var response = Encoding.UTF8.GetString(result.Buffer);
  
    try
       {
      var json = JObject.Parse(response);
     var pc = new DiscoveredPC
                {
      Hostname = json["Hostname"]?.ToString() ?? "Unknown",
       IpAddress = result.RemoteEndPoint.Address.ToString(),
     Port = json["Port"]?.Value<int>() ?? 5000,
 RequiresAuth = json["RequiresAuth"]?.Value<bool>() ?? false,
       Version = json["Version"]?.ToString() ?? "1.0.0"
       };
     
          if (!discovered.Any(d => d.IpAddress == pc.IpAddress && d.Port == pc.Port))
       {
 discovered.Add(pc);
  Console.WriteLine($"Discovered PC: {pc.Hostname} at {pc.IpAddress}:{pc.Port}");
               }
     }
      catch (Exception ex)
           {
       Console.WriteLine($"Failed to parse discovery response: {ex.Message}");
        }
          }
 }
  catch (Exception)
     {
                break;
       }
            }
        }

     public async Task<RemotePCStatus> GetStatusAsync(string host, int port, string? authToken = null)
        {
   if (string.IsNullOrEmpty(host))
return new RemotePCStatus { IsOnline = false };

       var isReachable = await PingAsync(host, port);
      if (!isReachable)
     {
          Console.WriteLine($"Host {host}:{port} is not reachable");
       return new RemotePCStatus { IsOnline = false };
            }

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
         Console.WriteLine($"Response content: {content}");
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
    Console.WriteLine($"HTTP error getting PC status: {ex.Message}");
    }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting PC status: {ex.GetType().Name}: {ex.Message}");
        }

          return new RemotePCStatus { IsOnline = false };
        }

        public async Task<bool> PingAsync(string host, int port)
  {
            try
        {
            using var client = new TcpClient();
    var connectTask = client.ConnectAsync(host, port);
       var completedTask = await Task.WhenAny(connectTask, Task.Delay(3000));

                if (completedTask == connectTask && client.Connected)
        {
      Console.WriteLine($"Successfully connected to {host}:{port}");
     return true;
        }

        Console.WriteLine($"Connection to {host}:{port} timed out or failed");
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
