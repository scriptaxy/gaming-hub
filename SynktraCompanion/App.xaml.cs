using System.Windows;
using SynktraCompanion.Services;

namespace SynktraCompanion;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private ApiServer? _apiServer;

    protected override void OnStartup(StartupEventArgs e)
    {
   base.OnStartup(e);
   
        // Initialize services
      _apiServer = new ApiServer();
   _ = _apiServer.StartAsync();

   // Setup system tray
        SetupTrayIcon();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
  {
            Icon = System.Drawing.SystemIcons.Application,
     Visible = true,
   Text = "Synktra Companion"
   };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("Open", null, (s, e) => ShowMainWindow());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());
     
        _trayIcon.ContextMenuStrip = contextMenu;
        _trayIcon.DoubleClick += (s, e) => ShowMainWindow();
    }

    private void ShowMainWindow()
    {
     if (MainWindow == null)
 {
          MainWindow = new MainWindow();
      }
        MainWindow.Show();
   MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
    }

    private void ExitApplication()
    {
        _trayIcon?.Dispose();
        _apiServer?.Stop();
     Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _apiServer?.Stop();
        base.OnExit(e);
    }
}
