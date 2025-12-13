using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace SynktraCompanion.Services;

/// <summary>
/// Virtual Xbox 360 controller service using ViGEmBus.
/// This creates a virtual gamepad that Windows and games recognize as a real Xbox 360 controller.
/// Includes automatic ViGEmBus driver installation if not present.
/// </summary>
public class VirtualControllerService : IDisposable
{
    private static VirtualControllerService? _instance;
  public static VirtualControllerService Instance => _instance ??= new VirtualControllerService();

    private ViGEmClient? _client;
    private IXbox360Controller? _controller;
    private bool _isConnected;
    private bool _isEnabled = true;
    private bool _vigemAvailable;
  private string? _lastError;
    private bool _isInstallingDriver;

    // ViGEmBus download URL (latest stable release)
    private const string ViGEmBusDownloadUrl = "https://github.com/nefarius/ViGEmBus/releases/download/v1.24.0/ViGEmBus_1.24.0_x64_x86_arm64.exe";
    private const string ViGEmBusInstallerName = "ViGEmBus_Setup.exe";

    private readonly object _stateLock = new();

    /// <summary>
    /// Whether the virtual controller is connected and active
    /// </summary>
    public bool IsConnected => _isConnected && _controller != null;

    /// <summary>
 /// Whether virtual controller emulation is enabled (vs keyboard/mouse fallback)
 /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
 if (value && _vigemAvailable && !_isConnected)
            {
        Connect();
            }
         else if (!value && _isConnected)
       {
              Disconnect();
      }
        }
    }

    /// <summary>
    /// Whether ViGEmBus driver is available on the system
    /// </summary>
    public bool IsViGEmAvailable => _vigemAvailable;

    /// <summary>
    /// Whether driver installation is in progress
    /// </summary>
    public bool IsInstallingDriver => _isInstallingDriver;

    /// <summary>
    /// Last error message if connection failed
    /// </summary>
    public string? LastError => _lastError;

    public event Action<bool>? OnConnectionChanged;
    public event Action<string>? OnError;
    public event Action<string>? OnInstallProgress;

    private VirtualControllerService()
    {
      CheckViGEmAvailability();
    }

    /// <summary>
/// Check if ViGEmBus driver is installed
/// </summary>
  private void CheckViGEmAvailability()
    {
     try
        {
    _client = new ViGEmClient();
   _vigemAvailable = true;
Console.WriteLine("ViGEmBus driver detected - virtual controller support available");
        }
        catch (Nefarius.ViGEm.Client.Exceptions.VigemBusNotFoundException)
        {
    _vigemAvailable = false;
 _lastError = "ViGEmBus driver not installed. Click 'Install Driver' to set it up automatically.";
            Console.WriteLine($"ViGEmBus not found: {_lastError}");
        }
        catch (Exception ex)
        {
_vigemAvailable = false;
      _lastError = $"ViGEmBus initialization failed: {ex.Message}";
      Console.WriteLine(_lastError);
        }
    }

    /// <summary>
    /// Download and install ViGEmBus driver automatically
    /// </summary>
    public async Task<bool> InstallViGEmBusAsync()
    {
  if (_vigemAvailable)
  {
            OnInstallProgress?.Invoke("ViGEmBus is already installed!");
            return true;
   }

     if (_isInstallingDriver)
        {
            OnInstallProgress?.Invoke("Installation already in progress...");
         return false;
        }

        _isInstallingDriver = true;

  try
 {
   var tempPath = Path.Combine(Path.GetTempPath(), ViGEmBusInstallerName);

       // Download the installer
    OnInstallProgress?.Invoke("Downloading ViGEmBus driver...");
       Console.WriteLine($"Downloading ViGEmBus from {ViGEmBusDownloadUrl}");

            using var httpClient = new HttpClient();
          httpClient.Timeout = TimeSpan.FromMinutes(5);

   var response = await httpClient.GetAsync(ViGEmBusDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

    var totalBytes = response.Content.Headers.ContentLength ?? 0;
  var buffer = new byte[8192];
   var bytesRead = 0L;

        await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

        int read;
        while ((read = await contentStream.ReadAsync(buffer)) > 0)
            {
        await fileStream.WriteAsync(buffer.AsMemory(0, read));
    bytesRead += read;

        if (totalBytes > 0)
     {
        var progress = (int)((bytesRead * 100) / totalBytes);
     OnInstallProgress?.Invoke($"Downloading... {progress}%");
    }
    }

            OnInstallProgress?.Invoke("Download complete. Starting installation...");
            Console.WriteLine($"Downloaded to {tempPath}");

        // Run the installer silently
   OnInstallProgress?.Invoke("Installing ViGEmBus driver (may require admin approval)...");

            var processInfo = new ProcessStartInfo
  {
    FileName = tempPath,
             Arguments = "/passive /norestart",
         UseShellExecute = true,
                Verb = "runas"
         };

      using var process = Process.Start(processInfo);
 if (process == null)
     {
    throw new Exception("Failed to start installer");
   }

      await process.WaitForExitAsync();

            // Clean up installer
            try { File.Delete(tempPath); } catch { }

  if (process.ExitCode == 0 || process.ExitCode == 3010)
       {
     OnInstallProgress?.Invoke("Installation successful! Initializing driver...");

  await Task.Delay(2000);

                CheckViGEmAvailability();

                if (_vigemAvailable)
                {
        OnInstallProgress?.Invoke("ViGEmBus driver ready! Virtual controller available.");
      Connect();
        return true;
     }
           else
                {
     OnInstallProgress?.Invoke("Driver installed but may require a restart to activate.");
     _lastError = "Please restart your computer to complete ViGEmBus installation.";
    return false;
                }
            }
         else if (process.ExitCode == 1602)
{
      OnInstallProgress?.Invoke("Installation cancelled by user.");
             _lastError = "Installation was cancelled.";
   return false;
      }
            else
          {
      OnInstallProgress?.Invoke($"Installation failed with code {process.ExitCode}");
                _lastError = $"Installation failed (exit code: {process.ExitCode})";
      return false;
   }
        }
        catch (HttpRequestException ex)
        {
            _lastError = $"Download failed: {ex.Message}";
         OnInstallProgress?.Invoke(_lastError);
 Console.WriteLine(_lastError);
  return false;
     }
    catch (Exception ex) when (ex.Message.Contains("cancelled") || ex.Message.Contains("elevation"))
        {
  _lastError = "Administrator permission required to install driver.";
      OnInstallProgress?.Invoke(_lastError);
            Console.WriteLine(_lastError);
            return false;
        }
        catch (Exception ex)
 {
     _lastError = $"Installation error: {ex.Message}";
         OnInstallProgress?.Invoke(_lastError);
    Console.WriteLine(_lastError);
            return false;
        }
        finally
        {
       _isInstallingDriver = false;
   }
    }

    /// <summary>
    /// Open the ViGEmBus GitHub releases page for manual download
 /// </summary>
    public void OpenViGEmDownloadPage()
    {
  try
        {
            Process.Start(new ProcessStartInfo
   {
                FileName = "https://github.com/ViGEm/ViGEmBus/releases",
       UseShellExecute = true
     });
 }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open browser: {ex.Message}");
        }
 }

    /// <summary>
    /// Connect and plug in the virtual controller
    /// </summary>
    public bool Connect()
 {
        if (_isConnected) return true;
 if (!_vigemAvailable || !_isEnabled) return false;

        try
 {
            _client ??= new ViGEmClient();
            _controller = _client.CreateXbox360Controller();
     _controller.Connect();

          _isConnected = true;
   _lastError = null;

            Console.WriteLine("Virtual Xbox 360 controller connected");
       OnConnectionChanged?.Invoke(true);
     return true;
      }
        catch (Exception ex)
        {
      _lastError = $"Failed to connect virtual controller: {ex.Message}";
   Console.WriteLine(_lastError);
   OnError?.Invoke(_lastError);
        return false;
        }
    }

    /// <summary>
    /// Disconnect and unplug the virtual controller
    /// </summary>
    public void Disconnect()
    {
        if (!_isConnected) return;

        try
        {
   _controller?.Disconnect();
            _controller = null;
  _isConnected = false;

            Console.WriteLine("Virtual Xbox 360 controller disconnected");
         OnConnectionChanged?.Invoke(false);
     }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disconnecting virtual controller: {ex.Message}");
}
    }

    /// <summary>
    /// Update the virtual controller state with gamepad input
    /// </summary>
    public void UpdateState(InputCommand cmd)
    {
        if (!_isConnected || _controller == null) return;

        try
    {
            lock (_stateLock)
   {
     // Set analog sticks (values are -1 to 1, need to convert to short range)
    _controller.SetAxisValue(Xbox360Axis.LeftThumbX, (short)(cmd.LeftStickX * short.MaxValue));
    _controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)(-cmd.LeftStickY * short.MaxValue));
 _controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)(cmd.RightStickX * short.MaxValue));
          _controller.SetAxisValue(Xbox360Axis.RightThumbY, (short)(-cmd.RightStickY * short.MaxValue));

                // Set triggers (values are 0 to 1, need to convert to byte range)
    _controller.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(cmd.LeftTrigger * 255));
    _controller.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(cmd.RightTrigger * 255));

    // Set buttons
 SetButtons(cmd.Buttons);

     // Submit the report
          _controller.SubmitReport();
       }
     }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating virtual controller: {ex.Message}");
        }
    }

    /// <summary>
    /// Set button states on the controller
 /// </summary>
    private void SetButtons(GamepadButtons buttons)
    {
        if (_controller == null) return;

        _controller.SetButtonState(Xbox360Button.A, (buttons & GamepadButtons.A) != 0);
        _controller.SetButtonState(Xbox360Button.B, (buttons & GamepadButtons.B) != 0);
   _controller.SetButtonState(Xbox360Button.X, (buttons & GamepadButtons.X) != 0);
        _controller.SetButtonState(Xbox360Button.Y, (buttons & GamepadButtons.Y) != 0);
        _controller.SetButtonState(Xbox360Button.LeftShoulder, (buttons & GamepadButtons.LeftBumper) != 0);
        _controller.SetButtonState(Xbox360Button.RightShoulder, (buttons & GamepadButtons.RightBumper) != 0);
        _controller.SetButtonState(Xbox360Button.Back, (buttons & GamepadButtons.Back) != 0);
        _controller.SetButtonState(Xbox360Button.Start, (buttons & GamepadButtons.Start) != 0);
        _controller.SetButtonState(Xbox360Button.LeftThumb, (buttons & GamepadButtons.LeftStick) != 0);
        _controller.SetButtonState(Xbox360Button.RightThumb, (buttons & GamepadButtons.RightStick) != 0);
        _controller.SetButtonState(Xbox360Button.Up, (buttons & GamepadButtons.DPadUp) != 0);
  _controller.SetButtonState(Xbox360Button.Down, (buttons & GamepadButtons.DPadDown) != 0);
_controller.SetButtonState(Xbox360Button.Left, (buttons & GamepadButtons.DPadLeft) != 0);
        _controller.SetButtonState(Xbox360Button.Right, (buttons & GamepadButtons.DPadRight) != 0);
    }

    /// <summary>
    /// Update individual axis values
    /// </summary>
    public void UpdateAxes(float leftX, float leftY, float rightX, float rightY)
    {
        if (!_isConnected || _controller == null) return;

        try
        {
lock (_stateLock)
            {
       _controller.SetAxisValue(Xbox360Axis.LeftThumbX, (short)(leftX * short.MaxValue));
     _controller.SetAxisValue(Xbox360Axis.LeftThumbY, (short)(-leftY * short.MaxValue));
                _controller.SetAxisValue(Xbox360Axis.RightThumbX, (short)(rightX * short.MaxValue));
       _controller.SetAxisValue(Xbox360Axis.RightThumbY, (short)(-rightY * short.MaxValue));
      _controller.SubmitReport();
       }
        }
     catch (Exception ex)
     {
          Console.WriteLine($"Error updating virtual controller axes: {ex.Message}");
   }
    }

    /// <summary>
    /// Update trigger values
 /// </summary>
    public void UpdateTriggers(float left, float right)
    {
        if (!_isConnected || _controller == null) return;

        try
        {
         lock (_stateLock)
       {
   _controller.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(left * 255));
                _controller.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(right * 255));
     _controller.SubmitReport();
   }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating virtual controller triggers: {ex.Message}");
        }
    }

    /// <summary>
    /// Update button states
    /// </summary>
    public void UpdateButtons(GamepadButtons buttons)
    {
        if (!_isConnected || _controller == null) return;

        try
        {
   lock (_stateLock)
       {
        SetButtons(buttons);
            _controller.SubmitReport();
    }
        }
        catch (Exception ex)
        {
   Console.WriteLine($"Error updating virtual controller buttons: {ex.Message}");
        }
    }

    /// <summary>
    /// Press and release a button (for single-tap actions)
    /// </summary>
    public async Task PressButtonAsync(Xbox360Button button, int durationMs = 100)
    {
        if (!_isConnected || _controller == null) return;

        try
        {
    _controller.SetButtonState(button, true);
     _controller.SubmitReport();
            await Task.Delay(durationMs);
            _controller.SetButtonState(button, false);
            _controller.SubmitReport();
        }
        catch (Exception ex)
    {
            Console.WriteLine($"Error pressing virtual controller button: {ex.Message}");
        }
    }

    /// <summary>
    /// Set rumble/vibration feedback
    /// </summary>
    public void SetVibration(byte largeMotor, byte smallMotor)
    {
      Console.WriteLine($"Vibration request: Large={largeMotor}, Small={smallMotor}");
    }

    /// <summary>
    /// Reset all inputs to neutral state
    /// </summary>
    public void ResetState()
    {
        if (!_isConnected || _controller == null) return;

        try
        {
        lock (_stateLock)
            {
         // Reset all axes to center
              _controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
 _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
       _controller.SetAxisValue(Xbox360Axis.RightThumbX, 0);
 _controller.SetAxisValue(Xbox360Axis.RightThumbY, 0);

       // Reset triggers
           _controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
                _controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);

         // Release all buttons
          SetButtons(GamepadButtons.None);

_controller.SubmitReport();
            }
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Error resetting virtual controller: {ex.Message}");
   }
    }

    /// <summary>
    /// Get current controller status for diagnostics
    /// </summary>
    public ControllerStatus GetStatus()
    {
   return new ControllerStatus
        {
      IsViGEmInstalled = _vigemAvailable,
   IsConnected = _isConnected,
   IsEnabled = _isEnabled,
            IsInstallingDriver = _isInstallingDriver,
   ControllerType = "Xbox 360",
            LastError = _lastError,
    CanAutoInstall = true
        };
    }

    public void Dispose()
 {
        Disconnect();
     _client?.Dispose();
        _client = null;
    }
}

/// <summary>
/// Status information for the virtual controller
/// </summary>
public class ControllerStatus
{
    public bool IsViGEmInstalled { get; set; }
    public bool IsConnected { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsInstallingDriver { get; set; }
    public string ControllerType { get; set; } = string.Empty;
    public string? LastError { get; set; }
    public bool CanAutoInstall { get; set; }
}
