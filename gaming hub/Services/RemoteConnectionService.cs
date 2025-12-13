using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace gaming_hub.Services;

/// <summary>
/// Remote connection client for connecting to PC from outside the local network
/// Uses STUN/hole punching for NAT traversal
/// </summary>
public class RemoteConnectionService
{
    private static RemoteConnectionService? _instance;
    public static RemoteConnectionService Instance => _instance ??= new RemoteConnectionService();

    // Free public STUN servers
    private static readonly string[] StunServers =
    [
   "stun.l.google.com:19302",
     "stun1.l.google.com:19302",
        "stun.cloudflare.com:3478"
    ];

    public bool IsConnected { get; private set; }
    public string? ConnectedIP { get; private set; }
    public int? ConnectedPort { get; private set; }
    public string? LastError { get; private set; }

    public event Action<string>? OnLog;
    public event Action<bool>? OnConnectionStatusChanged;

    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;

    private RemoteConnectionService() { }

    /// <summary>
    /// Connect to a remote PC using the connection code
    /// </summary>
    public async Task<RemoteConnectionResult> ConnectWithCodeAsync(string connectionCode, string? knownPublicIp = null, int? knownPublicPort = null)
    {
        Log($"Attempting remote connection with code: {connectionCode}");
 _cts = new CancellationTokenSource();

     try
    {
         // Step 1: Get our own public endpoint via STUN
    var ourEndpoint = await GetOurPublicEndpointAsync();
   Log($"Our public endpoint: {ourEndpoint.ip}:{ourEndpoint.port}");

     // Step 2: Try to connect to the PC
   _udpClient = new UdpClient();
_udpClient.Client.ReceiveTimeout = 5000;

            // If we know the public IP, try direct connection
          if (!string.IsNullOrEmpty(knownPublicIp) && knownPublicPort.HasValue)
    {
                var result = await TryDirectConnectionAsync(connectionCode, knownPublicIp, knownPublicPort.Value);
if (result.Success)
     {
    return result;
  }
            }

            // Step 3: Try hole punching if we have endpoint info
 // This would work if both sides are doing STUN simultaneously

   LastError = "Direct connection failed. Ensure both devices are on the same network, or set up port forwarding.";
            return new RemoteConnectionResult { Success = false, Error = LastError };
        }
        catch (Exception ex)
        {
       LastError = ex.Message;
   Log($"Connection failed: {ex.Message}");
            return new RemoteConnectionResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Connect using QR code data
    /// </summary>
    public async Task<RemoteConnectionResult> ConnectWithQrDataAsync(string qrData)
    {
  try
      {
  var json = Encoding.UTF8.GetString(Convert.FromBase64String(qrData));
            var data = JsonSerializer.Deserialize<QrConnectionData>(json);

  if (data == null)
            {
    return new RemoteConnectionResult { Success = false, Error = "Invalid QR code" };
      }

            // Try public IP first, then local IP
            var result = await ConnectWithCodeAsync(data.Code, data.Ip, data.Port);
      
if (!result.Success && !string.IsNullOrEmpty(data.LocalIp))
     {
      // Fallback to local IP (same network)
       Log("Trying local IP fallback...");
      result = await TryDirectConnectionAsync(data.Code, data.LocalIp, data.LocalPort);
            }

 return result;
        }
catch (Exception ex)
        {
   return new RemoteConnectionResult { Success = false, Error = ex.Message };
      }
    }

    /// <summary>
    /// Try direct UDP connection to the PC
    /// </summary>
    private async Task<RemoteConnectionResult> TryDirectConnectionAsync(string code, string ip, int port)
    {
 try
        {
    Log($"Trying direct connection to {ip}:{port}...");

   var endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
       
            // Send connection request
      var request = Encoding.UTF8.GetBytes($"SYNKTRA_CONNECT:{code}");
     
          for (int attempt = 0; attempt < 3; attempt++)
   {
      await _udpClient!.SendAsync(request, request.Length, endpoint);
          
            try
       {
         using var cts = new CancellationTokenSource(3000);
    var response = await _udpClient.ReceiveAsync(cts.Token);
          var responseStr = Encoding.UTF8.GetString(response.Buffer);
            
      if (responseStr.Contains("CONNECTED"))
             {
                 var connectionData = JsonSerializer.Deserialize<ConnectionResponse>(responseStr);
              
            IsConnected = true;
    ConnectedIP = ip;
      ConnectedPort = connectionData?.ApiPort ?? 19500;
             
          OnConnectionStatusChanged?.Invoke(true);
       Log($"Connected successfully to {ip}");
            
     return new RemoteConnectionResult
    {
       Success = true,
             Host = ip,
  ApiPort = connectionData?.ApiPort ?? 19500,
      StreamWsPort = connectionData?.StreamWsPort ?? 19501,
        StreamUdpPort = connectionData?.StreamUdpPort ?? 19502,
     AudioPort = connectionData?.AudioPort ?? 19503
           };
         }
  }
                catch (OperationCanceledException)
                {
   Log($"Attempt {attempt + 1} timed out");
        }
 }

            return new RemoteConnectionResult { Success = false, Error = "Connection timed out" };
        }
        catch (Exception ex)
        {
 Log($"Direct connection failed: {ex.Message}");
            return new RemoteConnectionResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Get our public endpoint via STUN
    /// </summary>
    private async Task<(string? ip, int? port)> GetOurPublicEndpointAsync()
    {
   foreach (var server in StunServers)
        {
         try
 {
           var parts = server.Split(':');
              var host = parts[0];
        var port = int.Parse(parts[1]);

    using var udp = new UdpClient();
      udp.Client.ReceiveTimeout = 3000;

        var stunRequest = BuildStunRequest();
          var serverEndpoint = new IPEndPoint(
     (await Dns.GetHostAddressesAsync(host)).First(a => a.AddressFamily == AddressFamily.InterNetwork),
         port
            );

 await udp.SendAsync(stunRequest, stunRequest.Length, serverEndpoint);
            var response = await udp.ReceiveAsync();
              
    return ParseStunResponse(response.Buffer);
            }
catch { }
 }

        return (null, null);
    }

    private byte[] BuildStunRequest()
    {
      var request = new byte[20];
        request[0] = 0x00;
        request[1] = 0x01;
        request[4] = 0x21;
        request[5] = 0x12;
        request[6] = 0xA4;
        request[7] = 0x42;
    
    var random = new Random();
        for (int i = 8; i < 20; i++)
            request[i] = (byte)random.Next(256);
       
        return request;
    }

    private (string? ip, int? port) ParseStunResponse(byte[] response)
    {
      if (response.Length < 20 || response[0] != 0x01 || response[1] != 0x01)
            return (null, null);

        var messageLength = (response[2] << 8) | response[3];
   var offset = 20;

  while (offset < 20 + messageLength)
      {
        var attrType = (response[offset] << 8) | response[offset + 1];
   var attrLength = (response[offset + 2] << 8) | response[offset + 3];

   if (attrType == 0x0020 && response[offset + 5] == 0x01) // XOR-MAPPED-ADDRESS IPv4
            {
           var port = ((response[offset + 6] << 8) | response[offset + 7]) ^ 0x2112;
     var ip = new IPAddress(new[]
       {
 (byte)(response[offset + 8] ^ 0x21),
      (byte)(response[offset + 9] ^ 0x12),
    (byte)(response[offset + 10] ^ 0xA4),
           (byte)(response[offset + 11] ^ 0x42)
    }).ToString();
 
      return (ip, port);
 }

            offset += 4 + attrLength;
        if (attrLength % 4 != 0)
        offset += 4 - (attrLength % 4);
        }

        return (null, null);
    }

    /// <summary>
    /// Disconnect from remote PC
    /// </summary>
    public void Disconnect()
    {
      _cts?.Cancel();
        _udpClient?.Close();
        _udpClient?.Dispose();
 _udpClient = null;
      
        IsConnected = false;
      ConnectedIP = null;
      ConnectedPort = null;
        
  OnConnectionStatusChanged?.Invoke(false);
        Log("Disconnected");
    }

    /// <summary>
    /// Send keep-alive to maintain connection
    /// </summary>
    public async Task SendKeepAliveAsync()
    {
  if (!IsConnected || _udpClient == null || string.IsNullOrEmpty(ConnectedIP))
    return;

        try
        {
    var ping = Encoding.UTF8.GetBytes("SYNKTRA_PING");
            var endpoint = new IPEndPoint(IPAddress.Parse(ConnectedIP), ConnectedPort ?? 19505);
   await _udpClient.SendAsync(ping, ping.Length, endpoint);
    }
        catch { }
    }

    private void Log(string message)
    {
        Console.WriteLine($"[RemoteConnection] {message}");
        OnLog?.Invoke(message);
    }
}

public class RemoteConnectionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? Host { get; set; }
    public int ApiPort { get; set; }
    public int StreamWsPort { get; set; }
    public int StreamUdpPort { get; set; }
    public int AudioPort { get; set; }
}

public class QrConnectionData
{
    public string Code { get; set; } = string.Empty;
    public string? Ip { get; set; }
    public int? Port { get; set; }
    public string? LocalIp { get; set; }
    public int LocalPort { get; set; }
  public string? Nat { get; set; }
}

public class ConnectionResponse
{
    public string? Type { get; set; }
    public int ApiPort { get; set; }
    public int StreamWsPort { get; set; }
    public int StreamUdpPort { get; set; }
    public int AudioPort { get; set; }
    public string? Key { get; set; }
}
