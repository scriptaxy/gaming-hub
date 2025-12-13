using System.Net.WebSockets;
using AVFoundation;
using Foundation;

namespace gaming_hub.Services
{
    /// <summary>
    /// Client for receiving audio stream from Windows Companion
    /// Uses AVAudioEngine for low-latency playback
  /// </summary>
    public class AudioStreamClient
{
        private static AudioStreamClient? _instance;
      public static AudioStreamClient Instance => _instance ??= new AudioStreamClient();

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cts;
        private bool _isConnected;
        private bool _isPlaying;

        // Audio playback using AVAudioEngine
        private AVAudioEngine? _audioEngine;
 private AVAudioPlayerNode? _playerNode;
        private AVAudioFormat? _audioFormat;

        // Audio settings
 private double _sampleRate = 48000;
        private int _channels = 2;
   private float _volume = 1.0f;

        // Stats
        private int _packetsReceived;

        public bool IsConnected => _isConnected && _webSocket?.State == WebSocketState.Open;
        public bool IsPlaying => _isPlaying;

        public event Action? OnConnected;
        public event Action<string>? OnDisconnected;
        public event Action<string>? OnError;

 private AudioStreamClient() { }

    public async Task<bool> ConnectAsync(string host, int port)
   {
         if (IsConnected)
      await DisconnectAsync();

         _cts = new CancellationTokenSource();
   _packetsReceived = 0;

            try
      {
          _webSocket = new ClientWebSocket();
       var uri = new Uri($"ws://{host}:{port}/audio");

   Console.WriteLine($"Connecting to audio stream at {uri}");

     await _webSocket.ConnectAsync(uri, _cts.Token);

       if (_webSocket.State == WebSocketState.Open)
            {
         _isConnected = true;
        OnConnected?.Invoke();

       // Initialize audio engine
  InitializeAudioEngine();

       // Start receiving audio
  _ = Task.Run(ReceiveAudioAsync);

        Console.WriteLine("Audio stream connected");
   return true;
          }
     }
            catch (Exception ex)
            {
   Console.WriteLine($"Audio connection failed: {ex.Message}");
        OnError?.Invoke(ex.Message);
        }

 return false;
        }

     public async Task DisconnectAsync()
        {
   _isConnected = false;
            _isPlaying = false;
            _cts?.Cancel();

         StopAudioEngine();

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

  private void InitializeAudioEngine()
      {
  try
     {
     _audioEngine = new AVAudioEngine();
   _playerNode = new AVAudioPlayerNode();

  // Audio format: 48kHz, 16-bit stereo PCM
                _audioFormat = new AVAudioFormat(AVAudioCommonFormat.PCMInt16, _sampleRate, (uint)_channels, false);

_audioEngine.AttachNode(_playerNode);
            _audioEngine.Connect(_playerNode, _audioEngine.MainMixerNode, _audioFormat);

       // Set volume
 _audioEngine.MainMixerNode.Volume = _volume;

 NSError? error;
                _audioEngine.StartAndReturnError(out error);

 if (error != null)
    {
     Console.WriteLine($"Audio engine start error: {error.LocalizedDescription}");
      OnError?.Invoke($"Audio init failed: {error.LocalizedDescription}");
     return;
  }

     _playerNode.Play();
                _isPlaying = true;
  Console.WriteLine($"Audio engine initialized: {_sampleRate}Hz, {_channels} channels");
            }
      catch (Exception ex)
  {
    Console.WriteLine($"Failed to initialize audio engine: {ex.Message}");
          OnError?.Invoke($"Audio init failed: {ex.Message}");
       }
   }

        private void StopAudioEngine()
   {
  try
      {
            _playerNode?.Stop();
   _audioEngine?.Stop();
            _playerNode?.Dispose();
            _audioEngine?.Dispose();
            _playerNode = null;
              _audioEngine = null;
       }
            catch { }
 }

        private async Task ReceiveAudioAsync()
        {
     var buffer = new byte[8192];

            try
      {
                while (_isConnected && _webSocket?.State == WebSocketState.Open && !_cts!.Token.IsCancellationRequested)
      {
      var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                 if (result.MessageType == WebSocketMessageType.Close)
          break;

             if (result.MessageType == WebSocketMessageType.Binary && result.Count > 0)
{
      var audioData = new byte[result.Count];
        Array.Copy(buffer, audioData, result.Count);

    PlayAudioData(audioData);
   _packetsReceived++;
        }
          else if (result.MessageType == WebSocketMessageType.Text)
            {
       // Handle audio format info
            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
   ProcessAudioInfo(text);
                    }
       }
      }
      catch (OperationCanceledException) { }
         catch (WebSocketException ex)
            {
       Console.WriteLine($"Audio WebSocket error: {ex.Message}");
       OnError?.Invoke(ex.Message);
            }
            catch (Exception ex)
     {
    Console.WriteLine($"Audio receive error: {ex.Message}");
    OnError?.Invoke(ex.Message);
  }

            _isConnected = false;
  _isPlaying = false;
            OnDisconnected?.Invoke("Audio stream ended");
  }

        private void PlayAudioData(byte[] audioData)
        {
            if (_playerNode == null || _audioFormat == null || !_isPlaying) return;

  try
            {
     // Convert byte array to PCM buffer
    var frameCount = (uint)(audioData.Length / (_channels * 2)); // 2 bytes per sample
       var pcmBuffer = new AVAudioPcmBuffer(_audioFormat, frameCount);

                if (pcmBuffer.Int16ChannelData == IntPtr.Zero) return;

       pcmBuffer.FrameLength = frameCount;

       // Copy audio data to buffer
                System.Runtime.InteropServices.Marshal.Copy(audioData, 0, pcmBuffer.Int16ChannelData, audioData.Length);

     // Schedule buffer for playback
     _playerNode.ScheduleBuffer(pcmBuffer, () =>
          {
        // Buffer completed
           });
            }
        catch (Exception ex)
      {
Console.WriteLine($"Audio playback error: {ex.Message}");
  }
        }

        private void ProcessAudioInfo(string json)
        {
       try
   {
       var info = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
    if (info == null) return;

           if (info.TryGetValue("sampleRate", out var sr))
       {
        _sampleRate = Convert.ToDouble(sr);
}
        if (info.TryGetValue("channels", out var ch))
     {
   _channels = Convert.ToInt32(ch);
       }

    Console.WriteLine($"Audio format updated: {_sampleRate}Hz, {_channels} channels");

   // Reinitialize audio engine with new format
                StopAudioEngine();
  InitializeAudioEngine();
     }
       catch (Exception ex)
         {
        Console.WriteLine($"Failed to parse audio info: {ex.Message}");
        }
        }

        /// <summary>
        /// Set playback volume (0.0 - 1.0)
        /// </summary>
        public void SetVolume(float volume)
        {
      _volume = Math.Clamp(volume, 0f, 1f);
            if (_audioEngine != null)
     {
    _audioEngine.MainMixerNode.Volume = _volume;
     }
        }

        /// <summary>
 /// Pause audio playback
        /// </summary>
        public void Pause()
        {
     _playerNode?.Pause();
       _isPlaying = false;
        }

        /// <summary>
        /// Resume audio playback
        /// </summary>
        public void Resume()
      {
            _playerNode?.Play();
     _isPlaying = true;
  }
    }
}
