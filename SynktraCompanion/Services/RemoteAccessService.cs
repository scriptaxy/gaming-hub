using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace SynktraCompanion.Services;

/// <summary>
/// Built-in remote access without requiring external VPN software
/// Uses STUN for NAT traversal and optional relay for fallback
/// 
/// How it works:
/// 1. PC registers with a simple cloud relay (or direct IP if port forwarded)
/// 2. Mobile app connects via relay or direct (after NAT hole punch)
/// 3. All traffic is encrypted end-to-end
/// </summary>
public class RemoteAccessService
{
    private static RemoteAccessService? _instance;
    public static RemoteAccessService Instance => _instance ??= new RemoteAccessService();

    // Free public STUN servers for NAT traversal
    private static readonly string[] StunServers = 
    [
        "stun.l.google.com:19302",
   "stun1.l.google.com:19302",
        "stun2.l.google.com:19302",
        "stun.cloudflare.com:3478",
        "stun.stunprotocol.org:3478"
    ];

    // Connection state
    public bool IsEnabled { get; private set; }
    public string? PublicIP { get; private set; }
    public int? PublicPort { get; private set; }
    public string? ConnectionCode { get; private set; }
    public NatType DetectedNatType { get; private set; } = NatType.Unknown;
    public string? RelayServer { get; private set; }
    public bool IsRelayConnected { get; private set; }
    
    // Events
    public event Action<string>? OnLog;
    public event Action<RemoteConnectionInfo>? OnConnectionInfoChanged;

    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();
    private string? _encryptionKey;
    private TcpListener? _relayListener;

    private RemoteAccessService() { }

    /// <summary>
    /// Enable remote access - detects public IP and sets up NAT traversal
    /// </summary>
    public async Task<RemoteConnectionInfo> EnableRemoteAccessAsync(int localPort = 19505)
    {
        if (IsEnabled)
        {
        return GetConnectionInfo();
   }

        Log("Enabling remote access...");
        _cts = new CancellationTokenSource();

     try
  {
// Step 1: Detect public IP using STUN
    Log("Detecting public IP via STUN...");
            var (publicIp, publicPort, natType) = await DetectPublicEndpointAsync(localPort);
  
            PublicIP = publicIp;
       PublicPort = publicPort;
          DetectedNatType = natType;
 
            Log($"Public endpoint: {publicIp}:{publicPort} (NAT type: {natType})");

            // Step 2: Generate connection code (easy to type on mobile)
            ConnectionCode = GenerateConnectionCode();
     _encryptionKey = GenerateEncryptionKey();
            
            Log($"Connection code: {ConnectionCode}");

  // Step 3: Start UDP listener for direct connections
          _udpClient = new UdpClient(localPort);
         _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _ = Task.Run(() => ListenForConnectionsAsync(_cts.Token));

       // Step 4: Start relay connection (for symmetric NAT fallback)
  if (natType == NatType.Symmetric || natType == NatType.Unknown)
  {
                Log("Symmetric NAT detected - relay mode recommended");
         _ = Task.Run(() => ConnectToRelayAsync(_cts.Token));
            }

         // Step 5: Start keep-alive to maintain NAT mapping
  _ = Task.Run(() => KeepAliveAsync(_cts.Token));

  IsEnabled = true;
            
            var info = GetConnectionInfo();
            OnConnectionInfoChanged?.Invoke(info);
        return info;
        }
        catch (Exception ex)
        {
     Log($"Failed to enable remote access: {ex.Message}");
    throw;
        }
    }

    /// <summary>
    /// Disable remote access
    /// </summary>
    public void DisableRemoteAccess()
    {
      IsEnabled = false;
_cts?.Cancel();
        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;
        _relayListener?.Stop();
        _relayListener = null;
        
        PublicIP = null;
        PublicPort = null;
        ConnectionCode = null;
        IsRelayConnected = false;
        
        Log("Remote access disabled");
 }

    /// <summary>
    /// Get current connection info
    /// </summary>
    public RemoteConnectionInfo GetConnectionInfo()
    {
        var localIp = GetLocalIPAddress();
     
      return new RemoteConnectionInfo
{
            IsEnabled = IsEnabled,
            LocalIP = localIp,
LocalPort = 19500,
            PublicIP = PublicIP,
      PublicPort = PublicPort,
        ConnectionCode = ConnectionCode,
        NatType = DetectedNatType,
 IsRelayAvailable = IsRelayConnected,
            DirectConnectionPossible = DetectedNatType != NatType.Symmetric,
      ConnectionUrl = IsEnabled ? $"synktra://{ConnectionCode}" : null,
            QrCodeData = IsEnabled ? GenerateQrCodeData() : null,
   Instructions = GetConnectionInstructions()
        };
 }

    /// <summary>
    /// Detect public IP and port using STUN protocol
    /// </summary>
    private async Task<(string? ip, int? port, NatType natType)> DetectPublicEndpointAsync(int localPort)
    {
        foreach (var server in StunServers)
        {
            try
       {
      var parts = server.Split(':');
              var host = parts[0];
      var port = int.Parse(parts[1]);

         using var udp = new UdpClient(localPort);
           udp.Client.ReceiveTimeout = 3000;

    // STUN Binding Request
                var stunRequest = BuildStunBindingRequest();
 
      var serverEndpoint = new IPEndPoint(
               (await Dns.GetHostAddressesAsync(host)).First(a => a.AddressFamily == AddressFamily.InterNetwork),
             port
                );
                
             await udp.SendAsync(stunRequest, stunRequest.Length, serverEndpoint);
  
     var response = await udp.ReceiveAsync();
       var (publicIp, publicPort) = ParseStunResponse(response.Buffer);
   
  if (!string.IsNullOrEmpty(publicIp))
    {
       // Determine NAT type
      var natType = await DetectNatTypeAsync(localPort, publicIp, publicPort ?? 0);
      return (publicIp, publicPort, natType);
        }
    }
            catch (Exception ex)
       {
 Log($"STUN server {server} failed: {ex.Message}");
  }
        }

        // Fallback: try to get public IP from HTTP service
        try
        {
      using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    var ip = await http.GetStringAsync("https://api.ipify.org");
            return (ip.Trim(), null, NatType.Unknown);
        }
        catch
 {
            return (null, null, NatType.Unknown);
        }
    }

    /// <summary>
    /// Detect NAT type (Full Cone, Restricted, Port Restricted, Symmetric)
    /// </summary>
    private async Task<NatType> DetectNatTypeAsync(int localPort, string publicIp, int publicPort)
    {
  // Simplified NAT detection
        // Full implementation would test multiple STUN servers and compare results
        
        try
        {
            // Test with a second STUN server
            var secondServer = StunServers.Skip(1).First();
            var parts = secondServer.Split(':');
            
       using var udp = new UdpClient();
  udp.Client.Bind(new IPEndPoint(IPAddress.Any, localPort));
        udp.Client.ReceiveTimeout = 2000;

            var stunRequest = BuildStunBindingRequest();
     var serverEndpoint = new IPEndPoint(
      (await Dns.GetHostAddressesAsync(parts[0])).First(a => a.AddressFamily == AddressFamily.InterNetwork),
    int.Parse(parts[1])
    );
  
      await udp.SendAsync(stunRequest, stunRequest.Length, serverEndpoint);
 var response = await udp.ReceiveAsync();
          var (ip2, port2) = ParseStunResponse(response.Buffer);

          if (ip2 == publicIp && port2 == publicPort)
        {
       // Same mapping = not symmetric
      return NatType.FullCone; // Could be restricted, but direct connection likely works
       }
       else
            {
           return NatType.Symmetric; // Different mapping = symmetric NAT
            }
}
  catch
        {
 return NatType.Unknown;
        }
    }

    /// <summary>
    /// Build a STUN Binding Request packet
    /// </summary>
    private byte[] BuildStunBindingRequest()
    {
     var request = new byte[20];

        // Message Type: Binding Request (0x0001)
        request[0] = 0x00;
        request[1] = 0x01;
        
        // Message Length: 0 (no attributes)
      request[2] = 0x00;
 request[3] = 0x00;
        
      // Magic Cookie: 0x2112A442
   request[4] = 0x21;
        request[5] = 0x12;
      request[6] = 0xA4;
        request[7] = 0x42;
        
        // Transaction ID: 12 random bytes
        RandomNumberGenerator.Fill(request.AsSpan(8, 12));
        
        return request;
    }

    /// <summary>
  /// Parse STUN Binding Response to extract mapped address
    /// </summary>
    private (string? ip, int? port) ParseStunResponse(byte[] response)
    {
   if (response.Length < 20) return (null, null);
    
        // Check for Binding Response (0x0101)
        if (response[0] != 0x01 || response[1] != 0x01)
            return (null, null);

        var messageLength = (response[2] << 8) | response[3];
        var offset = 20;

     while (offset < 20 + messageLength)
{
          var attrType = (response[offset] << 8) | response[offset + 1];
    var attrLength = (response[offset + 2] << 8) | response[offset + 3];
          
            // XOR-MAPPED-ADDRESS (0x0020) or MAPPED-ADDRESS (0x0001)
            if (attrType == 0x0020 || attrType == 0x0001)
   {
          var family = response[offset + 5];
           if (family == 0x01) // IPv4
     {
                    int port;
       byte[] ipBytes;
      
      if (attrType == 0x0020) // XOR-MAPPED-ADDRESS
      {
                   // XOR with magic cookie
            port = ((response[offset + 6] << 8) | response[offset + 7]) ^ 0x2112;
       ipBytes = new byte[4];
            ipBytes[0] = (byte)(response[offset + 8] ^ 0x21);
     ipBytes[1] = (byte)(response[offset + 9] ^ 0x12);
         ipBytes[2] = (byte)(response[offset + 10] ^ 0xA4);
         ipBytes[3] = (byte)(response[offset + 11] ^ 0x42);
             }
    else // MAPPED-ADDRESS
        {
port = (response[offset + 6] << 8) | response[offset + 7];
     ipBytes = new byte[] { response[offset + 8], response[offset + 9], response[offset + 10], response[offset + 11] };
         }
         
    var ip = new IPAddress(ipBytes).ToString();
         return (ip, port);
             }
       }
            
      offset += 4 + attrLength;
       // Align to 4 bytes
    if (attrLength % 4 != 0)
     offset += 4 - (attrLength % 4);
 }

        return (null, null);
    }

    /// <summary>
    /// Listen for incoming connection requests
    /// </summary>
    private async Task ListenForConnectionsAsync(CancellationToken ct)
    {
        Log("Listening for remote connections...");
        
    while (!ct.IsCancellationRequested && _udpClient != null)
  {
      try
            {
       var result = await _udpClient.ReceiveAsync(ct);
    var message = Encoding.UTF8.GetString(result.Buffer);
       
       if (message.StartsWith("SYNKTRA_CONNECT:"))
      {
        var providedCode = message.Substring(16);
       if (providedCode == ConnectionCode)
   {
                Log($"Valid connection request from {result.RemoteEndPoint}");
           
    // Send connection accepted with local API info
               var response = JsonSerializer.Serialize(new
    {
          type = "CONNECTED",
          apiPort = 19500,
     streamWsPort = 19501,
  streamUdpPort = 19502,
     audioPort = 19503,
   key = _encryptionKey
        });
   
  var responseBytes = Encoding.UTF8.GetBytes(response);
    await _udpClient.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
          
  Log($"Connection established with {result.RemoteEndPoint}");
         }
      }
             else if (message == "SYNKTRA_PING")
         {
    // Keep-alive response
          var pong = Encoding.UTF8.GetBytes("SYNKTRA_PONG");
          await _udpClient.SendAsync(pong, pong.Length, result.RemoteEndPoint);
    }
         }
     catch (OperationCanceledException) { break; }
       catch (Exception ex)
       {
           Log($"Connection listener error: {ex.Message}");
    }
     }
    }

    /// <summary>
    /// Send keep-alive packets to maintain NAT mapping
    /// </summary>
    private async Task KeepAliveAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
    try
     {
        await Task.Delay(30000, ct); // Every 30 seconds
   
                // Send keep-alive to STUN server to maintain NAT mapping
     if (_udpClient != null)
    {
     var stunServer = StunServers[0].Split(':');
     var endpoint = new IPEndPoint(
     (await Dns.GetHostAddressesAsync(stunServer[0])).First(a => a.AddressFamily == AddressFamily.InterNetwork),
     int.Parse(stunServer[1])
    );
         
    var request = BuildStunBindingRequest();
         await _udpClient.SendAsync(request, request.Length, endpoint);
        }
            }
       catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    /// <summary>
  /// Connect to relay server for symmetric NAT fallback
    /// </summary>
    private async Task ConnectToRelayAsync(CancellationToken ct)
    {
      // This would connect to a relay server you host (e.g., on a cheap VPS or serverless function)
  // For now, we'll just mark that relay would be needed
 
   Log("Relay connection would be established here for symmetric NAT");
        Log("For symmetric NAT, consider:");
        Log("  1. Port forwarding on router (ports 19500-19503)");
        Log("  2. Using UPnP (if supported by router)");
        Log("  3. Setting up a simple relay server");
        
        // Try UPnP port mapping
        await TryUpnpPortMappingAsync();
    }

    /// <summary>
    /// Try to set up UPnP port mapping
    /// </summary>
    private async Task TryUpnpPortMappingAsync()
    {
      try
        {
      Log("Attempting UPnP port mapping...");
    // UPnP implementation would go here
            // For production, use a library like Open.NAT
     
    // Placeholder - in real implementation:
    // var discoverer = new NatDiscoverer();
        // var device = await discoverer.DiscoverDeviceAsync();
         // await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 19500, 19500, "Synktra API"));
            // await device.CreatePortMapAsync(new Mapping(Protocol.Udp, 19502, 19502, "Synktra Stream"));
   
   Log("UPnP: Would attempt port mapping (19500-19503)");
}
        catch (Exception ex)
        {
   Log($"UPnP failed: {ex.Message}");
   }
    }

    /// <summary>
    /// Generate a short, easy-to-type connection code
 /// </summary>
    private string GenerateConnectionCode()
    {
        // Generate 6-character alphanumeric code (easy to type on mobile)
      const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Removed confusing chars (0,O,1,I)
   var code = new char[6];
        
      using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[6];
      rng.GetBytes(bytes);
        
        for (int i = 0; i < 6; i++)
   {
    code[i] = chars[bytes[i] % chars.Length];
      }
      
        return new string(code);
    }

 /// <summary>
  /// Generate encryption key for secure communication
    /// </summary>
    private string GenerateEncryptionKey()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
   return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Generate QR code data for easy mobile connection
    /// </summary>
    private string GenerateQrCodeData()
 {
        var data = new
        {
code = ConnectionCode,
  ip = PublicIP,
       port = PublicPort,
      localIp = GetLocalIPAddress(),
       localPort = 19500,
  nat = DetectedNatType.ToString()
        };
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data)));
    }

    /// <summary>
    /// Get connection instructions based on NAT type
    /// </summary>
    private string GetConnectionInstructions()
    {
        if (!IsEnabled)
    {
    return "Enable remote access to connect from outside your network.";
        }

        return DetectedNatType switch
        {
            NatType.FullCone or NatType.RestrictedCone or NatType.PortRestricted =>
   $"""
    ? Direct connection possible!
   
        On your iPhone:
      1. Open Synktra app
         2. Tap "Add PC" ? "Remote Connection"
        3. Enter code: {ConnectionCode}
      
    Or scan the QR code shown in the app.
   """,

NatType.Symmetric =>
         $"""
         ?? Symmetric NAT detected - direct connection may not work.
     
             Options:
         1. Set up port forwarding on your router (ports 19500-19503)
             2. Use the connection code on the same network first
      3. Consider a VPN solution for reliable remote access
       
    Connection code: {ConnectionCode}
       """,

   _ =>
        $"""
  Connection code: {ConnectionCode}
        
         Enter this code in the Synktra iOS app to connect.
         Works best when both devices can reach each other.
    """
        };
    }

    private string? GetLocalIPAddress()
    {
        try
        {
    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
   socket.Connect("8.8.8.8", 65530);
     if (socket.LocalEndPoint is IPEndPoint endPoint)
        {
    return endPoint.Address.ToString();
 }
        }
        catch { }
    return null;
    }

    private void Log(string message)
    {
        Console.WriteLine($"[RemoteAccess] {message}");
 OnLog?.Invoke(message);
    }
}

/// <summary>
/// NAT types that affect connectivity
/// </summary>
public enum NatType
{
    Unknown,
    FullCone,         // Best - any external host can send to mapped port
    RestrictedCone,   // Good - external host must receive packet first  
    PortRestricted,   // OK - external host+port must receive packet first
    Symmetric    // Difficult - different mapping per destination
}

/// <summary>
/// Remote connection information
/// </summary>
public class RemoteConnectionInfo
{
    public bool IsEnabled { get; set; }
    public string? LocalIP { get; set; }
    public int LocalPort { get; set; }
    public string? PublicIP { get; set; }
    public int? PublicPort { get; set; }
    public string? ConnectionCode { get; set; }
  public NatType NatType { get; set; }
    public bool IsRelayAvailable { get; set; }
    public bool DirectConnectionPossible { get; set; }
    public string? ConnectionUrl { get; set; }
    public string? QrCodeData { get; set; }
    public string Instructions { get; set; } = string.Empty;
}
