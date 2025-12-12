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
    private readonly ApiServer _apiServer;
    private readonly DispatcherTimer _updateTimer;
    private List<InstalledGame> _allGames = [];
    private List<InstalledGame> _filteredGames = [];

    public MainWindow()
    {
        InitializeComponent();

        _gameScanner = new GameScanner();
        _systemMonitor = new SystemMonitor();
        _apiServer = new ApiServer();

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateNetworkInfo();
        _updateTimer.Start();

        var settings = SettingsManager.Load();
        await _apiServer.StartAsync(settings.Port);

        _ = Task.Run(async () =>
        {
            var games = await _gameScanner.ScanAllGamesAsync();
            Dispatcher.Invoke(() =>
            {
                _allGames = games;
                ApplyFilter();
                UpdateStats();
            });
        });
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _updateTimer.Stop();
        _apiServer.Stop();
        Application.Current.Shutdown();
    }

    private void MinBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaxBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdateStats();
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

            var currentGame = _systemMonitor.GetRunningGame(_allGames);
            CurrentGameText.Text = currentGame ?? "No game running";
        }
        catch { }
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
                    var settings = SettingsManager.Load();
                    IpAddressText.Text = $"{ip}:{settings.Port}";
                    return;
                }
            }
        }
        catch { }
        IpAddressText.Text = "localhost:5000";
    }

    private void ApplyFilter()
    {
        string? filter = null;

        if (FilterSteam?.IsChecked == true) filter = "Steam";
        else if (FilterEpic?.IsChecked == true) filter = "Epic";
        else if (FilterGOG?.IsChecked == true) filter = "GOG";
        else if (FilterOther?.IsChecked == true) filter = "Other";

        if (filter == null)
        {
            _filteredGames = _allGames;
        }
        else if (filter == "Other")
        {
            _filteredGames = _allGames
                .Where(g => g.Platform != "Steam" && g.Platform != "Epic" && g.Platform != "GOG")
                .ToList();
        }
        else
        {
            _filteredGames = _allGames.Where(g => g.Platform == filter).ToList();
        }

        GamesListBox.ItemsSource = _filteredGames;
    }

    private void LoadSettings()
    {
        try
        {
            var settings = SettingsManager.Load();
            PortTextBox.Text = settings.Port.ToString();
            AuthTokenTextBox.Text = settings.AuthToken ?? "";
            StartWithWindowsCheckbox.IsChecked = settings.StartWithWindows;
            MinimizeToTrayCheckbox.IsChecked = settings.MinimizeToTray;
        }
        catch { }
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

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string page)
        {
            DashboardPage.Visibility = page == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
            GamesPage.Visibility = page == "Games" ? Visibility.Visible : Visibility.Collapsed;
            AccountsPage.Visibility = page == "Accounts" ? Visibility.Visible : Visibility.Collapsed;
            SettingsPage.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;

            if (page == "Settings") LoadSettings();
        }
    }

    private async void ScanGames_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;

        await Task.Run(async () =>
        {
            var games = await _gameScanner.ScanAllGamesAsync();
            Dispatcher.Invoke(() =>
            {
                _allGames = games;
                ApplyFilter();
                GamesCountText.Text = _allGames.Count.ToString();
                if (btn != null) btn.IsEnabled = true;
            });
        });
    }

    private void Filter_Click(object sender, RoutedEventArgs e) => ApplyFilter();

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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

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

    private void GenerateToken_Click(object sender, RoutedEventArgs e)
    {
        AuthTokenTextBox.Text = Guid.NewGuid().ToString("N")[..16];
        SaveSettings();
    }
}
