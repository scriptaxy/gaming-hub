using UIKit;
using CoreGraphics;
using Foundation;
using gaming_hub.Services;

namespace gaming_hub.ViewControllers
{
    /// <summary>
    /// Input mode for streaming
    /// </summary>
    public enum StreamInputMode
    {
   Touch,      // Mouse/touch input
        Controller  // Virtual gamepad
    }

    /// <summary>
    /// Controller style
    /// </summary>
    public enum ControllerStyle
    {
        Xbox,
        PlayStation
    }

    /// <summary>
    /// Controller size
    /// </summary>
    public enum ControllerSize
    {
Minimal,
   Default
  }

    /// <summary>
    /// Full-screen game streaming with virtual gamepad overlay
    /// </summary>
    public class GameStreamViewController : UIViewController
    {
        private UIImageView _streamView = null!;
private VirtualGamepadView? _gamepadView;
        private TouchInputView? _touchView;
    private UIActivityIndicatorView _loadingIndicator = null!;
 private UILabel _statusLabel = null!;
   private UIButton _closeButton = null!;
        private UIButton _modeButton = null!;
        private UIButton _settingsButton = null!;
 private UIView _controlsOverlay = null!;
   private UIView _cursorView = null!;

        private string _host = "";
      private int _port = 19501;
        private string? _authToken;
        private StreamInputMode _inputMode = StreamInputMode.Controller;
 private ControllerStyle _controllerStyle = ControllerStyle.Xbox;
private ControllerSize _controllerSize = ControllerSize.Default;
        private bool _controlsVisible = true;
        private DateTime _lastInteraction = DateTime.Now;
        private NSTimer? _hideTimer;

        public GameStreamViewController(string host, int port, string? authToken = null)
        {
 _host = host;
      _port = port;
        _authToken = authToken;
            ModalPresentationStyle = UIModalPresentationStyle.FullScreen;
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
    LoadSettings();
            SetupUI();
       SetupEvents();
            StartHideTimer();
        }

    public override void ViewDidAppear(bool animated)
      {
    base.ViewDidAppear(animated);
            _ = StartStreamingAsync();
        }

      public override void ViewWillDisappear(bool animated)
  {
    base.ViewWillDisappear(animated);
            _hideTimer?.Invalidate();
     _ = StopStreamingAsync();
        }

        public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations()
            => UIInterfaceOrientationMask.LandscapeLeft | UIInterfaceOrientationMask.LandscapeRight;

        public override bool PrefersStatusBarHidden() => true;
        public override bool PrefersHomeIndicatorAutoHidden => true;

      private void LoadSettings()
        {
          var defaults = NSUserDefaults.StandardUserDefaults;
      _inputMode = (StreamInputMode)(int)defaults.IntForKey("stream_input_mode");
      _controllerStyle = (ControllerStyle)(int)defaults.IntForKey("stream_controller_style");
            _controllerSize = (ControllerSize)(int)defaults.IntForKey("stream_controller_size");
        }

        private void SaveSettings()
        {
     var defaults = NSUserDefaults.StandardUserDefaults;
     defaults.SetInt((int)_inputMode, "stream_input_mode");
            defaults.SetInt((int)_controllerStyle, "stream_controller_style");
            defaults.SetInt((int)_controllerSize, "stream_controller_size");
   defaults.Synchronize();
    }

        private void SetupUI()
        {
            View!.BackgroundColor = UIColor.Black;

            // Stream display
      _streamView = new UIImageView
          {
              ContentMode = UIViewContentMode.ScaleAspectFit,
        BackgroundColor = UIColor.Black,
      TranslatesAutoresizingMaskIntoConstraints = false
        };
       View.AddSubview(_streamView);

        // Virtual cursor for touch mode - use a simple dot
            _cursorView = new UIView
            {
     BackgroundColor = UIColor.White,
    Hidden = true,
    Frame = new CGRect(0, 0, 12, 12)
       };
 _cursorView.Layer.CornerRadius = 6;
            _cursorView.Layer.BorderColor = UIColor.Black.CGColor;
            _cursorView.Layer.BorderWidth = 1;
            View.AddSubview(_cursorView);

     // Controls overlay (top bar)
   _controlsOverlay = new UIView
{
                TranslatesAutoresizingMaskIntoConstraints = false,
      BackgroundColor = UIColor.FromRGBA(0, 0, 0, 120)
   };
            View.AddSubview(_controlsOverlay);

            // Loading indicator
  _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large)
            {
      TranslatesAutoresizingMaskIntoConstraints = false,
     Color = UIColor.White,
   HidesWhenStopped = true
            };
            View.AddSubview(_loadingIndicator);

 // Status label
   _statusLabel = new UILabel
            {
    TranslatesAutoresizingMaskIntoConstraints = false,
                TextColor = UIColor.White,
         Font = UIFont.SystemFontOfSize(14),
          TextAlignment = UITextAlignment.Center,
       Text = "Connecting..."
  };
      View.AddSubview(_statusLabel);

   // Close button
      _closeButton = CreateOverlayButton("xmark.circle.fill", UIColor.SystemRed);
      _closeButton.TouchUpInside += (s, e) => DismissViewController(true, null);
   _controlsOverlay.AddSubview(_closeButton);

    // Mode toggle button (Touch/Controller)
            _modeButton = CreateOverlayButton(_inputMode == StreamInputMode.Touch ? "hand.tap.fill" : "gamecontroller.fill", UIColor.White);
      _modeButton.TouchUpInside += (s, e) => ToggleInputMode();
     _controlsOverlay.AddSubview(_modeButton);

       // Settings button
  _settingsButton = CreateOverlayButton("gearshape.fill", UIColor.White);
            _settingsButton.TouchUpInside += (s, e) => ShowSettingsMenu();
   _controlsOverlay.AddSubview(_settingsButton);

    SetupConstraints();
       SetupInputView();
      _loadingIndicator.StartAnimating();
        }

        private UIButton CreateOverlayButton(string iconName, UIColor tint)
     {
            var button = new UIButton(UIButtonType.System)
        {
           TranslatesAutoresizingMaskIntoConstraints = false,
TintColor = tint
};
            var config = UIImageSymbolConfiguration.Create(UIFont.SystemFontOfSize(22));
      button.SetImage(UIImage.GetSystemImage(iconName, config), UIControlState.Normal);
    return button;
        }

        private void SetupConstraints()
        {
          NSLayoutConstraint.ActivateConstraints(new[]
      {
                _streamView.TopAnchor.ConstraintEqualTo(View!.TopAnchor),
         _streamView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
      _streamView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
    _streamView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),

        _controlsOverlay.TopAnchor.ConstraintEqualTo(View.TopAnchor),
     _controlsOverlay.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
  _controlsOverlay.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
           _controlsOverlay.HeightAnchor.ConstraintEqualTo(60),

        _closeButton.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
      _closeButton.TrailingAnchor.ConstraintEqualTo(_controlsOverlay.SafeAreaLayoutGuide.TrailingAnchor, -16),
          _closeButton.WidthAnchor.ConstraintEqualTo(44),
                _closeButton.HeightAnchor.ConstraintEqualTo(44),

  _settingsButton.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
        _settingsButton.TrailingAnchor.ConstraintEqualTo(_closeButton.LeadingAnchor, -8),
           _settingsButton.WidthAnchor.ConstraintEqualTo(44),
      _settingsButton.HeightAnchor.ConstraintEqualTo(44),

        _modeButton.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
         _modeButton.TrailingAnchor.ConstraintEqualTo(_settingsButton.LeadingAnchor, -8),
           _modeButton.WidthAnchor.ConstraintEqualTo(44),
       _modeButton.HeightAnchor.ConstraintEqualTo(44),

      _loadingIndicator.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
      _loadingIndicator.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor),

    _statusLabel.TopAnchor.ConstraintEqualTo(_loadingIndicator.BottomAnchor, 16),
      _statusLabel.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor)
        });
        }

        private void SetupInputView()
        {
     _gamepadView?.RemoveFromSuperview();
            _touchView?.RemoveFromSuperview();
        _gamepadView = null;
            _touchView = null;

     if (_inputMode == StreamInputMode.Controller)
            {
                _gamepadView = new VirtualGamepadView(_controllerStyle, _controllerSize)
  {
 TranslatesAutoresizingMaskIntoConstraints = false,
      BackgroundColor = UIColor.Clear
    };
     _gamepadView.OnStateChanged += OnGamepadStateChanged;
    View!.InsertSubviewBelow(_gamepadView, _controlsOverlay);

         NSLayoutConstraint.ActivateConstraints(new[]
     {
          _gamepadView.TopAnchor.ConstraintEqualTo(View.TopAnchor),
         _gamepadView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
       _gamepadView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
     _gamepadView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor)
      });

                _cursorView.Hidden = true;
            }
            else
        {
            _touchView = new TouchInputView
        {
     TranslatesAutoresizingMaskIntoConstraints = false,
       BackgroundColor = UIColor.Clear
     };
      _touchView.OnTouchInput += OnTouchInput;
      _touchView.OnCursorMoved += OnCursorMoved;
      View!.InsertSubviewBelow(_touchView, _controlsOverlay);

     NSLayoutConstraint.ActivateConstraints(new[]
    {
            _touchView.TopAnchor.ConstraintEqualTo(View.TopAnchor),
 _touchView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
         _touchView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
             _touchView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor)
         });

  _cursorView.Hidden = false;
             _cursorView.Center = new CGPoint(View.Bounds.Width / 2, View.Bounds.Height / 2);
         }
        }

  private void SetupEvents()
        {
            StreamingClient.Instance.OnFrameReceived += OnFrameReceived;
  StreamingClient.Instance.OnConnected += OnStreamConnected;
            StreamingClient.Instance.OnDisconnected += OnStreamDisconnected;
            StreamingClient.Instance.OnError += OnStreamError;
        }

      private void StartHideTimer()
 {
            _hideTimer = NSTimer.CreateRepeatingScheduledTimer(3.0, t =>
          {
     if ((DateTime.Now - _lastInteraction).TotalSeconds > 5 && _controlsVisible)
     {
              HideControls();
     }
            });
  }

        private void ShowControls()
      {
            _lastInteraction = DateTime.Now;
            if (!_controlsVisible)
            {
       _controlsVisible = true;
       UIView.Animate(0.3, () => _controlsOverlay.Alpha = 1);
            }
    }

    private void HideControls()
        {
            _controlsVisible = false;
            UIView.Animate(0.3, () => _controlsOverlay.Alpha = 0);
  }

        private void ToggleInputMode()
        {
 _inputMode = _inputMode == StreamInputMode.Touch ? StreamInputMode.Controller : StreamInputMode.Touch;

            var config = UIImageSymbolConfiguration.Create(UIFont.SystemFontOfSize(22));
        _modeButton.SetImage(UIImage.GetSystemImage(
      _inputMode == StreamInputMode.Touch ? "hand.tap.fill" : "gamecontroller.fill", config),
       UIControlState.Normal);

     SetupInputView();
            SaveSettings();
     ShowToast(_inputMode == StreamInputMode.Touch ? "Touch Mode" : "Controller Mode");
        }

        private void ShowSettingsMenu()
      {
     ShowControls();

    var alert = UIAlertController.Create("Stream Settings", null, UIAlertControllerStyle.ActionSheet);

        alert.AddAction(UIAlertAction.Create(
    $"Controller: {(_controllerStyle == ControllerStyle.Xbox ? "Xbox ?" : "Xbox")}",
     UIAlertActionStyle.Default, _ =>
       {
          _controllerStyle = ControllerStyle.Xbox;
       if (_inputMode == StreamInputMode.Controller) SetupInputView();
          SaveSettings();
    }));

            alert.AddAction(UIAlertAction.Create(
  $"Controller: {(_controllerStyle == ControllerStyle.PlayStation ? "PlayStation ?" : "PlayStation")}",
   UIAlertActionStyle.Default, _ =>
    {
   _controllerStyle = ControllerStyle.PlayStation;
     if (_inputMode == StreamInputMode.Controller) SetupInputView();
         SaveSettings();
     }));

     alert.AddAction(UIAlertAction.Create("", UIAlertActionStyle.Default, null));

            alert.AddAction(UIAlertAction.Create(
   $"Size: {(_controllerSize == ControllerSize.Minimal ? "Minimal ?" : "Minimal")}",
      UIAlertActionStyle.Default, _ =>
    {
           _controllerSize = ControllerSize.Minimal;
          if (_inputMode == StreamInputMode.Controller) SetupInputView();
 SaveSettings();
   }));

     alert.AddAction(UIAlertAction.Create(
        $"Size: {(_controllerSize == ControllerSize.Default ? "Default ?" : "Default")}",
     UIAlertActionStyle.Default, _ =>
{
      _controllerSize = ControllerSize.Default;
       if (_inputMode == StreamInputMode.Controller) SetupInputView();
         SaveSettings();
     }));

    alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));

    if (alert.PopoverPresentationController != null)
  {
      alert.PopoverPresentationController.SourceView = _settingsButton;
         alert.PopoverPresentationController.SourceRect = _settingsButton.Bounds;
            }

   PresentViewController(alert, true, null);
      }

        private void ShowToast(string message)
        {
            var toast = new UILabel
{
     Text = message,
                TextColor = UIColor.White,
         BackgroundColor = UIColor.FromRGBA(0, 0, 0, 180),
                TextAlignment = UITextAlignment.Center,
       Font = UIFont.SystemFontOfSize(14, UIFontWeight.Medium),
      Alpha = 0
        };
            toast.Layer.CornerRadius = 8;
    toast.ClipsToBounds = true;
       toast.SizeToFit();
    toast.Frame = new CGRect(0, 0, toast.Frame.Width + 32, 36);
            toast.Center = new CGPoint(View!.Bounds.Width / 2, View.Bounds.Height - 100);
            View.AddSubview(toast);

    UIView.Animate(0.3, () => toast.Alpha = 1, () =>
            {
        UIView.Animate(0.3, 1.5, UIViewAnimationOptions.CurveEaseIn, () => toast.Alpha = 0, toast.RemoveFromSuperview);
});
        }

    private async Task StartStreamingAsync()
        {
  try
 {
                var apiPort = 19500;
      using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
          var request = new HttpRequestMessage(HttpMethod.Post, $"http://{_host}:{apiPort}/api/stream/start");

          if (!string.IsNullOrEmpty(_authToken))
      request.Headers.Add("Authorization", $"Bearer {_authToken}");

        request.Content = new StringContent(
       "{\"quality\":50,\"fps\":60,\"width\":1280,\"height\":720}",
      System.Text.Encoding.UTF8,
 "application/json");

  var response = await client.SendAsync(request);
            Console.WriteLine($"Start stream response: {response.StatusCode}");
  }
            catch (Exception ex)
         {
     Console.WriteLine($"Failed to start stream on PC: {ex.Message}");
        }

            var connected = await StreamingClient.Instance.ConnectAsync(_host, _port);

         if (!connected)
  {
 InvokeOnMainThread(() =>
        {
    _statusLabel.Text = "Failed to connect";
 _loadingIndicator.StopAnimating();
       });
      }
        }

   private async Task StopStreamingAsync()
      {
  StreamingClient.Instance.OnFrameReceived -= OnFrameReceived;
   StreamingClient.Instance.OnConnected -= OnStreamConnected;
            StreamingClient.Instance.OnDisconnected -= OnStreamDisconnected;
        StreamingClient.Instance.OnError -= OnStreamError;

            await StreamingClient.Instance.DisconnectAsync();

   try
            {
  var apiPort = 19500;
      using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var request = new HttpRequestMessage(HttpMethod.Post, $"http://{_host}:{apiPort}/api/stream/stop");
         if (!string.IsNullOrEmpty(_authToken))
       request.Headers.Add("Authorization", $"Bearer {_authToken}");
    await client.SendAsync(request);
      }
            catch { }
        }

     private void OnFrameReceived(byte[] frameData)
     {
  InvokeOnMainThread(() =>
{
          try
    {
                using var data = NSData.FromArray(frameData);
          var image = UIImage.LoadFromData(data);
if (image != null)
    _streamView.Image = image;
  }
      catch { }
       });
        }

        private void OnStreamConnected()
        {
     InvokeOnMainThread(() =>
        {
      _loadingIndicator.StopAnimating();
_statusLabel.Hidden = true;
          });
     }

        private void OnStreamDisconnected(string reason)
        {
          InvokeOnMainThread(() =>
            {
      _statusLabel.Hidden = false;
           _statusLabel.Text = $"Disconnected: {reason}";
   });
        }

        private void OnStreamError(string error)
        {
            InvokeOnMainThread(() =>
        {
    _statusLabel.Hidden = false;
          _statusLabel.Text = $"Error: {error}";
    });
        }

        private void OnGamepadStateChanged(GamepadState state)
        {
        ShowControls();
            _ = StreamingClient.Instance.SendGamepadInputAsync(state);
        }

        private void OnTouchInput(float x, float y, TouchInputType type)
        {
            ShowControls();
       _ = StreamingClient.Instance.SendTouchInputAsync(x, y, type == TouchInputType.Tap);
 }

     private void OnCursorMoved(CGPoint position)
        {
            InvokeOnMainThread(() =>
            {
     _cursorView.Center = position;
            });
    }
    }

    /// <summary>
    /// Touch input types
    /// </summary>
    public enum TouchInputType
    {
     Move,
      Tap,
        DoubleTap,
        LongPress
    }

    /// <summary>
    /// Touch input view for mouse control - optimized for smooth movement
    /// </summary>
    public class TouchInputView : UIView
    {
        public event Action<float, float, TouchInputType>? OnTouchInput;
  public event Action<CGPoint>? OnCursorMoved;

        private CGPoint _cursorPosition;
        private CGPoint _lastTouchPosition;
    private bool _isDragging;
        private const float Sensitivity = 2.0f;

  public TouchInputView()
        {
    MultipleTouchEnabled = true;
   ExclusiveTouch = false;

         // Tap gesture - no delay
          var tap = new UITapGestureRecognizer(OnTap);
       tap.DelaysTouchesBegan = false;
            tap.DelaysTouchesEnded = false;
  AddGestureRecognizer(tap);

   // Double tap
            var doubleTap = new UITapGestureRecognizer(OnDoubleTap) { NumberOfTapsRequired = 2 };
            doubleTap.DelaysTouchesBegan = false;
    doubleTap.DelaysTouchesEnded = false;
            AddGestureRecognizer(doubleTap);
       tap.RequireGestureRecognizerToFail(doubleTap);

            // Long press - reduced delay
            var longPress = new UILongPressGestureRecognizer(OnLongPress);
   longPress.MinimumPressDuration = 0.3; // Faster long press
      longPress.DelaysTouchesBegan = false;
         AddGestureRecognizer(longPress);
      }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            if (_cursorPosition == CGPoint.Empty)
          {
        _cursorPosition = new CGPoint(Bounds.Width / 2, Bounds.Height / 2);
     }
        }

    public override void TouchesBegan(NSSet touches, UIEvent? evt)
   {
     var touch = touches.AnyObject as UITouch;
          if (touch == null) return;

            _lastTouchPosition = touch.LocationInView(this);
            _isDragging = true;
        }

        public override void TouchesMoved(NSSet touches, UIEvent? evt)
      {
   var touch = touches.AnyObject as UITouch;
     if (touch == null || !_isDragging) return;

    var currentPos = touch.LocationInView(this);
            var deltaX = (currentPos.X - _lastTouchPosition.X) * Sensitivity;
         var deltaY = (currentPos.Y - _lastTouchPosition.Y) * Sensitivity;

            _cursorPosition = new CGPoint(
      Math.Clamp(_cursorPosition.X + deltaX, 0, Bounds.Width),
                Math.Clamp(_cursorPosition.Y + deltaY, 0, Bounds.Height)
            );

    _lastTouchPosition = currentPos;
            OnCursorMoved?.Invoke(_cursorPosition);

  var normalizedX = (float)(_cursorPosition.X / Bounds.Width);
 var normalizedY = (float)(_cursorPosition.Y / Bounds.Height);
          OnTouchInput?.Invoke(normalizedX, normalizedY, TouchInputType.Move);
        }

    public override void TouchesEnded(NSSet touches, UIEvent? evt)
        {
     _isDragging = false;
        }

  private void OnTap(UITapGestureRecognizer gesture)
     {
            var normalizedX = (float)(_cursorPosition.X / Bounds.Width);
            var normalizedY = (float)(_cursorPosition.Y / Bounds.Height);
         OnTouchInput?.Invoke(normalizedX, normalizedY, TouchInputType.Tap);
  }

private void OnDoubleTap(UITapGestureRecognizer gesture)
 {
            var normalizedX = (float)(_cursorPosition.X / Bounds.Width);
            var normalizedY = (float)(_cursorPosition.Y / Bounds.Height);
    OnTouchInput?.Invoke(normalizedX, normalizedY, TouchInputType.DoubleTap);
  }

     private void OnLongPress(UILongPressGestureRecognizer gesture)
        {
    if (gesture.State == UIGestureRecognizerState.Began)
      {
           var normalizedX = (float)(_cursorPosition.X / Bounds.Width);
       var normalizedY = (float)(_cursorPosition.Y / Bounds.Height);
      OnTouchInput?.Invoke(normalizedX, normalizedY, TouchInputType.LongPress);
      }
        }
    }

    /// <summary>
 /// Modern virtual gamepad with Xbox/PlayStation styles - no haptics, instant response
 /// </summary>
    public class VirtualGamepadView : UIView
    {
        private readonly ControllerStyle _style;
        private readonly ControllerSize _size;

private ModernAnalogStick _leftStick = null!;
        private ModernAnalogStick _rightStick = null!;
   private ModernDPad _dpad = null!;
        private ModernFaceButtons _faceButtons = null!;
   private ModernShoulderButton _leftBumper = null!;
    private ModernShoulderButton _rightBumper = null!;
  private ModernTrigger _leftTrigger = null!;
      private ModernTrigger _rightTrigger = null!;
      private ModernMenuButton _menuButton = null!;
        private ModernMenuButton _optionsButton = null!;

      public event Action<GamepadState>? OnStateChanged;
        private GamepadState _state = new();

        public VirtualGamepadView(ControllerStyle style, ControllerSize size)
     {
        _style = style;
    _size = size;
     MultipleTouchEnabled = true;
       SetupControls();
     }

      private void SetupControls()
        {
       var scale = _size == ControllerSize.Minimal ? 0.7f : 1.0f;
      var opacity = _size == ControllerSize.Minimal ? 0.5f : 0.7f;

     _leftStick = new ModernAnalogStick(scale, opacity);
     _leftStick.OnValueChanged += (x, y) =>
          {
     _state.LeftStickX = x;
      _state.LeftStickY = y;
  NotifyStateChanged();
            };
       _leftStick.OnPressed += pressed => SetButton(GamepadButtons.LeftStick, pressed);
       AddSubview(_leftStick);

            _rightStick = new ModernAnalogStick(scale, opacity);
            _rightStick.OnValueChanged += (x, y) =>
     {
    _state.RightStickX = x;
  _state.RightStickY = y;
                NotifyStateChanged();
    };
       _rightStick.OnPressed += pressed => SetButton(GamepadButtons.RightStick, pressed);
  AddSubview(_rightStick);

            _dpad = new ModernDPad(scale, opacity);
            _dpad.OnDirectionChanged += dir =>
       {
   _state.Buttons &= ~(GamepadButtons.DPadUp | GamepadButtons.DPadDown | GamepadButtons.DPadLeft | GamepadButtons.DPadRight);
      _state.Buttons |= dir;
         NotifyStateChanged();
         };
            AddSubview(_dpad);

            _faceButtons = new ModernFaceButtons(_style, scale, opacity);
  _faceButtons.OnButtonChanged += (button, pressed) => SetButton(button, pressed);
      AddSubview(_faceButtons);

            _leftBumper = new ModernShoulderButton(_style == ControllerStyle.Xbox ? "LB" : "L1", scale, opacity);
            _leftBumper.OnPressed += pressed => SetButton(GamepadButtons.LeftBumper, pressed);
       AddSubview(_leftBumper);

            _rightBumper = new ModernShoulderButton(_style == ControllerStyle.Xbox ? "RB" : "R1", scale, opacity);
       _rightBumper.OnPressed += pressed => SetButton(GamepadButtons.RightBumper, pressed);
         AddSubview(_rightBumper);

          _leftTrigger = new ModernTrigger(_style == ControllerStyle.Xbox ? "LT" : "L2", scale, opacity);
            _leftTrigger.OnValueChanged += v => { _state.LeftTrigger = v; NotifyStateChanged(); };
      AddSubview(_leftTrigger);

  _rightTrigger = new ModernTrigger(_style == ControllerStyle.Xbox ? "RT" : "R2", scale, opacity);
            _rightTrigger.OnValueChanged += v => { _state.RightTrigger = v; NotifyStateChanged(); };
            AddSubview(_rightTrigger);

    var menuSymbol = _style == ControllerStyle.Xbox ? "line.3.horizontal" : "square.and.arrow.up";
      var optionsSymbol = _style == ControllerStyle.Xbox ? "ellipsis" : "line.3.horizontal";

        _menuButton = new ModernMenuButton(menuSymbol, scale, opacity);
   _menuButton.OnPressed += pressed => SetButton(GamepadButtons.Back, pressed);
            AddSubview(_menuButton);

            _optionsButton = new ModernMenuButton(optionsSymbol, scale, opacity);
            _optionsButton.OnPressed += pressed => SetButton(GamepadButtons.Start, pressed);
        AddSubview(_optionsButton);
        }

        public override void LayoutSubviews()
        {
   base.LayoutSubviews();

   var bounds = Bounds;
            var safeLeft = SafeAreaInsets.Left + 16;
            var safeRight = bounds.Width - SafeAreaInsets.Right - 16;
            var safeBottom = bounds.Height - SafeAreaInsets.Bottom - 16;

            var scale = _size == ControllerSize.Minimal ? 0.7f : 1.0f;
  var stickSize = 100 * scale;
      var dpadSize = 90 * scale;
            var faceButtonsSize = 120 * scale;
    var shoulderWidth = 60 * scale;
         var shoulderHeight = 32 * scale;
          var triggerWidth = 50 * scale;
    var triggerHeight = 40 * scale;
    var menuSize = 36 * scale;
         var bottomOffset = _size == ControllerSize.Minimal ? 20 : 30;

            _leftStick.Frame = new CGRect(safeLeft, safeBottom - stickSize - bottomOffset, stickSize, stickSize);
 _dpad.Frame = new CGRect(safeLeft + stickSize + 20, safeBottom - dpadSize - bottomOffset - 20, dpadSize, dpadSize);

            _faceButtons.Frame = new CGRect(safeRight - faceButtonsSize - 10, safeBottom - faceButtonsSize - bottomOffset, faceButtonsSize, faceButtonsSize);
            _rightStick.Frame = new CGRect(safeRight - faceButtonsSize - stickSize - 30, safeBottom - stickSize - bottomOffset, stickSize, stickSize);

     var shoulderY = safeBottom - stickSize - bottomOffset - shoulderHeight - 20;
   _leftBumper.Frame = new CGRect(safeLeft, shoulderY, shoulderWidth, shoulderHeight);
            _rightBumper.Frame = new CGRect(safeRight - shoulderWidth, shoulderY, shoulderWidth, shoulderHeight);

            var triggerY = shoulderY - triggerHeight - 8;
          _leftTrigger.Frame = new CGRect(safeLeft, triggerY, triggerWidth, triggerHeight);
         _rightTrigger.Frame = new CGRect(safeRight - triggerWidth, triggerY, triggerWidth, triggerHeight);

            var centerX = bounds.Width / 2;
            var menuY = safeBottom - menuSize - 10;
            _menuButton.Frame = new CGRect(centerX - menuSize - 20, menuY, menuSize, menuSize);
            _optionsButton.Frame = new CGRect(centerX + 20, menuY, menuSize, menuSize);
        }

        private void SetButton(GamepadButtons button, bool pressed)
      {
if (pressed)
        _state.Buttons |= button;
     else
   _state.Buttons &= ~button;
            NotifyStateChanged();
     }

        private void NotifyStateChanged()
   {
            OnStateChanged?.Invoke(_state);
    }
    }

    /// <summary>
 /// Analog stick - instant response, no haptics
 /// </summary>
    public class ModernAnalogStick : UIView
    {
        private UIView _knob;
        private UIView _ring;
 private CGPoint _center;
 private nfloat _maxRadius;
      private readonly float _scale;
        private readonly float _opacity;

        public event Action<float, float>? OnValueChanged;
      public event Action<bool>? OnPressed;

        public ModernAnalogStick(float scale, float opacity)
        {
_scale = scale;
  _opacity = opacity;
    MultipleTouchEnabled = false;
     ExclusiveTouch = true;
       SetupUI();
     }

        private void SetupUI()
        {
   var size = 100 * _scale;
            Frame = new CGRect(0, 0, size, size);

  _ring = new UIView(Bounds) { BackgroundColor = UIColor.Clear };
    _ring.Layer.BorderColor = UIColor.FromWhiteAlpha(1, _opacity * 0.5f).CGColor;
     _ring.Layer.BorderWidth = 2;
        _ring.Layer.CornerRadius = size / 2;
  AddSubview(_ring);

            var baseSize = size * 0.6f;
  var baseView = new UIView(new CGRect((size - baseSize) / 2, (size - baseSize) / 2, baseSize, baseSize))
  {
        BackgroundColor = UIColor.FromWhiteAlpha(0.2f, _opacity)
          };
            baseView.Layer.CornerRadius = baseSize / 2;
            AddSubview(baseView);

            var knobSize = size * 0.45f;
            _knob = new UIView(new CGRect(0, 0, knobSize, knobSize))
       {
       BackgroundColor = UIColor.FromWhiteAlpha(0.9f, _opacity)
  };
        _knob.Layer.CornerRadius = knobSize / 2;
        _knob.Layer.ShadowColor = UIColor.Black.CGColor;
    _knob.Layer.ShadowOffset = new CGSize(0, 2);
   _knob.Layer.ShadowRadius = 4;
      _knob.Layer.ShadowOpacity = 0.3f;
            AddSubview(_knob);

       _maxRadius = (nfloat)(size * 0.25f);
            _center = new CGPoint(size / 2, size / 2);
         _knob.Center = _center;
     }

  public override void TouchesBegan(NSSet touches, UIEvent? evt)
        {
  UpdateKnob(touches);
 }

        public override void TouchesMoved(NSSet touches, UIEvent? evt)
    {
            UpdateKnob(touches);
        }

   public override void TouchesEnded(NSSet touches, UIEvent? evt)
        {
    _knob.Center = _center; // Instant snap back
 OnValueChanged?.Invoke(0, 0);
        }

 public override void TouchesCancelled(NSSet touches, UIEvent? evt) => TouchesEnded(touches, evt);

        private void UpdateKnob(NSSet touches)
   {
     var touch = touches.AnyObject as UITouch;
         if (touch == null) return;

       var location = touch.LocationInView(this);
  var dx = location.X - _center.X;
            var dy = location.Y - _center.Y;
  var distance = Math.Sqrt(dx * dx + dy * dy);

         if (distance > _maxRadius)
     {
       dx = (nfloat)(dx / distance * _maxRadius);
     dy = (nfloat)(dy / distance * _maxRadius);
         }

      _knob.Center = new CGPoint(_center.X + dx, _center.Y + dy);

            var normalizedX = (float)(dx / _maxRadius);
     var normalizedY = (float)(dy / _maxRadius);
            OnValueChanged?.Invoke(normalizedX, normalizedY);
      }
    }

    /// <summary>
    /// D-Pad - instant response
    /// </summary>
    public class ModernDPad : UIView
    {
    private readonly float _scale;
private readonly float _opacity;

        public event Action<GamepadButtons>? OnDirectionChanged;

        public ModernDPad(float scale, float opacity)
        {
      _scale = scale;
    _opacity = opacity;
            BackgroundColor = UIColor.Clear;
 MultipleTouchEnabled = false;
       ExclusiveTouch = true;
        }

   public override void TouchesBegan(NSSet touches, UIEvent? evt)
        {
UpdateDirection(touches);
        }

        public override void TouchesMoved(NSSet touches, UIEvent? evt) => UpdateDirection(touches);

        public override void TouchesEnded(NSSet touches, UIEvent? evt)
        {
     OnDirectionChanged?.Invoke(GamepadButtons.None);
   SetNeedsDisplay();
   }

        public override void TouchesCancelled(NSSet touches, UIEvent? evt) => TouchesEnded(touches, evt);

        private void UpdateDirection(NSSet touches)
        {
    var touch = touches.AnyObject as UITouch;
            if (touch == null) return;

            var location = touch.LocationInView(this);
            var centerX = Bounds.Width / 2;
            var centerY = Bounds.Height / 2;
            var threshold = Bounds.Width * 0.2f;

       var direction = GamepadButtons.None;

  if (location.Y < centerY - threshold) direction |= GamepadButtons.DPadUp;
    if (location.Y > centerY + threshold) direction |= GamepadButtons.DPadDown;
            if (location.X < centerX - threshold) direction |= GamepadButtons.DPadLeft;
   if (location.X > centerX + threshold) direction |= GamepadButtons.DPadRight;

  OnDirectionChanged?.Invoke(direction);
  SetNeedsDisplay();
  }

        public override void Draw(CGRect rect)
   {
            base.Draw(rect);

     using var context = UIGraphics.GetCurrentContext();
    if (context == null) return;

       var size = rect.Width;
        var armWidth = size * 0.35f;
  var centerX = size / 2;
         var centerY = size / 2;

            context.SetFillColor(UIColor.FromWhiteAlpha(0.3f, _opacity).CGColor);

       var path = new CGPath();
   path.AddRoundedRect(new CGRect(centerX - armWidth / 2, 0, armWidth, size), 6, 6);
      context.AddPath(path);
            context.FillPath();

        path = new CGPath();
    path.AddRoundedRect(new CGRect(0, centerY - armWidth / 2, size, armWidth), 6, 6);
          context.AddPath(path);
      context.FillPath();

     context.SetFillColor(UIColor.FromWhiteAlpha(0.8f, _opacity).CGColor);
        DrawArrow(context, centerX, armWidth * 0.5f, 0);
   DrawArrow(context, centerX, size - armWidth * 0.5f, 180);
            DrawArrow(context, armWidth * 0.5f, centerY, 270);
   DrawArrow(context, size - armWidth * 0.5f, centerY, 90);
        }

        private void DrawArrow(CGContext context, nfloat x, nfloat y, float rotation)
{
            context.SaveState();
          context.TranslateCTM(x, y);
   context.RotateCTM((nfloat)(rotation * Math.PI / 180));

     var arrowSize = 8 * _scale;
   var path = new CGPath();
            path.MoveToPoint(-arrowSize, arrowSize * 0.6f);
  path.AddLineToPoint(0, -arrowSize * 0.6f);
         path.AddLineToPoint(arrowSize, arrowSize * 0.6f);
        path.CloseSubpath();

 context.AddPath(path);
            context.FillPath();
            context.RestoreState();
 }
    }

    /// <summary>
    /// Face buttons container
 /// </summary>
    public class ModernFaceButtons : UIView
    {
  private readonly ControllerStyle _style;
        private readonly float _scale;
        private readonly float _opacity;

      private ModernFaceButton _bottomButton = null!;
     private ModernFaceButton _rightButton = null!;
        private ModernFaceButton _leftButton = null!;
        private ModernFaceButton _topButton = null!;

   public event Action<GamepadButtons, bool>? OnButtonChanged;

        public ModernFaceButtons(ControllerStyle style, float scale, float opacity)
        {
     _style = style;
      _scale = scale;
         _opacity = opacity;
  MultipleTouchEnabled = true;
  SetupButtons();
   }

        private void SetupButtons()
     {
         var buttonSize = 44 * _scale;

          if (_style == ControllerStyle.Xbox)
{
              _bottomButton = new ModernFaceButton("A", null, UIColor.FromRGB(80, 180, 80), buttonSize, _opacity);
    _rightButton = new ModernFaceButton("B", null, UIColor.FromRGB(220, 80, 80), buttonSize, _opacity);
  _leftButton = new ModernFaceButton("X", null, UIColor.FromRGB(80, 140, 220), buttonSize, _opacity);
        _topButton = new ModernFaceButton("Y", null, UIColor.FromRGB(220, 180, 60), buttonSize, _opacity);
    }
        else
            {
   _bottomButton = new ModernFaceButton(null, "xmark", UIColor.FromRGB(100, 150, 200), buttonSize, _opacity);
           _rightButton = new ModernFaceButton(null, "circle", UIColor.FromRGB(220, 100, 100), buttonSize, _opacity);
 _leftButton = new ModernFaceButton(null, "square", UIColor.FromRGB(200, 120, 180), buttonSize, _opacity);
   _topButton = new ModernFaceButton(null, "triangle", UIColor.FromRGB(100, 200, 180), buttonSize, _opacity);
}

    _bottomButton.OnPressed += p => OnButtonChanged?.Invoke(GamepadButtons.A, p);
            _rightButton.OnPressed += p => OnButtonChanged?.Invoke(GamepadButtons.B, p);
     _leftButton.OnPressed += p => OnButtonChanged?.Invoke(GamepadButtons.X, p);
        _topButton.OnPressed += p => OnButtonChanged?.Invoke(GamepadButtons.Y, p);

            AddSubview(_bottomButton);
         AddSubview(_rightButton);
     AddSubview(_leftButton);
            AddSubview(_topButton);
    }

        public override void LayoutSubviews()
        {
   base.LayoutSubviews();

          var size = Bounds.Width;
            var buttonSize = _bottomButton.Frame.Width;
            var center = size / 2;
  var offset = size * 0.28f;

            _bottomButton.Center = new CGPoint(center, center + offset);
            _rightButton.Center = new CGPoint(center + offset, center);
  _leftButton.Center = new CGPoint(center - offset, center);
            _topButton.Center = new CGPoint(center, center - offset);
        }
    }

    /// <summary>
    /// Single face button - instant response, no haptics
    /// </summary>
    public class ModernFaceButton : UIView
    {
        private UILabel? _label;
        private UIImageView? _iconView;
        private UIColor _color;
      private nfloat _originalOpacity;

        public event Action<bool>? OnPressed;

        public ModernFaceButton(string? text, string? sfSymbol, UIColor color, float size, float opacity)
      {
  _color = color;
   _originalOpacity = (nfloat)opacity;
  Frame = new CGRect(0, 0, size, size);
   BackgroundColor = color.ColorWithAlpha((nfloat)opacity);
            Layer.CornerRadius = size / 2;
       Layer.BorderColor = UIColor.FromWhiteAlpha(1, 0.3f).CGColor;
        Layer.BorderWidth = 1;
            MultipleTouchEnabled = false;
            ExclusiveTouch = true;

         if (!string.IsNullOrEmpty(sfSymbol))
  {
       var config = UIImageSymbolConfiguration.Create(UIFont.BoldSystemFontOfSize(size * 0.4f));
     _iconView = new UIImageView
      {
     Image = UIImage.GetSystemImage(sfSymbol, config),
        TintColor = UIColor.White,
   ContentMode = UIViewContentMode.Center
                };
    _iconView.SizeToFit();
          AddSubview(_iconView);
            }
     else if (!string.IsNullOrEmpty(text))
            {
     _label = new UILabel
                {
 Text = text,
      TextColor = UIColor.White,
    Font = UIFont.BoldSystemFontOfSize(size * 0.4f),
      TextAlignment = UITextAlignment.Center
     };
     _label.SizeToFit();
  AddSubview(_label);
            }
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
    var center = new CGPoint(Bounds.Width / 2, Bounds.Height / 2);
            if (_label != null) _label.Center = center;
            if (_iconView != null) _iconView.Center = center;
        }

     public override void TouchesBegan(NSSet touches, UIEvent? evt)
        {
       BackgroundColor = _color.ColorWithAlpha(_originalOpacity + 0.2f);
         Transform = CGAffineTransform.MakeScale(0.92f, 0.92f);
            OnPressed?.Invoke(true);
        }

 public override void TouchesEnded(NSSet touches, UIEvent? evt)
        {
  BackgroundColor = _color.ColorWithAlpha(_originalOpacity);
            Transform = CGAffineTransform.MakeIdentity();
            OnPressed?.Invoke(false);
    }

        public override void TouchesCancelled(NSSet touches, UIEvent? evt) => TouchesEnded(touches, evt);
    }

    /// <summary>
    /// Shoulder button - instant response
    /// </summary>
    public class ModernShoulderButton : UIView
    {
        private UILabel _label;
      private nfloat _originalOpacity;

        public event Action<bool>? OnPressed;

        public ModernShoulderButton(string text, float scale, float opacity)
        {
            _originalOpacity = (nfloat)opacity;
  var width = 60 * scale;
            var height = 32 * scale;
         Frame = new CGRect(0, 0, width, height);
            BackgroundColor = UIColor.FromWhiteAlpha(0.3f, opacity);
            Layer.CornerRadius = 6;
        Layer.BorderColor = UIColor.FromWhiteAlpha(1, 0.3f).CGColor;
Layer.BorderWidth = 1;
     MultipleTouchEnabled = false;
            ExclusiveTouch = true;

       _label = new UILabel
            {
         Text = text,
    TextColor = UIColor.White,
       Font = UIFont.BoldSystemFontOfSize(12 * scale),
  TextAlignment = UITextAlignment.Center,
          Frame = Bounds
      };
            AddSubview(_label);
   }

 public override void TouchesBegan(NSSet touches, UIEvent? evt)
        {
     BackgroundColor = UIColor.FromWhiteAlpha(0.5f, 0.9f);
    OnPressed?.Invoke(true);
   }

public override void TouchesEnded(NSSet touches, UIEvent? evt)
  {
            BackgroundColor = UIColor.FromWhiteAlpha(0.3f, _originalOpacity);
        OnPressed?.Invoke(false);
        }

        public override void TouchesCancelled(NSSet touches, UIEvent? evt) => TouchesEnded(touches, evt);
    }

    /// <summary>
    /// Trigger button - instant response
    /// </summary>
    public class ModernTrigger : UIView
    {
     private UILabel _label;
        private UIView _fillView;
        private float _value;
      private nfloat _originalOpacity;

        public event Action<float>? OnValueChanged;

        public ModernTrigger(string text, float scale, float opacity)
        {
            _originalOpacity = (nfloat)opacity;
  var width = 50 * scale;
       var height = 40 * scale;
      Frame = new CGRect(0, 0, width, height);
        BackgroundColor = UIColor.FromWhiteAlpha(0.2f, opacity);
    Layer.CornerRadius = 8;
  ClipsToBounds = true;
   MultipleTouchEnabled = false;
       ExclusiveTouch = true;

    _fillView = new UIView
     {
          BackgroundColor = UIColor.FromRGB(100, 180, 255).ColorWithAlpha((nfloat)opacity),
              Frame = new CGRect(0, height, width, 0)
  };
  AddSubview(_fillView);

   _label = new UILabel
    {
    Text = text,
         TextColor = UIColor.White,
        Font = UIFont.BoldSystemFontOfSize(11 * scale),
 TextAlignment = UITextAlignment.Center,
     Frame = Bounds
            };
            AddSubview(_label);
        }

        public override void TouchesBegan(NSSet touches, UIEvent? evt)
        {
            _value = 1.0f;
   _fillView.Frame = new CGRect(0, 0, Bounds.Width, Bounds.Height);
            OnValueChanged?.Invoke(_value);
     }

        public override void TouchesEnded(NSSet touches, UIEvent? evt)
     {
     _value = 0;
 _fillView.Frame = new CGRect(0, Bounds.Height, Bounds.Width, 0);
            OnValueChanged?.Invoke(_value);
        }

        public override void TouchesCancelled(NSSet touches, UIEvent? evt) => TouchesEnded(touches, evt);
    }

    /// <summary>
    /// Menu button - instant response
    /// </summary>
    public class ModernMenuButton : UIView
    {
        private UIImageView? _iconView;
        private nfloat _originalOpacity;

      public event Action<bool>? OnPressed;

        public ModernMenuButton(string sfSymbolName, float scale, float opacity)
        {
         _originalOpacity = (nfloat)opacity;
  var size = 36 * scale;
  Frame = new CGRect(0, 0, size, size);
   BackgroundColor = UIColor.FromWhiteAlpha(0.25f, opacity);
       Layer.CornerRadius = size / 2;
   Layer.BorderColor = UIColor.FromWhiteAlpha(1, 0.2f).CGColor;
       Layer.BorderWidth = 1;
          MultipleTouchEnabled = false;
 ExclusiveTouch = true;

        var config = UIImageSymbolConfiguration.Create(UIFont.SystemFontOfSize(12 * scale));
            _iconView = new UIImageView
            {
     Image = UIImage.GetSystemImage(sfSymbolName, config),
       TintColor = UIColor.White,
             ContentMode = UIViewContentMode.Center,
           Frame = Bounds
  };
   AddSubview(_iconView);
        }

        public override void LayoutSubviews()
        {
       base.LayoutSubviews();
            if (_iconView != null) _iconView.Center = new CGPoint(Bounds.Width / 2, Bounds.Height / 2);
 }

        public override void TouchesBegan(NSSet touches, UIEvent? evt)
        {
            Alpha = 0.6f;
          OnPressed?.Invoke(true);
        }

        public override void TouchesEnded(NSSet touches, UIEvent? evt)
     {
      Alpha = 1;
       OnPressed?.Invoke(false);
        }

    public override void TouchesCancelled(NSSet touches, UIEvent? evt) => TouchesEnded(touches, evt);
    }
}
