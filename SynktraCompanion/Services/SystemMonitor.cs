using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using SynktraCompanion.Models;

namespace SynktraCompanion.Services;

public class SystemMonitor
{
    private DateTime _lastCheck = DateTime.MinValue;
    private SystemStats _cachedStats = new();
    private readonly TimeSpan _cacheInterval = TimeSpan.FromSeconds(2);

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);

    private long _lastIdleTime;
    private long _lastKernelTime;
    private long _lastUserTime;
    private bool _initialized;

    public SystemStats GetCurrentStats()
    {
        if (DateTime.Now - _lastCheck < _cacheInterval)
            return _cachedStats;

        _lastCheck = DateTime.Now;

        _cachedStats = new SystemStats
        {
            CpuUsage = GetCpuUsage(),
            MemoryUsage = GetMemoryUsage(),
            GpuUsage = null // Skip GPU for performance
        };

        return _cachedStats;
    }

    private double GetCpuUsage()
    {
        try
        {
            if (GetSystemTimes(out long idleTime, out long kernelTime, out long userTime))
            {
                if (!_initialized)
                {
                    _lastIdleTime = idleTime;
                    _lastKernelTime = kernelTime;
                    _lastUserTime = userTime;
                    _initialized = true;
                    return 0;
                }

                var idleDiff = idleTime - _lastIdleTime;
                var kernelDiff = kernelTime - _lastKernelTime;
                var userDiff = userTime - _lastUserTime;

                _lastIdleTime = idleTime;
                _lastKernelTime = kernelTime;
                _lastUserTime = userTime;

                var total = kernelDiff + userDiff;
                if (total == 0) return 0;

                return (1.0 - (double)idleDiff / total) * 100;
            }
        }
        catch { }
        return 0;
    }

    private double GetMemoryUsage()
    {
        try
        {
            var info = GC.GetGCMemoryInfo();
            if (info.TotalAvailableMemoryBytes > 0)
            {
                var used = info.TotalAvailableMemoryBytes - info.HighMemoryLoadThresholdBytes;
                // Simplified calculation
                return (double)Process.GetCurrentProcess().WorkingSet64 / info.TotalAvailableMemoryBytes * 100 * 10;
            }
        }
        catch { }

        // Fallback: use process memory relative to typical system
        try
        {
            var processMemory = Process.GetCurrentProcess().WorkingSet64;
            return Math.Min(processMemory / (16.0 * 1024 * 1024 * 1024) * 100, 100); // Assume 16GB
        }
        catch { }

        return 0;
    }

    public string? GetRunningGame(List<InstalledGame> installedGames)
    {
        if (installedGames.Count == 0) return null;

        try
        {
            var processes = Process.GetProcesses();
            var processNames = new HashSet<string>(
                processes.Select(p =>
                {
                    try { return p.ProcessName.ToLowerInvariant(); }
                    catch { return ""; }
                }).Where(n => !string.IsNullOrEmpty(n)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var game in installedGames)
            {
                if (!string.IsNullOrEmpty(game.LaunchCommand))
                {
                    var exeName = Path.GetFileNameWithoutExtension(game.LaunchCommand)?.ToLowerInvariant();
                    if (!string.IsNullOrEmpty(exeName) && processNames.Contains(exeName))
                        return game.Name;
                }
            }

            // Check common games
            var knownGames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["cs2"] = "Counter-Strike 2",
                ["dota2"] = "Dota 2",
                ["valorant-win64-shipping"] = "Valorant",
                ["fortniteclient-win64-shipping"] = "Fortnite",
                ["leagueclient"] = "League of Legends",
                ["rocketleague"] = "Rocket League",
                ["gta5"] = "GTA V",
                ["cyberpunk2077"] = "Cyberpunk 2077"
            };

            foreach (var kvp in knownGames)
            {
                if (processNames.Contains(kvp.Key))
                    return kvp.Value;
            }
        }
        catch { }

        return null;
    }
}
