using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace SynktraCompanion.Services;

/// <summary>
/// Tailscale integration for secure remote access across different networks
/// Tailscale creates a mesh VPN so devices appear on the same network
/// 
/// Benefits:
/// - No port forwarding needed
/// - Works behind NAT/CGNAT
/// - End-to-end encrypted (WireGuard)
/// - Free for personal use (up to 100 devices)
/// 
/// Setup: Install Tailscale on both PC and iPhone, sign in with same account
/// https://tailscale.com/download
/// </summary>
public class TailscaleIntegration
{
    private static TailscaleIntegration? _instance;
    public static TailscaleIntegration Instance => _instance ??= new TailscaleIntegration();

    public bool IsInstalled { get; private set; }
  public bool IsConnected { get; private set; }
    public string? TailscaleIP { get; private set; }
    public string? Hostname { get; private set; }
    public string? Status { get; private set; }

    private TailscaleIntegration()
  {
        CheckTailscaleStatus();
    }

    /// <summary>
    /// Check if Tailscale is installed and get connection status
    /// </summary>
    public void CheckTailscaleStatus()
    {
        try
        {
     // Check if Tailscale CLI is available
            var process = new Process
            {
           StartInfo = new ProcessStartInfo
       {
   FileName = "tailscale",
       Arguments = "status --json",
         UseShellExecute = false,
    RedirectStandardOutput = true,
         RedirectStandardError = true,
    CreateNoWindow = true
    }
            };

            process.Start();
       var output = process.StandardOutput.ReadToEnd();
          process.WaitForExit(5000);

      if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
    IsInstalled = true;
         ParseTailscaleStatus(output);
 }
    else
    {
         IsInstalled = false;
  IsConnected = false;
        Status = "Tailscale not running";
            }
      }
        catch (Exception)
        {
       IsInstalled = false;
      IsConnected = false;
      Status = "Tailscale not installed";
        }
  }

    private void ParseTailscaleStatus(string jsonOutput)
    {
     try
     {
  using var doc = JsonDocument.Parse(jsonOutput);
      var root = doc.RootElement;

        // Check backend state
    if (root.TryGetProperty("BackendState", out var state))
        {
           IsConnected = state.GetString() == "Running";
          Status = state.GetString();
       }

       // Get our Tailscale IP
         if (root.TryGetProperty("Self", out var self))
            {
 if (self.TryGetProperty("TailscaleIPs", out var ips) && ips.GetArrayLength() > 0)
            {
     TailscaleIP = ips[0].GetString();
       }
      if (self.TryGetProperty("HostName", out var hostname))
    {
          Hostname = hostname.GetString();
      }
            }

       Console.WriteLine($"[Tailscale] Status: {Status}, IP: {TailscaleIP}, Hostname: {Hostname}");
   }
        catch (Exception ex)
 {
            Console.WriteLine($"[Tailscale] Error parsing status: {ex.Message}");
 }
    }

    /// <summary>
    /// Get the best IP address for streaming (prefers Tailscale for remote access)
    /// </summary>
    public string GetBestStreamingIP()
    {
        // If Tailscale is connected, use Tailscale IP for remote access capability
        if (IsConnected && !string.IsNullOrEmpty(TailscaleIP))
   {
            return TailscaleIP;
    }

// Fall back to local IP
        return GetLocalIP() ?? "127.0.0.1";
    }

    private string? GetLocalIP()
    {
        try
        {
   foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
      if (ni.OperationalStatus == OperationalStatus.Up &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
    !ni.Description.ToLower().Contains("virtual") &&
          !ni.Description.ToLower().Contains("tailscale"))
                {
       var props = ni.GetIPProperties();
      foreach (var addr in props.UnicastAddresses)
             {
      if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
    {
    return addr.Address.ToString();
            }
   }
      }
         }
}
    catch { }
    return null;
    }

    /// <summary>
    /// Get connection info for display in the app
    /// </summary>
    public TailscaleConnectionInfo GetConnectionInfo()
    {
        CheckTailscaleStatus(); // Refresh status

     return new TailscaleConnectionInfo
  {
IsInstalled = IsInstalled,
   IsConnected = IsConnected,
         TailscaleIP = TailscaleIP,
        LocalIP = GetLocalIP(),
            Hostname = Hostname,
         Status = Status,
 DownloadUrl = "https://tailscale.com/download",
            SetupInstructions = GetSetupInstructions()
        };
}

    private string GetSetupInstructions()
    {
        return """
     ?? Tailscale Setup for Remote Streaming:
            
          1. Install Tailscale on your PC:
      https://tailscale.com/download/windows
            
            2. Install Tailscale on your iPhone:
            App Store ? Search "Tailscale"
       
       3. Sign in with the SAME account on both devices
           (Google, Microsoft, GitHub, etc.)
            
      4. Both devices will get a 100.x.x.x IP address
          
     5. Use the Tailscale IP to connect from anywhere!
  
      ? Benefits:
            • Works through any firewall/NAT
       • No port forwarding needed
            • Military-grade encryption (WireGuard)
            • Free for personal use
            • ~5-15ms added latency (acceptable for gaming)
            """;
    }
}

public class TailscaleConnectionInfo
{
    public bool IsInstalled { get; set; }
    public bool IsConnected { get; set; }
    public string? TailscaleIP { get; set; }
    public string? LocalIP { get; set; }
    public string? Hostname { get; set; }
    public string? Status { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
  public string SetupInstructions { get; set; } = string.Empty;
}
