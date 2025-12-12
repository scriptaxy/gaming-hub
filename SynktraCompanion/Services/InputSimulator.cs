using System.Runtime.InteropServices;

namespace SynktraCompanion.Services;

/// <summary>
/// Simulates keyboard, mouse, and gamepad inputs using Windows API
/// </summary>
public class InputSimulator
{
    private static InputSimulator? _instance;
public static InputSimulator Instance => _instance ??= new InputSimulator();

  // Virtual gamepad state using ViGEm would be ideal, but for simplicity we'll use keyboard mapping
    private readonly Dictionary<GamepadButtons, ushort> _buttonToKey = new()
    {
     { GamepadButtons.A, 0x20 },        // Space
        { GamepadButtons.B, 0x1B },        // Escape
  { GamepadButtons.X, 0x45 },        // E
      { GamepadButtons.Y, 0x52 },        // R
      { GamepadButtons.LeftBumper, 0x51 },  // Q
     { GamepadButtons.RightBumper, 0x46 }, // F
        { GamepadButtons.Start, 0x0D },    // Enter
        { GamepadButtons.Back, 0x09 },     // Tab
        { GamepadButtons.DPadUp, 0x26 },   // Up Arrow
    { GamepadButtons.DPadDown, 0x28 }, // Down Arrow
        { GamepadButtons.DPadLeft, 0x25 }, // Left Arrow
        { GamepadButtons.DPadRight, 0x27 } // Right Arrow
    };

    // Movement keys for analog sticks
    private const ushort KEY_W = 0x57;
    private const ushort KEY_A = 0x41;
 private const ushort KEY_S = 0x53;
    private const ushort KEY_D = 0x44;

    private GamepadButtons _lastButtons;
    private bool _leftStickUp, _leftStickDown, _leftStickLeft, _leftStickRight;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    public void ProcessCommand(InputCommand cmd)
    {
        switch (cmd.Type.ToLower())
        {
            case "gamepad":
                ProcessGamepad(cmd);
    break;
    case "mouse":
     ProcessMouse(cmd);
          break;
   case "keyboard":
 ProcessKeyboard(cmd);
       break;
        }
    }

    private void ProcessGamepad(InputCommand cmd)
    {
      // Process buttons
        ProcessButtons(cmd.Buttons);

        // Process left stick (WASD)
        ProcessLeftStick(cmd.LeftStickX, cmd.LeftStickY);

        // Process right stick (mouse movement)
        ProcessRightStick(cmd.RightStickX, cmd.RightStickY);

   // Process triggers
        ProcessTriggers(cmd.LeftTrigger, cmd.RightTrigger);
    }

    private void ProcessButtons(GamepadButtons buttons)
    {
        foreach (var mapping in _buttonToKey)
  {
            bool wasPressed = (_lastButtons & mapping.Key) != 0;
      bool isPressed = (buttons & mapping.Key) != 0;

 if (isPressed && !wasPressed)
  {
                // Button pressed
      keybd_event((byte)mapping.Value, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        }
         else if (!isPressed && wasPressed)
     {
        // Button released
         keybd_event((byte)mapping.Value, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
        }

    _lastButtons = buttons;
    }

    private void ProcessLeftStick(float x, float y)
    {
        const float deadzone = 0.2f;

      // Up/Down (W/S)
        bool up = y < -deadzone;
        bool down = y > deadzone;

        if (up != _leftStickUp)
        {
          keybd_event(KEY_W, 0, up ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP, UIntPtr.Zero);
            _leftStickUp = up;
        }

        if (down != _leftStickDown)
        {
 keybd_event(KEY_S, 0, down ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP, UIntPtr.Zero);
      _leftStickDown = down;
        }

        // Left/Right (A/D)
        bool left = x < -deadzone;
        bool right = x > deadzone;

        if (left != _leftStickLeft)
 {
       keybd_event(KEY_A, 0, left ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP, UIntPtr.Zero);
    _leftStickLeft = left;
        }

        if (right != _leftStickRight)
        {
 keybd_event(KEY_D, 0, right ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP, UIntPtr.Zero);
            _leftStickRight = right;
        }
    }

    private void ProcessRightStick(float x, float y)
    {
        const float deadzone = 0.15f;
      const int sensitivity = 15;

if (Math.Abs(x) > deadzone || Math.Abs(y) > deadzone)
        {
     int dx = (int)(x * sensitivity);
            int dy = (int)(y * sensitivity);
      mouse_event(MOUSEEVENTF_MOVE, dx, dy, 0, UIntPtr.Zero);
        }
    }

    private bool _leftTriggerDown, _rightTriggerDown;

    private void ProcessTriggers(float left, float right)
    {
        const float threshold = 0.5f;

      // Left trigger = right mouse button (aim)
        bool leftPressed = left > threshold;
        if (leftPressed != _leftTriggerDown)
        {
    mouse_event(leftPressed ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
            _leftTriggerDown = leftPressed;
        }

        // Right trigger = left mouse button (shoot)
     bool rightPressed = right > threshold;
        if (rightPressed != _rightTriggerDown)
        {
     mouse_event(rightPressed ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            _rightTriggerDown = rightPressed;
      }
    }

    private void ProcessMouse(InputCommand cmd)
    {
        // Move mouse to position
 SetCursorPos(cmd.MouseX, cmd.MouseY);

        // Handle clicks
        if (cmd.MouseLeft)
        {
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        }
     if (cmd.MouseRight)
        {
            mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
        }
    }

    private void ProcessKeyboard(InputCommand cmd)
    {
  keybd_event((byte)cmd.KeyCode, 0, cmd.KeyDown ? KEYEVENTF_KEYDOWN : KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public void ReleaseAllKeys()
    {
     // Release all mapped keys
    foreach (var key in _buttonToKey.Values)
 {
 keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        // Release WASD
   keybd_event(KEY_W, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
 keybd_event(KEY_A, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(KEY_S, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(KEY_D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        // Release mouse buttons
     mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);

     _lastButtons = GamepadButtons.None;
        _leftStickUp = _leftStickDown = _leftStickLeft = _leftStickRight = false;
        _leftTriggerDown = _rightTriggerDown = false;
    }
}
