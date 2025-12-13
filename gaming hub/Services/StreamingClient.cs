using System.Net.WebSockets;
using System.Text;
using Newtonsoft.Json;

namespace gaming_hub.Services
{
    /// <summary>
    /// Client for receiving screen stream from Windows Companion
    /// </summary>
    public class StreamingClient
    {
        private static StreamingClient? _instance;
        public static StreamingClient Instance => _instance ??= new StreamingClient();

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cts;
  private string? _host;
        private int _port;
      private bool _isConnected;

        public bool IsConnected => _isConnected && _webSocket?.State == WebSocketState.Open;

        public event Action<byte[]>? OnFrameReceived;
        public event Action? OnConnected;
        public event Action<string>? OnDisconnected;
   public event Action<string>? OnError;

   private StreamingClient() { }

      public async Task<bool> ConnectAsync(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                OnError?.Invoke("Invalid host address");
                return false;
            }

            if (port <= 0 || port > 65535)
            {
                OnError?.Invoke("Invalid port number");
                return false;
            }

            if (IsConnected)
            {
                await DisconnectAsync();
            }

            _host = host;
            _port = port;

            try
            {
                _webSocket = new ClientWebSocket();
                _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(5);
                var uri = new Uri($"ws://{host}:{port}/");

                Console.WriteLine($"Connecting to stream at {uri}");

                // Use a separate timeout for connection only
                using var connectionCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _webSocket.ConnectAsync(uri, connectionCts.Token);

                // Create long-lived token source for the connection lifecycle
                _cts = new CancellationTokenSource();

 if (_webSocket.State == WebSocketState.Open)
                {
 _isConnected = true;
       OnConnected?.Invoke();

          // Start receiving frames
   _ = Task.Run(ReceiveFramesAsync);

           Console.WriteLine("Stream connected");
    return true;
        }
            }
            catch (Exception ex)
            {
      Console.WriteLine($"Stream connection failed: {ex.Message}");
     OnError?.Invoke(ex.Message);
            }

   return false;
        }

        public async Task DisconnectAsync()
        {
            _isConnected = false;
            _cts?.Cancel();

            try
            {
        if (_webSocket?.State == WebSocketState.Open)
        {
    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
           }
            }
   catch { }

     _webSocket?.Dispose();
            _webSocket = null;

            OnDisconnected?.Invoke("Disconnected");
        }

        private async Task ReceiveFramesAsync()
        {
            var buffer = new byte[2 * 1024 * 1024]; // 2MB buffer for frames (increased for high-quality streams)
            var frameBuffer = new List<byte>();

try
       {
     while (_isConnected && _webSocket?.State == WebSocketState.Open && !_cts!.Token.IsCancellationRequested)
   {
           var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

       if (result.MessageType == WebSocketMessageType.Close)
      {
             break;
    }

                    if (result.MessageType == WebSocketMessageType.Binary)
         {
     frameBuffer.AddRange(buffer.Take(result.Count));

  if (result.EndOfMessage)
            {
          // Complete frame received
  var frameData = frameBuffer.ToArray();
        frameBuffer.Clear();

          OnFrameReceived?.Invoke(frameData);
          }
        }
         }
            }
      catch (OperationCanceledException) { }
    catch (WebSocketException ex)
            {
         Console.WriteLine($"WebSocket error: {ex.Message}");
       OnError?.Invoke(ex.Message);
    }
        catch (Exception ex)
          {
         Console.WriteLine($"Receive error: {ex.Message}");
                OnError?.Invoke(ex.Message);
 }

            _isConnected = false;
            OnDisconnected?.Invoke("Stream ended");
   }

        /// <summary>
        /// Send gamepad input to PC
     /// </summary>
        public async Task SendGamepadInputAsync(GamepadState state)
        {
    if (!IsConnected) return;

          try
            {
    var command = new
          {
       Type = "gamepad",
         state.LeftStickX,
               state.LeftStickY,
          state.RightStickX,
           state.RightStickY,
             state.LeftTrigger,
     state.RightTrigger,
             Buttons = (int)state.Buttons
      };

                var json = JsonConvert.SerializeObject(command);
    var bytes = Encoding.UTF8.GetBytes(json);

       await _webSocket!.SendAsync(
     new ArraySegment<byte>(bytes),
       WebSocketMessageType.Text,
         true,
          _cts!.Token);
     }
            catch (Exception ex)
  {
         Console.WriteLine($"Send input error: {ex.Message}");
         }
  }

    /// <summary>
        /// Send touch/mouse input to PC
        /// </summary>
        public async Task SendTouchInputAsync(float normalizedX, float normalizedY, bool tap)
        {
  if (!IsConnected) return;

  try
            {
              var command = new
         {
     Type = "mouse",
 MouseX = (int)(normalizedX * 1920), // Assume 1080p for now
         MouseY = (int)(normalizedY * 1080),
 MouseLeft = tap,
         MouseRight = false
    };

        var json = JsonConvert.SerializeObject(command);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _webSocket!.SendAsync(
          new ArraySegment<byte>(bytes),
        WebSocketMessageType.Text,
         true,
        _cts!.Token);
          }
    catch (Exception ex)
            {
        Console.WriteLine($"Send touch error: {ex.Message}");
          }
      }
    }

 /// <summary>
    /// Gamepad state for sending to PC
    /// </summary>
    public class GamepadState
    {
        public float LeftStickX { get; set; }
        public float LeftStickY { get; set; }
        public float RightStickX { get; set; }
   public float RightStickY { get; set; }
        public float LeftTrigger { get; set; }
 public float RightTrigger { get; set; }
        public GamepadButtons Buttons { get; set; }
}

    [Flags]
    public enum GamepadButtons
    {
        None = 0,
    A = 1,
        B = 2,
        X = 4,
        Y = 8,
        LeftBumper = 16,
        RightBumper = 32,
      Back = 64,
    Start = 128,
      LeftStick = 256,
     RightStick = 512,
        DPadUp = 1024,
      DPadDown = 2048,
        DPadLeft = 4096,
      DPadRight = 8192
    }
}
