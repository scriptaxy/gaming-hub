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
}

public class SystemStats
{
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double? GpuUsage { get; set; }
    public double? GpuTemperature { get; set; }
    public string? CurrentGame { get; set; }
}

public class AppSettings
{
    public int Port { get; set; } = 5000;
    public string? AuthToken { get; set; }
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
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
    public string? Uptime { get; set; }
}
