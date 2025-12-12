using System.Net.Sockets;
using System.Net.NetworkInformation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace gaming_hub.Services
{
    public class RemotePCService
    {
        private static RemotePCService? _instance;
    private readonly HttpClient _httpClient;

        public static RemotePCService Instance => _instance ??= new RemotePCService();

        private RemotePCService()
        {
   var handler = new HttpClientHandler
  {
                // Allow self-signed certificates for local network
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
      };
        _httpClient = new HttpClient(handler) 
 { 
        Timeout = TimeSpan.FromSeconds(5) 
    };
     }

 public async Task<RemotePCStatus> GetStatusAsync(string host, int port, string? authToken = null)
        {
        if (string.IsNullOrEmpty(host))
  return new RemotePCStatus { IsOnline = false };

  // First try a quick ping to check if host is reachable
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
     Hostname = json["hostname"]?.ToString() ?? json["computer_name"]?.ToString() ?? host,
     CpuUsage = json["cpu_usage"]?.Value<double>() ?? json["cpu"]?.Value<double>() ?? 0,
         MemoryUsage = json["memory_usage"]?.Value<double>() ?? json["memory"]?.Value<double>() ?? json["ram"]?.Value<double>() ?? 0,
         GpuUsage = json["gpu_usage"]?.Value<double?>() ?? json["gpu"]?.Value<double?>(),
        GpuTemperature = json["gpu_temp"]?.Value<double?>() ?? json["gpu_temperature"]?.Value<double?>(),
          CurrentGame = json["current_game"]?.ToString() ?? json["game"]?.ToString() ?? json["running_game"]?.ToString(),
         IsStreaming = json["is_streaming"]?.Value<bool>() ?? json["streaming"]?.Value<bool>() ?? false,
       Uptime = json["uptime"]?.ToString()
        };
             }
        else
   {
    Console.WriteLine($"Non-success status code: {response.StatusCode}");
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

   /// <summary>
        /// Simple check if host responds to ICMP ping
        /// </summary>
        public async Task<bool> IcmpPingAsync(string host)
    {
            try
 {
        using var ping = new Ping();
    var reply = await ping.SendPingAsync(host, 2000);
          return reply.Status == IPStatus.Success;
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
   if (macBytes == null)
   {
      Console.WriteLine($"Invalid MAC address: {macAddress}");
       return false;
       }
    
           // Build magic packet: 6 bytes of 0xFF followed by MAC address repeated 16 times
            var magicPacket = new byte[102];
                for (int i = 0; i < 6; i++) 
                  magicPacket[i] = 0xFF;
       for (int i = 6; i < 102; i += 6) 
      Array.Copy(macBytes, 0, magicPacket, i, 6);
       
   using var udpClient = new UdpClient();
                udpClient.EnableBroadcast = true;
  
      // Send to multiple broadcast addresses for better chance of success
      var endpoints = new List<System.Net.IPEndPoint>();
     
  if (!string.IsNullOrEmpty(broadcastAddress))
               endpoints.Add(new System.Net.IPEndPoint(System.Net.IPAddress.Parse(broadcastAddress), 9));
             
              // Also send to common broadcast addresses
     endpoints.Add(new System.Net.IPEndPoint(System.Net.IPAddress.Broadcast, 9));
     endpoints.Add(new System.Net.IPEndPoint(System.Net.IPAddress.Parse("255.255.255.255"), 7));
      endpoints.Add(new System.Net.IPEndPoint(System.Net.IPAddress.Parse("255.255.255.255"), 9));
  
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

        public async Task<StreamingSession?> StartStreamingAsync(string host, int port, string? authToken = null)
        {
 try
        {
var url = $"http://{host}:{port}/api/stream/start";
          var request = new HttpRequestMessage(HttpMethod.Post, url);
     if (!string.IsNullOrEmpty(authToken))
       request.Headers.Add("Authorization", $"Bearer {authToken}");
   
  var response = await _httpClient.SendAsync(request);
   if (response.IsSuccessStatusCode)
       {
       var content = await response.Content.ReadAsStringAsync();
             return JsonConvert.DeserializeObject<StreamingSession>(content);
      }
            }
         catch (Exception ex)
     {
    Console.WriteLine($"Error starting stream: {ex.Message}");
 }
      return null;
        }

      public async Task<bool> StopStreamingAsync(string host, int port, string? authToken = null)
        {
  try
{
   var url = $"http://{host}:{port}/api/stream/stop";
      var request = new HttpRequestMessage(HttpMethod.Post, url);
      if (!string.IsNullOrEmpty(authToken))
 request.Headers.Add("Authorization", $"Bearer {authToken}");

   var response = await _httpClient.SendAsync(request);
    return response.IsSuccessStatusCode;
  }
      catch (Exception ex)
    {
              Console.WriteLine($"Error stopping stream: {ex.Message}");
        return false;
}
 }
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

    public class StreamingSession
 {
        public string SessionId { get; set; } = string.Empty;
      public string StreamUrl { get; set; } = string.Empty;
        public int Port { get; set; }
        public string? PairingCode { get; set; }
    }
}
