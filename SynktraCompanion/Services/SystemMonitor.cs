using System.Diagnostics;
using System.IO;
using System.Management;
using SynktraCompanion.Models;

namespace SynktraCompanion.Services;

public class SystemMonitor
{
    private readonly PerformanceCounter? _cpuCounter;
    private DateTime _lastCpuCheck = DateTime.MinValue;
    private double _lastCpuValue;

    public SystemMonitor()
    {
    try
        {
  _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
 _cpuCounter.NextValue(); // First call always returns 0
        }
catch
   {
        _cpuCounter = null;
        }
    }

    public SystemStats GetCurrentStats()
    {
        return new SystemStats
    {
       CpuUsage = GetCpuUsage(),
            MemoryUsage = GetMemoryUsage(),
 GpuUsage = GetGpuUsage(),
      GpuTemperature = GetGpuTemperature()
      };
 }

    private double GetCpuUsage()
    {
   try
     {
     // Throttle CPU checks to avoid high overhead
     if ((DateTime.Now - _lastCpuCheck).TotalSeconds < 1)
    return _lastCpuValue;

     _lastCpuCheck = DateTime.Now;
         _lastCpuValue = _cpuCounter?.NextValue() ?? 0;
  return _lastCpuValue;
        }
        catch
  {
            return 0;
 }
    }

    private double GetMemoryUsage()
 {
        try
  {
     var gcMemory = GC.GetGCMemoryInfo();
 var totalMemory = gcMemory.TotalAvailableMemoryBytes;
       
       // Use WMI for more accurate system-wide memory
      using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
     foreach (ManagementObject obj in searcher.Get())
            {
     var total = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
       var free = Convert.ToDouble(obj["FreePhysicalMemory"]);
 return ((total - free) / total) * 100;
          }
        }
        catch { }
        
        return 0;
    }

 private double? GetGpuUsage()
    {
        try
        {
     // Try NVIDIA first
    using var searcher = new ManagementObjectSearcher(
     @"root\CIMV2", 
        "SELECT * FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
            
 double totalUsage = 0;
      int count = 0;
  
    foreach (ManagementObject obj in searcher.Get())
 {
   var engType = obj["Name"]?.ToString() ?? "";
    if (engType.Contains("engtype_3D"))
      {
                    var usage = Convert.ToDouble(obj["UtilizationPercentage"]);
                    totalUsage += usage;
count++;
  }
          }

            if (count > 0)
  return totalUsage / count;
   }
        catch { }

        return null;
    }

    private double? GetGpuTemperature()
    {
 try
  {
     // Try NVIDIA
            using var searcher = new ManagementObjectSearcher(
   @"root\WMI",
          "SELECT * FROM MSAcpi_ThermalZoneTemperature");
  
          foreach (ManagementObject obj in searcher.Get())
        {
       var temp = Convert.ToDouble(obj["CurrentTemperature"]);
   // Convert from tenths of Kelvin to Celsius
 var celsius = (temp / 10.0) - 273.15;
  if (celsius > 0 && celsius < 150)
          return celsius;
     }
        }
      catch { }

 return null;
    }

    public string? GetRunningGame(List<InstalledGame> installedGames)
    {
        try
   {
         var runningProcesses = Process.GetProcesses()
     .Select(p => p.ProcessName.ToLowerInvariant())
       .ToHashSet();

   foreach (var game in installedGames)
         {
       // Check if game executable is running
   if (!string.IsNullOrEmpty(game.LaunchCommand))
       {
 var exeName = Path.GetFileNameWithoutExtension(game.LaunchCommand)?.ToLowerInvariant();
      if (!string.IsNullOrEmpty(exeName) && runningProcesses.Contains(exeName))
           {
         return game.Name;
  }
       }

    // Check by install path
        if (!string.IsNullOrEmpty(game.InstallPath) && Directory.Exists(game.InstallPath))
     {
          var exeFiles = Directory.GetFiles(game.InstallPath, "*.exe", SearchOption.TopDirectoryOnly);
      foreach (var exe in exeFiles)
        {
     var exeName = Path.GetFileNameWithoutExtension(exe)?.ToLowerInvariant();
               if (!string.IsNullOrEmpty(exeName) && 
   !exeName.Contains("unins") && 
              !exeName.Contains("setup") &&
  !exeName.Contains("crash") &&
           runningProcesses.Contains(exeName))
          {
        return game.Name;
      }
    }
     }
    }

         // Check common game processes
 var knownGameProcesses = new Dictionary<string, string>
      {
        { "csgo", "Counter-Strike 2" },
                { "cs2", "Counter-Strike 2" },
       { "dota2", "Dota 2" },
  { "valorant", "Valorant" },
         { "valorant-win64-shipping", "Valorant" },
        { "fortnite", "Fortnite" },
              { "fortniteclient-win64-shipping", "Fortnite" },
       { "leagueclient", "League of Legends" },
    { "league of legends", "League of Legends" },
      { "minecraft", "Minecraft" },
    { "rocketleague", "Rocket League" },
       { "gta5", "Grand Theft Auto V" },
     { "gtavlauncher", "Grand Theft Auto V" },
            { "eldenring", "Elden Ring" },
     { "cyberpunk2077", "Cyberpunk 2077" },
     { "hogwartslegacy", "Hogwarts Legacy" },
           { "baldursgate3", "Baldur's Gate 3" },
      { "bg3", "Baldur's Gate 3" }
   };

 foreach (var kvp in knownGameProcesses)
            {
     if (runningProcesses.Contains(kvp.Key))
    return kvp.Value;
 }
        }
        catch { }

  return null;
    }
}
