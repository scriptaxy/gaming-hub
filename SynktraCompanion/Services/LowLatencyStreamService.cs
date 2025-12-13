using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace SynktraCompanion.Services;

/// <summary>
/// Ultra low-latency screen streaming using Desktop Duplication API + UDP
/// Target: ~30-50ms end-to-end latency on LAN
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
    private int _wsPort = 5002;
    private int _udpPort = 5003;
    
    // Stream settings optimized for low latency
    private int _targetFps = 60;
    private int _quality = 40; // Lower quality = faster encoding
  private int _width = 1280;
    private int _height = 720;
    private bool _useUdp = true; // UDP is faster but may lose frames

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

    public double CaptureLatency => _lastCaptureMs;
    public double EncodeLatency => _lastEncodeMs;
  public double TotalLatency => _lastCaptureMs + _lastEncodeMs + _lastSendMs;
    public int CurrentFps => _fps;

    // Desktop Duplication (Windows 8+)
    private DesktopDuplicator? _duplicator;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    public async Task StartAsync(int wsPort = 5002, int udpPort = 5003)
    {
        if (_isStreaming) return;

        _wsPort = wsPort;
        _udpPort = udpPort;
        _cts = new CancellationTokenSource();

        try
        {
            // Initialize Desktop Duplication for GPU-accelerated capture
            _duplicator = new DesktopDuplicator();
      
            // Start UDP server for low-latency clients
            _udpServer = new UdpClient();
            _udpServer.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpServer.Client.Bind(new IPEndPoint(IPAddress.Any, _udpPort));
            _ = Task.Run(() => ListenForUdpClientsAsync(_cts.Token));

            // Start WebSocket server for fallback
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://*:{wsPort}/");
            _httpListener.Start();
            _ = Task.Run(() => AcceptWebSocketConnectionsAsync(_cts.Token));

_isStreaming = true;
  Console.WriteLine($"Low-latency streaming started - UDP:{udpPort} WS:{wsPort}");

  // Start streaming frames
_ = Task.Run(() => StreamFramesAsync(_cts.Token));
      }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start low-latency streaming: {ex.Message}");
            _isStreaming = false;
            Stop(); // Clean up any partially started services
        }
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

        Console.WriteLine("Low-latency streaming stopped");
    }

    public void SetQuality(int quality) => _quality = Math.Clamp(quality, 10, 100);
    public void SetTargetFps(int fps) => _targetFps = Math.Clamp(fps, 30, 120);
  public void SetResolution(int width, int height)
    {
   _width = Math.Clamp(width, 320, 1920);
     _height = Math.Clamp(height, 240, 1080);
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
               
 // Send ACK
      var ack = Encoding.UTF8.GetBytes("STREAM_ACK");
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
         // Input command
 ProcessInputCommand(message);
           }
            }
  catch (OperationCanceledException) { break; }
   catch (Exception ex)
     {
     Console.WriteLine($"UDP listener error: {ex.Message}");
      }
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

     lock (_clientsLock)
   {
     _wsClients.Add(ws);
           }

         Console.WriteLine($"WebSocket client connected. Total clients: {ClientCount}");
        _ = Task.Run(() => HandleWebSocketClientAsync(ws, ct));
        }
  else if (context.Request.Url?.AbsolutePath == "/stream/stats")
          {
     // Return latency stats
          var stats = new
        {
                captureMs = _lastCaptureMs,
           encodeMs = _lastEncodeMs,
        sendMs = _lastSendMs,
      totalMs = TotalLatency,
        fps = _fps,
             clients = ClientCount
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
   ProcessInputCommand(message);
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

    private void ProcessInputCommand(string json)
    {
        try
        {
       var cmd = JsonConvert.DeserializeObject<InputCommand>(json);
            if (cmd != null)
            {
            InputSimulator.Instance.ProcessCommand(cmd);
      }
        }
        catch (Exception ex)
     {
    Console.WriteLine($"Input command error: {ex.Message}");
        }
}

    private async Task StreamFramesAsync(CancellationToken ct)
    {
        var sw = new Stopwatch();
        var targetFrameTime = 1000.0 / _targetFps;

        while (!ct.IsCancellationRequested && _isStreaming)
        {
            sw.Restart();

          try
            {
        if (ClientCount > 0)
         {
         // Capture
            var captureStart = sw.ElapsedMilliseconds;
            var frameData = CaptureFrame();
      _lastCaptureMs = sw.ElapsedMilliseconds - captureStart;

    if (frameData != null && frameData.Length > 0)
          {
              // Send
              var sendStart = sw.ElapsedMilliseconds;
             await BroadcastFrameAsync(frameData, ct);
            _lastSendMs = sw.ElapsedMilliseconds - sendStart;
         }

            // Update FPS counter
 _frameCount++;
       if ((DateTime.Now - _lastFpsUpdate).TotalSeconds >= 1)
        {
      _fps = _frameCount;
     _frameCount = 0;
           _lastFpsUpdate = DateTime.Now;
    }
}
            }
 catch (Exception ex)
            {
 Console.WriteLine($"Frame streaming error: {ex.Message}");
    }

  // Maintain target FPS
      var elapsed = sw.ElapsedMilliseconds;
            var delay = (int)(targetFrameTime - elapsed);
       if (delay > 0)
         {
     await Task.Delay(delay, ct);
      }
  }
    }

    private byte[]? CaptureFrame()
    {
        var sw = Stopwatch.StartNew();
        
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

            // Resize if needed (on GPU would be faster, but this works)
            Bitmap finalBitmap;
            if (bitmap.Width != _width || bitmap.Height != _height)
   {
      finalBitmap = new Bitmap(bitmap, _width, _height);
 bitmap.Dispose();
        }
            else
            {
    finalBitmap = bitmap;
  }

          var encodeStart = sw.ElapsedMilliseconds;

          // Encode to JPEG with turbo settings
          using var stream = new MemoryStream();
            var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
  var encoderParams = new EncoderParameters(1);
    encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)_quality);
            finalBitmap.Save(stream, encoder, encoderParams);

          _lastEncodeMs = sw.ElapsedMilliseconds - encodeStart;

     finalBitmap.Dispose();
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Capture error: {ex.Message}");
            return null;
        }
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

    private async Task BroadcastFrameAsync(byte[] frameData, CancellationToken ct)
    {
        // Send via UDP (faster, may lose frames)
        if (_useUdp && _udpServer != null)
        {
List<IPEndPoint> udpClientsCopy;
  lock (_clientsLock)
            {
   udpClientsCopy = [.. _udpClients];
   }

            // UDP has size limits, so we may need to fragment large frames
            // For simplicity, we'll just send if under ~64KB (actual UDP max is ~65507 bytes)
            const int MaxUdpPacketSize = 65000;
            if (frameData.Length < MaxUdpPacketSize)
            {
     foreach (var client in udpClientsCopy)
    {
     try
          {
     await _udpServer.SendAsync(frameData, frameData.Length, client);
          }
          catch { }
     }
      }
        }

     // Send via WebSocket (reliable but slower)
        List<WebSocket> wsClientsCopy;
   lock (_clientsLock)
        {
    wsClientsCopy = [.. _wsClients];
        }

  var deadClients = new List<WebSocket>();

        foreach (var client in wsClientsCopy)
 {
    try
            {
          if (client.State == WebSocketState.Open)
      {
        await client.SendAsync(
      new ArraySegment<byte>(frameData),
      WebSocketMessageType.Binary,
      true,
    ct);
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
  // Create D3D11 device
        _device = new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware);

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

        Console.WriteLine($"Desktop Duplication initialized: {_width}x{_height}");
    }

    public Bitmap? CaptureFrame()
    {
        if (!_initialized || _duplicatedOutput == null || _device == null || _stagingTexture == null)
          return null;

        try
        {
            // Try to acquire next frame (1ms timeout for low latency)
         var result = _duplicatedOutput.TryAcquireNextFrame(1, 
     out var frameInfo, 
     out var desktopResource);

if (result.Failure)
      return null;

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
       // Create bitmap from data
     var bitmap = new Bitmap(_width, _height, PixelFormat.Format32bppArgb);
                var boundsRect = new Rectangle(0, 0, _width, _height);
      var bmpData = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);

     var sourcePtr = mapSource.DataPointer;
     var destPtr = bmpData.Scan0;

     for (int y = 0; y < _height; y++)
       {
      SharpDX.Utilities.CopyMemory(
    destPtr + y * bmpData.Stride,
          sourcePtr + y * mapSource.RowPitch,
     _width * 4);
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
     Dispose();
            try { Initialize(); } catch { _initialized = false; }
return null;
   }
      catch (Exception ex)
        {
Console.WriteLine($"Desktop Duplication capture error: {ex.Message}");
   return null;
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
