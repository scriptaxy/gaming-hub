namespace SynktraCompanion.Models;

public class InstalledGame
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? InstallPath { get; set; }
    public string? LaunchCommand { get; set; }
    public string? IconPath { get; set; }
    public long? SizeBytes { get; set; }
    public DateTime? LastPlayed { get; set; }
    public bool IsRunning { get; set; }
    public bool IsFavorite { get; set; }
    public TimeSpan TotalPlayTime { get; set; }
}

public class SystemStats
{
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double? GpuUsage { get; set; }
    public double? GpuTemperature { get; set; }
    public double? CpuTemperature { get; set; }
    public string? CurrentGame { get; set; }
    public long NetworkUploadBps { get; set; }
    public long NetworkDownloadBps { get; set; }
}

public class AppSettings
{
    // Server Settings - Using high ports to avoid conflicts
    public int Port { get; set; } = 19500;
    public string? AuthToken { get; set; }
    
    // Startup Settings
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; }
    
    // Discord Settings
    public bool EnableDiscordRPC { get; set; } = true;
    public bool DiscordShowGame { get; set; } = true;
    public bool DiscordShowPCStats { get; set; } = true;

    // Streaming Settings
    public int StreamQuality { get; set; } = 50;
    public int StreamFps { get; set; } = 60;
    public int StreamWidth { get; set; } = 1280;
    public int StreamHeight { get; set; } = 720;
    public bool LowLatencyMode { get; set; } = true;
    
    // Virtual Controller Settings
    public bool EnableVirtualController { get; set; } = true;
    
    // UI Settings
    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "Purple";
    public bool ShowNotifications { get; set; } = true;
    
    // Performance Settings
    public int StatsUpdateInterval { get; set; } = 3;
    public bool MonitorGpu { get; set; } = true;
    public bool MonitorNetwork { get; set; } = true;
    
    // Hotkeys
    public string? HotkeyToggleStream { get; set; } = "Ctrl+Shift+S";
    public string? HotkeyToggleMute { get; set; } = "Ctrl+Shift+M";
}

public class ApiStatusResponse
{
    public string Hostname { get; set; } = Environment.MachineName;
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double? GpuUsage { get; set; }
    public double? GpuTemp { get; set; }
    public string? CurrentGame { get; set; }
    public bool IsStreaming { get; set; }
    public int StreamClients { get; set; }
    public double StreamLatencyMs { get; set; }
    public int StreamFps { get; set; }
    public double StreamBitrateKbps { get; set; }
    public string? Uptime { get; set; }
    
    // Virtual controller status
    public bool VirtualControllerConnected { get; set; }
    public string VirtualControllerType { get; set; } = string.Empty;
    public string InputMode { get; set; } = string.Empty;
}

public class ConnectionInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public string ConnectionType { get; set; } = "Unknown";
    public bool IsStreaming { get; set; }
}

/// <summary>
/// Latency stats for streaming
/// </summary>
public class StreamLatencyStats
{
    public double CaptureMs { get; set; }
    public double EncodeMs { get; set; }
    public double SendMs { get; set; }
    public double TotalMs { get; set; }
    public int Fps { get; set; }
    public double BitrateKbps { get; set; }
    public int ClientCount { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public int Quality { get; set; }
}
