using UIKit;
using CoreGraphics;
using Foundation;
using gaming_hub.Services;

namespace gaming_hub.ViewControllers
{
    /// <summary>
    /// Full-screen game streaming with virtual gamepad overlay
    /// </summary>
    public class GameStreamViewController : UIViewController
    {
   private UIImageView _streamView = null!;
        private VirtualGamepadView _gamepadView = null!;
        private UIActivityIndicatorView _loadingIndicator = null!;
   private UILabel _statusLabel = null!;
    private UIButton _closeButton = null!;
        private UIButton _settingsButton = null!;

     private string _host = "";
        private int _port = 5002;
        private string? _authToken;
    private bool _isLandscape = true;
        private GamepadState _currentState = new();

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
     SetupUI();
      SetupEvents();
        }

  public override void ViewDidAppear(bool animated)
        {
        base.ViewDidAppear(animated);
    _ = StartStreamingAsync();
        }

        public override void ViewWillDisappear(bool animated)
      {
            base.ViewWillDisappear(animated);
         _ = StopStreamingAsync();
    }

        public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations()
 {
            return UIInterfaceOrientationMask.LandscapeLeft | UIInterfaceOrientationMask.LandscapeRight;
   }

        public override bool PrefersStatusBarHidden()
        {
            return true;
        }

        public override bool PrefersHomeIndicatorAutoHidden => true;

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

    // Virtual gamepad overlay
      _gamepadView = new VirtualGamepadView
            {
                TranslatesAutoresizingMaskIntoConstraints = false,
                BackgroundColor = UIColor.Clear
     };
            _gamepadView.OnStateChanged += OnGamepadStateChanged;
        View.AddSubview(_gamepadView);

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
 _closeButton = new UIButton(UIButtonType.System)
  {
          TranslatesAutoresizingMaskIntoConstraints = false,
    TintColor = UIColor.White
  };
    _closeButton.SetImage(UIImage.GetSystemImage("xmark.circle.fill"), UIControlState.Normal);
            _closeButton.TouchUpInside += (s, e) => DismissViewController(true, null);
            View.AddSubview(_closeButton);

            // Settings button (toggle gamepad visibility)
        _settingsButton = new UIButton(UIButtonType.System)
     {
     TranslatesAutoresizingMaskIntoConstraints = false,
  TintColor = UIColor.White
            };
       _settingsButton.SetImage(UIImage.GetSystemImage("gamecontroller.fill"), UIControlState.Normal);
     _settingsButton.TouchUpInside += (s, e) => ToggleGamepad();
            View.AddSubview(_settingsButton);

            // Constraints
            NSLayoutConstraint.ActivateConstraints(new[]
            {
       // Stream view fills screen
       _streamView.TopAnchor.ConstraintEqualTo(View.TopAnchor),
       _streamView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
           _streamView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
          _streamView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),

                // Gamepad overlay fills screen
                _gamepadView.TopAnchor.ConstraintEqualTo(View.TopAnchor),
 _gamepadView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
                _gamepadView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
                _gamepadView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),

   // Loading indicator centered
     _loadingIndicator.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
                _loadingIndicator.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor),

            // Status label below loading
  _statusLabel.TopAnchor.ConstraintEqualTo(_loadingIndicator.BottomAnchor, 16),
     _statusLabel.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),

     // Close button top-right with safe area
     _closeButton.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 8),
  _closeButton.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor, -8),
      _closeButton.WidthAnchor.ConstraintEqualTo(44),
      _closeButton.HeightAnchor.ConstraintEqualTo(44),

    // Settings button next to close
      _settingsButton.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 8),
      _settingsButton.TrailingAnchor.ConstraintEqualTo(_closeButton.LeadingAnchor, -8),
    _settingsButton.WidthAnchor.ConstraintEqualTo(44),
     _settingsButton.HeightAnchor.ConstraintEqualTo(44)
            });

   _loadingIndicator.StartAnimating();
      }

        private void SetupEvents()
      {
            StreamingClient.Instance.OnFrameReceived += OnFrameReceived;
   StreamingClient.Instance.OnConnected += OnStreamConnected;
   StreamingClient.Instance.OnDisconnected += OnStreamDisconnected;
   StreamingClient.Instance.OnError += OnStreamError;
        }

        private async Task StartStreamingAsync()
        {
       // First, tell the PC to start streaming
     try
            {
          var baseHost = _host;
          var apiPort = _port == 5002 ? 5000 : _port; // Assume API is on 5000 if stream is 5002

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
      var request = new HttpRequestMessage(HttpMethod.Post, $"http://{baseHost}:{apiPort}/api/stream/start");
    
        if (!string.IsNullOrEmpty(_authToken))
           request.Headers.Add("Authorization", $"Bearer {_authToken}");

     request.Content = new StringContent(
           "{\"quality\":50,\"fps\":30,\"width\":1280,\"height\":720}",
         System.Text.Encoding.UTF8,
  "application/json");

        var response = await client.SendAsync(request);
    Console.WriteLine($"Start stream response: {response.StatusCode}");
}
       catch (Exception ex)
          {
      Console.WriteLine($"Failed to start stream on PC: {ex.Message}");
            }

            // Connect to the WebSocket stream
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
            // Unsubscribe from events
            StreamingClient.Instance.OnFrameReceived -= OnFrameReceived;
       StreamingClient.Instance.OnConnected -= OnStreamConnected;
      StreamingClient.Instance.OnDisconnected -= OnStreamDisconnected;
   StreamingClient.Instance.OnError -= OnStreamError;

          await StreamingClient.Instance.DisconnectAsync();

            // Tell PC to stop streaming
            try
            {
    var baseHost = _host;
         var apiPort = _port == 5002 ? 5000 : _port;

      using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
   var request = new HttpRequestMessage(HttpMethod.Post, $"http://{baseHost}:{apiPort}/api/stream/stop");
       
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
            {
       _streamView.Image = image;
         }
        }
    catch (Exception ex)
  {
      Console.WriteLine($"Frame decode error: {ex.Message}");
            }
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
   _currentState = state;
            _ = StreamingClient.Instance.SendGamepadInputAsync(state);
        }

        private void ToggleGamepad()
      {
            _gamepadView.Hidden = !_gamepadView.Hidden;
            _settingsButton.TintColor = _gamepadView.Hidden ? UIColor.Gray : UIColor.White;
 }
    }

    /// <summary>
    /// Virtual gamepad overlay with touch controls
/// </summary>
    public class VirtualGamepadView : UIView
    {
        private AnalogStickView _leftStick = null!;
        private AnalogStickView _rightStick = null!;
        private GamepadButtonView _buttonA = null!;
        private GamepadButtonView _buttonB = null!;
        private GamepadButtonView _buttonX = null!;
     private GamepadButtonView _buttonY = null!;
   private GamepadButtonView _leftBumper = null!;
        private GamepadButtonView _rightBumper = null!;
        private TriggerView _leftTrigger = null!;
        private TriggerView _rightTrigger = null!;
      private DPadView _dpad = null!;
        private GamepadButtonView _startButton = null!;
        private GamepadButtonView _backButton = null!;

        public event Action<GamepadState>? OnStateChanged;

        private GamepadState _state = new();

      public VirtualGamepadView()
   {
            SetupControls();
        }

        private void SetupControls()
        {
            var buttonSize = 50f;
            var stickSize = 120f;
            var padding = 20f;

            // Left analog stick (bottom-left)
            _leftStick = new AnalogStickView(new CGRect(padding, 0, stickSize, stickSize));
      _leftStick.OnValueChanged += (x, y) =>
        {
         _state.LeftStickX = x;
     _state.LeftStickY = y;
     NotifyStateChanged();
       };
     AddSubview(_leftStick);

            // D-Pad (above left stick)
  _dpad = new DPadView(new CGRect(padding + 10, 0, 100, 100));
_dpad.OnDirectionChanged += dir =>
            {
 _state.Buttons &= ~(GamepadButtons.DPadUp | GamepadButtons.DPadDown | GamepadButtons.DPadLeft | GamepadButtons.DPadRight);
  _state.Buttons |= dir;
                NotifyStateChanged();
  };
       AddSubview(_dpad);

      // Right analog stick (bottom-right)
  _rightStick = new AnalogStickView(new CGRect(0, 0, stickSize, stickSize));
         _rightStick.OnValueChanged += (x, y) =>
            {
          _state.RightStickX = x;
           _state.RightStickY = y;
       NotifyStateChanged();
   };
       AddSubview(_rightStick);

            // Face buttons (right side)
    var btnColor = UIColor.FromRGBA(255, 255, 255, 100);

            _buttonA = new GamepadButtonView("A", UIColor.FromRGBA(0, 200, 0, 180));
            _buttonA.OnPressed += p => { SetButton(GamepadButtons.A, p); };
            AddSubview(_buttonA);

         _buttonB = new GamepadButtonView("B", UIColor.FromRGBA(200, 0, 0, 180));
            _buttonB.OnPressed += p => { SetButton(GamepadButtons.B, p); };
         AddSubview(_buttonB);

            _buttonX = new GamepadButtonView("X", UIColor.FromRGBA(0, 100, 200, 180));
         _buttonX.OnPressed += p => { SetButton(GamepadButtons.X, p); };
   AddSubview(_buttonX);

   _buttonY = new GamepadButtonView("Y", UIColor.FromRGBA(200, 200, 0, 180));
      _buttonY.OnPressed += p => { SetButton(GamepadButtons.Y, p); };
            AddSubview(_buttonY);

        // Bumpers
            _leftBumper = new GamepadButtonView("LB", btnColor) { Frame = new CGRect(0, 0, 70, 40) };
     _leftBumper.OnPressed += p => { SetButton(GamepadButtons.LeftBumper, p); };
        AddSubview(_leftBumper);

      _rightBumper = new GamepadButtonView("RB", btnColor) { Frame = new CGRect(0, 0, 70, 40) };
        _rightBumper.OnPressed += p => { SetButton(GamepadButtons.RightBumper, p); };
         AddSubview(_rightBumper);

   // Triggers
          _leftTrigger = new TriggerView("LT");
            _leftTrigger.OnValueChanged += v => { _state.LeftTrigger = v; NotifyStateChanged(); };
            AddSubview(_leftTrigger);

     _rightTrigger = new TriggerView("RT");
      _rightTrigger.OnValueChanged += v => { _state.RightTrigger = v; NotifyStateChanged(); };
            AddSubview(_rightTrigger);

      // Start/Back
            _startButton = new GamepadButtonView("?", btnColor) { Frame = new CGRect(0, 0, 50, 30) };
  _startButton.OnPressed += p => { SetButton(GamepadButtons.Start, p); };
  AddSubview(_startButton);

            _backButton = new GamepadButtonView("?", btnColor) { Frame = new CGRect(0, 0, 50, 30) };
            _backButton.OnPressed += p => { SetButton(GamepadButtons.Back, p); };
         AddSubview(_backButton);
        }

        public override void LayoutSubviews()
        {
    base.LayoutSubviews();

     var bounds = Bounds;
            var safeLeft = SafeAreaInsets.Left + 20;
      var safeRight = bounds.Width - SafeAreaInsets.Right - 20;
        var safeBottom = bounds.Height - SafeAreaInsets.Bottom - 20;

            // Left side controls
      _leftStick.Frame = new CGRect(safeLeft, safeBottom - 140, 120, 120);
     _dpad.Frame = new CGRect(safeLeft + 10, safeBottom - 260, 100, 100);

  // Right side controls
            _rightStick.Frame = new CGRect(safeRight - 220, safeBottom - 140, 120, 120);

  // Face buttons (diamond layout)
var btnX = safeRight - 80;
         var btnY = safeBottom - 180;
       var spacing = 55f;

     _buttonA.Frame = new CGRect(btnX - 25, btnY + spacing - 25, 50, 50);
    _buttonB.Frame = new CGRect(btnX + spacing - 25, btnY - 25, 50, 50);
            _buttonX.Frame = new CGRect(btnX - spacing - 25, btnY - 25, 50, 50);
     _buttonY.Frame = new CGRect(btnX - 25, btnY - spacing - 25, 50, 50);

       // Bumpers
        _leftBumper.Frame = new CGRect(safeLeft, safeBottom - 300, 70, 40);
    _rightBumper.Frame = new CGRect(safeRight - 70, safeBottom - 300, 70, 40);

            // Triggers
          _leftTrigger.Frame = new CGRect(safeLeft, safeBottom - 350, 70, 40);
  _rightTrigger.Frame = new CGRect(safeRight - 70, safeBottom - 350, 70, 40);

   // Start/Back (center)
    var centerX = bounds.Width / 2;
  _backButton.Frame = new CGRect(centerX - 60, safeBottom - 50, 50, 30);
      _startButton.Frame = new CGRect(centerX + 10, safeBottom - 50, 50, 30);
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
    /// Analog stick control
    /// </summary>
    public class AnalogStickView : UIView
    {
        private UIView _knob;
 private CGPoint _center;
        private nfloat _maxRadius;

        public event Action<float, float>? OnValueChanged;

        public AnalogStickView(CGRect frame) : base(frame)
 {
       BackgroundColor = UIColor.FromRGBA(50, 50, 50, 150);
            Layer.CornerRadius = frame.Width / 2;

       _knob = new UIView(new CGRect(0, 0, 50, 50))
            {
      BackgroundColor = UIColor.FromRGBA(150, 150, 150, 200),
        UserInteractionEnabled = false
            };
            _knob.Layer.CornerRadius = 25;
            AddSubview(_knob);

        _maxRadius = (nfloat)(frame.Width / 2 - 30);
      _center = new CGPoint(frame.Width / 2, frame.Height / 2);
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
   _knob.Center = _center;
      OnValueChanged?.Invoke(0, 0);
    }

        public override void TouchesCancelled(NSSet touches, UIEvent? evt)
        {
    TouchesEnded(touches, evt);
        }

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
    /// Gamepad button control
    /// </summary>
    public class GamepadButtonView : UIView
    {
        private UILabel _label;
        public event Action<bool>? OnPressed;

   public GamepadButtonView(string text, UIColor color)
        {
       BackgroundColor = color;
  Layer.CornerRadius = 25;
            Frame = new CGRect(0, 0, 50, 50);

    _label = new UILabel
     {
          Text = text,
      TextColor = UIColor.White,
                Font = UIFont.BoldSystemFontOfSize(16),
  TextAlignment = UITextAlignment.Center,
         TranslatesAutoresizingMaskIntoConstraints = false
      };
         AddSubview(_label);

      _label.CenterXAnchor.ConstraintEqualTo(CenterXAnchor).Active = true;
            _label.CenterYAnchor.ConstraintEqualTo(CenterYAnchor).Active = true;
        }

        public override void TouchesBegan(NSSet touches, UIEvent? evt)
        {
        Alpha = 0.6f;
    OnPressed?.Invoke(true);
        }

      public override void TouchesEnded(NSSet touches, UIEvent? evt)
        {
         Alpha = 1.0f;
        OnPressed?.Invoke(false);
        }

        public override void TouchesCancelled(NSSet touches, UIEvent? evt)
        {
            TouchesEnded(touches, evt);
      }
    }

    /// <summary>
    /// D-Pad control
    /// </summary>
    public class DPadView : UIView
    {
        public event Action<GamepadButtons>? OnDirectionChanged;

  public DPadView(CGRect frame) : base(frame)
   {
            BackgroundColor = UIColor.FromRGBA(50, 50, 50, 150);
            Layer.CornerRadius = 10;
        }

     public override void TouchesBegan(NSSet touches, UIEvent? evt) => UpdateDirection(touches);
        public override void TouchesMoved(NSSet touches, UIEvent? evt) => UpdateDirection(touches);

   public override void TouchesEnded(NSSet touches, UIEvent? evt)
   {
     OnDirectionChanged?.Invoke(GamepadButtons.None);
  }

        public override void TouchesCancelled(NSSet touches, UIEvent? evt) => TouchesEnded(touches, evt);

        private void UpdateDirection(NSSet touches)
    {
      var touch = touches.AnyObject as UITouch;
     if (touch == null) return;

            var location = touch.LocationInView(this);
        var centerX = Bounds.Width / 2;
        var centerY = Bounds.Height / 2;

   var direction = GamepadButtons.None;

     if (location.Y < centerY - 15) direction |= GamepadButtons.DPadUp;
   if (location.Y > centerY + 15) direction |= GamepadButtons.DPadDown;
  if (location.X < centerX - 15) direction |= GamepadButtons.DPadLeft;
            if (location.X > centerX + 15) direction |= GamepadButtons.DPadRight;

            OnDirectionChanged?.Invoke(direction);
 }

        public override void Draw(CGRect rect)
        {
  base.Draw(rect);

            using var context = UIGraphics.GetCurrentContext();
     if (context == null) return;

         context.SetFillColor(UIColor.FromRGBA(100, 100, 100, 200).CGColor);

 var centerX = rect.Width / 2;
            var centerY = rect.Height / 2;
  var armWidth = 30f;
       var armLength = 35f;

            // Draw cross
     context.FillRect(new CGRect(centerX - armWidth / 2, centerY - armLength, armWidth, armLength * 2));
          context.FillRect(new CGRect(centerX - armLength, centerY - armWidth / 2, armLength * 2, armWidth));

         // Draw arrows
            context.SetFillColor(UIColor.White.CGColor);
     DrawArrow(context, centerX, 10, 0);      // Up
            DrawArrow(context, centerX, rect.Height - 10, 180); // Down
 DrawArrow(context, 10, centerY, 270);   // Left
          DrawArrow(context, rect.Width - 10, centerY, 90); // Right
   }

        private void DrawArrow(CGContext context, nfloat x, nfloat y, float rotation)
     {
     context.SaveState();
            context.TranslateCTM(x, y);
    context.RotateCTM((nfloat)(rotation * Math.PI / 180));

            var path = new CGPath();
            path.MoveToPoint(-6, 4);
 path.AddLineToPoint(0, -4);
            path.AddLineToPoint(6, 4);
            path.CloseSubpath();

         context.AddPath(path);
   context.FillPath();
            context.RestoreState();
        }
    }

    /// <summary>
    /// Trigger control
    /// </summary>
    public class TriggerView : UIView
    {
      private UILabel _label;
        private float _value;

        public event Action<float>? OnValueChanged;

      public TriggerView(string text)
        {
       BackgroundColor = UIColor.FromRGBA(50, 50, 50, 150);
   Layer.CornerRadius = 8;
         Frame = new CGRect(0, 0, 70, 40);

    _label = new UILabel
            {
                Text = text,
   TextColor = UIColor.White,
      Font = UIFont.BoldSystemFontOfSize(14),
  TextAlignment = UITextAlignment.Center,
      TranslatesAutoresizingMaskIntoConstraints = false
            };
         AddSubview(_label);

     _label.CenterXAnchor.ConstraintEqualTo(CenterXAnchor).Active = true;
            _label.CenterYAnchor.ConstraintEqualTo(CenterYAnchor).Active = true;
   }

        public override void TouchesBegan(NSSet touches, UIEvent? evt)
      {
            _value = 1.0f;
    Alpha = 0.6f;
    OnValueChanged?.Invoke(_value);
        }

        public override void TouchesEnded(NSSet touches, UIEvent? evt)
        {
    _value = 0f;
            Alpha = 1.0f;
   OnValueChanged?.Invoke(_value);
    }

        public override void TouchesCancelled(NSSet touches, UIEvent? evt) => TouchesEnded(touches, evt);
    }
}
