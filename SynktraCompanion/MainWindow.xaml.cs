using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SynktraCompanion.Models;
using SynktraCompanion.Services;

namespace SynktraCompanion;

/// <summary>
/// Converter to get the first letter of a game name for display
/// </summary>
public class FirstLetterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
   if (value is string s && !string.IsNullOrEmpty(s))
            return s[0].ToString().ToUpper();
        return "G";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
      throw new NotImplementedException();
    }
}

public partial class MainWindow : Window
{
    private readonly GameScanner _gameScanner;
    private readonly SystemMonitor _systemMonitor;
    private readonly ApiServer _apiServer;
    private readonly DispatcherTimer _updateTimer;
    private readonly DiscordPresenceService _discord;
    private readonly DispatcherTimer _toastTimer;
    
    private List<InstalledGame> _allGames = [];
    private List<InstalledGame> _filteredGames = [];
    private DateTime _sessionStart;
    private string? _currentGame;
    private bool _isInitialized;

    public MainWindow()
    {
        InitializeComponent();

        _gameScanner = new GameScanner();
        _systemMonitor = new SystemMonitor();
        _apiServer = new ApiServer();
   _discord = DiscordPresenceService.Instance;
  _sessionStart = DateTime.Now;

  _updateTimer = new DispatcherTimer
        {
    Interval = TimeSpan.FromSeconds(2)
        };
     _updateTimer.Tick += UpdateTimer_Tick;

        _toastTimer = new DispatcherTimer
        {
    Interval = TimeSpan.FromSeconds(3)
     };
        _toastTimer.Tick += ToastTimer_Tick;

      // Handle source initialized for proper maximize
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Hook into window message processing to handle maximize properly
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
     System.Windows.Interop.HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
 {
        // WM_GETMINMAXINFO - allows us to control maximize size
        if (msg == 0x0024)
        {
       WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
     return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
     var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

     // Get monitor info for the window
      var monitor = MonitorFromWindow(hwnd, 0x00000002); // MONITOR_DEFAULTTONEAREST
      if (monitor != IntPtr.Zero)
        {
       var monitorInfo = new MONITORINFO();
   monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
     GetMonitorInfo(monitor, ref monitorInfo);

         var workArea = monitorInfo.rcWork;
     var monitorArea = monitorInfo.rcMonitor;

  mmi.ptMaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
mmi.ptMaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
       mmi.ptMaxSize.X = Math.Abs(workArea.Right - workArea.Left);
  mmi.ptMaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);
        }

        Marshal.StructureToPtr(mmi, lParam, true);
  }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
   public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
 private struct MONITORINFO
    {
        public int cbSize;
     public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    #region Toast Notifications
    private void ShowToast(string message, bool isSuccess = true, bool isError = false)
    {
        ToastMessage.Text = message;
        
        if (isError)
        {
 ToastIcon.Text = "\uE783"; // Error icon
            ToastIcon.Foreground = (SolidColorBrush)FindResource("DangerBrush");
 ToastNotification.BorderBrush = (SolidColorBrush)FindResource("DangerBrush");
        }
      else if (isSuccess)
     {
            ToastIcon.Text = "\uE73E"; // Checkmark icon
          ToastIcon.Foreground = (SolidColorBrush)FindResource("SuccessBrush");
            ToastNotification.BorderBrush = (SolidColorBrush)FindResource("SuccessBrush");
        }
        else
        {
 ToastIcon.Text = "\uE946"; // Info icon
   ToastIcon.Foreground = (SolidColorBrush)FindResource("PrimaryBrush");
 ToastNotification.BorderBrush = (SolidColorBrush)FindResource("PrimaryBrush");
 }

        ToastNotification.Visibility = Visibility.Visible;
        
     // Animate in
    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        var slideIn = new ThicknessAnimation(
        new Thickness(20, -50, 20, 0),
            new Thickness(20, 20, 20, 0),
      TimeSpan.FromMilliseconds(200));
        
ToastNotification.BeginAnimation(OpacityProperty, fadeIn);
        ToastNotification.BeginAnimation(MarginProperty, slideIn);

        _toastTimer.Stop();
      _toastTimer.Start();
    }

    private void ToastTimer_Tick(object? sender, EventArgs e)
    {
        _toastTimer.Stop();
        HideToast();
    }

    private void HideToast()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
        fadeOut.Completed += (s, e) => ToastNotification.Visibility = Visibility.Collapsed;
        ToastNotification.BeginAnimation(OpacityProperty, fadeOut);
    }
    #endregion

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
    var settings = SettingsManager.Load();
        
        // Initialize Discord RPC
     if (settings.EnableDiscordRPC)
        {
            _discord.Initialize();
     DiscordToggle.IsChecked = true;
 }

  // Load settings into UI
        LoadSettingsToUI(settings);
        
   // Start update timer
        _updateTimer.Start();

    // Start API server
        await _apiServer.StartAsync(settings.Port);
     
        // Update network info after server starts (to show actual port)
        UpdateNetworkInfo();
        
 // Show server status
        if (_apiServer.IsRunning)
  {
      StreamingBadge.Visibility = Visibility.Visible;
    StatusText.Text = "Server Running";
    StatusIndicator.Fill = (SolidColorBrush)FindResource("SuccessBrush");
            ShowToast($"Server started on port {_apiServer.Port}", true);
        }
   else
  {
   StatusText.Text = "Server Error";
            StatusIndicator.Fill = (SolidColorBrush)FindResource("DangerBrush");
            if (!string.IsNullOrEmpty(_apiServer.LastError))
  {
                ShowToast(_apiServer.LastError, isSuccess: false, isError: true);
       }
        }

        // Scan games in background
        _ = Task.Run(async () =>
        {
            var games = await _gameScanner.ScanAllGamesAsync();
      Dispatcher.Invoke(() =>
         {
        _allGames = games;
            ApplyFilter();
  UpdatePlatformStats();
           UpdateStats();
         });
        });

     // Check if should start minimized
        if (settings.StartMinimized)
        {
    WindowState = WindowState.Minimized;
            if (settings.MinimizeToTray)
{
  Hide();
                TrayIcon.Visibility = Visibility.Visible;
            }
        }
        
   _isInitialized = true;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        var settings = SettingsManager.Load();
        if (WindowState == WindowState.Minimized && settings.MinimizeToTray)
      {
  Hide();
            TrayIcon.Visibility = Visibility.Visible;
        }
  }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveSettingsFromUI();
        
 _updateTimer.Stop();
        _toastTimer.Stop();
        _apiServer.Stop();
        _discord.Dispose();
   TrayIcon.Dispose();
        
        Application.Current.Shutdown();
    }

    #region Window Controls
    private void MinBtn_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    
    private void MaxBtn_Click(object sender, RoutedEventArgs e)
    {
 WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
    
    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    #endregion

    #region Tray Icon
    private void TrayShow_Click(object sender, RoutedEventArgs e)
  {
        Show();
  WindowState = WindowState.Normal;
      TrayIcon.Visibility = Visibility.Collapsed;
        Activate();
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
  TrayIcon.Visibility = Visibility.Collapsed;
        Close();
    }
#endregion

    #region Stats Update
    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdateStats();
        UpdateStreamStats();
        UpdateDiscordStatus();
    }

    private void UpdateStats()
    {
        try
     {
            var stats = _systemMonitor.GetCurrentStats();

            CpuUsageText.Text = $"{stats.CpuUsage:0}%";
 MemoryUsageText.Text = $"{stats.MemoryUsage:0}%";
            GpuUsageText.Text = stats.GpuUsage.HasValue ? $"{stats.GpuUsage:0}%" : "-";
          GamesCountText.Text = _allGames.Count.ToString();

            CpuProgress.Value = stats.CpuUsage;
 MemoryProgress.Value = stats.MemoryUsage;
      GpuProgress.Value = stats.GpuUsage ?? 0;

     // Current game
   var newCurrentGame = _systemMonitor.GetRunningGame(_allGames);
    if (newCurrentGame != _currentGame)
   {
          _currentGame = newCurrentGame;
     CurrentGameText.Text = _currentGame ?? "No game running";
            CurrentGameIcon.Foreground = _currentGame != null 
    ? (SolidColorBrush)FindResource("PrimaryBrush") 
       : (SolidColorBrush)FindResource("TextMutedBrush");
      
     _discord.UpdatePresence(_currentGame, stats);
        }

// Uptime
            var uptime = DateTime.Now - _sessionStart;
  UptimeText.Text = $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
     }
        catch { }
    }

    private void UpdateStreamStats()
    {
     var streamService = _apiServer.StreamService;
        if (streamService != null)
        {
            StreamViewersText.Text = streamService.ClientCount.ToString();
 StreamFpsText.Text = streamService.CurrentFps.ToString();
StreamLatencyText.Text = $"{streamService.TotalLatency:0}ms";
    
            if (streamService.ClientCount > 0)
        {
           StreamInfoText.Text = $"{streamService.ClientCount} device(s) connected";
 StreamStatusDot.Fill = (SolidColorBrush)FindResource("SuccessBrush");
            }
            else
          {
        StreamInfoText.Text = "Waiting for mobile app to connect...";
 StreamStatusDot.Fill = (SolidColorBrush)FindResource("WarningBrush");
        }
        }
    }

    private void UpdateDiscordStatus()
    {
        if (_discord.IsConnected)
        {
            DiscordIndicator.Fill = (SolidColorBrush)FindResource("SuccessBrush");
            DiscordStatusText.Text = "Discord: Connected";
            DiscordRpcStatus.Text = "Connected";
   }
        else
        {
   DiscordIndicator.Fill = (SolidColorBrush)FindResource("TextMutedBrush");
          DiscordStatusText.Text = "Discord: Disconnected";
 DiscordRpcStatus.Text = "Disconnected";
  }
    }

    private void UpdateNetworkInfo()
    {
        try
        {
          // Get the actual port from the running server
 var port = _apiServer.Port;
            if (port == 0)
      port = 19500;
  
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
        {
         if (ip.AddressFamily == AddressFamily.InterNetwork)
 {
      IpAddressText.Text = $"{ip}:{port}";
        LocalIpDisplay.Text = ip.ToString();
     ApiPortDisplay.Text = port.ToString();
            return;
      }
            }
 // Fallback if no IPv4 found
    IpAddressText.Text = $"localhost:{port}";
            LocalIpDisplay.Text = "localhost";
            ApiPortDisplay.Text = port.ToString();
  }
 catch
 {
            IpAddressText.Text = $"localhost:{_apiServer.Port}";
    }
    }

    private void UpdatePlatformStats()
    {
   var steamCount = _allGames.Count(g => g.Platform == "Steam");
        var epicCount = _allGames.Count(g => g.Platform == "Epic");
     var gogCount = _allGames.Count(g => g.Platform == "GOG");

        SteamStatusText.Text = $"{steamCount} games found";
EpicStatusText.Text = $"{epicCount} games found";
        GOGStatusText.Text = $"{gogCount} games found";
    }
    #endregion

    #region Navigation
    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
if (sender is RadioButton rb && rb.Tag is string page)
     {
            DashboardPage.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
GamesPage.Visibility = page == "Games" ? Visibility.Visible : Visibility.Collapsed;
     StreamingPage.Visibility = page == "Streaming" ? Visibility.Visible : Visibility.Collapsed;
         AccountsPage.Visibility = page == "Accounts" ? Visibility.Visible : Visibility.Collapsed;
     DevicesPage.Visibility = page == "Devices" ? Visibility.Visible : Visibility.Collapsed;
     SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
            DiscordPage.Visibility = page == "Discord" ? Visibility.Visible : Visibility.Collapsed;
   AboutPage.Visibility = page == "About" ? Visibility.Visible : Visibility.Collapsed;

      if (page == "Settings") LoadSettingsToUI(SettingsManager.Load());
        }
  }
    #endregion

    #region Games
    private void ApplyFilter()
    {
     string? filter = null;
        bool favoritesOnly = FilterFavorites?.IsChecked == true;

        if (FilterSteam?.IsChecked == true) filter = "Steam";
    else if (FilterEpic?.IsChecked == true) filter = "Epic";
        else if (FilterGOG?.IsChecked == true) filter = "GOG";
        else if (FilterOther?.IsChecked == true) filter = "Other";

 var filtered = _allGames.AsEnumerable();

   if (favoritesOnly)
     filtered = filtered.Where(g => g.IsFavorite);
        
    if (filter == "Other")
    filtered = filtered.Where(g => g.Platform != "Steam" && g.Platform != "Epic" && g.Platform != "GOG");
        else if (filter != null)
 filtered = filtered.Where(g => g.Platform == filter);

        var searchText = GameSearchBox?.Text?.Trim()?.ToLower();
  if (!string.IsNullOrEmpty(searchText))
            filtered = filtered.Where(g => g.Name.ToLower().Contains(searchText));

        _filteredGames = filtered.ToList();
        GamesListBox.ItemsSource = _filteredGames;
    }

    private async void ScanGames_Click(object sender, RoutedEventArgs e)
    {
    var btn = sender as Button;
   if (btn != null) btn.IsEnabled = false;

ShowToast("Scanning for games...", isSuccess: false);

        await Task.Run(async () =>
        {
   var games = await _gameScanner.ScanAllGamesAsync();
            Dispatcher.Invoke(() =>
            {
           _allGames = games;
           ApplyFilter();
                GamesCountText.Text = _allGames.Count.ToString();
    UpdatePlatformStats();
     if (btn != null) btn.IsEnabled = true;
          ShowToast($"Found {_allGames.Count} games");
            });
        });
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => ApplyFilter();
    private void GameSearch_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

 private void LaunchGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is InstalledGame game && !string.IsNullOrEmpty(game.LaunchCommand))
        {
   try
            {
     System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
      {
FileName = game.LaunchCommand,
        UseShellExecute = true
     });
         ShowToast($"Launching {game.Name}...");
 }
         catch (Exception ex)
  {
   ShowToast($"Failed to launch: {ex.Message}", isSuccess: false, isError: true);
         }
    }
    }

    private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is InstalledGame game)
        {
            game.IsFavorite = !game.IsFavorite;
       btn.Content = game.IsFavorite ? "?" : "*";
        ApplyFilter();
        }
    }
    #endregion

    #region Streaming Settings
    private void StreamSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || QualitySlider == null || FpsSlider == null || QualityValueText == null || FpsValueText == null)
            return;
      
   QualityValueText.Text = $"{(int)QualitySlider.Value}%";
        FpsValueText.Text = $"{(int)FpsSlider.Value}";
        
        if (ResolutionCombo?.SelectedItem is ComboBoxItem item)
  {
            var content = item.Content?.ToString() ?? "720p";
            if (content.Contains("1080")) StreamQualityText.Text = "1080p";
            else if (content.Contains("720")) StreamQualityText.Text = "720p";
            else if (content.Contains("480")) StreamQualityText.Text = "480p";
     else if (content.Contains("360")) StreamQualityText.Text = "360p";
        }
    }
    
    private void ApplyStreamSettings_Click(object sender, RoutedEventArgs e)
    {
    var streamService = _apiServer.StreamService;
    if (streamService == null) return;
    
  streamService.SetQuality((int)QualitySlider.Value);
    streamService.SetTargetFps((int)FpsSlider.Value);
 
 if (ResolutionCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            var parts = tag.Split(',');
         if (parts.Length == 2)
   {
           streamService.SetResolution(int.Parse(parts[0]), int.Parse(parts[1]));
    }
        }
        
    SaveSettingsFromUI();
   ShowToast("Stream settings applied!");
    }
  #endregion

#region Platform Connections
    private void ConnectSteam_Click(object sender, RoutedEventArgs e)
    {
  try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "steam://", UseShellExecute = true }); } catch { }
    }

    private void ConnectEpic_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "com.epicgames.launcher://", UseShellExecute = true }); } catch { }
    }

    private void ConnectGOG_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "goggalaxy://", UseShellExecute = true }); } catch { }
  }
    #endregion

    #region Settings
    private void LoadSettingsToUI(AppSettings settings)
    {
        PortTextBox.Text = settings.Port.ToString();
 AuthTokenTextBox.Text = settings.AuthToken ?? "";
        StartWithWindowsCheckbox.IsChecked = settings.StartWithWindows;
        MinimizeToTrayCheckbox.IsChecked = settings.MinimizeToTray;
        StartMinimizedCheckbox.IsChecked = settings.StartMinimized;
        
        DiscordToggle.IsChecked = settings.EnableDiscordRPC;
        DiscordShowGameCheckbox.IsChecked = settings.DiscordShowGame;
    DiscordShowStatsCheckbox.IsChecked = settings.DiscordShowPCStats;

        QualitySlider.Value = settings.StreamQuality;
        FpsSlider.Value = settings.StreamFps;
        
        foreach (ComboBoxItem item in ResolutionCombo.Items)
        {
   if (item.Tag is string tag)
{
      var parts = tag.Split(',');
      if (parts.Length == 2 && 
   int.Parse(parts[0]) == settings.StreamWidth &&
           int.Parse(parts[1]) == settings.StreamHeight)
         {
          ResolutionCombo.SelectedItem = item;
        break;
    }
            }
        }
    }

    private void SaveSettingsFromUI()
    {
        var settings = new AppSettings
        {
     Port = int.TryParse(PortTextBox.Text, out var port) ? port : 19500,
      AuthToken = AuthTokenTextBox.Text,
      StartWithWindows = StartWithWindowsCheckbox.IsChecked == true,
            MinimizeToTray = MinimizeToTrayCheckbox.IsChecked == true,
       StartMinimized = StartMinimizedCheckbox.IsChecked == true,
       EnableDiscordRPC = DiscordToggle.IsChecked == true,
  DiscordShowGame = DiscordShowGameCheckbox.IsChecked == true,
     DiscordShowPCStats = DiscordShowStatsCheckbox.IsChecked == true,
        StreamQuality = (int)QualitySlider.Value,
    StreamFps = (int)FpsSlider.Value
        };

        if (ResolutionCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            var parts = tag.Split(',');
  if (parts.Length == 2)
            {
   settings.StreamWidth = int.Parse(parts[0]);
         settings.StreamHeight = int.Parse(parts[1]);
  }
        }

  SettingsManager.Save(settings);
    }

 private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUI();
        ShowToast("Settings saved successfully!");
    }

 private void GenerateToken_Click(object sender, RoutedEventArgs e)
    {
        AuthTokenTextBox.Text = Guid.NewGuid().ToString("N")[..16];
        ShowToast("Auth token generated");
    }
    #endregion

    #region Discord
    private void DiscordToggle_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = DiscordToggle.IsChecked == true;
 
        if (enabled && !_discord.IsConnected)
     {
   _discord.Initialize();
}
     
        _discord.SetEnabled(enabled);
        SaveSettingsFromUI();
    }

    private void DiscordOption_Changed(object sender, RoutedEventArgs e)
    {
        _discord.SetShowGameActivity(DiscordShowGameCheckbox.IsChecked == true);
        _discord.SetShowPCStatus(DiscordShowStatsCheckbox.IsChecked == true);

DiscordPreviewLine1.Text = DiscordShowGameCheckbox.IsChecked == true ? "Cyberpunk 2077" : "Synktra Companion";
  DiscordPreviewLine2.Text = DiscordShowStatsCheckbox.IsChecked == true ? "CPU: 45% | RAM: 62%" : "Online";
    }
    #endregion

 #region Quick Actions
    private void CopyIP_Click(object sender, RoutedEventArgs e)
    {
        try
        {
     Clipboard.SetText(IpAddressText.Text);
            ShowToast("IP address copied to clipboard");
        }
        catch { }
    }
    #endregion

    #region About Links
    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
 {
try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://github.com/scriptaxy/gaming-hub", UseShellExecute = true }); } catch { }
    }

    private void OpenIssues_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://github.com/scriptaxy/gaming-hub/issues", UseShellExecute = true }); } catch { }
    }
    #endregion
}
