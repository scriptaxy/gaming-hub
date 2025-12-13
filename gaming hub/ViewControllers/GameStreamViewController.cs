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
        private UIButton _menuButton = null!;
        private UIButton _closeButton = null!;
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

            // Stream view
    _streamView = new UIImageView
     {
      ContentMode = UIViewContentMode.ScaleAspectFit,
    BackgroundColor = UIColor.Black,
           TranslatesAutoresizingMaskIntoConstraints = false
            };
            View.AddSubview(_streamView);

    // Cursor for touch mode
          _cursorView = new UIView
      {
           BackgroundColor = UIColor.Clear,
          Hidden = true,
    Frame = new CGRect(0, 0, 24, 24)
        };
       // Create cursor shape
            var cursorOuter = new UIView { Frame = new CGRect(0, 0, 24, 24), BackgroundColor = UIColor.White };
       cursorOuter.Layer.CornerRadius = 12;
   cursorOuter.Layer.BorderColor = UIColor.Black.CGColor;
            cursorOuter.Layer.BorderWidth = 2;
 var cursorInner = new UIView { Frame = new CGRect(8, 8, 8, 8), BackgroundColor = UIColor.SystemBlue };
            cursorInner.Layer.CornerRadius = 4;
         _cursorView.AddSubview(cursorOuter);
            _cursorView.AddSubview(cursorInner);
            View.AddSubview(_cursorView);

  // Top controls overlay with blur
            _controlsOverlay = new UIView
{
             TranslatesAutoresizingMaskIntoConstraints = false,
            BackgroundColor = UIColor.Clear
            };
    var blurEffect = UIBlurEffect.FromStyle(UIBlurEffectStyle.Dark);
     var blurView = new UIVisualEffectView(blurEffect)
         {
 TranslatesAutoresizingMaskIntoConstraints = false
         };
  _controlsOverlay.InsertSubview(blurView, 0);
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

            // Menu button (hamburger)
            _menuButton = CreateOverlayButton("line.3.horizontal", UIColor.White);
          _menuButton.TouchUpInside += (s, e) => ShowMenu();
_controlsOverlay.AddSubview(_menuButton);

     // Close button
         _closeButton = CreateOverlayButton("xmark", UIColor.White);
          _closeButton.TouchUpInside += (s, e) => DismissViewController(true, null);
            _controlsOverlay.AddSubview(_closeButton);

            // Screenshot button
        _screenshotButton = CreateOverlayButton("camera", UIColor.White);
         _screenshotButton.TouchUpInside += (s, e) => TakeScreenshot();
     _controlsOverlay.AddSubview(_screenshotButton);

          // Record button
        _recordButton = CreateOverlayButton("record.circle", UIColor.White);
    _recordButton.TouchUpInside += (s, e) => ToggleRecording();
     _controlsOverlay.AddSubview(_recordButton);

    SetupConstraints(blurView);
            SetupInputView();
            _loadingIndicator.StartAnimating();
   }

        private UIButton CreateOverlayButton(string iconName,UIColor tint)
        {
       var button = new UIButton(UIButtonType.System)
    {
 TranslatesAutoresizingMaskIntoConstraints = false,
         TintColor = tint
  };
    var config = UIImageSymbolConfiguration.Create(20, UIImageSymbolWeight.Medium);
   button.SetImage(UIImage.GetSystemImage(iconName, config), UIControlState.Normal);
            button.Layer.CornerRadius = 20;
 button.BackgroundColor = UIColor.FromWhiteAlpha(0.2f, 0.5f);
      return button;
 }

        private void SetupConstraints(UIVisualEffectView blurView)
        {
            NSLayoutConstraint.ActivateConstraints([
   // Stream view - full screen
  _streamView.TopAnchor.ConstraintEqualTo(View!.TopAnchor),
     _streamView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
     _streamView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
        _streamView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),

         // Controls overlay - top bar
    _controlsOverlay.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 8),
   _controlsOverlay.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor, -16),
         _controlsOverlay.HeightAnchor.ConstraintEqualTo(48),

         // Blur view fills overlay
    blurView.TopAnchor.ConstraintEqualTo(_controlsOverlay.TopAnchor),
blurView.BottomAnchor.ConstraintEqualTo(_controlsOverlay.BottomAnchor),
          blurView.LeadingAnchor.ConstraintEqualTo(_controlsOverlay.LeadingAnchor),
        blurView.TrailingAnchor.ConstraintEqualTo(_controlsOverlay.TrailingAnchor),

        // Close button - rightmost
                _closeButton.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
                _closeButton.TrailingAnchor.ConstraintEqualTo(_controlsOverlay.TrailingAnchor, -8),
   _closeButton.WidthAnchor.ConstraintEqualTo(40),
_closeButton.HeightAnchor.ConstraintEqualTo(40),

           // Menu button - left of close
    _menuButton.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
  _menuButton.TrailingAnchor.ConstraintEqualTo(_closeButton.LeadingAnchor, -8),
       _menuButton.WidthAnchor.ConstraintEqualTo(40),
      _menuButton.HeightAnchor.ConstraintEqualTo(40),

    // Screenshot button
         _screenshotButton!.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
  _screenshotButton.TrailingAnchor.ConstraintEqualTo(_menuButton.LeadingAnchor, -8),
_screenshotButton.WidthAnchor.ConstraintEqualTo(40),
          _screenshotButton.HeightAnchor.ConstraintEqualTo(40),

     // Record button
      _recordButton!.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
         _recordButton.TrailingAnchor.ConstraintEqualTo(_screenshotButton.LeadingAnchor, -8),
        _recordButton.WidthAnchor.ConstraintEqualTo(40),
     _recordButton.HeightAnchor.ConstraintEqualTo(40),

// Controls overlay width based on content
       _controlsOverlay.LeadingAnchor.ConstraintEqualTo(_recordButton.LeadingAnchor, -8),

           // Loading indicator
         _loadingIndicator.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
_loadingIndicator.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor),

      // Status label
       _statusLabel.TopAnchor.ConstraintEqualTo(_loadingIndicator.BottomAnchor, 16),
     _statusLabel.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
      ]);

          _controlsOverlay.Layer.CornerRadius = 24;
            _controlsOverlay.ClipsToBounds = true;
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

    NSLayoutConstraint.ActivateConstraints([
        _gamepadView.TopAnchor.ConstraintEqualTo(View.TopAnchor),
   _gamepadView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
             _gamepadView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
       _gamepadView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor)
            ]);

       _cursorView.Hidden = true;
  }
            else
            {
    _touchView = new TouchInputView
        {
    TranslatesAutoresizingMaskIntoConstraints = false,
    BackgroundColor = UIColor.Clear
         };
        _touchView.OnMouseMove += OnMouseMove;
        _touchView.OnMouseClick += OnMouseClick;
         _touchView.OnCursorMoved += OnCursorMoved;
          View!.InsertSubviewBelow(_touchView, _controlsOverlay);

         NSLayoutConstraint.ActivateConstraints([
         _touchView.TopAnchor.ConstraintEqualTo(View.TopAnchor),
  _touchView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
     _touchView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
      _touchView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor)
 ]);

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
            var modeIcon = _inputMode == StreamInputMode.Touch ? "gamecontroller" : "hand.tap";
            var modeText = _inputMode == StreamInputMode.Touch ? "Switch to Controller" : "Switch to Touch";
            alert.AddAction(UIAlertAction.Create(modeText, UIAlertActionStyle.Default, _ => ToggleInputMode()));

if (GyroscopeAimingService.Instance.IsAvailable)
  {
         var gyroText = _gyroEnabled ? "Disable Gyro Aiming" : "Enable Gyro Aiming";
       alert.AddAction(UIAlertAction.Create(gyroText, UIAlertActionStyle.Default, _ => ToggleGyroscope()));
   }

         if (PhysicalControllerService.Instance.IsControllerConnected)
         {
         alert.AddAction(UIAlertAction.Create(
           $"Connected: {PhysicalControllerService.Instance.ControllerName}",
          UIAlertActionStyle.Default, null));
     }

            alert.AddAction(UIAlertAction.Create("Quick Actions", UIAlertActionStyle.Default, _ => ShowQuickActionsMenu()));
            alert.AddAction(UIAlertAction.Create("Button Mapping", UIAlertActionStyle.Default, _ => ShowButtonMappingMenu()));

            var styleText = $"Style: {(_controllerStyle == ControllerStyle.Xbox ? "Xbox" : "PlayStation")}";
  alert.AddAction(UIAlertAction.Create(styleText, UIAlertActionStyle.Default, _ =>
    {
     _controllerStyle = _controllerStyle == ControllerStyle.Xbox ? ControllerStyle.PlayStation : ControllerStyle.Xbox;
    if (_inputMode == StreamInputMode.Controller) SetupInputView();
    SaveSettings();
            }));

            var sizeText = $"Size: {(_controllerSize == ControllerSize.Default ? "Default" : "Compact")}";
    alert.AddAction(UIAlertAction.Create(sizeText, UIAlertActionStyle.Default, _ =>
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
     var title = isActive ? $"{profile.Name} (Active)" : profile.Name;
         alert.AddAction(UIAlertAction.Create(title, UIAlertActionStyle.Default, _ =>
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
     BackgroundColor = UIColor.FromRGBA(0, 0, 0, 200),
      TextAlignment = UITextAlignment.Center,
 Font = UIFont.SystemFontOfSize(14, UIFontWeight.Medium),
     Alpha = 0
      };
         toast.Layer.CornerRadius = 12;
     toast.ClipsToBounds = true;
   toast.SizeToFit();
            toast.Frame = new CGRect(0, 0, toast.Frame.Width + 32, 40);
            toast.Center = new CGPoint(View!.Bounds.Width / 2, View.Bounds.Height - 120);
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

        private void OnStreamConnected() => InvokeOnMainThread(() =>
        {
            _loadingIndicator.StopAnimating();
       _statusLabel.Hidden = true;
        });

        private void OnStreamDisconnected(string reason) => InvokeOnMainThread(() =>
        {
  _statusLabel.Hidden = false;
    _statusLabel.Text = $"Disconnected: {reason}";
        });

        private void OnStreamError(string error) => InvokeOnMainThread(() =>
        {
     _statusLabel.Hidden = false;
       _statusLabel.Text = $"Error: {error}";
        });

   // Touch mode handlers
    private void OnMouseMove(float normalizedX, float normalizedY)
        {
     _ = StreamingClient.Instance.SendMouseMoveAsync(normalizedX, normalizedY);
 }

        private void OnMouseClick(bool left, bool right, bool down)
        {
        _ = StreamingClient.Instance.SendMouseClickAsync(left, right, down);
  }

 private void OnCursorMoved(CGPoint position) => InvokeOnMainThread(() => _cursorView.Center = position);

        private void OnGamepadStateChanged(GamepadState state)
        {
        if (_physicalControllerPriority && PhysicalControllerService.Instance.IsControllerConnected) return;
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
         var config = UIImageSymbolConfiguration.Create(20, UIImageSymbolWeight.Medium);
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

    /// <summary>
    /// Touch input view for mouse control mode
    /// </summary>
    public class TouchInputView : UIView
    {
        public event Action<float, float>? OnMouseMove;
     public event Action<bool, bool, bool>? OnMouseClick; // left, right, down
        public event Action<CGPoint>? OnCursorMoved;

        private CGPoint _cursorPosition;
      private CGPoint _lastTouchPosition;
        private bool _isDragging;
    private const float Sensitivity = 1.5f;
        private DateTime _lastTapTime = DateTime.MinValue;
        private bool _isLeftDown;

        public TouchInputView()
        {
            MultipleTouchEnabled = true;
   UserInteractionEnabled = true;

            // Single tap for left click
    var tap = new UITapGestureRecognizer(OnTap);
            tap.NumberOfTapsRequired = 1;
    AddGestureRecognizer(tap);

  // Double tap for double click
            var doubleTap = new UITapGestureRecognizer(OnDoubleTap);
    doubleTap.NumberOfTapsRequired = 2;
     AddGestureRecognizer(doubleTap);
    tap.RequireGestureRecognizerToFail(doubleTap);

         // Two finger tap for right click
    var twoFingerTap = new UITapGestureRecognizer(OnRightClick);
         twoFingerTap.NumberOfTouchesRequired = 2;
    AddGestureRecognizer(twoFingerTap);

  // Long press for drag
            var longPress = new UILongPressGestureRecognizer(OnLongPress);
     longPress.MinimumPressDuration = 0.3;
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

          // Send mouse move
    var normalizedX = (float)(_cursorPosition.X / Bounds.Width);
       var normalizedY = (float)(_cursorPosition.Y / Bounds.Height);
    OnMouseMove?.Invoke(normalizedX, normalizedY);
            }
    }

        public override void TouchesEnded(NSSet touches, UIEvent? evt)
    {
            _isDragging = false;
 if (_isLeftDown)
            {
                _isLeftDown = false;
    OnMouseClick?.Invoke(true, false, false); // left up
      }
        }

        private void OnTap(UITapGestureRecognizer g)
        {
      // Left click
            OnMouseClick?.Invoke(true, false, true);// left down
   OnMouseClick?.Invoke(true, false, false); // left up
   }

        private void OnDoubleTap(UITapGestureRecognizer g)
        {
       // Double click
            OnMouseClick?.Invoke(true, false, true);
          OnMouseClick?.Invoke(true, false, false);
            OnMouseClick?.Invoke(true, false, true);
      OnMouseClick?.Invoke(true, false, false);
        }

        private void OnRightClick(UITapGestureRecognizer g)
        {
   // Right click
       OnMouseClick?.Invoke(false, true, true);
          OnMouseClick?.Invoke(false, true, false);
        }

        private void OnLongPress(UILongPressGestureRecognizer g)
        {
 if (g.State == UIGestureRecognizerState.Began)
            {
          _isLeftDown = true;
  OnMouseClick?.Invoke(true, false, true); // left down for drag
 }
     }
    }

    /// <summary>
 /// Virtual gamepad view with modern design
    /// </summary>
    public class VirtualGamepadView : UIView
    {
        private readonly ControllerStyle _style;
   private readonly ControllerSize _size;
        public event Action<GamepadState>? OnStateChanged;
 private GamepadState _state = new();

        // Controls
private VirtualStickView? _leftStick;
        private VirtualStickView? _rightStick;
        private UIButton? _buttonA, _buttonB, _buttonX, _buttonY;
        private UIButton? _dpadUp, _dpadDown, _dpadLeft, _dpadRight;
        private UIButton? _leftBumper, _rightBumper;
        private UIButton? _leftTrigger, _rightTrigger;
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
       var stickSize = isMinimal ? 90 : 120;
          var buttonSize = isMinimal ? 42 : 52;
            var smallButtonSize = isMinimal ? 34 : 42;

       // Left analog stick
      _leftStick = new VirtualStickView(stickSize);
        _leftStick.OnStickMoved += (x, y) =>
      {
         _state.LeftStickX = x;
              _state.LeftStickY = y;
        NotifyStateChanged();
   };
       AddSubview(_leftStick);

            // Right analog stick
        _rightStick = new VirtualStickView(stickSize);
    _rightStick.OnStickMoved += (x, y) =>
            {
      _state.RightStickX = x;
           _state.RightStickY = y;
        NotifyStateChanged();
     };
    AddSubview(_rightStick);

            // Face buttons with SF Symbols
    var (aIcon, bIcon, xIcon, yIcon) = _style == ControllerStyle.Xbox
    ? ("a.circle.fill", "b.circle.fill", "x.circle.fill", "y.circle.fill")
                : ("xmark.circle.fill", "circle.circle.fill", "square.fill", "triangle.fill");

            var (aColor, bColor, xColor, yColor) = _style == ControllerStyle.Xbox
           ? (UIColor.FromRGB(96, 185, 58), UIColor.FromRGB(221, 68, 59), UIColor.FromRGB(63, 130, 205), UIColor.FromRGB(245, 199, 72))
         : (UIColor.FromRGB(100, 150, 255), UIColor.FromRGB(255, 100, 120), UIColor.FromRGB(255, 120, 200), UIColor.FromRGB(100, 220, 180));

  _buttonA = CreateFaceButton(aIcon, aColor, buttonSize, GamepadButtons.A);
            _buttonB = CreateFaceButton(bIcon, bColor, buttonSize, GamepadButtons.B);
   _buttonX = CreateFaceButton(xIcon, xColor, buttonSize, GamepadButtons.X);
            _buttonY = CreateFaceButton(yIcon, yColor, buttonSize, GamepadButtons.Y);

            AddSubview(_buttonA);
       AddSubview(_buttonB);
        AddSubview(_buttonX);
   AddSubview(_buttonY);

    // D-Pad with SF Symbols
     _dpadUp = CreateDPadButton("chevron.up", smallButtonSize, GamepadButtons.DPadUp);
            _dpadDown = CreateDPadButton("chevron.down", smallButtonSize, GamepadButtons.DPadDown);
            _dpadLeft = CreateDPadButton("chevron.left", smallButtonSize, GamepadButtons.DPadLeft);
 _dpadRight = CreateDPadButton("chevron.right", smallButtonSize, GamepadButtons.DPadRight);

            AddSubview(_dpadUp);
            AddSubview(_dpadDown);
 AddSubview(_dpadLeft);
    AddSubview(_dpadRight);

            // Bumpers
            _leftBumper = CreateShoulderButton("LB", 65, 32, GamepadButtons.LeftBumper);
       _rightBumper = CreateShoulderButton("RB", 65, 32, GamepadButtons.RightBumper);
            AddSubview(_leftBumper);
     AddSubview(_rightBumper);

       // Triggers
   _leftTrigger = CreateTriggerButton("LT", 55, 36, true);
   _rightTrigger = CreateTriggerButton("RT", 55, 36, false);
            AddSubview(_leftTrigger);
   AddSubview(_rightTrigger);

      // Menu buttons with SF Symbols
            _startButton = CreateMenuButton("line.3.horizontal", smallButtonSize - 6, GamepadButtons.Start);
          _backButton = CreateMenuButton("square.on.square", smallButtonSize - 6, GamepadButtons.Back);
          AddSubview(_startButton);
    AddSubview(_backButton);
     }

        private UIButton CreateFaceButton(string iconName, UIColor color, int size, GamepadButtons button)
        {
   var btn = new UIButton(UIButtonType.Custom)
  {
 TranslatesAutoresizingMaskIntoConstraints = false,
    BackgroundColor = color.ColorWithAlpha(0.85f),
  Frame = new CGRect(0, 0, size, size)
     };
   var config = UIImageSymbolConfiguration.Create(size * 0.5f, UIImageSymbolWeight.Bold);
    var image = UIImage.GetSystemImage(iconName, config);
      btn.SetImage(image, UIControlState.Normal);
            btn.TintColor = UIColor.White;
            btn.Layer.CornerRadius = size / 2;
   btn.Layer.ShadowColor = UIColor.Black.CGColor;
          btn.Layer.ShadowOffset = new CGSize(0, 2);
         btn.Layer.ShadowRadius = 4;
            btn.Layer.ShadowOpacity = 0.3f;

            btn.TouchDown += (s, e) =>
    {
   _state.Buttons |= button;
         NotifyStateChanged();
UIView.Animate(0.1, () => btn.Transform = CGAffineTransform.MakeScale(0.9f, 0.9f));
 };
      btn.TouchUpInside += (s, e) => ReleaseButton(btn, button);
    btn.TouchUpOutside += (s, e) => ReleaseButton(btn, button);
        btn.TouchCancel += (s, e) => ReleaseButton(btn, button);

     return btn;
    }

        private void ReleaseButton(UIButton btn, GamepadButtons button)
      {
            _state.Buttons &= ~button;
         NotifyStateChanged();
   UIView.Animate(0.1, () => btn.Transform = CGAffineTransform.MakeIdentity());
        }

        private UIButton CreateDPadButton(string iconName, int size, GamepadButtons button)
 {
    var btn = new UIButton(UIButtonType.Custom)
      {
     TranslatesAutoresizingMaskIntoConstraints = false,
           BackgroundColor = UIColor.FromWhiteAlpha(0.3f, 0.8f),
        Frame = new CGRect(0, 0, size, size)
 };
   var config = UIImageSymbolConfiguration.Create(size * 0.4f, UIImageSymbolWeight.Bold);
            var image = UIImage.GetSystemImage(iconName, config);
            btn.SetImage(image, UIControlState.Normal);
    btn.TintColor = UIColor.White;
 btn.Layer.CornerRadius = 8;

     btn.TouchDown += (s, e) =>
   {
        _state.Buttons |= button;
      NotifyStateChanged();
          btn.BackgroundColor = UIColor.FromWhiteAlpha(0.5f, 0.9f);
 };
          btn.TouchUpInside += (s, e) => ReleaseDPad(btn, button);
   btn.TouchUpOutside += (s, e) => ReleaseDPad(btn, button);
        btn.TouchCancel += (s, e) => ReleaseDPad(btn, button);

 return btn;
     }

    private void ReleaseDPad(UIButton btn, GamepadButtons button)
        {
            _state.Buttons &= ~button;
      NotifyStateChanged();
  btn.BackgroundColor = UIColor.FromWhiteAlpha(0.3f, 0.8f);
        }

        private UIButton CreateShoulderButton(string label, int width, int height, GamepadButtons button)
     {
            var btn = new UIButton(UIButtonType.Custom)
{
  TranslatesAutoresizingMaskIntoConstraints = false,
        BackgroundColor = UIColor.FromWhiteAlpha(0.25f, 0.85f),
       Frame = new CGRect(0, 0, width, height)
    };
            btn.SetTitle(label, UIControlState.Normal);
      btn.SetTitleColor(UIColor.White, UIControlState.Normal);
            btn.TitleLabel!.Font = UIFont.BoldSystemFontOfSize(13);
  btn.Layer.CornerRadius = height / 2;

            btn.TouchDown += (s, e) =>
    {
       _state.Buttons |= button;
         NotifyStateChanged();
 btn.BackgroundColor = UIColor.FromWhiteAlpha(0.45f, 0.9f);
            };
         btn.TouchUpInside += (s, e) => ReleaseShoulder(btn, button);
  btn.TouchUpOutside += (s, e) => ReleaseShoulder(btn, button);
            btn.TouchCancel += (s, e) => ReleaseShoulder(btn, button);

         return btn;
        }

        private void ReleaseShoulder(UIButton btn, GamepadButtons button)
        {
            _state.Buttons &= ~button;
      NotifyStateChanged();
  btn.BackgroundColor = UIColor.FromWhiteAlpha(0.25f, 0.85f);
        }

        private UIButton CreateTriggerButton(string label, int width, int height, bool isLeft)
        {
   var btn = new UIButton(UIButtonType.Custom)
            {
      TranslatesAutoresizingMaskIntoConstraints = false,
      BackgroundColor = UIColor.FromWhiteAlpha(0.2f, 0.85f),
    Frame = new CGRect(0, 0, width, height)
            };
            btn.SetTitle(label, UIControlState.Normal);
          btn.SetTitleColor(UIColor.White, UIControlState.Normal);
            btn.TitleLabel!.Font = UIFont.BoldSystemFontOfSize(12);
            btn.Layer.CornerRadius = 8;

            btn.TouchDown += (s, e) =>
 {
           if (isLeft) _state.LeftTrigger = 1.0f;
      else _state.RightTrigger = 1.0f;
  NotifyStateChanged();
      btn.BackgroundColor = UIColor.FromWhiteAlpha(0.4f, 0.9f);
            };
            btn.TouchUpInside += (s, e) => ReleaseTrigger(btn, isLeft);
          btn.TouchUpOutside += (s, e) => ReleaseTrigger(btn, isLeft);
      btn.TouchCancel += (s, e) => ReleaseTrigger(btn, isLeft);

return btn;
    }

        private void ReleaseTrigger(UIButton btn, bool isLeft)
        {
            if (isLeft) _state.LeftTrigger = 0f;
     else _state.RightTrigger = 0f;
         NotifyStateChanged();
    btn.BackgroundColor = UIColor.FromWhiteAlpha(0.2f, 0.85f);
        }

        private UIButton CreateMenuButton(string iconName, int size, GamepadButtons button)
        {
     var btn = new UIButton(UIButtonType.Custom)
        {
    TranslatesAutoresizingMaskIntoConstraints = false,
         BackgroundColor = UIColor.FromWhiteAlpha(0.15f, 0.7f),
     Frame = new CGRect(0, 0, size, size)
     };
 var config = UIImageSymbolConfiguration.Create(size * 0.4f, UIImageSymbolWeight.Medium);
     var image = UIImage.GetSystemImage(iconName, config);
         btn.SetImage(image, UIControlState.Normal);
          btn.TintColor = UIColor.White.ColorWithAlpha(0.8f);
     btn.Layer.CornerRadius = size / 2;

        btn.TouchDown += (s, e) => { _state.Buttons |= button; NotifyStateChanged(); };
            btn.TouchUpInside += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); };
       btn.TouchUpOutside += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); };
          btn.TouchCancel += (s, e) => { _state.Buttons &= ~button; NotifyStateChanged(); };

        return btn;
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();

   var safeArea = SafeAreaInsets;
            var isMinimal = _size == ControllerSize.Minimal;
            var stickSize = isMinimal ? 90 : 120;
  var buttonSize = isMinimal ? 42 : 52;
            var margin = isMinimal ? 15 : 25;
            var bottomMargin = safeArea.Bottom + 20;

       // Left stick - bottom left
            if (_leftStick != null)
   {
      _leftStick.Frame = new CGRect(
     safeArea.Left + margin,
         Bounds.Height - stickSize - bottomMargin,
      stickSize,
       stickSize);
            }

         // D-Pad - above left stick
  var dpadCenterX = safeArea.Left + margin + stickSize / 2;
     var dpadCenterY = Bounds.Height - stickSize - bottomMargin - 70;
            var dpadButtonSize = isMinimal ? 34 : 42;
            var dpadSpacing = dpadButtonSize + 4;

 if (_dpadUp != null) _dpadUp.Center = new CGPoint(dpadCenterX, dpadCenterY - dpadSpacing / 2);
       if (_dpadDown != null) _dpadDown.Center = new CGPoint(dpadCenterX, dpadCenterY + dpadSpacing / 2);
 if (_dpadLeft != null) _dpadLeft.Center = new CGPoint(dpadCenterX - dpadSpacing / 2, dpadCenterY);
            if (_dpadRight != null) _dpadRight.Center = new CGPoint(dpadCenterX + dpadSpacing / 2, dpadCenterY);

     // Right stick - bottom right
        if (_rightStick != null)
            {
       _rightStick.Frame = new CGRect(
    Bounds.Width - stickSize - safeArea.Right - margin,
        Bounds.Height - stickSize - bottomMargin,
         stickSize,
     stickSize);
     }

            // Face buttons - diamond layout above right stick
            var faceCenterX = Bounds.Width - stickSize / 2 - safeArea.Right - margin;
            var faceCenterY = Bounds.Height - stickSize - bottomMargin - 70;
    var faceSpacing = buttonSize + 8;

       if (_buttonA != null) _buttonA.Center = new CGPoint(faceCenterX, faceCenterY + faceSpacing / 2);
 if (_buttonB != null) _buttonB.Center = new CGPoint(faceCenterX + faceSpacing / 2, faceCenterY);
      if (_buttonX != null) _buttonX.Center = new CGPoint(faceCenterX - faceSpacing / 2, faceCenterY);
     if (_buttonY != null) _buttonY.Center = new CGPoint(faceCenterX, faceCenterY - faceSpacing / 2);

            // Bumpers - top sides
        var shoulderY = safeArea.Top + 70;
   if (_leftBumper != null) _leftBumper.Center = new CGPoint(safeArea.Left + 50, shoulderY);
            if (_rightBumper != null) _rightBumper.Center = new CGPoint(Bounds.Width - safeArea.Right - 50, shoulderY);

        // Triggers - above bumpers
        var triggerY = safeArea.Top + 110;
            if (_leftTrigger != null) _leftTrigger.Center = new CGPoint(safeArea.Left + 45, triggerY);
       if (_rightTrigger != null) _rightTrigger.Center = new CGPoint(Bounds.Width - safeArea.Right - 45, triggerY);

            // Menu buttons - center
  var menuY = safeArea.Top + 90;
            if (_backButton != null) _backButton.Center = new CGPoint(Bounds.Width / 2 - 35, menuY);
        if (_startButton != null) _startButton.Center = new CGPoint(Bounds.Width / 2 + 35, menuY);
   }

        private void NotifyStateChanged() => OnStateChanged?.Invoke(_state);
    }

    /// <summary>
    /// Virtual analog stick with modern design
    /// </summary>
    public class VirtualStickView : UIView
    {
        public event Action<float, float>? OnStickMoved;

    private UIView _thumbView;
        private UIView _trackView;
        private CGPoint _centerPoint;
  private readonly nfloat _maxDistance;
        private bool _isTracking;

        public VirtualStickView(int size)
 {
   _maxDistance = size / 2 - 25;
 Frame = new CGRect(0, 0, size, size);

      // Track (outer circle)
  _trackView = new UIView
        {
  Frame = new CGRect(0, 0, size, size),
       BackgroundColor = UIColor.FromWhiteAlpha(0.15f, 0.6f)
            };
       _trackView.Layer.CornerRadius = size / 2;
            _trackView.Layer.BorderColor = UIColor.FromWhiteAlpha(0.3f, 0.5f).CGColor;
 _trackView.Layer.BorderWidth = 2;
   AddSubview(_trackView);

            // Thumb (inner circle)
       var thumbSize = size * 0.45;
    _thumbView = new UIView
            {
       Frame = new CGRect(0, 0, thumbSize, thumbSize),
         BackgroundColor = UIColor.FromWhiteAlpha(0.9f, 0.9f)
            };
  _thumbView.Layer.CornerRadius = (nfloat)(thumbSize / 2);
      _thumbView.Layer.ShadowColor = UIColor.Black.CGColor;
    _thumbView.Layer.ShadowOffset = new CGSize(0, 2);
 _thumbView.Layer.ShadowRadius = 4;
      _thumbView.Layer.ShadowOpacity = 0.4f;
         AddSubview(_thumbView);
        }

        public override void LayoutSubviews()
        {
    base.LayoutSubviews();
        _centerPoint = new CGPoint(Bounds.Width / 2, Bounds.Height / 2);
 if (!_isTracking)
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
