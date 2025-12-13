using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace SynktraCompanion.Services;

/// <summary>
/// Hardware-accelerated video encoding using FFmpeg with NVENC/AMF/QuickSync support
/// Supports both H.264 and H.265/HEVC for optimal quality/bandwidth
/// Provides ultra-low latency encoding for game streaming
/// </summary>
public class HardwareEncoderService : IDisposable
{
    private static HardwareEncoderService? _instance;
    public static HardwareEncoderService Instance => _instance ??= new HardwareEncoderService();

    private Process? _ffmpegProcess;
    private Stream? _ffmpegInput;
    private bool _isInitialized;
    private bool _isEncoding;
 private string? _ffmpegPath;
    
    // Encoder settings
    private HardwareEncoder _selectedEncoder = HardwareEncoder.Auto;
    private VideoCodec _codec = VideoCodec.H264; // Default to H.264 for compatibility
    private int _width = 1280;
  private int _height = 720;
    private int _fps = 60;
    private int _bitrate = 8000; // kbps
    private int _quality = 28; // CRF/CQ value (lower = better quality)
    
    // Output buffer for encoded frames
    private readonly Queue<byte[]> _encodedFrames = new();
    private readonly object _frameLock = new();
    private const int MaxQueuedFrames = 5;
    
    // Events
    public event Action<byte[]>? OnFrameEncoded;
    public event Action<string>? OnEncoderLog;
    
    // Properties
    public bool IsInitialized => _isInitialized;
    public bool IsEncoding => _isEncoding;
    public HardwareEncoder ActiveEncoder => _selectedEncoder;
    public VideoCodec ActiveCodec => _codec;
    public string EncoderName => GetEncoderName(_selectedEncoder);
    public string CodecName => _codec == VideoCodec.H265 ? "H.265/HEVC" : "H.264/AVC";
    public bool SupportsH265 { get; private set; }
    
    private HardwareEncoderService() { }

    /// <summary>
    /// Initialize the hardware encoder
    /// </summary>
    public async Task<bool> InitializeAsync(int width = 1280, int height = 720, int fps = 60, int bitrateKbps = 8000, VideoCodec codec = VideoCodec.Auto)
    {
        if (_isInitialized) return true;
        
        _width = width;
        _height = height;
        _fps = fps;
        _bitrate = bitrateKbps;
     
        // Find FFmpeg
        _ffmpegPath = await FindOrDownloadFFmpegAsync();
        if (string.IsNullOrEmpty(_ffmpegPath))
{
       Log("FFmpeg not found and download failed");
     return false;
}
  
        // Detect best hardware encoder
        _selectedEncoder = await DetectBestEncoderAsync();
        Log($"Selected encoder: {EncoderName}");
    
        // Check H.265 support
 SupportsH265 = await TestH265SupportAsync();
        Log($"H.265/HEVC support: {(SupportsH265 ? "Yes" : "No")}");
        
        // Auto-select codec: prefer H.265 if supported (50% better compression)
        if (codec == VideoCodec.Auto)
        {
 _codec = SupportsH265 ? VideoCodec.H265 : VideoCodec.H264;
        }
     else
        {
      _codec = (codec == VideoCodec.H265 && !SupportsH265) ? VideoCodec.H264 : codec;
 }
        Log($"Using codec: {CodecName}");
        
    _isInitialized = true;
   return true;
    }

    /// <summary>
    /// Set the video codec (H.264 or H.265)
 /// </summary>
    public bool SetCodec(VideoCodec codec)
    {
      if (codec == VideoCodec.H265 && !SupportsH265)
        {
    Log("H.265 not supported, falling back to H.264");
            return false;
        }
        
      var wasEncoding = _isEncoding;
      if (wasEncoding) StopEncoding();
        
     _codec = codec;
        Log($"Codec changed to: {CodecName}");
        
        if (wasEncoding) StartEncoding();
        return true;
    }

    /// <summary>
 /// Start the encoding pipeline
    /// </summary>
    public bool StartEncoding()
    {
        if (!_isInitialized || _isEncoding) return false;
      
try
     {
            var encoderArgs = BuildEncoderArgs();
 Log($"Starting FFmpeg with: {encoderArgs}");
          
            _ffmpegProcess = new Process
     {
           StartInfo = new ProcessStartInfo
          {
FileName = _ffmpegPath,
        Arguments = encoderArgs,
       UseShellExecute = false,
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
        RedirectStandardError = true,
 CreateNoWindow = true
       },
          EnableRaisingEvents = true
 };
 
            _ffmpegProcess.ErrorDataReceived += (s, e) =>
          {
      if (!string.IsNullOrEmpty(e.Data))
                Log($"FFmpeg: {e.Data}");
            };
    
   _ffmpegProcess.Start();
        _ffmpegProcess.BeginErrorReadLine();
      
   _ffmpegInput = _ffmpegProcess.StandardInput.BaseStream;
          
       // Start reading encoded output
            _ = Task.Run(ReadEncodedOutputAsync);
      
            _isEncoding = true;
            Log($"Hardware encoder started ({CodecName} via {EncoderName})");
            return true;
        }
        catch (Exception ex)
        {
 Log($"Failed to start encoder: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Send a raw frame (BGR24 or BGRA) to be encoded
    /// </summary>
  public async Task<bool> SendFrameAsync(byte[] frameData, int width, int height, bool isBgra = true)
    {
        if (!_isEncoding || _ffmpegInput == null) return false;
        
        try
  {
            await _ffmpegInput.WriteAsync(frameData);
         await _ffmpegInput.FlushAsync();
            return true;
        }
        catch (Exception ex)
        {
  Log($"Error sending frame: {ex.Message}");
      return false;
        }
    }

    /// <summary>
    /// Get the next encoded frame
    /// </summary>
    public byte[]? GetEncodedFrame()
    {
        lock (_frameLock)
        {
       return _encodedFrames.Count > 0 ? _encodedFrames.Dequeue() : null;
        }
 }

    /// <summary>
    /// Stop encoding
    /// </summary>
    public void StopEncoding()
    {
    _isEncoding = false;
    
        try
        {
 _ffmpegInput?.Close();
     _ffmpegProcess?.Kill();
      _ffmpegProcess?.WaitForExit(1000);
        }
     catch { }
        finally
        {
            _ffmpegProcess?.Dispose();
   _ffmpegProcess = null;
      _ffmpegInput = null;
    }
    
lock (_frameLock)
     {
   _encodedFrames.Clear();
        }
   
        Log("Hardware encoder stopped");
    }

    /// <summary>
  /// Update encoding parameters (requires restart)
    /// </summary>
    public void SetParameters(int width, int height, int fps, int bitrateKbps, int quality = 28)
    {
        _width = width;
        _height = height;
        _fps = fps;
        _bitrate = bitrateKbps;
   _quality = quality;
        
    if (_isEncoding)
    {
      StopEncoding();
       StartEncoding();
      }
    }

    private string BuildEncoderArgs()
    {
    var pixFmt = "bgra";
    var encoderName = GetFFmpegEncoderName(_selectedEncoder, _codec);
        var presetArg = GetPresetArg(_selectedEncoder, _codec);
var qualityArg = GetQualityArg(_selectedEncoder, _codec);
        var tuneArg = GetTuneArg(_selectedEncoder, _codec);
        var outputFormat = _codec == VideoCodec.H265 ? "hevc" : "h264";
        
        // Build FFmpeg arguments for ultra-low latency streaming
   var args = $"-f rawvideo -pix_fmt {pixFmt} -s {_width}x{_height} -r {_fps} -i pipe:0 " +
         $"-c:v {encoderName} " +
        $"{presetArg} " +
    $"{qualityArg} " +
         $"{tuneArg} " +
       $"-b:v {_bitrate}k -maxrate {_bitrate * 2}k -bufsize {_bitrate}k " +
         $"-g {_fps * 2} "; // Keyframe every 2 seconds
  
        // Add codec-specific profile
   if (_codec == VideoCodec.H265)
        {
     args += "-profile:v main -level 4.1 ";
        }
        else
     {
        args += "-profile:v high -level 4.1 ";
}
    
        // Add encoder-specific low latency flags
        if (_selectedEncoder == HardwareEncoder.NVENC)
        {
            // NVENC specific: disable B-frames, enable low-delay
         args += "-bf 0 -rc-lookahead 0 -delay 0 -zerolatency 1 ";
   
          // NVENC spatial AQ for better quality in complex scenes
            args += "-spatial-aq 1 -aq-strength 8 ";
        }
        else if (_selectedEncoder == HardwareEncoder.AMF)
 {
            args += "-bf 0 ";
        }
        else if (_selectedEncoder == HardwareEncoder.Software)
        {
    args += "-tune zerolatency ";
        }
        
        // Common low-latency output flags
  args += $"-flags +low_delay -fflags nobuffer -f {outputFormat} pipe:1";
   
        return args;
  }

    private string GetFFmpegEncoderName(HardwareEncoder encoder, VideoCodec codec)
    {
        if (codec == VideoCodec.H265)
      {
    return encoder switch
       {
         HardwareEncoder.NVENC => "hevc_nvenc",
            HardwareEncoder.AMF => "hevc_amf",
           HardwareEncoder.QuickSync => "hevc_qsv",
 HardwareEncoder.VideoToolbox => "hevc_videotoolbox",
   _ => "libx265"
   };
        }
        
        return encoder switch
        {
 HardwareEncoder.NVENC => "h264_nvenc",
            HardwareEncoder.AMF => "h264_amf",
     HardwareEncoder.QuickSync => "h264_qsv",
       HardwareEncoder.VideoToolbox => "h264_videotoolbox",
            _ => "libx264"
        };
 }

    private string GetPresetArg(HardwareEncoder encoder, VideoCodec codec)
    {
    // NVENC presets: p1 (fastest) to p7 (slowest/best quality)
    // For ultra-low latency streaming, use p1 or p2
        return encoder switch
      {
       HardwareEncoder.NVENC => "-preset p1",
 HardwareEncoder.AMF => "-quality speed -rc cqp",
      HardwareEncoder.QuickSync => "-preset veryfast -look_ahead 0",
  _ => codec == VideoCodec.H265 ? "-preset ultrafast" : "-preset ultrafast"
        };
    }

    private string GetQualityArg(HardwareEncoder encoder, VideoCodec codec)
    {
        // Use CBR for consistent streaming bitrate
      return encoder switch
 {
      HardwareEncoder.NVENC => "-rc cbr",
         HardwareEncoder.AMF => $"-qp_i {_quality} -qp_p {_quality}",
     HardwareEncoder.QuickSync => $"-global_quality {_quality}",
            _ => $"-crf {_quality}"
        };
    }

    private string GetTuneArg(HardwareEncoder encoder, VideoCodec codec)
    {
        // NVENC tune: hq, ll (low latency), ull (ultra low latency), lossless
        return encoder switch
        {
   HardwareEncoder.NVENC => "-tune ull",
            _ => ""
      };
}

    private string GetEncoderName(HardwareEncoder encoder) => encoder switch
    {
        HardwareEncoder.NVENC => "NVIDIA NVENC",
      HardwareEncoder.AMF => "AMD AMF",
        HardwareEncoder.QuickSync => "Intel QuickSync",
        HardwareEncoder.VideoToolbox => "Apple VideoToolbox",
        HardwareEncoder.Software => "Software (x264/x265)",
        _ => "Unknown"
    };

    private async Task<bool> TestH265SupportAsync()
    {
 var hevcEncoder = GetFFmpegEncoderName(_selectedEncoder, VideoCodec.H265);
        return await TestEncoderAsync(hevcEncoder);
    }

    private async Task<HardwareEncoder> DetectBestEncoderAsync()
{
        var encodersToTest = new[]
        {
            (HardwareEncoder.NVENC, "h264_nvenc"),
 (HardwareEncoder.AMF, "h264_amf"),
            (HardwareEncoder.QuickSync, "h264_qsv"),
            (HardwareEncoder.Software, "libx264")
        };
      
        foreach (var (encoder, ffmpegName) in encodersToTest)
     {
 if (await TestEncoderAsync(ffmpegName))
            {
        Log($"Encoder {ffmpegName} is available");
   return encoder;
          }
        }
  
        return HardwareEncoder.Software;
    }

    private async Task<bool> TestEncoderAsync(string encoderName)
    {
        try
        {
   var process = new Process
            {
  StartInfo = new ProcessStartInfo
   {
                    FileName = _ffmpegPath,
    Arguments = $"-f lavfi -i testsrc=duration=0.1:size=320x240:rate=30 -c:v {encoderName} -f null -",
        UseShellExecute = false,
            RedirectStandardOutput = true,
       RedirectStandardError = true,
     CreateNoWindow = true
         }
   };
    
 process.Start();
          await process.WaitForExitAsync();
       
            return process.ExitCode == 0;
}
        catch
        {
         return false;
        }
    }

    private async Task ReadEncodedOutputAsync()
    {
        if (_ffmpegProcess?.StandardOutput?.BaseStream == null) return;
   
        var buffer = new byte[65536];
        var stream = _ffmpegProcess.StandardOutput.BaseStream;
        var frameBuffer = new MemoryStream();
  
 try
        {
            while (_isEncoding && !_ffmpegProcess.HasExited)
            {
        var bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead == 0) break;
       
    frameBuffer.Write(buffer, 0, bytesRead);
  
         if (frameBuffer.Length > 0)
     {
          var frameData = frameBuffer.ToArray();
              frameBuffer.SetLength(0);
 
              lock (_frameLock)
      {
         while (_encodedFrames.Count >= MaxQueuedFrames)
           _encodedFrames.Dequeue();
     
         _encodedFrames.Enqueue(frameData);
  }
         
          OnFrameEncoded?.Invoke(frameData);
                }
      }
        }
        catch (Exception ex)
        {
       Log($"Error reading encoded output: {ex.Message}");
    }
    }

    private async Task<string?> FindOrDownloadFFmpegAsync()
    {
     var possiblePaths = new[]
      {
         Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
          @"C:\ffmpeg\bin\ffmpeg.exe",
   @"C:\Program Files\ffmpeg\bin\ffmpeg.exe"
        };
     
     foreach (var path in possiblePaths)
      {
         if (File.Exists(path))
          {
       Log($"Found FFmpeg at: {path}");
    return path;
       }
   }
    
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var ffmpegPath = Path.Combine(dir, "ffmpeg.exe");
            if (File.Exists(ffmpegPath))
            {
             Log($"Found FFmpeg in PATH: {ffmpegPath}");
             return ffmpegPath;
      }
        }
        
    Log("FFmpeg not found, attempting to download...");
        return await DownloadFFmpegAsync();
    }

    private async Task<string?> DownloadFFmpegAsync()
    {
 try
        {
            var ffmpegDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg");
    var ffmpegExe = Path.Combine(ffmpegDir, "ffmpeg.exe");
 
     if (File.Exists(ffmpegExe))
       return ffmpegExe;
     
      Directory.CreateDirectory(ffmpegDir);
            
         // Download full build (includes H.265 support)
            const string downloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
            var zipPath = Path.Combine(ffmpegDir, "ffmpeg.zip");
 
        Log("Downloading FFmpeg (with H.265 support)...");
            using var httpClient = new HttpClient();
      httpClient.Timeout = TimeSpan.FromMinutes(5);
            
          var response = await httpClient.GetAsync(downloadUrl);
      response.EnsureSuccessStatusCode();
 
            await using var fs = new FileStream(zipPath, FileMode.Create);
            await response.Content.CopyToAsync(fs);
 fs.Close();
        
            Log("Extracting FFmpeg...");
    ZipFile.ExtractToDirectory(zipPath, ffmpegDir, true);
         
            var extractedFfmpeg = Directory.GetFiles(ffmpegDir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (extractedFfmpeg != null && extractedFfmpeg != ffmpegExe)
    {
     File.Copy(extractedFfmpeg, ffmpegExe, true);
        
     var ffprobe = Path.Combine(Path.GetDirectoryName(extractedFfmpeg)!, "ffprobe.exe");
        if (File.Exists(ffprobe))
        File.Copy(ffprobe, Path.Combine(ffmpegDir, "ffprobe.exe"), true);
            }
            
   File.Delete(zipPath);
      
          if (File.Exists(ffmpegExe))
            {
                Log($"FFmpeg installed to: {ffmpegExe}");
          return ffmpegExe;
            }
        }
        catch (Exception ex)
     {
       Log($"Failed to download FFmpeg: {ex.Message}");
        }
    
        return null;
    }

    /// <summary>
    /// Get encoding statistics
    /// </summary>
    public EncoderStats GetStats()
    {
     return new EncoderStats
    {
   Encoder = EncoderName,
   Codec = CodecName,
        Resolution = $"{_width}x{_height}",
       Fps = _fps,
         Bitrate = _bitrate,
            IsEncoding = _isEncoding,
      SupportsH265 = SupportsH265,
 QueuedFrames = _encodedFrames.Count
    };
    }

    private void Log(string message)
    {
        Console.WriteLine($"[HardwareEncoder] {message}");
        OnEncoderLog?.Invoke(message);
    }

    public void Dispose()
    {
        StopEncoding();
 _isInitialized = false;
    }
}

/// <summary>
/// Video codec options
/// </summary>
public enum VideoCodec
{
 Auto,   // Auto-select best (H.265 if supported)
    H264,   // H.264/AVC - widest compatibility
    H265    // H.265/HEVC - 50% better compression
}

/// <summary>
/// Available hardware encoders
/// </summary>
public enum HardwareEncoder
{
    Auto,
    NVENC,        // NVIDIA
    AMF,          // AMD
    QuickSync,    // Intel
    VideoToolbox, // Apple (macOS)
    Software      // x264/x265 fallback
}

/// <summary>
/// Encoder statistics
/// </summary>
public class EncoderStats
{
    public string Encoder { get; set; } = string.Empty;
    public string Codec { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
 public int Fps { get; set; }
    public int Bitrate { get; set; }
    public bool IsEncoding { get; set; }
    public bool SupportsH265 { get; set; }
    public int QueuedFrames { get; set; }
}
