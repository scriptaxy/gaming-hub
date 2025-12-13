using System.Runtime.InteropServices;

namespace SynktraCompanion.Services;

/// <summary>
/// High-performance input simulator using SendInput API
/// Optimized for low-latency remote gaming
/// 
/// Supports two modes:
/// 1. Virtual Controller Mode (preferred): Uses ViGEmBus to create a virtual Xbox 360 controller
///    that games recognize as a real gamepad. Requires ViGEmBus driver installation.
/// 2. Keyboard/Mouse Fallback: Maps gamepad inputs to keyboard keys and mouse movements
///    when ViGEmBus is not available.
/// </summary>
public class InputSimulator
{
    private static InputSimulator? _instance;
    public static InputSimulator Instance => _instance ??= new InputSimulator();

    // Input mode
    private bool _useVirtualController = true;
    private bool _virtualControllerInitialized;

    /// <summary>
    /// Whether to use virtual controller (true) or keyboard/mouse fallback (false)
    /// </summary>
    public bool UseVirtualController
  {
      get => _useVirtualController && VirtualControllerService.Instance.IsViGEmAvailable;
        set
 {
  _useVirtualController = value;
     if (value)
  {
        InitializeVirtualController();
          }
            else
            {
        VirtualControllerService.Instance.Disconnect();
     }
     }
    }

    /// <summary>
    /// Whether virtual controller is currently active
    /// </summary>
    public bool IsVirtualControllerActive => VirtualControllerService.Instance.IsConnected;

    /// <summary>
    /// Whether ViGEmBus is available on the system
    /// </summary>
    public bool IsViGEmAvailable => VirtualControllerService.Instance.IsViGEmAvailable;

    /// <summary>
    /// Get current input mode description
    /// </summary>
    public string InputModeDescription => IsVirtualControllerActive 
        ? "Xbox 360 Controller (Virtual)" 
        : "Keyboard/Mouse (Fallback)";

  // Virtual gamepad to keyboard mapping (fallback mode)
    private readonly Dictionary<GamepadButtons, ushort> _buttonToKey = new()
    {
        { GamepadButtons.A, 0x20 }, // Space
   { GamepadButtons.B, 0x1B }, // Escape
        { GamepadButtons.X, 0x45 }, // E
        { GamepadButtons.Y, 0x52 },           // R
        { GamepadButtons.LeftBumper, 0x51 },  // Q
 { GamepadButtons.RightBumper, 0x46 }, // F
        { GamepadButtons.Start, 0x0D },       // Enter
    { GamepadButtons.Back, 0x09 },        // Tab
        { GamepadButtons.DPadUp, 0x26 },      // Up Arrow
        { GamepadButtons.DPadDown, 0x28 },    // Down Arrow
     { GamepadButtons.DPadLeft, 0x25 },    // Left Arrow
        { GamepadButtons.DPadRight, 0x27 },   // Right Arrow
        { GamepadButtons.LeftStick, 0x10 },   // Shift (sprint)
        { GamepadButtons.RightStick, 0x11 }   // Ctrl (crouch)
    };

    // Movement keys for analog sticks (fallback mode)
    private const ushort KEY_W = 0x57;
    private const ushort KEY_A = 0x41;
    private const ushort KEY_S = 0x53;
    private const ushort KEY_D = 0x44;

private GamepadButtons _lastButtons;
    private bool _leftStickUp, _leftStickDown, _leftStickLeft, _leftStickRight;
    private bool _leftTriggerDown, _rightTriggerDown;
    private float _lastRightStickX, _lastRightStickY;

    // Mouse accumulator for smooth movement (fallback mode)
    private float _mouseAccumX, _mouseAccumY;
    private const float MouseSensitivity = 20f;
    private const float MouseDeadzone = 0.12f;

  // Input batching
    private readonly List<INPUT> _inputBatch = new(32);
    private readonly object _batchLock = new();

  #region Win32 API - SendInput (faster than keybd_event/mouse_event)

    [DllImport("user32.dll", SetLastError = true)]
  private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
    public int X;
   public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
  public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
  [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
    public int dy;
        public uint mouseData;
     public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
      public ushort wVk;
    public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;

    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

    #endregion

    private InputSimulator()
    {
// Try to initialize virtual controller on startup
        InitializeVirtualController();
    }

/// <summary>
    /// Initialize the virtual controller if ViGEmBus is available
    /// </summary>
    public void InitializeVirtualController()
    {
   if (_virtualControllerInitialized) return;

 if (VirtualControllerService.Instance.IsViGEmAvailable)
     {
    if (VirtualControllerService.Instance.Connect())
            {
     Console.WriteLine("Virtual Xbox 360 controller initialized - games will detect controller support");
     _virtualControllerInitialized = true;
 }
            else
          {
     Console.WriteLine($"Failed to connect virtual controller: {VirtualControllerService.Instance.LastError}");
    Console.WriteLine("Falling back to keyboard/mouse emulation");
            }
        }
     else
      {
  Console.WriteLine("ViGEmBus not installed - using keyboard/mouse fallback");
    Console.WriteLine("Install ViGEmBus for full controller support: https://github.com/ViGEm/ViGEmBus/releases");
   }
    }

    public void ProcessCommand(InputCommand cmd)
    {
        switch (cmd.Type?.ToLowerInvariant())
        {
     case "gamepad":
ProcessGamepad(cmd);
   break;
    case "mouse":
       ProcessMouse(cmd);
         break;
     case "mouseclick":
                ProcessMouseClick(cmd);
   break;
            case "keyboard":
ProcessKeyboard(cmd);
     break;
        }
    }

    private void ProcessGamepad(InputCommand cmd)
    {
        // Use virtual controller if available and enabled
        if (UseVirtualController && VirtualControllerService.Instance.IsConnected)
        {
            VirtualControllerService.Instance.UpdateState(cmd);
   return;
     }

        // Fallback to keyboard/mouse emulation
        ProcessGamepadAsFallback(cmd);
    }

    /// <summary>
    /// Process gamepad input as keyboard/mouse (fallback when ViGEmBus not available)
    /// </summary>
    private void ProcessGamepadAsFallback(InputCommand cmd)
    {
        lock (_batchLock)
     {
            _inputBatch.Clear();

            // Process buttons
    ProcessButtons(cmd.Buttons);

// Process left stick (WASD)
  ProcessLeftStick(cmd.LeftStickX, cmd.LeftStickY);

            // Process right stick (mouse movement)
            ProcessRightStick(cmd.RightStickX, cmd.RightStickY);

            // Process triggers
            ProcessTriggers(cmd.LeftTrigger, cmd.RightTrigger);

        // Send all batched inputs at once
 if (_inputBatch.Count > 0)
{
           var inputs = _inputBatch.ToArray();
   SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
      }
        }
    }

    private void ProcessButtons(GamepadButtons buttons)
    {
        foreach (var mapping in _buttonToKey)
        {
    bool wasPressed = (_lastButtons & mapping.Key) != 0;
        bool isPressed = (buttons & mapping.Key) != 0;

     if (isPressed && !wasPressed)
          {
   AddKeyInput(mapping.Value, true);
    }
        else if (!isPressed && wasPressed)
            {
         AddKeyInput(mapping.Value, false);
            }
        }

        _lastButtons = buttons;
    }

    private void ProcessLeftStick(float x, float y)
    {
        const float deadzone = 0.25f;

     // Up/Down (W/S)
        bool up = y < -deadzone;
        bool down = y > deadzone;

      if (up != _leftStickUp)
        {
      AddKeyInput(KEY_W, up);
         _leftStickUp = up;
        }

  if (down != _leftStickDown)
  {
  AddKeyInput(KEY_S, down);
   _leftStickDown = down;
        }

        // Left/Right (A/D)
  bool left = x < -deadzone;
        bool right = x > deadzone;

  if (left != _leftStickLeft)
     {
    AddKeyInput(KEY_A, left);
   _leftStickLeft = left;
        }

  if (right != _leftStickRight)
      {
    AddKeyInput(KEY_D, right);
   _leftStickRight = right;
      }
    }

private void ProcessRightStick(float x, float y)
    {
        // Apply deadzone
        if (Math.Abs(x) < MouseDeadzone) x = 0;
        if (Math.Abs(y) < MouseDeadzone) y = 0;

if (x == 0 && y == 0)
        {
            _mouseAccumX = 0;
         _mouseAccumY = 0;
            return;
        }

        // Apply non-linear curve for better precision at low speeds
        x = Math.Sign(x) * MathF.Pow(Math.Abs(x), 1.5f);
        y = Math.Sign(y) * MathF.Pow(Math.Abs(y), 1.5f);

        // Accumulate fractional movement
      _mouseAccumX += x * MouseSensitivity;
 _mouseAccumY += y * MouseSensitivity;

        int dx = (int)_mouseAccumX;
        int dy = (int)_mouseAccumY;

        if (dx != 0 || dy != 0)
        {
      _mouseAccumX -= dx;
 _mouseAccumY -= dy;
            AddMouseMove(dx, dy);
        }
    }

    private void ProcessTriggers(float left, float right)
 {
        const float threshold = 0.3f;

     // Left trigger = right mouse button (aim)
      bool leftPressed = left > threshold;
        if (leftPressed != _leftTriggerDown)
        {
        AddMouseButton(false, true, leftPressed);
            _leftTriggerDown = leftPressed;
        }

      // Right trigger = left mouse button (shoot)
        bool rightPressed = right > threshold;
     if (rightPressed != _rightTriggerDown)
  {
   AddMouseButton(true, false, rightPressed);
            _rightTriggerDown = rightPressed;
        }
    }

    private void ProcessMouse(InputCommand cmd)
    {
        // Absolute positioning
        if (cmd.MoveOnly != true && (cmd.MouseX != 0 || cmd.MouseY != 0))
        {
       SetCursorPos(cmd.MouseX, cmd.MouseY);
        }

      // Handle clicks
        if (cmd.MouseLeft)
        {
    var inputs = new INPUT[2];
            inputs[0] = CreateMouseInput(MOUSEEVENTF_LEFTDOWN);
          inputs[1] = CreateMouseInput(MOUSEEVENTF_LEFTUP);
        SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        }

   if (cmd.MouseRight)
        {
            var inputs = new INPUT[2];
       inputs[0] = CreateMouseInput(MOUSEEVENTF_RIGHTDOWN);
   inputs[1] = CreateMouseInput(MOUSEEVENTF_RIGHTUP);
            SendInput(2, inputs, Marshal.SizeOf<INPUT>());
      }
    }

private void ProcessMouseClick(InputCommand cmd)
    {
        var inputs = new INPUT[1];

        if (cmd.LeftButton)
     {
            inputs[0] = CreateMouseInput(cmd.Down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP);
  SendInput(1, inputs, Marshal.SizeOf<INPUT>());
        }

  if (cmd.RightButton)
        {
            inputs[0] = CreateMouseInput(cmd.Down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP);
     SendInput(1, inputs, Marshal.SizeOf<INPUT>());
   }
    }

    private void ProcessKeyboard(InputCommand cmd)
    {
      var inputs = new INPUT[1];
        inputs[0] = CreateKeyInput((ushort)cmd.KeyCode, !cmd.KeyDown);
        SendInput(1, inputs, Marshal.SizeOf<INPUT>());
    }

    #region Input Helpers

    private void AddKeyInput(ushort keyCode, bool down)
    {
        _inputBatch.Add(CreateKeyInput(keyCode, !down));
    }

    private void AddMouseMove(int dx, int dy)
    {
        _inputBatch.Add(new INPUT
        {
        type = INPUT_MOUSE,
  U = new InputUnion
            {
        mi = new MOUSEINPUT
      {
            dx = dx,
        dy = dy,
    dwFlags = MOUSEEVENTF_MOVE,
  time = 0,
          dwExtraInfo = IntPtr.Zero
        }
            }
        });
    }

    private void AddMouseButton(bool left, bool right, bool down)
    {
        uint flags = 0;
        if (left) flags |= down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
        if (right) flags |= down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;

    _inputBatch.Add(CreateMouseInput(flags));
    }

    private static INPUT CreateKeyInput(ushort keyCode, bool up)
    {
  uint flags = up ? KEYEVENTF_KEYUP : KEYEVENTF_KEYDOWN;

        // Extended keys (arrows, etc.)
        if (keyCode is >= 0x21 and <= 0x2E or 0x2D or 0x2E)
        {
            flags |= KEYEVENTF_EXTENDEDKEY;
      }

      return new INPUT
        {
   type = INPUT_KEYBOARD,
        U = new InputUnion
     {
   ki = new KEYBDINPUT
      {
  wVk = keyCode,
wScan = 0,
             dwFlags = flags,
            time = 0,
  dwExtraInfo = IntPtr.Zero
      }
 }
        };
    }

    private static INPUT CreateMouseInput(uint flags)
    {
        return new INPUT
      {
    type = INPUT_MOUSE,
        U = new InputUnion
            {
     mi = new MOUSEINPUT
        {
   dx = 0,
      dy = 0,
        dwFlags = flags,
              time = 0,
         dwExtraInfo = IntPtr.Zero
          }
            }
        };
}

    #endregion

    public void ReleaseAllKeys()
    {
        // Reset virtual controller if active
        if (VirtualControllerService.Instance.IsConnected)
    {
        VirtualControllerService.Instance.ResetState();
        }

        // Also release keyboard/mouse inputs
        var inputs = new List<INPUT>();

        // Release all mapped keys
        foreach (var key in _buttonToKey.Values)
    {
     inputs.Add(CreateKeyInput(key, true));
}

        // Release WASD
        inputs.Add(CreateKeyInput(KEY_W, true));
        inputs.Add(CreateKeyInput(KEY_A, true));
    inputs.Add(CreateKeyInput(KEY_S, true));
        inputs.Add(CreateKeyInput(KEY_D, true));

        // Release mouse buttons
        inputs.Add(CreateMouseInput(MOUSEEVENTF_LEFTUP));
        inputs.Add(CreateMouseInput(MOUSEEVENTF_RIGHTUP));

        if (inputs.Count > 0)
        {
    var inputArray = inputs.ToArray();
  SendInput((uint)inputArray.Length, inputArray, Marshal.SizeOf<INPUT>());
        }

        _lastButtons = GamepadButtons.None;
  _leftStickUp = _leftStickDown = _leftStickLeft = _leftStickRight = false;
        _leftTriggerDown = _rightTriggerDown = false;
 _mouseAccumX = _mouseAccumY = 0;
    }

    /// <summary>
    /// Get controller status for diagnostics
    /// </summary>
    public ControllerStatus GetControllerStatus()
    {
     return VirtualControllerService.Instance.GetStatus();
    }

    /// <summary>
    /// Shutdown and cleanup
    /// </summary>
    public void Shutdown()
 {
        ReleaseAllKeys();
        VirtualControllerService.Instance.Dispose();
    }
}
