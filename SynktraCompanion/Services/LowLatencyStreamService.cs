using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace SynktraCompanion.Services;

/// <summary>
/// Ultra low-latency screen streaming using Desktop Duplication API + UDP
/// Target: ~16-30ms end-to-end latency on LAN
/// With GPU priority to maintain smooth streaming during gaming
/// </summary>
public class LowLatencyStreamService
{
    private UdpClient? _udpServer;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cts;
    private readonly List<IPEndPoint> _udpClients = [];
    private readonly List<WebSocket> _wsClients = [];
    private readonly object _clientsLock = new();
    private bool _isStreaming;
    private int _wsPort = 19501;
    private int _udpPort = 19502;

    // Stream settings optimized for low latency
    private int _targetFps = 60;
    private int _quality = 35; // Lower quality = faster encoding
    private int _width = 1280;
    private int _height = 720;
    private bool _useUdp = true;

    // Performance optimization settings
    private bool _useHardwareEncoding = true;
    private bool _useDeltaFrames = false; // Send only changed regions
    private int _keyFrameInterval = 30; // Full frame every N frames
    private int _framesSinceKeyFrame = 0;
 private byte[]? _lastFrameData;
    
    // GPU resource allocation - percentage of GPU to reserve for streaming (0.2 = 20%, 0.3 = 30%)
private float _gpuResourceAllocation = 0.25f;
    private bool _adaptiveQuality = true;
    private bool _adaptiveBitrate = true;
    private int _minQuality = 20;
    private int _maxQuality = 70;
    private int _adaptiveTargetFps = 45; // Minimum acceptable FPS before quality adjustment
    
 // Network monitoring for adaptive bitrate
    private double _lastRtt = 0;
    private double _avgRtt = 0;
    private int _droppedFrames = 0;
    private DateTime _lastBitrateAdjustment = DateTime.MinValue;
    private readonly Queue<double> _rttSamples = new();

    // Performance stats
    private double _lastCaptureMs;
    private double _lastEncodeMs;
    private double _lastSendMs;
  private int _fps;
    private int _frameCount;
    private DateTime _lastFpsUpdate = DateTime.Now;
    private long _bytesSent;
  private double _bitrate;

    // Adaptive quality tracking
    private readonly Queue<int> _recentFps = new();
    private DateTime _lastQualityAdjustment = DateTime.MinValue;

    public double CaptureLatency => _lastCaptureMs;
    public double EncodeLatency => _lastEncodeMs;
    public double TotalLatency => _lastCaptureMs + _lastEncodeMs + _lastSendMs;
    public int CurrentFps => _fps;
    public double BitrateKbps => _bitrate;
    public int CurrentQuality => _quality;

    // Desktop Duplication (Windows 8+)
    private DesktopDuplicator? _duplicator;

    // Reusable buffers to reduce allocations
  private MemoryStream? _encodeBuffer;
    private readonly object _encodeLock = new();

    // Encoder settings
    private ImageCodecInfo? _jpegEncoder;
    private EncoderParameters? _encoderParams;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    public async Task StartAsync(int wsPort = 19501, int udpPort = 19502)
    {
        if (_isStreaming) return;

     _wsPort = wsPort;
        _udpPort = udpPort;
        _cts = new CancellationTokenSource();

        try
      {
// Set process priority for streaming
  SetStreamingPriority();
            
      // Pre-initialize encoder for faster first frame
         InitializeEncoder();

   // Initialize Desktop Duplication for GPU-accelerated capture
    _duplicator = new DesktopDuplicator();

        // Start UDP server for low-latency clients
            _udpServer = new UdpClient(_udpPort);
          _udpServer.Client.SendBufferSize = 1024 * 1024; // 1MB send buffer
_udpServer.Client.ReceiveBufferSize = 64 * 1024; // 64KB receive buffer
            _ = Task.Run(() => ListenForUdpClientsAsync(_cts.Token));

            // Start WebSocket server for fallback
        _httpListener = new HttpListener();
      _httpListener.Prefixes.Add($"http://*:{wsPort}/");
            _httpListener.Start();
_ = Task.Run(() => AcceptWebSocketConnectionsAsync(_cts.Token));

// Start high-priority input processor thread
_inputProcessorThread = new Thread(ProcessInputQueue)
            {
           Priority = ThreadPriority.Highest,
 IsBackground = true,
        Name = "InputProcessor"
       };
            _inputProcessorThread.Start();

            _isStreaming = true;
   Console.WriteLine($"Low-latency streaming started - UDP:{udpPort} WS:{wsPort}");
 Console.WriteLine($"  GPU allocation: {_gpuResourceAllocation * 100}%, Adaptive quality: {_adaptiveQuality}");

 // Start streaming frames on high priority thread
          var streamThread = new Thread(() => StreamFramesSync(_cts.Token))
     {
        Priority = ThreadPriority.AboveNormal,
      IsBackground = true,
           Name = "FrameStreamer"
};
   streamThread.Start();
        }
        catch (Exception ex)
  {
        Console.WriteLine($"Failed to start low-latency streaming: {ex.Message}");
            _isStreaming = false;
        }
    }

    /// <summary>
  /// Set process and thread priorities for smooth streaming during gaming
    /// </summary>
  private void SetStreamingPriority()
    {
        try
      {
            // Set process to above normal priority
      var process = System.Diagnostics.Process.GetCurrentProcess();
            process.PriorityClass = System.Diagnostics.ProcessPriorityClass.AboveNormal;
      
      // Set GPU priority via registry (requires admin, fail silently if not possible)
 SetGpuPriority();
      
Console.WriteLine("Streaming priority configured for smooth performance during gaming");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not set streaming priority: {ex.Message}");
        }
  }

    /// <summary>
    /// Configure GPU scheduling priority for the streaming process
    /// </summary>
    private void SetGpuPriority()
    {
      try
{
    var processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            var keyPath = $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{processName}.exe\PerfOptions";
     
         using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath, true);
          if (key != null)
     {
              // GPU Priority: 0 = Low, 1 = Normal, 2 = High
        // Set to 2 for high GPU priority for streaming
       key.SetValue("GpuPriority", 2, Microsoft.Win32.RegistryValueKind.DWord);
    
    // Also set CPU priority
   key.SetValue("CpuPriorityClass", 3, Microsoft.Win32.RegistryValueKind.DWord); // Above normal
            
                Console.WriteLine("GPU priority set to High for streaming process");
       }
        }
        catch
    {
   // Silently fail - requires admin privileges
        }
    }

    private void InitializeEncoder()
    {
        _jpegEncoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        _encoderParams = new EncoderParameters(2);
        _encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)_quality);
        _encoderParams.Param[1] = new EncoderParameter(System.Drawing.Imaging.Encoder.Compression, (long)EncoderValue.CompressionNone);
        _encodeBuffer = new MemoryStream(512 * 1024); // 512KB initial buffer
    }

    public void Stop()
    {
    _isStreaming = false;
        _cts?.Cancel();

  lock (_clientsLock)
     {
            _udpClients.Clear();
        foreach (var ws in _wsClients)
 {
       try { ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server stopped", CancellationToken.None).Wait(500); }
             catch { }
            }
            _wsClients.Clear();
      }

        try { _udpServer?.Close(); } catch { }
     try { _httpListener?.Stop(); } catch { }
        try { _duplicator?.Dispose(); } catch { }

        _udpServer = null;
        _httpListener = null;
     _duplicator = null;
        _encodeBuffer?.Dispose();
        _encodeBuffer = null;
     _lastFrameData = null;

     Console.WriteLine("Low-latency streaming stopped");
    }

    public void SetQuality(int quality)
    {
  _quality = Math.Clamp(quality, 10, 100);
        // Update encoder params
        if (_encoderParams != null)
      _encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)_quality);
    }

    public void SetTargetFps(int fps) => _targetFps = Math.Clamp(fps, 30, 120);

    public void SetResolution(int width, int height)
    {
        _width = Math.Clamp(width, 320, 1920);
    _height = Math.Clamp(height, 240, 1080);
    }

    /// <summary>
    /// Get list of available monitors
    /// </summary>
  public List<MonitorInfo> GetMonitors()
    {
        return _duplicator?.Monitors ?? new List<MonitorInfo>();
    }

    /// <summary>
    /// Select which monitor to stream
    /// </summary>
    public void SelectMonitor(int monitorIndex)
    {
        _duplicator?.SelectMonitor(monitorIndex);
        Console.WriteLine($"Switched to monitor {monitorIndex}");
    }

  /// <summary>
  /// Get currently selected monitor index
    /// </summary>
    public int GetSelectedMonitor()
    {
        return _duplicator?.SelectedMonitor ?? 0;
    }

    /// <summary>
    /// Set GPU resource allocation for streaming (0.0 to 1.0)
  /// Higher values = smoother streaming but may impact game performance
    /// Recommended: 0.2 - 0.3 (20-30%)
    /// </summary>
    public void SetGpuAllocation(float allocation)
    {
    _gpuResourceAllocation = Math.Clamp(allocation, 0.1f, 0.5f);
        Console.WriteLine($"GPU allocation set to {_gpuResourceAllocation * 100}%");
    }

    /// <summary>
    /// Enable/disable adaptive quality based on performance
    /// </summary>
    public void SetAdaptiveQuality(bool enabled, int minQuality = 25, int maxQuality = 60)
    {
      _adaptiveQuality = enabled;
        _minQuality = Math.Clamp(minQuality, 15, 50);
    _maxQuality = Math.Clamp(maxQuality, 40, 80);
    }

    public void SetLowLatencyMode(bool enabled)
    {
        if (enabled)
        {
            _quality = 30;
            _targetFps = 60;
        _width = 1280;
            _height = 720;
          _adaptiveQuality = true;
 }
   else
        {
       _quality = 50;
 _targetFps = 30;
  _width = 1920;
        _height = 1080;
            _adaptiveQuality = false;
     }
    }

    private async Task ListenForUdpClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udpServer != null)
        {
            try
       {
            var result = await _udpServer.ReceiveAsync(ct);
                var message = Encoding.UTF8.GetString(result.Buffer);

          if (message == "STREAM_CONNECT")
   {
            lock (_clientsLock)
         {
if (!_udpClients.Any(c => c.Equals(result.RemoteEndPoint)))
       {
            _udpClients.Add(result.RemoteEndPoint);
             Console.WriteLine($"UDP client connected: {result.RemoteEndPoint}");
            }
         }

            // Send ACK with stream info
    var ack = Encoding.UTF8.GetBytes($"STREAM_ACK|{_width}|{_height}|{_targetFps}");
await _udpServer.SendAsync(ack, ack.Length, result.RemoteEndPoint);
       }
  else if (message == "STREAM_DISCONNECT")
    {
    lock (_clientsLock)
 {
    _udpClients.RemoveAll(c => c.Equals(result.RemoteEndPoint));
}
         Console.WriteLine($"UDP client disconnected: {result.RemoteEndPoint}");
       }
  else if (message.StartsWith("{"))
                {
      // Queue input command for batched processing
    try
      {
  var cmd = JsonConvert.DeserializeObject<InputCommand>(message);
  if (cmd != null)
            _inputQueue.Enqueue(cmd);
     }
       catch { }
                }
 }
      catch (OperationCanceledException) { break; }
catch (Exception ex)
            {
          Console.WriteLine($"UDP listener error: {ex.Message}");
       }
        }
    }

    private void ProcessInputQueue()
    {
  while (_isStreaming)
      {
            // Process all queued inputs immediately
while (_inputQueue.TryDequeue(out var cmd))
    {
  try
     {
        InputSimulator.Instance.ProcessCommand(cmd);
     }
  catch { }
  }
            
            // Small sleep to prevent busy-waiting, but keep responsive
            Thread.Sleep(1);
    }
    }

    private async Task AcceptWebSocketConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _httpListener?.IsListening == true)
        {
  try
            {
    var context = await _httpListener.GetContextAsync();

        if (context.Request.IsWebSocketRequest)
    {
         var wsContext = await context.AcceptWebSocketAsync(null);
             var ws = wsContext.WebSocket;

    // Send stream info immediately
           var info = JsonConvert.SerializeObject(new
      {
        type = "info",
    width = _width,
            height = _height,
  fps = _targetFps
  });
        await ws.SendAsync(
      new ArraySegment<byte>(Encoding.UTF8.GetBytes(info)),
  WebSocketMessageType.Text,
                   true,
                ct);

    lock (_clientsLock)
    {
      _wsClients.Add(ws);
}

     Console.WriteLine($"WebSocket client connected. Total clients: {ClientCount}");
    _ = Task.Run(() => HandleWebSocketClientAsync(ws, ct));
                }
 else if (context.Request.Url?.AbsolutePath == "/stream/stats")
      {
   var stats = new
         {
           captureMs = _lastCaptureMs,
 encodeMs = _lastEncodeMs,
          sendMs = _lastSendMs,
      totalMs = TotalLatency,
     fps = _fps,
     bitrateKbps = _bitrate,
         clients = ClientCount,
       resolution = $"{_width}x{_height}",
  quality = _quality
    };
  var json = JsonConvert.SerializeObject(stats);
   var buffer = Encoding.UTF8.GetBytes(json);
          context.Response.ContentType = "application/json";
          context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, ct);
             context.Response.Close();
      }
             else
         {
  context.Response.StatusCode = 400;
     context.Response.Close();
      }
    }
          catch (OperationCanceledException) { break; }
     catch (HttpListenerException) { break; }
            catch (Exception ex)
         {
            Console.WriteLine($"WebSocket accept error: {ex.Message}");
    }
        }
    }

    private async Task HandleWebSocketClientAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];

        try
        {
         while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
          {
 var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

           if (result.MessageType == WebSocketMessageType.Close)
break;

     if (result.MessageType == WebSocketMessageType.Text)
       {
          var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
           try
        {
          var cmd = JsonConvert.DeserializeObject<InputCommand>(message);
    if (cmd != null)
                _inputQueue.Enqueue(cmd);
      }
        catch { }
           }
            }
        }
        catch { }
        finally
        {
         lock (_clientsLock)
{
 _wsClients.Remove(ws);
     }
          Console.WriteLine($"WebSocket client disconnected. Total clients: {ClientCount}");
    }
    }

    // Synchronous frame streaming for more precise timing
    private void StreamFramesSync(CancellationToken ct)
    {
        var sw = new Stopwatch();
  var targetFrameTimeMs = 1000.0 / _targetFps;
        var statsTimer = Stopwatch.StartNew();
    long bytesThisSecond = 0;
        
    // GPU throttling - add small delays to prevent GPU starvation for games
        var gpuThrottleMs = CalculateGpuThrottleDelay();

    while (!ct.IsCancellationRequested && _isStreaming)
    {
        sw.Restart();

 try
         {
   if (ClientCount > 0)
      {
      // Capture
      var captureStart = sw.ElapsedTicks;
      var frameData = CaptureAndEncodeFrame();
     _lastCaptureMs = (sw.ElapsedTicks - captureStart) * 1000.0 / Stopwatch.Frequency;

         if (frameData != null && frameData.Length > 0)
          {
         // Send
            var sendStart = sw.ElapsedTicks;
BroadcastFrameSync(frameData);
            _lastSendMs = (sw.ElapsedTicks - sendStart) * 1000.0 / Stopwatch.Frequency;

      bytesThisSecond += frameData.Length;
        }

     // Update stats
              _frameCount++;
          if (statsTimer.ElapsedMilliseconds >= 1000)
             {
       _fps = _frameCount;
       _bitrate = bytesThisSecond * 8.0 / 1000.0; // kbps
   _frameCount = 0;
bytesThisSecond = 0;
       statsTimer.Restart();
              
              // Adaptive quality adjustment
 if (_adaptiveQuality)
          {
          AdjustQualityBasedOnPerformance();
        }
   
      // Recalculate GPU throttle
   gpuThrottleMs = CalculateGpuThrottleDelay();
         }
            }
    }
       catch (Exception ex)
   {
                Console.WriteLine($"Frame streaming error: {ex.Message}");
        }

  // Precise frame timing using SpinWait for last few ms
            var elapsedMs = sw.ElapsedTicks * 1000.0 / Stopwatch.Frequency;
        var remainingMs = targetFrameTimeMs - elapsedMs;
    
            // Add GPU throttle delay to prevent starving games of GPU resources
         remainingMs += gpuThrottleMs;

         if (remainingMs > 2)
            {
      Thread.Sleep((int)(remainingMs - 1));
    }

            // SpinWait for precise timing on last millisecond
        var targetWithThrottle = targetFrameTimeMs + gpuThrottleMs;
            while (sw.ElapsedTicks * 1000.0 / Stopwatch.Frequency < targetWithThrottle)
            {
         Thread.SpinWait(10);
     }
        }
    }

    /// <summary>
    /// Calculate delay to add between frames to leave GPU headroom for games
    /// Based on _gpuResourceAllocation setting
 /// </summary>
    private double CalculateGpuThrottleDelay()
    {
  // If we want 25% GPU, we need to leave 75% for games
        // This translates to adding delay between captures
        // At 60fps with 16.67ms frame time, adding ~4ms delay gives ~20% more headroom
        
        var baseFrameTime = 1000.0 / _targetFps;
  
        // Calculate additional delay based on desired allocation
        // Lower allocation = more delay = more GPU for games
     var allocationFactor = 1.0 - _gpuResourceAllocation;
        var throttleDelay = baseFrameTime * allocationFactor * 0.3; // 30% of allocation factor
        
        return Math.Clamp(throttleDelay, 0, 10); // Max 10ms extra delay
    }

    /// <summary>
 /// Automatically adjust quality based on achieved FPS and network conditions
    /// </summary>
    private void AdjustQualityBasedOnPerformance()
    {
        // Don't adjust too frequently
        if ((DateTime.Now - _lastQualityAdjustment).TotalSeconds < 2)
        return;

_recentFps.Enqueue(_fps);
        while (_recentFps.Count > 5) _recentFps.Dequeue();

        if (_recentFps.Count < 3) return;

        var avgFps = _recentFps.Average();
        var qualityChanged = false;

        // Adaptive bitrate based on network conditions
        if (_adaptiveBitrate && _rttSamples.Count > 3)
        {
         _avgRtt = _rttSamples.Average();

          // High latency or packet loss - reduce quality aggressively
  if (_avgRtt > 50 || _droppedFrames > 5)
            {
      var reduction = _avgRtt > 100 ? 10 : (_avgRtt > 50 ? 5 : 3);
       var newQuality = Math.Max(_minQuality, _quality - reduction);
       if (newQuality != _quality)
    {
      SetQuality(newQuality);
   qualityChanged = true;
        Console.WriteLine($"Adaptive bitrate: Reduced to {_quality} (RTT: {_avgRtt:F1}ms, dropped: {_droppedFrames})");
           }
 _droppedFrames = 0; // Reset counter
            }
            // Low latency and stable - can increase quality
      else if (_avgRtt < 20 && _droppedFrames == 0 && avgFps >= _targetFps * 0.95 && _quality < _maxQuality)
            {
                var newQuality = Math.Min(_maxQuality, _quality + 2);
  if (newQuality != _quality)
                {
     SetQuality(newQuality);
        qualityChanged = true;
      Console.WriteLine($"Adaptive bitrate: Increased to {_quality} (RTT: {_avgRtt:F1}ms)");
     }
            }
 }

        // FPS-based adjustment (fallback)
        if (!qualityChanged && _adaptiveQuality)
        {
            // If FPS is dropping significantly below target, reduce quality
  if (avgFps < _adaptiveTargetFps && _quality > _minQuality)
   {
   var newQuality = Math.Max(_minQuality, _quality - 5);
       if (newQuality != _quality)
      {
            SetQuality(newQuality);
               Console.WriteLine($"Adaptive quality: Reduced to {_quality} (FPS: {avgFps:F1})");
        }
}
            // If FPS is good and we have headroom, increase quality slightly
        else if (avgFps >= _targetFps * 0.95 && _quality < _maxQuality)
            {
  var newQuality = Math.Min(_maxQuality, _quality + 2);
 if (newQuality != _quality)
    {
        SetQuality(newQuality);
     Console.WriteLine($"Adaptive quality: Increased to {_quality} (FPS: {avgFps:F1})");
       }
     }
        }

        _lastQualityAdjustment = DateTime.Now;
    }

    /// <summary>
    /// Record RTT sample from client ping
    /// </summary>
    public void RecordRttSample(double rttMs)
    {
        lock (_rttSamples)
        {
     _rttSamples.Enqueue(rttMs);
   while (_rttSamples.Count > 10) _rttSamples.Dequeue();
     }
        _lastRtt = rttMs;
    }

    /// <summary>
    /// Record a dropped frame (client didn't receive)
    /// </summary>
    public void RecordDroppedFrame()
    {
        _droppedFrames++;
    }

    /// <summary>
  /// Enable/disable adaptive bitrate based on network conditions
    /// </summary>
    public void SetAdaptiveBitrate(bool enabled)
    {
        _adaptiveBitrate = enabled;
      Console.WriteLine($"Adaptive bitrate: {(enabled ? "enabled" : "disabled")}");
    }
}

/// <summary>
/// Desktop Duplication API wrapper for fast GPU-based screen capture
/// Supports multiple monitors
/// </summary>
public class DesktopDuplicator : IDisposable
{
    private SharpDX.DXGI.OutputDuplication? _duplicatedOutput;
    private SharpDX.Direct3D11.Device? _device;
    private SharpDX.Direct3D11.Texture2D? _stagingTexture;
    private int _width;
    private int _height;
    private bool _initialized;
   private int _consecutiveFailures = 0;
    private const int MaxFailuresBeforeReinit = 10;
    private int _selectedMonitor = 0;

    public int Width => _width;
    public int Height => _height;
    public int SelectedMonitor => _selectedMonitor;
    public int MonitorCount { get; private set; }
    public List<MonitorInfo> Monitors { get; private set; } = new();

    public DesktopDuplicator(int monitorIndex = 0)
    {
        _selectedMonitor = monitorIndex;
     try
  {
       Initialize();
        }
        catch (Exception ex)
        {
     Console.WriteLine($"Desktop Duplication init failed (falling back to GDI): {ex.Message}");
      _initialized = false;
      }
    }

    private void Initialize()
    {
        // Create D3D11 device with optimized flags
      _device = new SharpDX.Direct3D11.Device(
         SharpDX.Direct3D.DriverType.Hardware,
        SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport);

  // Get DXGI device
        using var dxgiDevice = _device.QueryInterface<SharpDX.DXGI.Device>();
        using var adapter = dxgiDevice.GetParent<SharpDX.DXGI.Adapter>();
        using var factory = adapter.GetParent<SharpDX.DXGI.Factory1>();

      // Enumerate all monitors
  Monitors.Clear();
        MonitorCount = adapter.GetOutputCount();
        for (int i = 0; i < MonitorCount; i++)
        {
       using var output = adapter.GetOutput(i);
            var bounds = output.Description.DesktopBounds;
        Monitors.Add(new MonitorInfo
            {
      Index = i,
     Name = output.Description.DeviceName,
      Width = bounds.Right - bounds.Left,
             Height = bounds.Bottom - bounds.Top,
             X = bounds.Left,
        Y = bounds.Top,
       IsPrimary = i == 0
      });
        }
      Console.WriteLine($"Found {MonitorCount} monitor(s)");

        // Clamp selected monitor to valid range
      _selectedMonitor = Math.Clamp(_selectedMonitor, 0, MonitorCount - 1);

   // Get selected output (monitor)
        using var selectedOutput = adapter.GetOutput(_selectedMonitor);
      using var output1 = selectedOutput.QueryInterface<SharpDX.DXGI.Output1>();

        var selectedBounds = selectedOutput.Description.DesktopBounds;
       _width = selectedBounds.Right - selectedBounds.Left;
 _height = selectedBounds.Bottom - selectedBounds.Top;

       // Create staging texture for CPU access
var stagingDesc = new SharpDX.Direct3D11.Texture2DDescription
   {
  Width = _width,
     Height = _height,
     MipLevels = 1,
 ArraySize = 1,
 Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
      SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
   Usage = SharpDX.Direct3D11.ResourceUsage.Staging,
    BindFlags = SharpDX.Direct3D11.BindFlags.None,
            CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.Read,
     OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None
    };
    _stagingTexture = new SharpDX.Direct3D11.Texture2D(_device, stagingDesc);

     // Duplicate output
        _duplicatedOutput = output1.DuplicateOutput(_device);
    _initialized = true;
    _consecutiveFailures = 0;

        Console.WriteLine($"Desktop Duplication initialized: Monitor {_selectedMonitor} ({_width}x{_height})");
    }

  /// <summary>
    /// Switch to a different monitor
    /// </summary>
    public void SelectMonitor(int monitorIndex)
    {
   if (monitorIndex == _selectedMonitor && _initialized) return;
        _selectedMonitor = monitorIndex;
        Reinitialize();
    }

    public Bitmap? CaptureFrame()
    {
        if (!_initialized || _duplicatedOutput == null || _device == null || _stagingTexture == null)
      return null;

    try
        {
        // Try to acquire next frame (0ms timeout for minimum latency)
      var result = _duplicatedOutput.TryAcquireNextFrame(0,
                out var frameInfo,
       out var desktopResource);

            if (result.Failure)
       {
            _consecutiveFailures++;
    if (_consecutiveFailures > MaxFailuresBeforeReinit)
   {
           Reinitialize();
           }
    return null;
            }

      _consecutiveFailures = 0;

          using (desktopResource)
            {
   using var texture = desktopResource.QueryInterface<SharpDX.Direct3D11.Texture2D>();
        // Copy to staging texture
       _device.ImmediateContext.CopyResource(texture, _stagingTexture);
          }

            _duplicatedOutput.ReleaseFrame();

      // Map staging texture for CPU read
      var mapSource = _device.ImmediateContext.MapSubresource(
     _stagingTexture, 0,
     SharpDX.Direct3D11.MapMode.Read,
                SharpDX.Direct3D11.MapFlags.None);

            try
            {
                // Create bitmap from data - use 24bpp for faster JPEG encoding
     var bitmap = new Bitmap(_width, _height, PixelFormat.Format24bppRgb);
 var boundsRect = new Rectangle(0, 0, _width, _height);
     var bmpData = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);

     unsafe
        {
          var srcPtr = (byte*)mapSource.DataPointer;
         var dstPtr = (byte*)bmpData.Scan0;

  // Convert BGRA to BGR and copy
        for (int y = 0; y < _height; y++)
           {
   var srcRow = srcPtr + y * mapSource.RowPitch;
       var dstRow = dstPtr + y * bmpData.Stride;

            for (int x = 0; x < _width; x++)
   {
         // BGRA -> BGR (skip alpha)
     dstRow[x * 3 + 0] = srcRow[x * 4 + 0]; // B
      dstRow[x * 3 + 1] = srcRow[x * 4 + 1]; // G
          dstRow[x * 3 + 2] = srcRow[x * 4 + 2]; // R
 }
                  }
         }

                bitmap.UnlockBits(bmpData);
      return bitmap;
     }
        finally
     {
      _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);
       }
   }
        catch (SharpDX.SharpDXException ex) when (ex.ResultCode.Code == SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
    {
         // No new frame available
            return null;
  }
        catch (SharpDX.SharpDXException ex) when (ex.ResultCode.Code == SharpDX.DXGI.ResultCode.AccessLost.Result.Code)
        {
            // Access lost, need to reinitialize
            Console.WriteLine("Desktop Duplication access lost, reinitializing...");
       Reinitialize();
            return null;
        }
     catch (Exception ex)
  {
            Console.WriteLine($"Desktop Duplication capture error: {ex.Message}");
 _consecutiveFailures++;
      return null;
        }
 }

    private void Reinitialize()
    {
        Dispose();
        try
{
          Initialize();
   }
        catch
      {
            _initialized = false;
        }
    }

    public void Dispose()
    {
        _duplicatedOutput?.Dispose();
        _stagingTexture?.Dispose();
   _device?.Dispose();
    _duplicatedOutput = null;
        _stagingTexture = null;
        _device = null;
   _initialized = false;
    }
}

/// <summary>
/// Information about a connected monitor
/// </summary>
public class MonitorInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public bool IsPrimary { get; set; }
}
