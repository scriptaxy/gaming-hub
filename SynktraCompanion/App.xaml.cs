using System.Windows;
using System.Threading;

namespace SynktraCompanion;

public partial class App : Application
{
    private static Mutex? _mutex;
    private const string MutexName = "SynktraCompanionSingleInstance";

    protected override void OnStartup(StartupEventArgs e)
    {
        // Single instance check
        _mutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running
            MessageBox.Show("Synktra Companion is already running!", "Synktra Companion",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
