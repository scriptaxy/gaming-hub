using GameController;
using Foundation;
using gaming_hub.ViewControllers;

namespace gaming_hub.Services
{
    /// <summary>
    /// Service for handling physical game controllers (MFi, Xbox, PlayStation)
    /// </summary>
    public class PhysicalControllerService : IDisposable
    {
        private static PhysicalControllerService? _instance;
        public static PhysicalControllerService Instance => _instance ??= new PhysicalControllerService();

        public event Action<GamepadState>? OnStateChanged;
        public event Action<GCController>? OnControllerConnected;
        public event Action<GCController>? OnControllerDisconnected;

        private GCController? _connectedController;
        private GamepadState _currentState = new();
        private NSObject? _connectObserver;
        private NSObject? _disconnectObserver;
        private bool _isEnabled;

        public bool IsControllerConnected => _connectedController != null;
        public string? ControllerName => _connectedController?.VendorName;

private PhysicalControllerService()
        {
   }

        /// <summary>
      /// Start listening for controller connections
        /// </summary>
        public void StartListening()
        {
            if (_isEnabled) return;
         _isEnabled = true;

            // Listen for controller connections
            _connectObserver = NSNotificationCenter.DefaultCenter.AddObserver(
                GCController.DidConnectNotification,
        notification => OnControllerDidConnect(notification));

            _disconnectObserver = NSNotificationCenter.DefaultCenter.AddObserver(
           GCController.DidDisconnectNotification,
              notification => OnControllerDidDisconnect(notification));

            // Check for already connected controllers
   var controllers = GCController.Controllers;
        if (controllers.Length > 0)
        {
    SetupController(controllers[0]);
     }

          // Start wireless controller discovery
   GCController.StartWirelessControllerDiscovery(() =>
            {
          Console.WriteLine("Wireless controller discovery completed");
            });
        }

        /// <summary>
/// Stop listening for controllers
   /// </summary>
        public void StopListening()
        {
   _isEnabled = false;

  if (_connectObserver != null)
 {
              NSNotificationCenter.DefaultCenter.RemoveObserver(_connectObserver);
    _connectObserver = null;
    }

     if (_disconnectObserver != null)
  {
         NSNotificationCenter.DefaultCenter.RemoveObserver(_disconnectObserver);
        _disconnectObserver = null;
          }

 GCController.StopWirelessControllerDiscovery();
            _connectedController = null;
        }

        private void OnControllerDidConnect(NSNotification notification)
        {
            var controller = notification.Object as GCController;
     if (controller != null)
       {
        SetupController(controller);
 OnControllerConnected?.Invoke(controller);
            }
        }

 private void OnControllerDidDisconnect(NSNotification notification)
        {
       var controller = notification.Object as GCController;
       if (controller == _connectedController)
       {
   _connectedController = null;
          OnControllerDisconnected?.Invoke(controller!);
  }
        }

        private void SetupController(GCController controller)
        {
    _connectedController = controller;
            controller.PlayerIndex = GCControllerPlayerIndex.Index1;

            // Extended gamepad (Xbox, PlayStation, modern MFi)
            var extendedGamepad = controller.ExtendedGamepad;
            if (extendedGamepad != null)
            {
      SetupExtendedGamepad(extendedGamepad);
    return;
      }

      // Micro gamepad (Siri Remote, etc.)
            var microGamepad = controller.MicroGamepad;
            if (microGamepad != null)
        {
    SetupMicroGamepad(microGamepad);
    }
      }

    private void SetupExtendedGamepad(GCExtendedGamepad gamepad)
        {
        // Use value changed handler for all inputs
            gamepad.ValueChangedHandler = (gp, element) =>
         {
  UpdateStateFromExtendedGamepad(gp);
    };
        }

        private void SetupMicroGamepad(GCMicroGamepad gamepad)
     {
            gamepad.ValueChangedHandler = (gp, element) =>
      {
       UpdateStateFromMicroGamepad(gp);
 };
        }

        private void UpdateStateFromExtendedGamepad(GCExtendedGamepad gamepad)
        {
      _currentState = new GamepadState();

            // Analog sticks
            _currentState.LeftStickX = gamepad.LeftThumbstick.XAxis.Value;
        _currentState.LeftStickY = -gamepad.LeftThumbstick.YAxis.Value; // Invert Y
            _currentState.RightStickX = gamepad.RightThumbstick.XAxis.Value;
          _currentState.RightStickY = -gamepad.RightThumbstick.YAxis.Value;

          // Triggers
     _currentState.LeftTrigger = gamepad.LeftTrigger.Value;
      _currentState.RightTrigger = gamepad.RightTrigger.Value;

// Face buttons
    if (gamepad.ButtonA.IsPressed) _currentState.Buttons |= GamepadButtons.A;
    if (gamepad.ButtonB.IsPressed) _currentState.Buttons |= GamepadButtons.B;
    if (gamepad.ButtonX.IsPressed) _currentState.Buttons |= GamepadButtons.X;
   if (gamepad.ButtonY.IsPressed) _currentState.Buttons |= GamepadButtons.Y;

 // Bumpers
            if (gamepad.LeftShoulder.IsPressed) _currentState.Buttons |= GamepadButtons.LeftBumper;
   if (gamepad.RightShoulder.IsPressed) _currentState.Buttons |= GamepadButtons.RightBumper;

            // D-Pad
if (gamepad.DPad.Up.IsPressed) _currentState.Buttons |= GamepadButtons.DPadUp;
        if (gamepad.DPad.Down.IsPressed) _currentState.Buttons |= GamepadButtons.DPadDown;
       if (gamepad.DPad.Left.IsPressed) _currentState.Buttons |= GamepadButtons.DPadLeft;
    if (gamepad.DPad.Right.IsPressed) _currentState.Buttons |= GamepadButtons.DPadRight;

         // Thumbstick buttons
       if (gamepad.LeftThumbstickButton?.IsPressed == true) _currentState.Buttons |= GamepadButtons.LeftStick;
  if (gamepad.RightThumbstickButton?.IsPressed == true) _currentState.Buttons |= GamepadButtons.RightStick;

            // Menu buttons
            if (gamepad.ButtonMenu.IsPressed) _currentState.Buttons |= GamepadButtons.Start;
     if (gamepad.ButtonOptions?.IsPressed == true) _currentState.Buttons |= GamepadButtons.Back;

 OnStateChanged?.Invoke(_currentState);
        }

        private void UpdateStateFromMicroGamepad(GCMicroGamepad gamepad)
        {
 _currentState = new GamepadState();

     // D-Pad acts as left stick for micro gamepad
  _currentState.LeftStickX = gamepad.Dpad.XAxis.Value;
        _currentState.LeftStickY = -gamepad.Dpad.YAxis.Value;

            // Limited buttons
            if (gamepad.ButtonA.IsPressed) _currentState.Buttons |= GamepadButtons.A;
            if (gamepad.ButtonX.IsPressed) _currentState.Buttons |= GamepadButtons.X;
          if (gamepad.ButtonMenu.IsPressed) _currentState.Buttons |= GamepadButtons.Start;

            OnStateChanged?.Invoke(_currentState);
        }

     /// <summary>
        /// Trigger haptic feedback on the controller (if supported)
        /// </summary>
        public void TriggerHaptic(float intensity = 0.5f)
        {
      var haptics = _connectedController?.Haptics;
          if (haptics == null) return;

       var engine = haptics.CreateEngine(GCHapticsLocality.Default);
            if (engine == null) return;

      try
       {
     engine.Start(out _);
   // Note: Full haptic pattern implementation would require CHHapticPattern
       }
    catch
            {
         // Haptics not supported on this controller
            }
        }

        /// <summary>
        /// Set the controller's light color (PlayStation controllers)
/// </summary>
        public void SetLightColor(float r, float g, float b)
   {
  var light = _connectedController?.Light;
            if (light != null)
        {
 light.Color = new GCColor(r, g, b);
 }
        }

        public void Dispose()
        {
            StopListening();
       _instance = null;
        }
    }
}
