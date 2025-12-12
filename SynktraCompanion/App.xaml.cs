using System.Windows;
using SynktraCompanion.Services;

namespace SynktraCompanion;

public partial class App : Application
{
    private ApiServer? _apiServer;

    protected override void OnStartup(StartupEventArgs e)
    {
   base.OnStartup(e);
   
        // Initialize API server
        _apiServer = new ApiServer();
     _ = _apiServer.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
     _apiServer?.Stop();
        base.OnExit(e);
    }
}
