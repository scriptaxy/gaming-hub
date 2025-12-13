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
    
    // Input queue for batched processing
    private readonly ConcurrentQueue<InputCommand> _inputQueue = new();
    private Thread? _inputProcessorThread;

    public bool IsStreaming => _isStreaming;
    public int ClientCount
    {
        get
        {
     lock (_clientsLock)
       return _udpClients.Count + _wsClients.Count;
        }
    }

    // Performance stats
    private double _lastCaptureMs;
    private double _lastEncodeMs;
    private double _lastSendMs;
private int _fps;
    private int _frameCount;
    private DateTime _lastFpsUpdate = DateTime.Now;
    private long _bytesSent;
    private double _bitrate;

  public double CaptureLatency => _lastCaptureMs;
    public double EncodeLatency => _lastEncodeMs;
    public double TotalLatency => _lastCaptureMs + _lastEncodeMs + _lastSendMs;
    public int CurrentFps => _fps;
    public double BitrateKbps => _bitrate;

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

  public void SetLowLatencyMode(bool enabled)
  {
        if (enabled)
        {
            _quality = 30;
_targetFps = 60;
         _width = 1280;
            _height = 720;
        }
        else
 {
          _quality = 50;
        _targetFps = 30;
         _width = 1920;
            _height = 1080;
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
      // Queue input command for batch processing
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

            if (remainingMs > 2)
            {
        Thread.Sleep((int)(remainingMs - 1));
            }
       
    // SpinWait for precise timing on last millisecond
   while (sw.ElapsedTicks * 1000.0 / Stopwatch.Frequency < targetFrameTimeMs)
        {
          Thread.SpinWait(10);
  }
        }
    }

  private byte[]? CaptureAndEncodeFrame()
    {
     try
   {
          Bitmap? bitmap = null;

  // Try Desktop Duplication first (fastest, GPU-based)
     if (_duplicator != null)
         {
                bitmap = _duplicator.CaptureFrame();
            }

     // Fallback to GDI
            if (bitmap == null)
     {
    bitmap = CaptureWithGdi();
}

            if (bitmap == null) return null;

        // Resize if needed
            Bitmap finalBitmap;
  if (bitmap.Width != _width || bitmap.Height != _height)
  {
     finalBitmap = ResizeFast(bitmap, _width, _height);
           bitmap.Dispose();
     }
   else
            {
        finalBitmap = bitmap;
            }

            var encodeStart = Stopwatch.GetTimestamp();

   // Encode to JPEG with optimized settings
            byte[] result;
  lock (_encodeLock)
         {
                _encodeBuffer!.SetLength(0);
      _encodeBuffer.Position = 0;
  finalBitmap.Save(_encodeBuffer, _jpegEncoder!, _encoderParams);
    result = _encodeBuffer.ToArray();
            }

     _lastEncodeMs = (Stopwatch.GetTimestamp() - encodeStart) * 1000.0 / Stopwatch.Frequency;

            finalBitmap.Dispose();
     return result;
        }
      catch (Exception ex)
        {
          Console.WriteLine($"Capture error: {ex.Message}");
            return null;
        }
    }

  private Bitmap ResizeFast(Bitmap source, int width, int height)
    {
      var dest = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(dest);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
        g.DrawImage(source, 0, 0, width, height);
    return dest;
 }

    private Bitmap? CaptureWithGdi()
    {
        try
        {
  int screenWidth = GetSystemMetrics(SM_CXSCREEN);
int screenHeight = GetSystemMetrics(SM_CYSCREEN);

        var bitmap = new Bitmap(screenWidth, screenHeight, PixelFormat.Format24bppRgb);
  using var graphics = Graphics.FromImage(bitmap);
         graphics.CopyFromScreen(0, 0, 0, 0, new Size(screenWidth, screenHeight), CopyPixelOperation.SourceCopy);
     return bitmap;
        }
        catch
        {
 return null;
        }
  }

    private void BroadcastFrameSync(byte[] frameData)
    {
        // Send via UDP (faster, may lose frames)
        if (_useUdp && _udpServer != null)
        {
        List<IPEndPoint> udpClientsCopy;
            lock (_clientsLock)
     {
      udpClientsCopy = [.. _udpClients];
            }

      // UDP has size limits - fragment if needed
        if (frameData.Length < 65000)
    {
          foreach (var client in udpClientsCopy)
           {
         try
        {
             _udpServer.Send(frameData, frameData.Length, client);
              }
          catch { }
              }
      }
    else
      {
                // Send fragmented for large frames
    SendFragmentedUdp(frameData, udpClientsCopy);
}
        }

        // Send via WebSocket (reliable but slightly higher latency)
        List<WebSocket> wsClientsCopy;
        lock (_clientsLock)
        {
          wsClientsCopy = [.. _wsClients];
        }

        var deadClients = new List<WebSocket>();

        // Use synchronous send for lower latency
        foreach (var client in wsClientsCopy)
      {
       try
          {
       if (client.State == WebSocketState.Open)
     {
                    client.SendAsync(
       new ArraySegment<byte>(frameData),
   WebSocketMessageType.Binary,
           true,
          CancellationToken.None).Wait(50); // Short timeout
         }
   else
    {
           deadClients.Add(client);
    }
            }
    catch
{
  deadClients.Add(client);
    }
        }

  if (deadClients.Count > 0)
        {
 lock (_clientsLock)
     {
            foreach (var dead in deadClients)
              {
   _wsClients.Remove(dead);
     }
   }
   }
    }

    private void SendFragmentedUdp(byte[] frameData, List<IPEndPoint> clients)
    {
        const int maxPacketSize = 60000;
        const int headerSize = 8; // 4 bytes frame ID + 2 bytes packet index + 2 bytes total packets
    var payloadSize = maxPacketSize - headerSize;
        var totalPackets = (frameData.Length + payloadSize - 1) / payloadSize;
        var frameId = Environment.TickCount;

        for (int i = 0; i < totalPackets; i++)
  {
     var offset = i * payloadSize;
        var length = Math.Min(payloadSize, frameData.Length - offset);
            var packet = new byte[headerSize + length];

    // Header
     BitConverter.GetBytes(frameId).CopyTo(packet, 0);
     BitConverter.GetBytes((ushort)i).CopyTo(packet, 4);
            BitConverter.GetBytes((ushort)totalPackets).CopyTo(packet, 6);

            // Payload
            Buffer.BlockCopy(frameData, offset, packet, headerSize, length);

         foreach (var client in clients)
            {
    try
         {
    _udpServer?.Send(packet, packet.Length, client);
       }
         catch { }
     }
        }
    }
}

/// <summary>
/// Desktop Duplication API wrapper for fast GPU-based screen capture
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

    public DesktopDuplicator()
    {
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

        // Get primary output (monitor)
        using var output = adapter.GetOutput(0);
      using var output1 = output.QueryInterface<SharpDX.DXGI.Output1>();

        var bounds = output.Description.DesktopBounds;
        _width = bounds.Right - bounds.Left;
        _height = bounds.Bottom - bounds.Top;

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

   Console.WriteLine($"Desktop Duplication initialized: {_width}x{_height}");
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
