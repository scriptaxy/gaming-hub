using System.Net.Sockets;
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
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
      }

        public async Task<RemotePCStatus> GetStatusAsync(string host, int port, string? authToken = null)
        {
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
            Hostname = json["hostname"]?.ToString() ?? host,
         CpuUsage = json["cpu_usage"]?.Value<double>() ?? 0,
        MemoryUsage = json["memory_usage"]?.Value<double>() ?? 0,
              GpuUsage = json["gpu_usage"]?.Value<double?>(),
              GpuTemperature = json["gpu_temp"]?.Value<double?>(),
     CurrentGame = json["current_game"]?.ToString(),
IsStreaming = json["is_streaming"]?.Value<bool>() ?? false,
 Uptime = json["uptime"]?.ToString()
};
     }
    }
catch (Exception ex)
  {
             Console.WriteLine($"Error getting PC status: {ex.Message}");
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
                return completedTask == connectTask && client.Connected;
          }
            catch { return false; }
        }

    public async Task<bool> WakeOnLanAsync(string macAddress, string? broadcastAddress = null)
 {
            try
          {
                var macBytes = ParseMacAddress(macAddress);
        if (macBytes == null) return false;
   var magicPacket = new byte[102];
                for (int i = 0; i < 6; i++) magicPacket[i] = 0xFF;
       for (int i = 6; i < 102; i += 6) Array.Copy(macBytes, 0, magicPacket, i, 6);
 using var udpClient = new UdpClient();
    udpClient.EnableBroadcast = true;
      var endpoint = new System.Net.IPEndPoint(
       string.IsNullOrEmpty(broadcastAddress) ? System.Net.IPAddress.Broadcast : System.Net.IPAddress.Parse(broadcastAddress), 9);
        await udpClient.SendAsync(magicPacket, magicPacket.Length, endpoint);
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
    var cleanMac = macAddress.Replace(":", "").Replace("-", "").Replace(" ", "");
      if (cleanMac.Length != 12) return null;
          var bytes = new byte[6];
    for (int i = 0; i < 6; i++)
                    bytes[i] = Convert.ToByte(cleanMac.Substring(i * 2, 2), 16);
   return bytes;
            }
            catch { return null; }
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
        public string ComputerName => Hostname; // Alias for compatibility
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
