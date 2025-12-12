using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SynktraCompanion.Models;
using SynktraCompanion.Services;

namespace SynktraCompanion;

public partial class MainWindow : Window
{
    private readonly GameScanner _gameScanner;
    private readonly SystemMonitor _systemMonitor;
  private readonly DispatcherTimer _updateTimer;
    private ObservableCollection<InstalledGame> _allGames = [];
  private ObservableCollection<InstalledGame> _filteredGames = [];

    public MainWindow()
    {
        InitializeComponent();
     
  _gameScanner = new GameScanner();
        _systemMonitor = new SystemMonitor();
   
     // Setup update timer for system stats
        _updateTimer = new DispatcherTimer
     {
        Interval = TimeSpan.FromSeconds(2)
        };
     _updateTimer.Tick += UpdateTimer_Tick;
  _updateTimer.Start();

   // Initial load
        Loaded += MainWindow_Loaded;
     Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateNetworkInfo();
 await ScanGamesAsync();
  UpdateStats();
   LoadSettings();
    }

  private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Minimize to tray instead of closing
        if (MinimizeToTrayCheckbox.IsChecked == true)
        {
     e.Cancel = true;
Hide();
      }
    }

  private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdateStats();
   }

  private void UpdateStats()
    {
        var stats = _systemMonitor.GetCurrentStats();
        
        CpuUsageText.Text = $"{stats.CpuUsage:0}%";
        MemoryUsageText.Text = $"{stats.MemoryUsage:0}%";
        GpuUsageText.Text = stats.GpuUsage.HasValue ? $"{stats.GpuUsage:0}%" : "N/A";
        GamesCountText.Text = _allGames.Count.ToString();
   
   var currentGame = _systemMonitor.GetRunningGame(_allGames.ToList());
        CurrentGameText.Text = currentGame ?? "No game running";
    }

    private void UpdateNetworkInfo()
    {
        try
        {
  var host = Dns.GetHostEntry(Dns.GetHostName());
   foreach (var ip in host.AddressList)
            {
        if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
       IpAddressText.Text = $"{ip}:5000";
         break;
 }
}
        }
   catch
      {
   IpAddressText.Text = "localhost:5000";
        }
    }

    private async Task ScanGamesAsync()
    {
        _allGames = new ObservableCollection<InstalledGame>(await _gameScanner.ScanAllGamesAsync());
   ApplyFilter();
    }

  private void ApplyFilter()
    {
        string? filter = null;
        
        if (FilterSteam.IsChecked == true) filter = "Steam";
        else if (FilterEpic.IsChecked == true) filter = "Epic";
   else if (FilterGOG.IsChecked == true) filter = "GOG";
        else if (FilterOther.IsChecked == true) filter = "Other";

 if (filter == null)
   {
            _filteredGames = _allGames;
        }
  else if (filter == "Other")
        {
  _filteredGames = new ObservableCollection<InstalledGame>(
      _allGames.Where(g => g.Platform != "Steam" && g.Platform != "Epic" && g.Platform != "GOG"));
     }
        else
        {
       _filteredGames = new ObservableCollection<InstalledGame>(
      _allGames.Where(g => g.Platform == filter));
        }

        GamesListBox.ItemsSource = _filteredGames;
    }

    private void LoadSettings()
    {
        var settings = SettingsManager.Load();
        PortTextBox.Text = settings.Port.ToString();
        AuthTokenTextBox.Text = settings.AuthToken ?? "";
    StartWithWindowsCheckbox.IsChecked = settings.StartWithWindows;
     MinimizeToTrayCheckbox.IsChecked = settings.MinimizeToTray;
    }

    private void SaveSettings()
    {
        var settings = new AppSettings
        {
     Port = int.TryParse(PortTextBox.Text, out var port) ? port : 5000,
         AuthToken = AuthTokenTextBox.Text,
      StartWithWindows = StartWithWindowsCheckbox.IsChecked == true,
         MinimizeToTray = MinimizeToTrayCheckbox.IsChecked == true
        };
        SettingsManager.Save(settings);
    }

    // Navigation
    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string page)
{
     DashboardPage.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
      GamesPage.Visibility = page == "Games" ? Visibility.Visible : Visibility.Collapsed;
        AccountsPage.Visibility = page == "Accounts" ? Visibility.Visible : Visibility.Collapsed;
       SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // Games
    private async void ScanGames_Click(object sender, RoutedEventArgs e)
    {
      await ScanGamesAsync();
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
  {
     ApplyFilter();
    }

    private void LaunchGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is InstalledGame game)
{
     try
            {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
     {
       FileName = game.LaunchCommand,
            UseShellExecute = true
    });
            }
            catch (Exception ex)
            {
       MessageBox.Show($"Failed to launch game: {ex.Message}", "Error", 
        MessageBoxButton.OK, MessageBoxImage.Error);
      }
     }
    }

    // Accounts
    private void ConnectSteam_Click(object sender, RoutedEventArgs e)
    {
        // Open Steam login
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
 {
            FileName = "https://steamcommunity.com/openid/login",
   UseShellExecute = true
  });
    }

    private void ConnectEpic_Click(object sender, RoutedEventArgs e)
    {
      MessageBox.Show("Epic Games connection is handled through the iOS app.\n\nGames are scanned from your local Epic Games installation.", 
   "Epic Games", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ConnectGOG_Click(object sender, RoutedEventArgs e)
    {
  MessageBox.Show("GOG connection is handled through the iOS app.\n\nGames are scanned from your local GOG Galaxy installation.", 
            "GOG Galaxy", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Settings
    private void GenerateToken_Click(object sender, RoutedEventArgs e)
    {
        AuthTokenTextBox.Text = Guid.NewGuid().ToString("N")[..16];
        SaveSettings();
    }
}
