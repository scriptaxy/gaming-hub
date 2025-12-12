using System.IO;
using Newtonsoft.Json;
using SynktraCompanion.Models;

namespace SynktraCompanion.Services;

public static class SettingsManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SynktraCompanion",
    "settings.json");

    public static AppSettings Load()
    {
        try
        {
     if (File.Exists(SettingsPath))
     {
      var json = File.ReadAllText(SettingsPath);
    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        
        return new AppSettings();
 }

    public static void Save(AppSettings settings)
    {
     try
  {
       var directory = Path.GetDirectoryName(SettingsPath);
     if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
       Directory.CreateDirectory(directory);
         }

  var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
     File.WriteAllText(SettingsPath, json);
   
      // Handle startup with Windows
   SetStartWithWindows(settings.StartWithWindows);
        }
        catch (Exception ex)
        {
      Console.WriteLine($"Failed to save settings: {ex.Message}");
   }
  }

    private static void SetStartWithWindows(bool enable)
    {
     try
        {
       using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
       @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
    
       if (key == null) return;

   var appPath = Environment.ProcessPath;
   
  if (enable && !string.IsNullOrEmpty(appPath))
 {
  key.SetValue("SynktraCompanion", $"\"{appPath}\" --minimized");
     }
            else
      {
          key.DeleteValue("SynktraCompanion", false);
   }
        }
 catch { }
 }
}
