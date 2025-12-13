using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Net;
using System.Net.WebSockets;

namespace SynktraCompanion.Services;

/// <summary>
/// Audio capture and streaming service for remote play
/// Captures system audio (what you hear) and streams to clients
/// </summary>
public class AudioStreamService
{
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cts;
    private readonly List<WebSocket> _clients = [];
    private readonly object _clientsLock = new();
    private bool _isStreaming;
    private int _port;

 // Audio capture
    private WasapiLoopbackCapture? _capture;
    private WaveFormat? _waveFormat;
    private readonly Queue<byte[]> _audioQueue = new();
    private readonly object _queueLock = new();

    public bool IsStreaming => _isStreaming;
    public int ClientCount { get { lock (_clientsLock) return _clients.Count; } }

 public async Task StartAsync(int port = 5003)
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
     Console.WriteLine($"Audio streaming server started on port {port}");

            // Initialize audio capture
 InitializeAudioCapture();

        // Start accepting WebSocket connections
_ = Task.Run(() => AcceptConnectionsAsync(_cts.Token));

      // Start streaming audio
            _ = Task.Run(() => StreamAudioAsync(_cts.Token));
        }
      catch (Exception ex)
   {
     Console.WriteLine($"Failed to start audio streaming: {ex.Message}");
      _isStreaming = false;
        }
    }

    public void Stop()
    {
        _isStreaming = false;
_cts?.Cancel();

        // Stop audio capture
   try
  {
     _capture?.StopRecording();
    _capture?.Dispose();
            _capture = null;
        }
        catch { }

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
        Console.WriteLine("Audio streaming stopped");
    }

    private void InitializeAudioCapture()
    {
        try
     {
    // Use WASAPI loopback to capture system audio
     _capture = new WasapiLoopbackCapture();
       _waveFormat = _capture.WaveFormat;

         Console.WriteLine($"Audio capture initialized: {_waveFormat.SampleRate}Hz, {_waveFormat.Channels} channels, {_waveFormat.BitsPerSample}-bit");

  _capture.DataAvailable += OnAudioDataAvailable;
            _capture.RecordingStopped += (s, e) =>
   {
    if (e.Exception != null)
        {
     Console.WriteLine($"Audio capture stopped with error: {e.Exception.Message}");
  }
            };

       _capture.StartRecording();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize audio capture: {ex.Message}");
     }
    }

    private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

  // Convert to 16-bit PCM if needed (for better compatibility)
        byte[] audioData;
        if (_waveFormat?.BitsPerSample == 32 && _waveFormat?.Encoding == WaveFormatEncoding.IeeeFloat)
        {
        // Convert 32-bit float to 16-bit PCM
 audioData = ConvertFloatTo16BitPcm(e.Buffer, e.BytesRecorded, _waveFormat.Channels);
        }
 else
  {
            audioData = new byte[e.BytesRecorded];
      Array.Copy(e.Buffer, audioData, e.BytesRecorded);
  }

        lock (_queueLock)
        {
            // Keep queue size reasonable to minimize latency
          while (_audioQueue.Count > 20)
     {
          _audioQueue.Dequeue();
       }
            _audioQueue.Enqueue(audioData);
        }
    }

    private byte[] ConvertFloatTo16BitPcm(byte[] floatBuffer, int bytesRecorded, int channels)
    {
        int samples = bytesRecorded / 4; // 4 bytes per float
        var pcmBuffer = new byte[samples * 2]; // 2 bytes per 16-bit sample

        for (int i = 0; i < samples; i++)
      {
      float sample = BitConverter.ToSingle(floatBuffer, i * 4);
      // Clamp and convert to 16-bit
         sample = Math.Clamp(sample, -1f, 1f);
    short pcmSample = (short)(sample * short.MaxValue);
            pcmBuffer[i * 2] = (byte)(pcmSample & 0xFF);
        pcmBuffer[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
        }

    return pcmBuffer;
 }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _httpListener?.IsListening == true)
   {
            try
     {
      var context = await _httpListener.GetContextAsync();

   if (context.Request.IsWebSocketRequest && context.Request.Url?.AbsolutePath == "/audio")
    {
        var wsContext = await context.AcceptWebSocketAsync(null);
  var ws = wsContext.WebSocket;

      lock (_clientsLock)
              {
               _clients.Add(ws);
     }

         Console.WriteLine($"Audio client connected. Total: {ClientCount}");

   // Send audio format info
       await SendAudioFormatInfoAsync(ws, ct);

     // Handle client (keep connection alive)
    _ = Task.Run(() => HandleClientAsync(ws, ct));
       }
       else if (context.Request.Url?.AbsolutePath == "/audio/config")
                {
            // Return audio configuration
         var config = new
           {
    SampleRate = _waveFormat?.SampleRate ?? 48000,
   Channels = _waveFormat?.Channels ?? 2,
   BitsPerSample = 16 // We convert to 16-bit
         };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(config);
        var buffer = System.Text.Encoding.UTF8.GetBytes(json);
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
         Console.WriteLine($"Error accepting audio connection: {ex.Message}");
     }
        }
    }

    private async Task SendAudioFormatInfoAsync(WebSocket ws, CancellationToken ct)
    {
     try
        {
       var info = new
            {
    type = "audioFormat",
      sampleRate = _waveFormat?.SampleRate ?? 48000,
             channels = _waveFormat?.Channels ?? 2,
     bitsPerSample = 16
      };
   var json = Newtonsoft.Json.JsonConvert.SerializeObject(info);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

          await ws.SendAsync(
          new ArraySegment<byte>(bytes),
  WebSocketMessageType.Text,
     true,
    ct);
    }
    catch (Exception ex)
        {
         Console.WriteLine($"Failed to send audio format: {ex.Message}");
        }
    }

    private async Task HandleClientAsync(WebSocket ws, CancellationToken ct)
  {
        var buffer = new byte[1024];

     try
        {
  while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
      {
var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

        if (result.MessageType == WebSocketMessageType.Close)
       {
             break;
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
       Console.WriteLine($"Audio client disconnected. Total: {ClientCount}");
        }
    }

    private async Task StreamAudioAsync(CancellationToken ct)
    {
   while (!ct.IsCancellationRequested && _isStreaming)
     {
          try
            {
  byte[]? audioData = null;

       lock (_queueLock)
     {
       if (_audioQueue.Count > 0)
     {
    audioData = _audioQueue.Dequeue();
   }
      }

      if (audioData != null && ClientCount > 0)
              {
    await BroadcastAudioAsync(audioData, ct);
  }
       else
          {
          // Small delay when no data
  await Task.Delay(5, ct);
            }
      }
        catch (OperationCanceledException) { break; }
       catch (Exception ex)
            {
           Console.WriteLine($"Audio stream error: {ex.Message}");
 }
    }
    }

    private async Task BroadcastAudioAsync(byte[] audioData, CancellationToken ct)
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
    new ArraySegment<byte>(audioData),
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
