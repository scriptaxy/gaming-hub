using System.Text.Json;

namespace gaming_hub.Services
{
    /// <summary>
    /// Quick actions for remote PC control (volume, media, power)
    /// </summary>
    public class QuickActionsService
    {
        private readonly RemotePCService _remotePCService;

        public QuickActionsService(RemotePCService remotePCService)
  {
            _remotePCService = remotePCService;
        }

        public async Task<bool> VolumeUpAsync() => await SendQuickActionAsync("volume", "up");
      public async Task<bool> VolumeDownAsync() => await SendQuickActionAsync("volume", "down");
        public async Task<bool> VolumeMuteAsync() => await SendQuickActionAsync("volume", "mute");
      public async Task<bool> MediaPlayPauseAsync() => await SendQuickActionAsync("media", "playpause");
        public async Task<bool> MediaNextAsync() => await SendQuickActionAsync("media", "next");
        public async Task<bool> MediaPreviousAsync() => await SendQuickActionAsync("media", "previous");
        public async Task<bool> ShutdownAsync(int delay = 0) => await SendQuickActionAsync("power", "shutdown", new { delay });
        public async Task<bool> RestartAsync(int delay = 0) => await SendQuickActionAsync("power", "restart", new { delay });
        public async Task<bool> SleepAsync() => await SendQuickActionAsync("power", "sleep");
        public async Task<bool> LockAsync() => await SendQuickActionAsync("power", "lock");
        public async Task<bool> SendKeyAsync(string key, bool ctrl = false, bool alt = false, bool shift = false, bool win = false)
    => await SendQuickActionAsync("keyboard", "key", new { key, ctrl, alt, shift, win });
        public async Task<bool> AltTabAsync() => await SendKeyAsync("Tab", alt: true);
        public async Task<bool> AltF4Async() => await SendKeyAsync("F4", alt: true);
        public async Task<bool> ShowDesktopAsync() => await SendKeyAsync("D", win: true);

     private async Task<bool> SendQuickActionAsync(string category, string action, object? parameters = null)
   {
     try
        {
  var payload = new Dictionary<string, object>
          {
         ["category"] = category,
            ["action"] = action
         };
     if (parameters != null) payload["params"] = parameters;

     var json = JsonSerializer.Serialize(payload);
     var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _remotePCService.HttpClient.PostAsync("/api/quick/action", content);
                return response.IsSuccessStatusCode;
            }
    catch (Exception ex)
       {
       Console.WriteLine($"Quick action failed: {ex.Message}");
     return false;
         }
  }
    }
}
