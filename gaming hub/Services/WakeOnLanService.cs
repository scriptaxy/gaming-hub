using System.Net;
using System.Net.Sockets;
using Foundation;

namespace gaming_hub.Services
{
    /// <summary>
    /// Wake-on-LAN service to remotely wake up the PC
    /// </summary>
    public class WakeOnLanService
    {
    private static WakeOnLanService? _instance;
     public static WakeOnLanService Instance => _instance ??= new WakeOnLanService();

    private const int WolPort = 9;

        private WakeOnLanService() { }

        /// <summary>
        /// Send Wake-on-LAN magic packet to wake up a PC
        /// </summary>
        /// <param name="macAddress">MAC address in format XX:XX:XX:XX:XX:XX or XX-XX-XX-XX-XX-XX</param>
        /// <param name="broadcastAddress">Broadcast address (default: 255.255.255.255)</param>
        /// <returns>True if packet was sent successfully</returns>
        public async Task<bool> WakeAsync(string macAddress, string? broadcastAddress = null)
     {
      try
            {
 var macBytes = ParseMacAddress(macAddress);
  if (macBytes == null)
       {
  Console.WriteLine($"Invalid MAC address: {macAddress}");
  return false;
 }

         var magicPacket = BuildMagicPacket(macBytes);
    var broadcast = broadcastAddress ?? "255.255.255.255";

        using var client = new UdpClient();
      client.EnableBroadcast = true;

    var endpoint = new IPEndPoint(IPAddress.Parse(broadcast), WolPort);
          
 // Send multiple times for reliability
      for (int i = 0; i < 3; i++)
       {
    await client.SendAsync(magicPacket, magicPacket.Length, endpoint);
        await Task.Delay(100);
}

                Console.WriteLine($"WOL packet sent to {macAddress} via {broadcast}");
                return true;
            }
 catch (Exception ex)
            {
    Console.WriteLine($"Failed to send WOL packet: {ex.Message}");
                return false;
  }
        }

        /// <summary>
        /// Send Wake-on-LAN to a specific subnet
        /// </summary>
   public async Task<bool> WakeOnSubnetAsync(string macAddress, string ipAddress)
      {
            try
   {
       // Calculate broadcast address from IP
        var parts = ipAddress.Split('.');
        if (parts.Length == 4)
                {
             var broadcast = $"{parts[0]}.{parts[1]}.{parts[2]}.255";
        return await WakeAsync(macAddress, broadcast);
            }
   return await WakeAsync(macAddress);
            }
      catch
       {
          return await WakeAsync(macAddress);
      }
        }

        /// <summary>
        /// Build the magic packet (6x 0xFF + 16x MAC address)
      /// </summary>
        private byte[] BuildMagicPacket(byte[] macAddress)
        {
     var packet = new byte[102]; // 6 + 16*6 = 102 bytes

// First 6 bytes are 0xFF
      for (int i = 0; i < 6; i++)
            {
             packet[i] = 0xFF;
        }

    // Repeat MAC address 16 times
            for (int i = 0; i < 16; i++)
            {
                Array.Copy(macAddress, 0, packet, 6 + i * 6, 6);
       }

       return packet;
        }

    /// <summary>
        /// Parse MAC address string to bytes
  /// </summary>
    private byte[]? ParseMacAddress(string macAddress)
        {
            try
            {
            // Remove common separators
  var cleaned = macAddress.Replace(":", "").Replace("-", "").Replace(" ", "");
     
          if (cleaned.Length != 12)
       return null;

        var bytes = new byte[6];
         for (int i = 0; i < 6; i++)
         {
bytes[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);
     }
        return bytes;
            }
            catch
  {
                return null;
   }
        }

        /// <summary>
        /// Validate MAC address format
        /// </summary>
        public bool IsValidMacAddress(string macAddress)
     {
        return ParseMacAddress(macAddress) != null;
        }

        /// <summary>
        /// Format MAC address to standard format (XX:XX:XX:XX:XX:XX)
        /// </summary>
        public string? FormatMacAddress(string macAddress)
        {
     var bytes = ParseMacAddress(macAddress);
     if (bytes == null) return null;
            return string.Join(":", bytes.Select(b => b.ToString("X2")));
        }
    }
}
