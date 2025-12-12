using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;

namespace SynktraCompanion.Services;

/// <summary>
/// Screen capture and streaming service for remote play
/// </summary>
public class ScreenStreamService
{
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cts;
    private readonly List<WebSocket> _clients = [];
    private readonly object _clientsLock = new();
    private bool _isStreaming;
    private int _port;
    private int _targetFps = 30;
    private int _quality = 50; // JPEG quality 1-100
    private Size _resolution = new(1280, 720);

  public bool IsStreaming => _isStreaming;
    public int ClientCount { get { lock (_clientsLock) return _clients.Count; } }

    // Windows API for screen capture
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSource, int xSrc, int ySrc, int rop);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SRCCOPY = 0x00CC0020;

    public async Task StartAsync(int port = 5002)
 {
        if (_isStreaming) return;

  _port = port;
        _cts = new CancellationTokenSource();
_httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://*:{port}/");

 try
      {
          _httpListener.Start();
_isStreaming = true;
            Console.WriteLine($"Screen streaming server started on port {port}");

         // Start accepting WebSocket connections
        _ = Task.Run(() => AcceptConnectionsAsync(_cts.Token));

            // Start streaming frames
     _ = Task.Run(() => StreamFramesAsync(_cts.Token));
        }
      catch (Exception ex)
        {
    Console.WriteLine($"Failed to start screen streaming: {ex.Message}");
        _isStreaming = false;
   }
    }

    public void Stop()
    {
        _isStreaming = false;
    _cts?.Cancel();

        lock (_clientsLock)
        {
       foreach (var client in _clients)
        {
   try { client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server stopped", CancellationToken.None).Wait(1000); }
       catch { }
            }
            _clients.Clear();
  }

        try { _httpListener?.Stop(); } catch { }
        _httpListener = null;
      Console.WriteLine("Screen streaming stopped");
    }

    public void SetQuality(int quality)
    {
        _quality = Math.Clamp(quality, 10, 100);
    }

 public void SetTargetFps(int fps)
    {
        _targetFps = Math.Clamp(fps, 5, 60);
    }

    public void SetResolution(int width, int height)
    {
        _resolution = new Size(Math.Clamp(width, 320, 1920), Math.Clamp(height, 240, 1080));
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
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
               _clients.Add(ws);
 }

     Console.WriteLine($"Stream client connected. Total: {ClientCount}");

       // Handle client messages (for receiving input commands)
      _ = Task.Run(() => HandleClientMessagesAsync(ws, ct));
   }
       else if (context.Request.Url?.AbsolutePath == "/stream/config")
                {
        // Return stream configuration
       var config = new
  {
    Width = _resolution.Width,
  Height = _resolution.Height,
            Fps = _targetFps,
           Quality = _quality
                    };
        var json = JsonConvert.SerializeObject(config);
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
   Console.WriteLine($"Error accepting connection: {ex.Message}");
            }
        }
    }

    private async Task HandleClientMessagesAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];

     try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
     {
var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

    if (result.MessageType == WebSocketMessageType.Close)
      {
           break;
    }

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
             _clients.Remove(ws);
            }
      Console.WriteLine($"Stream client disconnected. Total: {ClientCount}");
 }
    }

    private void ProcessInputCommand(string json)
    {
        try
        {
         var cmd = JsonConvert.DeserializeObject<InputCommand>(json);
          if (cmd == null) return;

   InputSimulator.Instance.ProcessCommand(cmd);
        }
  catch (Exception ex)
        {
            Console.WriteLine($"Error processing input: {ex.Message}");
        }
    }

    private async Task StreamFramesAsync(CancellationToken ct)
    {
        var frameInterval = TimeSpan.FromMilliseconds(1000.0 / _targetFps);

        while (!ct.IsCancellationRequested && _isStreaming)
        {
      var frameStart = DateTime.Now;

   try
       {
     if (ClientCount > 0)
           {
              var frameData = CaptureScreen();
        if (frameData != null && frameData.Length > 0)
            {
      await BroadcastFrameAsync(frameData, ct);
 }
      }
            }
          catch (Exception ex)
   {
     Console.WriteLine($"Frame capture error: {ex.Message}");
          }

     // Maintain target FPS
  var elapsed = DateTime.Now - frameStart;
     var delay = frameInterval - elapsed;
            if (delay > TimeSpan.Zero)
  {
    await Task.Delay(delay, ct);
       }
        }
    }

  private byte[]? CaptureScreen()
    {
     try
  {
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
  int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            using var bitmap = new Bitmap(screenWidth, screenHeight, PixelFormat.Format24bppRgb);
       using var graphics = Graphics.FromImage(bitmap);

            IntPtr desktopDc = GetWindowDC(GetDesktopWindow());
      IntPtr graphicsDc = graphics.GetHdc();

  BitBlt(graphicsDc, 0, 0, screenWidth, screenHeight, desktopDc, 0, 0, SRCCOPY);

graphics.ReleaseHdc(graphicsDc);
    ReleaseDC(GetDesktopWindow(), desktopDc);

        // Resize if needed
            Bitmap finalBitmap;
         if (screenWidth != _resolution.Width || screenHeight != _resolution.Height)
            {
      finalBitmap = new Bitmap(bitmap, _resolution);
  }
     else
          {
           finalBitmap = bitmap;
     }

     // Encode to JPEG
          using var stream = new MemoryStream();
          var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
   var encoderParams = new EncoderParameters(1);
       encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)_quality);
        finalBitmap.Save(stream, encoder, encoderParams);

       if (finalBitmap != bitmap)
            {
           finalBitmap.Dispose();
            }

          return stream.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Screen capture failed: {ex.Message}");
     return null;
   }
    }

    private async Task BroadcastFrameAsync(byte[] frameData, CancellationToken ct)
    {
        List<WebSocket> clientsCopy;
        lock (_clientsLock)
        {
            clientsCopy = [.. _clients];
        }

        var deadClients = new List<WebSocket>();

        foreach (var client in clientsCopy)
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

   // Remove dead clients
        if (deadClients.Count > 0)
{
            lock (_clientsLock)
            {
       foreach (var dead in deadClients)
   {
  _clients.Remove(dead);
             }
            }
     }
    }
}

/// <summary>
/// Input command from remote client
/// </summary>
public class InputCommand
{
    public string Type { get; set; } = string.Empty; // "gamepad", "mouse", "keyboard"
    
    // Gamepad
    public float LeftStickX { get; set; }
    public float LeftStickY { get; set; }
    public float RightStickX { get; set; }
    public float RightStickY { get; set; }
    public float LeftTrigger { get; set; }
 public float RightTrigger { get; set; }
 public GamepadButtons Buttons { get; set; }
    
    // Mouse
    public int MouseX { get; set; }
    public int MouseY { get; set; }
    public bool MouseLeft { get; set; }
    public bool MouseRight { get; set; }
    
    // Keyboard
public int KeyCode { get; set; }
    public bool KeyDown { get; set; }
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
