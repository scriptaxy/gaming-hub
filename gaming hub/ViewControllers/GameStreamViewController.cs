using UIKit;
using CoreGraphics;
using Foundation;
using gaming_hub.Services;

namespace gaming_hub.ViewControllers
{
    public enum StreamInputMode { Touch, Controller }
    public enum ControllerStyle { Xbox, PlayStation }
    public enum ControllerSize { Minimal, Default }

    public class GameStreamViewController : UIViewController
    {
        private UIImageView _streamView = null!;
        private VirtualGamepadView? _gamepadView;
     private TouchInputView? _touchView;
        private UIActivityIndicatorView _loadingIndicator = null!;
        private UILabel _statusLabel = null!;
        private UIButton _closeButton = null!;
    private UIButton _menuButton = null!;
     private UIButton? _screenshotButton;
   private UIButton? _recordButton;
        private UIView _controlsOverlay = null!;
        private UIView _cursorView = null!;
        private byte[]? _lastFrameData;

        private string _host;
        private int _port;
 private string? _authToken;
        private StreamInputMode _inputMode = StreamInputMode.Controller;
        private ControllerStyle _controllerStyle = ControllerStyle.Xbox;
        private ControllerSize _controllerSize = ControllerSize.Default;
        private bool _controlsVisible = true;
        private bool _gyroEnabled = false;
        private bool _physicalControllerPriority = true;
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
            SetupNewFeatures();
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
       CleanupNewFeatures();
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

        private void SetupNewFeatures()
        {
         PhysicalControllerService.Instance.OnStateChanged += OnPhysicalControllerInput;
  PhysicalControllerService.Instance.OnControllerConnected += OnControllerConnected;
        PhysicalControllerService.Instance.OnControllerDisconnected += OnControllerDisconnected;
  PhysicalControllerService.Instance.StartListening();

      if (GyroscopeAimingService.Instance.IsAvailable)
      GyroscopeAimingService.Instance.OnMotionUpdate += OnGyroInput;

          ButtonMappingService.Instance.OnSpecialAction += OnSpecialAction;
 StreamCaptureService.Instance.OnScreenshotSaved += OnScreenshotResult;
            StreamCaptureService.Instance.OnRecordingSaved += OnRecordingResult;
        }

        private void CleanupNewFeatures()
        {
            PhysicalControllerService.Instance.OnStateChanged -= OnPhysicalControllerInput;
   PhysicalControllerService.Instance.OnControllerConnected -= OnControllerConnected;
  PhysicalControllerService.Instance.OnControllerDisconnected -= OnControllerDisconnected;
          PhysicalControllerService.Instance.StopListening();

      GyroscopeAimingService.Instance.OnMotionUpdate -= OnGyroInput;
            GyroscopeAimingService.Instance.StopTracking();

    ButtonMappingService.Instance.OnSpecialAction -= OnSpecialAction;
       StreamCaptureService.Instance.OnScreenshotSaved -= OnScreenshotResult;
         StreamCaptureService.Instance.OnRecordingSaved -= OnRecordingResult;

            if (StreamCaptureService.Instance.IsRecording)
   StreamCaptureService.Instance.CancelRecording();
      }

        private void SetupUI()
        {
    View!.BackgroundColor = UIColor.Black;

            _streamView = new UIImageView
            {
       ContentMode = UIViewContentMode.ScaleAspectFit,
 BackgroundColor = UIColor.Black,
   TranslatesAutoresizingMaskIntoConstraints = false
            };
       View.AddSubview(_streamView);

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

            _controlsOverlay = new UIView
            {
        TranslatesAutoresizingMaskIntoConstraints = false,
     BackgroundColor = UIColor.FromRGBA(0, 0, 0, 120)
     };
        View.AddSubview(_controlsOverlay);

         _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large)
     {
         TranslatesAutoresizingMaskIntoConstraints = false,
                Color = UIColor.White,
          HidesWhenStopped = true
            };
            View.AddSubview(_loadingIndicator);

       _statusLabel = new UILabel
     {
           TranslatesAutoresizingMaskIntoConstraints = false,
     TextColor = UIColor.White,
         Font = UIFont.SystemFontOfSize(14),
                TextAlignment = UITextAlignment.Center,
     Text = "Connecting..."
          };
     View.AddSubview(_statusLabel);

            _closeButton = CreateOverlayButton("xmark.circle.fill", UIColor.SystemRed);
      _closeButton.TouchUpInside += (s, e) => DismissViewController(true, null);
        _controlsOverlay.AddSubview(_closeButton);

      _menuButton = CreateOverlayButton("ellipsis.circle", UIColor.White);
      _menuButton.TouchUpInside += (s, e) => ShowMenu();
        _controlsOverlay.AddSubview(_menuButton);

 _screenshotButton = CreateOverlayButton("camera.fill", UIColor.White);
         _screenshotButton.TouchUpInside += (s, e) => TakeScreenshot();
    _controlsOverlay.AddSubview(_screenshotButton);

            _recordButton = CreateOverlayButton("record.circle", UIColor.White);
            _recordButton.TouchUpInside += (s, e) => ToggleRecording();
 _controlsOverlay.AddSubview(_recordButton);

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

     _menuButton.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
      _menuButton.TrailingAnchor.ConstraintEqualTo(_closeButton.LeadingAnchor, -8),
           _menuButton.WidthAnchor.ConstraintEqualTo(44),
     _menuButton.HeightAnchor.ConstraintEqualTo(44),

     _screenshotButton!.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
     _screenshotButton.TrailingAnchor.ConstraintEqualTo(_menuButton.LeadingAnchor, -8),
        _screenshotButton.WidthAnchor.ConstraintEqualTo(44),
       _screenshotButton.HeightAnchor.ConstraintEqualTo(44),

_recordButton!.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
   _recordButton.TrailingAnchor.ConstraintEqualTo(_screenshotButton.LeadingAnchor, -8),
     _recordButton.WidthAnchor.ConstraintEqualTo(44),
                _recordButton.HeightAnchor.ConstraintEqualTo(44),

          _loadingIndicator.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
    _loadingIndicator.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor),

         _statusLabel.TopAnchor.ConstraintEqualTo(_loadingIndicator.BottomAnchor, 16),
    _statusLabel.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
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
          HideControls();
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
      SetupInputView();
         SaveSettings();
 ShowToast(_inputMode == StreamInputMode.Touch ? "Touch Mode" : "Controller Mode");
        }

        private void ShowMenu()
        {
ShowControls();
   var alert = UIAlertController.Create("Menu", null, UIAlertControllerStyle.ActionSheet);

         // Input mode toggle
     alert.AddAction(UIAlertAction.Create(
     _inputMode == StreamInputMode.Touch ? "Switch to Controller Mode" : "Switch to Touch Mode",
                UIAlertActionStyle.Default, _ => ToggleInputMode()));

    if (GyroscopeAimingService.Instance.IsAvailable)
     alert.AddAction(UIAlertAction.Create(_gyroEnabled ? "Disable Gyro" : "Enable Gyro",
       UIAlertActionStyle.Default, _ => ToggleGyroscope()));

       if (PhysicalControllerService.Instance.IsControllerConnected)
   alert.AddAction(UIAlertAction.Create($"Controller: {PhysicalControllerService.Instance.ControllerName} ?",
       UIAlertActionStyle.Default, null));

      alert.AddAction(UIAlertAction.Create("Quick Actions...", UIAlertActionStyle.Default, _ => ShowQuickActionsMenu()));
   alert.AddAction(UIAlertAction.Create("Button Mapping...", UIAlertActionStyle.Default, _ => ShowButtonMappingMenu()));

  alert.AddAction(UIAlertAction.Create($"Style: {(_controllerStyle == ControllerStyle.Xbox ? "Xbox" : "PlayStation")}",
       UIAlertActionStyle.Default, _ =>
     {
     _controllerStyle = _controllerStyle == ControllerStyle.Xbox ? ControllerStyle.PlayStation : ControllerStyle.Xbox;
     if (_inputMode == StreamInputMode.Controller) SetupInputView();
   SaveSettings();
     }));

 alert.AddAction(UIAlertAction.Create($"Size: {(_controllerSize == ControllerSize.Default ? "Default" : "Minimal")}",
      UIAlertActionStyle.Default, _ =>
     {
       _controllerSize = _controllerSize == ControllerSize.Default ? ControllerSize.Minimal : ControllerSize.Default;
     if (_inputMode == StreamInputMode.Controller) SetupInputView();
           SaveSettings();
     }));

         alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));

            if (alert.PopoverPresentationController != null)
     {
  alert.PopoverPresentationController.SourceView = _menuButton;
alert.PopoverPresentationController.SourceRect = _menuButton.Bounds;
            }
            PresentViewController(alert, true, null);
        }

        private void ShowQuickActionsMenu()
        {
          var alert = UIAlertController.Create("Quick Actions", null, UIAlertControllerStyle.ActionSheet);
     alert.AddAction(UIAlertAction.Create("Volume Up", UIAlertActionStyle.Default, async _ =>
       await new QuickActionsService(RemotePCService.Instance).VolumeUpAsync()));
            alert.AddAction(UIAlertAction.Create("Volume Down", UIAlertActionStyle.Default, async _ =>
      await new QuickActionsService(RemotePCService.Instance).VolumeDownAsync()));
      alert.AddAction(UIAlertAction.Create("Mute", UIAlertActionStyle.Default, async _ =>
          await new QuickActionsService(RemotePCService.Instance).VolumeMuteAsync()));
  alert.AddAction(UIAlertAction.Create("Play/Pause", UIAlertActionStyle.Default, async _ =>
          await new QuickActionsService(RemotePCService.Instance).MediaPlayPauseAsync()));
    alert.AddAction(UIAlertAction.Create("Alt+Tab", UIAlertActionStyle.Default, async _ =>
     await new QuickActionsService(RemotePCService.Instance).AltTabAsync()));
    alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));

      if (alert.PopoverPresentationController != null)
            {
                alert.PopoverPresentationController.SourceView = _menuButton;
    alert.PopoverPresentationController.SourceRect = _menuButton.Bounds;
    }
      PresentViewController(alert, true, null);
        }

 private void ShowButtonMappingMenu()
  {
        var alert = UIAlertController.Create("Button Mapping", null, UIAlertControllerStyle.ActionSheet);
            foreach (var profile in ButtonMappingService.Instance.Profiles)
    {
      var isActive = profile.Name == ButtonMappingService.Instance.ActiveProfile.Name;
             alert.AddAction(UIAlertAction.Create(isActive ? $"{profile.Name} ?" : profile.Name,
        UIAlertActionStyle.Default, _ =>
  {
             ButtonMappingService.Instance.SetActiveProfile(profile.Name);
       ShowToast($"Profile: {profile.Name}");
           }));
      }
 alert.AddAction(UIAlertAction.Create("Reset to Default", UIAlertActionStyle.Destructive, _ =>
            {
  ButtonMappingService.Instance.ResetToDefault();
            ShowToast("Mappings reset");
            }));
         alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));

            if (alert.PopoverPresentationController != null)
            {
      alert.PopoverPresentationController.SourceView = _menuButton;
              alert.PopoverPresentationController.SourceRect = _menuButton.Bounds;
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
 UIView.Animate(0.3, 1.5, UIViewAnimationOptions.CurveEaseIn, () => toast.Alpha = 0, toast.RemoveFromSuperview));
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
     request.Content = new StringContent("{\"quality\":50,\"fps\":60,\"width\":1280,\"height\":720}",
   System.Text.Encoding.UTF8, "application/json");
 await client.SendAsync(request);
            }
     catch (Exception ex) { Console.WriteLine($"Failed to start stream: {ex.Message}"); }

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
          using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    var request = new HttpRequestMessage(HttpMethod.Post, $"http://{_host}:19500/api/stream/stop");
       if (!string.IsNullOrEmpty(_authToken))
        request.Headers.Add("Authorization", $"Bearer {_authToken}");
            await client.SendAsync(request);
            }
          catch { }
    }

        private void OnFrameReceived(byte[] frameData)
        {
      _lastFrameData = frameData;
     InvokeOnMainThread(() =>
       {
   try
       {
     using var data = NSData.FromArray(frameData);
          var image = UIImage.LoadFromData(data);
            if (image != null) _streamView.Image = image;
       if (StreamCaptureService.Instance.IsRecording)
        StreamCaptureService.Instance.AddRecordingFrame(frameData);
    }
      catch { }
            });
  }

        private void OnStreamConnected() => InvokeOnMainThread(() => { _loadingIndicator.StopAnimating(); _statusLabel.Hidden = true; });
        private void OnStreamDisconnected(string reason) => InvokeOnMainThread(() => { _statusLabel.Hidden = false; _statusLabel.Text = $"Disconnected: {reason}"; });
        private void OnStreamError(string error) => InvokeOnMainThread(() => { _statusLabel.Hidden = false; _statusLabel.Text = $"Error: {error}"; });

        private void OnTouchInput(float x, float y, TouchInputType type)
      {
            ShowControls();
         _ = StreamingClient.Instance.SendTouchInputAsync(x, y, type == TouchInputType.Tap);
        }

  private void OnCursorMoved(CGPoint position) => InvokeOnMainThread(() => _cursorView.Center = position);

   private void OnGamepadStateChanged(GamepadState state)
        {
   if (_physicalControllerPriority && PhysicalControllerService.Instance.IsControllerConnected) return;
   ShowControls();
        SendProcessedInput(state);
        }

      private void OnPhysicalControllerInput(GamepadState state) => SendProcessedInput(state);

     private void SendProcessedInput(GamepadState state)
    {
       var mappedState = ButtonMappingService.Instance.ProcessInput(state);
         if (_gyroEnabled && GyroscopeAimingService.Instance.IsRunning)
            {
          mappedState.RightStickX = Math.Clamp(mappedState.RightStickX + GyroscopeAimingService.Instance.CurrentX, -1f, 1f);
    mappedState.RightStickY = Math.Clamp(mappedState.RightStickY + GyroscopeAimingService.Instance.CurrentY, -1f, 1f);
        }
        _ = StreamingClient.Instance.SendGamepadInputAsync(mappedState);
        }

        private void OnGyroInput(float deltaX, float deltaY) { }

        private void OnControllerConnected(GameController.GCController controller) =>
         InvokeOnMainThread(() => ShowToast($"Controller: {controller.VendorName}"));

     private void OnControllerDisconnected(GameController.GCController controller) =>
            InvokeOnMainThread(() => ShowToast("Controller disconnected"));

        private void OnSpecialAction(MappableAction action)
        {
     InvokeOnMainThread(() =>
            {
          switch (action)
           {
            case MappableAction.Screenshot: TakeScreenshot(); break;
           case MappableAction.ToggleRecording: ToggleRecording(); break;
      case MappableAction.ToggleGyro: ToggleGyroscope(); break;
      case MappableAction.CalibrateGyro:
    GyroscopeAimingService.Instance.Calibrate();
        ShowToast("Gyroscope calibrated");
  break;
            case MappableAction.ShowQuickMenu: ShowQuickActionsMenu(); break;
             }
  });
  }

        private void TakeScreenshot()
        {
            if (_lastFrameData != null)
                _ = StreamCaptureService.Instance.CaptureScreenshotAsync(_lastFrameData);
        }

        private void ToggleRecording()
{
         if (StreamCaptureService.Instance.IsRecording)
            {
          _ = StreamCaptureService.Instance.StopRecording();
          UpdateRecordButton(false);
 }
         else
       {
        StreamCaptureService.Instance.StartRecording();
  UpdateRecordButton(true);
             ShowToast("Recording started");
    }
        }

        private void UpdateRecordButton(bool isRecording)
        {
      var config = UIImageSymbolConfiguration.Create(UIFont.SystemFontOfSize(22));
            _recordButton?.SetImage(UIImage.GetSystemImage(isRecording ? "stop.circle.fill" : "record.circle", config), UIControlState.Normal);
  _recordButton!.TintColor = isRecording ? UIColor.SystemRed : UIColor.White;
        }

     private void ToggleGyroscope()
        {
          _gyroEnabled = !_gyroEnabled;
       if (_gyroEnabled)
         {
   GyroscopeAimingService.Instance.Calibrate();
           GyroscopeAimingService.Instance.StartTracking();
         ShowToast("Gyro aiming enabled");
}
      else
          {
          GyroscopeAimingService.Instance.StopTracking();
         ShowToast("Gyro aiming disabled");
   }
        }

        private void OnScreenshotResult(bool success, string? message) =>
            InvokeOnMainThread(() => ShowToast(message ?? (success ? "Screenshot saved" : "Screenshot failed")));

        private void OnRecordingResult(bool success, string? message) =>
            InvokeOnMainThread(() => { UpdateRecordButton(false); ShowToast(message ?? (success ? "Recording saved" : "Recording failed")); });
    }

    public enum TouchInputType { Move, Tap, DoubleTap, LongPress }

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
       var tap = new UITapGestureRecognizer(OnTap) { DelaysTouchesBegan = false };
   AddGestureRecognizer(tap);
            var doubleTap = new UITapGestureRecognizer(OnDoubleTap) { NumberOfTapsRequired = 2 };
   AddGestureRecognizer(doubleTap);
          tap.RequireGestureRecognizerToFail(doubleTap);
            var longPress = new UILongPressGestureRecognizer(OnLongPress) { MinimumPressDuration = 0.3 };
      AddGestureRecognizer(longPress);
 }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
   if (_cursorPosition == CGPoint.Empty)
      _cursorPosition = new CGPoint(Bounds.Width / 2, Bounds.Height / 2);
    }

  public override void TouchesBegan(NSSet touches, UIEvent? evt)
        {
  if (touches.AnyObject is UITouch touch)
         {
    _lastTouchPosition = touch.LocationInView(this);
          _isDragging = true;
            }
      }

        public override void TouchesMoved(NSSet touches, UIEvent? evt)
   {
            if (touches.AnyObject is UITouch touch && _isDragging)
        {
       var currentPos = touch.LocationInView(this);
           var deltaX = (currentPos.X - _lastTouchPosition.X) * Sensitivity;
    var deltaY = (currentPos.Y - _lastTouchPosition.Y) * Sensitivity;
     _cursorPosition = new CGPoint(
                  Math.Clamp(_cursorPosition.X + deltaX, 0, Bounds.Width),
   Math.Clamp(_cursorPosition.Y + deltaY, 0, Bounds.Height));
       _lastTouchPosition = currentPos;
        OnCursorMoved?.Invoke(_cursorPosition);
                OnTouchInput?.Invoke((float)(_cursorPosition.X / Bounds.Width), (float)(_cursorPosition.Y / Bounds.Height), TouchInputType.Move);
            }
        }

        public override void TouchesEnded(NSSet touches, UIEvent? evt) => _isDragging = false;

     private void OnTap(UITapGestureRecognizer g) =>
            OnTouchInput?.Invoke((float)(_cursorPosition.X / Bounds.Width), (float)(_cursorPosition.Y / Bounds.Height), TouchInputType.Tap);
        private void OnDoubleTap(UITapGestureRecognizer g) =>
OnTouchInput?.Invoke((float)(_cursorPosition.X / Bounds.Width), (float)(_cursorPosition.Y / Bounds.Height), TouchInputType.DoubleTap);
        private void OnLongPress(UILongPressGestureRecognizer g)
        {
            if (g.State == UIGestureRecognizerState.Began)
 OnTouchInput?.Invoke((float)(_cursorPosition.X / Bounds.Width), (float)(_cursorPosition.Y / Bounds.Height), TouchInputType.LongPress);
        }
    }

    public class VirtualGamepadView : UIView
    {
        private readonly ControllerStyle _style;
        private readonly ControllerSize _size;
   public event Action<GamepadState>? OnStateChanged;
        private GamepadState _state = new();

 // Left side controls
 private VirtualStickView? _leftStick;
        private UIButton? _dpadUp, _dpadDown, _dpadLeft, _dpadRight;

   // Right side controls  
        private VirtualStickView? _rightStick;
  private UIButton? _buttonA, _buttonB, _buttonX, _buttonY;
        
      // Triggers and bumpers
    private UIButton? _leftBumper, _rightBumper;
        private UIButton? _leftTrigger, _rightTrigger;
   
        // Menu buttons
        private UIButton? _startButton, _backButton;

        public VirtualGamepadView(ControllerStyle style, ControllerSize size)
        {
   _style = style;
            _size = size;
            MultipleTouchEnabled = true;
            SetupControls();
        }

     private void SetupControls()
        {
            var isMinimal = _size == ControllerSize.Minimal;
            var stickSize = isMinimal ? 100 : 130;
            var buttonSize = isMinimal ? 44 : 54;
            var smallButtonSize = isMinimal ? 36 : 44;

            // Left analog stick
   _leftStick = new VirtualStickView(stickSize)
        {
     TranslatesAutoresizingMaskIntoConstraints = false
         };
          _leftStick.OnStickMoved += (x, y) =>
            {
      _state.LeftStickX = x;
          _state.LeftStickY = y;
 NotifyStateChanged();
     };
     AddSubview(_leftStick);

 // Right analog stick
 _rightStick = new VirtualStickView(stickSize)
   {
    TranslatesAutoresizingMaskIntoConstraints = false
      };
            _rightStick.OnStickMoved += (x, y) =>
            {
     _state.RightStickX = x;
    _state.RightStickY = y;
       NotifyStateChanged();
         };
     AddSubview(_rightStick);

       // Face buttons (A, B, X, Y)
        var aLabel = _style == ControllerStyle.Xbox ? "A" : "X";
    var bLabel = _style == ControllerStyle.Xbox ? "B" : "O";
            var xLabel = _style == ControllerStyle.Xbox ? "X" : "?";
 var yLabel = _style == ControllerStyle.Xbox ? "Y" : "?";
            
            var aColor = _style == ControllerStyle.Xbox ? UIColor.FromRGB(96, 185, 58) : UIColor.FromRGB(70, 130, 255);
            var bColor = _style == ControllerStyle.Xbox ? UIColor.FromRGB(221, 68, 59) : UIColor.FromRGB(255, 70, 90);
       var xColor = _style == ControllerStyle.Xbox ? UIColor.FromRGB(63, 130, 205) : UIColor.FromRGB(255, 100, 180);
  var yColor = _style == ControllerStyle.Xbox ? UIColor.FromRGB(245, 199, 72) : UIColor.FromRGB(100, 220, 180);

     _buttonA = CreateFaceButton(aLabel, aColor, buttonSize, GamepadButtons.A);
    _buttonB = CreateFaceButton(bLabel, bColor, buttonSize, GamepadButtons.B);
       _buttonX = CreateFaceButton(xLabel, xColor, buttonSize, GamepadButtons.X);
 _buttonY = CreateFaceButton(yLabel, yColor, buttonSize, GamepadButtons.Y);

AddSubview(_buttonA);
            AddSubview(_buttonB);
            AddSubview(_buttonX);
            AddSubview(_buttonY);

            // D-Pad
    _dpadUp = CreateDPadButton("?", smallButtonSize, GamepadButtons.DPadUp);
        _dpadDown = CreateDPadButton("?", smallButtonSize, GamepadButtons.DPadDown);
        _dpadLeft = CreateDPadButton("?", smallButtonSize, GamepadButtons.DPadLeft);
            _dpadRight = CreateDPadButton("?", smallButtonSize, GamepadButtons.DPadRight);

            AddSubview(_dpadUp);
          AddSubview(_dpadDown);
      AddSubview(_dpadLeft);
            AddSubview(_dpadRight);

  // Bumpers
        _leftBumper = CreateBumperButton("LB", 70, 36, GamepadButtons.LeftBumper);
            _rightBumper = CreateBumperButton("RB", 70, 36, GamepadButtons.RightBumper);
    AddSubview(_leftBumper);
 AddSubview(_rightBumper);

    // Triggers
       _leftTrigger = CreateTriggerButton("LT", 60, 40);
            _rightTrigger = CreateTriggerButton("RT", 60, 40);
     AddSubview(_leftTrigger);
         AddSubview(_rightTrigger);

            // Menu buttons
            _startButton = CreateMenuButton("?", smallButtonSize - 8, GamepadButtons.Start);
            _backButton = CreateMenuButton("??", smallButtonSize - 8, GamepadButtons.Back);
       AddSubview(_startButton);
  AddSubview(_backButton);
        }

        private UIButton CreateFaceButton(string label, UIColor color, int size, GamepadButtons button)
        {
     var btn = new UIButton(UIButtonType.Custom)
       {
    TranslatesAutoresizingMaskIntoConstraints = false,
           BackgroundColor = color.ColorWithAlpha(0.7f)
   };
     btn.SetTitle(label, UIControlState.Normal);
   btn.SetTitleColor(UIColor.White, UIControlState.Normal);
    btn.TitleLabel!.Font = UIFont.BoldSystemFontOfSize(size > 50 ? 20 : 16);
        btn.Layer.CornerRadius = size / 2;
            btn.Layer.BorderColor = UIColor.White.ColorWithAlpha(0.3f).CGColor;
 btn.Layer.BorderWidth = 2;

            btn.TouchDown += (s, e) => { _state.Buttons |= button; NotifyStateChanged(); btn.Transform = CGAffineTransform.MakeScale(0.9f, 0.9f); };
     btn.TouchUpInside += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); btn.Transform = CGAffineTransform.MakeIdentity(); };
   btn.TouchUpOutside += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); btn.Transform = CGAffineTransform.MakeIdentity(); };
       btn.TouchCancel += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); btn.Transform = CGAffineTransform.MakeIdentity(); };

  NSLayoutConstraint.ActivateConstraints(new[]
            {
                btn.WidthAnchor.ConstraintEqualTo(size),
           btn.HeightAnchor.ConstraintEqualTo(size)
     });

            return btn;
  }

        private UIButton CreateDPadButton(string label, int size, GamepadButtons button)
     {
  var btn = new UIButton(UIButtonType.Custom)
            {
       TranslatesAutoresizingMaskIntoConstraints = false,
  BackgroundColor = UIColor.FromRGBA(80, 80, 80, 180)
            };
btn.SetTitle(label, UIControlState.Normal);
            btn.SetTitleColor(UIColor.White, UIControlState.Normal);
       btn.TitleLabel!.Font = UIFont.SystemFontOfSize(14);
  btn.Layer.CornerRadius = 6;

            btn.TouchDown += (s, e) => { _state.Buttons |= button; NotifyStateChanged(); btn.BackgroundColor = UIColor.FromRGBA(120, 120, 120, 220); };
            btn.TouchUpInside += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); btn.BackgroundColor = UIColor.FromRGBA(80, 80, 80, 180); };
            btn.TouchUpOutside += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); btn.BackgroundColor = UIColor.FromRGBA(80, 80, 80, 180); };
      btn.TouchCancel += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); btn.BackgroundColor = UIColor.FromRGBA(80, 80, 80, 180); };

   NSLayoutConstraint.ActivateConstraints(new[]
            {
  btn.WidthAnchor.ConstraintEqualTo(size),
    btn.HeightAnchor.ConstraintEqualTo(size)
            });

   return btn;
        }

        private UIButton CreateBumperButton(string label, int width, int height, GamepadButtons button)
        {
            var btn = new UIButton(UIButtonType.Custom)
 {
            TranslatesAutoresizingMaskIntoConstraints = false,
  BackgroundColor = UIColor.FromRGBA(60, 60, 60, 200)
 };
            btn.SetTitle(label, UIControlState.Normal);
     btn.SetTitleColor(UIColor.White, UIControlState.Normal);
            btn.TitleLabel!.Font = UIFont.BoldSystemFontOfSize(14);
          btn.Layer.CornerRadius = 8;

  btn.TouchDown += (s, e) => { _state.Buttons |= button; NotifyStateChanged(); btn.BackgroundColor = UIColor.FromRGBA(100, 100, 100, 220); };
            btn.TouchUpInside += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); btn.BackgroundColor = UIColor.FromRGBA(60, 60, 60, 200); };
 btn.TouchUpOutside += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); btn.BackgroundColor = UIColor.FromRGBA(60, 60, 60, 200); };
          btn.TouchCancel += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); btn.BackgroundColor = UIColor.FromRGBA(60, 60, 60, 200); };

        NSLayoutConstraint.ActivateConstraints(new[]
            {
                btn.WidthAnchor.ConstraintEqualTo(width),
btn.HeightAnchor.ConstraintEqualTo(height)
         });

            return btn;
        }

        private UIButton CreateTriggerButton(string label, int width, int height)
        {
    var btn = new UIButton(UIButtonType.Custom)
  {
     TranslatesAutoresizingMaskIntoConstraints = false,
        BackgroundColor = UIColor.FromRGBA(50, 50, 50, 200)
        };
            btn.SetTitle(label, UIControlState.Normal);
         btn.SetTitleColor(UIColor.White, UIControlState.Normal);
     btn.TitleLabel!.Font = UIFont.BoldSystemFontOfSize(12);
            btn.Layer.CornerRadius = 6;

            var isLeft = label == "LT";
   btn.TouchDown += (s, e) =>
     {
            if (isLeft) _state.LeftTrigger = 1.0f;
   else _state.RightTrigger = 1.0f;
  NotifyStateChanged();
            btn.BackgroundColor = UIColor.FromRGBA(100, 100, 100, 220);
            };
            btn.TouchUpInside += (s, e) =>
            {
      if (isLeft) _state.LeftTrigger = 0f;
                else _state.RightTrigger = 0f;
      NotifyStateChanged();
btn.BackgroundColor = UIColor.FromRGBA(50, 50, 50, 200);
            };
            btn.TouchUpOutside += (s, e) =>
 {
  if (isLeft) _state.LeftTrigger = 0f;
      else _state.RightTrigger = 0f;
      NotifyStateChanged();
                btn.BackgroundColor = UIColor.FromRGBA(50, 50, 50, 200);
      };
btn.TouchCancel += (s, e) =>
     {
      if (isLeft) _state.LeftTrigger = 0f;
            else _state.RightTrigger = 0f;
                NotifyStateChanged();
      btn.BackgroundColor = UIColor.FromRGBA(50, 50, 50, 200);
            };

  NSLayoutConstraint.ActivateConstraints(new[]
   {
     btn.WidthAnchor.ConstraintEqualTo(width),
         btn.HeightAnchor.ConstraintEqualTo(height)
  });

        return btn;
 }

        private UIButton CreateMenuButton(string label, int size, GamepadButtons button)
        {
            var btn = new UIButton(UIButtonType.Custom)
            {
   TranslatesAutoresizingMaskIntoConstraints = false,
       BackgroundColor = UIColor.FromRGBA(40, 40, 40, 180)
 };
    btn.SetTitle(label, UIControlState.Normal);
btn.SetTitleColor(UIColor.White.ColorWithAlpha(0.8f), UIControlState.Normal);
 btn.TitleLabel!.Font = UIFont.SystemFontOfSize(12);
            btn.Layer.CornerRadius = size / 2;

   btn.TouchDown += (s, e) => { _state.Buttons |= button; NotifyStateChanged(); };
  btn.TouchUpInside += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); };
   btn.TouchUpOutside += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); };
       btn.TouchCancel += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); };

            NSLayoutConstraint.ActivateConstraints(new[]
            {
       btn.WidthAnchor.ConstraintEqualTo(size),
         btn.HeightAnchor.ConstraintEqualTo(size)
         });

        return btn;
        }

        public override void LayoutSubviews()
     {
      base.LayoutSubviews();

  var safeArea = SafeAreaInsets;
            var isMinimal = _size == ControllerSize.Minimal;
            var stickSize = isMinimal ? 100 : 130;
      var buttonSize = isMinimal ? 44 : 54;
       var spacing = isMinimal ? 6 : 10;

     // Left stick - bottom left
     if (_leftStick != null)
      {
    _leftStick.Frame = new CGRect(
            safeArea.Left + 20,
       Bounds.Height - stickSize - safeArea.Bottom - 30,
        stickSize,
            stickSize);
    }

  // D-Pad - above left stick
  var dpadCenterX = safeArea.Left + 20 + stickSize / 2;
            var dpadCenterY = Bounds.Height - stickSize - safeArea.Bottom - 30 - 80;
     var dpadButtonSize = isMinimal ? 36 : 44;
       var dpadSpacing = dpadButtonSize + 2;

        if (_dpadUp != null) _dpadUp.Center = new CGPoint(dpadCenterX, dpadCenterY - dpadSpacing / 2);
       if (_dpadDown != null) _dpadDown.Center = new CGPoint(dpadCenterX, dpadCenterY + dpadSpacing / 2);
        if (_dpadLeft != null) _dpadLeft.Center = new CGPoint(dpadCenterX - dpadSpacing / 2, dpadCenterY);
   if (_dpadRight != null) _dpadRight.Center = new CGPoint(dpadCenterX + dpadSpacing / 2, dpadCenterY);

            // Right stick - bottom right
     if (_rightStick != null)
            {
      _rightStick.Frame = new CGRect(
    Bounds.Width - stickSize - safeArea.Right - 20,
           Bounds.Height - stickSize - safeArea.Bottom - 30,
        stickSize,
    stickSize);
    }

            // Face buttons - above right stick (diamond layout)
            var faceCenterX = Bounds.Width - stickSize / 2 - safeArea.Right - 20;
    var faceCenterY = Bounds.Height - stickSize - safeArea.Bottom - 30 - 80;
         var faceSpacing = buttonSize + spacing;

            if (_buttonA != null) _buttonA.Center = new CGPoint(faceCenterX, faceCenterY + faceSpacing / 2);
       if (_buttonB != null) _buttonB.Center = new CGPoint(faceCenterX + faceSpacing / 2, faceCenterY);
 if (_buttonX != null) _buttonX.Center = new CGPoint(faceCenterX - faceSpacing / 2, faceCenterY);
        if (_buttonY != null) _buttonY.Center = new CGPoint(faceCenterX, faceCenterY - faceSpacing / 2);

            // Bumpers - top corners
     if (_leftBumper != null) _leftBumper.Center = new CGPoint(safeArea.Left + 55, safeArea.Top + 80);
    if (_rightBumper != null) _rightBumper.Center = new CGPoint(Bounds.Width - safeArea.Right - 55, safeArea.Top + 80);

    // Triggers - above bumpers
   if (_leftTrigger != null) _leftTrigger.Center = new CGPoint(safeArea.Left + 50, safeArea.Top + 120);
            if (_rightTrigger != null) _rightTrigger.Center = new CGPoint(Bounds.Width - safeArea.Right - 50, safeArea.Top + 120);

            // Menu buttons - center top
 var menuY = safeArea.Top + 80;
   if (_backButton != null) _backButton.Center = new CGPoint(Bounds.Width / 2 - 40, menuY);
      if (_startButton != null) _startButton.Center = new CGPoint(Bounds.Width / 2 + 40, menuY);
        }

      private void NotifyStateChanged() => OnStateChanged?.Invoke(_state);
    }

    /// <summary>
    /// Virtual analog stick view
    /// </summary>
    public class VirtualStickView : UIView
    {
    public event Action<float, float>? OnStickMoved;

  private UIView _thumbView;
        private CGPoint _centerPoint;
        private readonly nfloat _maxDistance;
   private bool _isTracking;

        public VirtualStickView(int size)
   {
    _maxDistance = size / 2 - 20;

            BackgroundColor = UIColor.FromRGBA(40, 40, 40, 150);
   Layer.CornerRadius = size / 2;
 Layer.BorderColor = UIColor.White.ColorWithAlpha(0.2f).CGColor;
       Layer.BorderWidth = 2;

 _thumbView = new UIView
         {
     BackgroundColor = UIColor.FromRGBA(200, 200, 200, 200),
      Frame = new CGRect(0, 0, 50, 50)
            };
        _thumbView.Layer.CornerRadius = 25;
     _thumbView.Layer.BorderColor = UIColor.White.ColorWithAlpha(0.5f).CGColor;
            _thumbView.Layer.BorderWidth = 2;
       AddSubview(_thumbView);
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            _centerPoint = new CGPoint(Bounds.Width / 2, Bounds.Height / 2);
            _thumbView.Center = _centerPoint;
        }

      public override void TouchesBegan(NSSet touches, UIEvent? evt)
        {
 _isTracking = true;
UpdateThumbPosition(touches);
  }

        public override void TouchesMoved(NSSet touches, UIEvent? evt)
        {
        if (_isTracking)
                UpdateThumbPosition(touches);
        }

        public override void TouchesEnded(NSSet touches, UIEvent? evt)
        {
 _isTracking = false;
         ResetThumb();
        }

        public override void TouchesCancelled(NSSet touches, UIEvent? evt)
        {
            _isTracking = false;
    ResetThumb();
   }

    private void UpdateThumbPosition(NSSet touches)
        {
        if (touches.AnyObject is not UITouch touch) return;

var location = touch.LocationInView(this);
            var deltaX = location.X - _centerPoint.X;
          var deltaY = location.Y - _centerPoint.Y;
    var distance = (nfloat)Math.Sqrt((double)(deltaX * deltaX + deltaY * deltaY));

            if (distance > _maxDistance)
    {
          var scale = _maxDistance / distance;
         deltaX = deltaX * scale;
        deltaY = deltaY * scale;
   }

            _thumbView.Center = new CGPoint(_centerPoint.X + deltaX, _centerPoint.Y + deltaY);

      var normalizedX = (float)(deltaX / _maxDistance);
         var normalizedY = (float)(deltaY / _maxDistance);
OnStickMoved?.Invoke(normalizedX, normalizedY);
        }

        private void ResetThumb()
        {
   UIView.Animate(0.15, () => _thumbView.Center = _centerPoint);
            OnStickMoved?.Invoke(0, 0);
    }
    }
}
