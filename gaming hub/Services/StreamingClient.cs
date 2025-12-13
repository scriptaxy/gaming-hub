using System.Net.WebSockets;
using System.Text;
using System.Diagnostics;
using Newtonsoft.Json;

namespace gaming_hub.Services
{
    /// <summary>
    /// Client for receiving screen stream from Windows Companion
    /// Enhanced with stats, gyro support, quick actions
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

   // Stream info from PC
        private int _streamWidth = 1920;
        private int _streamHeight = 1080;

        // Performance stats
     private readonly Stopwatch _frameTimer = new();
   private int _frameCount;
        private int _fps;
        private double _latency;
        private double _bitrate;
        private long _bytesReceived;
   private DateTime _lastStatsUpdate = DateTime.Now;
        private readonly Queue<double> _frameTimes = new();
      private byte[]? _lastFrameData;

        public bool IsConnected => _isConnected && _webSocket?.State == WebSocketState.Open;
public int StreamWidth => _streamWidth;
        public int StreamHeight => _streamHeight;

        // Performance stats properties
     public int CurrentFps => _fps;
        public double Latency => _latency;
        public double BitrateKbps => _bitrate;
        public StreamStats Stats => new()
        {
   Fps = _fps,
 LatencyMs = _latency,
            BitrateKbps = _bitrate,
  Resolution = $"{_streamWidth}x{_streamHeight}",
        IsConnected = IsConnected
    };

        public event Action<byte[]>? OnFrameReceived;
        public event Action? OnConnected;
        public event Action<string>? OnDisconnected;
        public event Action<string>? OnError;
     public event Action<StreamInfo>? OnStreamInfo;
     public event Action<StreamStats>? OnStatsUpdated;

        private StreamingClient() 
        {
 _frameTimer.Start();
        }

  public async Task<bool> ConnectAsync(string host, int port)
        {
        if (IsConnected)
  await DisconnectAsync();

        _host = host;
            _port = port;
       _cts = new CancellationTokenSource();
            _frameCount = 0;
 _bytesReceived = 0;

       try
     {
       _webSocket = new ClientWebSocket();
  var uri = new Uri($"ws://{host}:{port}/");

      Console.WriteLine($"Connecting to stream at {uri}");

       await _webSocket.ConnectAsync(uri, _cts.Token);

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
      var buffer = new byte[1024 * 1024]; // 1MB buffer for frames
            var frameBuffer = new List<byte>();
   var frameStart = Stopwatch.GetTimestamp();

        try
            {
     while (_isConnected && _webSocket?.State == WebSocketState.Open && !_cts!.Token.IsCancellationRequested)
     {
      var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

             if (result.MessageType == WebSocketMessageType.Close)
             break;

          if (result.MessageType == WebSocketMessageType.Binary)
     {
          frameBuffer.AddRange(buffer.Take(result.Count));
         _bytesReceived += result.Count;

       if (result.EndOfMessage)
             {
                 var frameData = frameBuffer.ToArray();
          frameBuffer.Clear();
   _lastFrameData = frameData;

     // Calculate frame time
           var frameEnd = Stopwatch.GetTimestamp();
   var frameTimeMs = (frameEnd - frameStart) * 1000.0 / Stopwatch.Frequency;
frameStart = frameEnd;

           lock (_frameTimes)
              {
         _frameTimes.Enqueue(frameTimeMs);
  while (_frameTimes.Count > 60) _frameTimes.Dequeue();
         }

 _frameCount++;
              UpdateStats();
         OnFrameReceived?.Invoke(frameData);
                 }
           }
        else if (result.MessageType == WebSocketMessageType.Text)
         {
             var textData = Encoding.UTF8.GetString(buffer, 0, result.Count);
          ProcessTextMessage(textData);
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

      private void UpdateStats()
        {
 var now = DateTime.Now;
if ((now - _lastStatsUpdate).TotalMilliseconds >= 1000)
            {
    _fps = _frameCount;
       _frameCount = 0;

 // Calculate bitrate (kbps)
          _bitrate = _bytesReceived * 8.0 / 1000.0;
           _bytesReceived = 0;

            // Calculate average latency from frame times
           lock (_frameTimes)
       {
       if (_frameTimes.Count > 0)
                _latency = _frameTimes.Average();
}

    _lastStatsUpdate = now;
          OnStatsUpdated?.Invoke(Stats);
            }
        }

    private void ProcessTextMessage(string message)
        {
    try
          {
  var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
    if (json == null) return;

    if (json.TryGetValue("type", out var typeObj) && typeObj?.ToString() == "info")
{
            if (json.TryGetValue("width", out var w))
        _streamWidth = Convert.ToInt32(w);
   if (json.TryGetValue("height", out var h))
              _streamHeight = Convert.ToInt32(h);

         OnStreamInfo?.Invoke(new StreamInfo
     {
      Width = _streamWidth,
        Height = _streamHeight,
            Fps = json.TryGetValue("fps", out var fps) ? Convert.ToInt32(fps) : 30
              });
                }
            }
 catch (Exception ex)
       {
                Console.WriteLine($"Failed to parse text message: {ex.Message}");
            }
  }

        /// <summary>
        /// Get last frame for screenshot
        /// </summary>
 public byte[]? GetLastFrame() => _lastFrameData;

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
           Console.WriteLine($"Send gamepad error: {ex.Message}");
            }
        }

        /// <summary>
        /// Send gyroscope data for motion aiming
        /// </summary>
        public async Task SendGyroInputAsync(float pitch, float yaw, float roll)
        {
  if (!IsConnected) return;

            try
            {
         var command = new
  {
           Type = "gyro",
         Pitch = pitch,
             Yaw = yaw,
        Roll = roll
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
            Console.WriteLine($"Send gyro error: {ex.Message}");
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
        MouseX = (int)(normalizedX * _streamWidth),
       MouseY = (int)(normalizedY * _streamHeight),
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

        /// <summary>
        /// Send mouse move (relative movement)
        /// </summary>
   public async Task SendMouseMoveAsync(float normalizedX, float normalizedY)
        {
            if (!IsConnected) return;

    try
 {
 var command = new
      {
     Type = "mouse",
MouseX = (int)(normalizedX * _streamWidth),
  MouseY = (int)(normalizedY * _streamHeight),
           MouseLeft = false,
     MouseRight = false,
          MoveOnly = true
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
        Console.WriteLine($"Send mouse move error: {ex.Message}");
        }
   }

        /// <summary>
  /// Send mouse click
  /// </summary>
     public async Task SendMouseClickAsync(bool leftButton, bool rightButton, bool down)
        {
       if (!IsConnected) return;

            try
            {
       var command = new
        {
  Type = "mouseclick",
         LeftButton = leftButton,
             RightButton = rightButton,
        Down = down
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
       Console.WriteLine($"Send mouse click error: {ex.Message}");
     }
        }

        /// <summary>
     /// Send keyboard input
        /// </summary>
        public async Task SendKeyboardInputAsync(int keyCode, bool down)
        {
   if (!IsConnected) return;

            try
    {
            var command = new
      {
Type = "keyboard",
           KeyCode = keyCode,
  KeyDown = down
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
   Console.WriteLine($"Send keyboard error: {ex.Message}");
  }
    }

      /// <summary>
   /// Send quick action to PC (volume, media, etc.)
        /// </summary>
        public async Task SendQuickActionAsync(QuickActionType action, float? value = null)
        {
     if (!IsConnected) return;

      try
            {
                var command = new
           {
      Type = "quickaction",
             Action = action.ToString(),
  Value = value
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
      Console.WriteLine($"Send quick action error: {ex.Message}");
 }
        }
    }

    /// <summary>
    /// Stream info from PC
    /// </summary>
    public class StreamInfo
    {
 public int Width { get; set; }
        public int Height { get; set; }
        public int Fps { get; set; }
    }

    /// <summary>
    /// Stream performance stats
    /// </summary>
  public class StreamStats
    {
        public int Fps { get; set; }
public double LatencyMs { get; set; }
     public double BitrateKbps { get; set; }
        public string Resolution { get; set; } = "";
        public bool IsConnected { get; set; }
    }

  /// <summary>
    /// Quick action types
    /// </summary>
    public enum QuickActionType
  {
        VolumeUp,
      VolumeDown,
        VolumeMute,
        SetVolume,
     MediaPlayPause,
    MediaNext,
   MediaPrevious,
        MediaStop,
        Screenshot,
        OpenTaskManager,
  OpenSettings,
  MinimizeAll,
        ShowDesktop
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
    DPadRight = 8192,
        Guide = 16384
    }
}
