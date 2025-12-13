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
    private UIButton _modeButton = null!;
 private UIButton _settingsButton = null!;
        private UIView _controlsOverlay = null!;
        private UIView _cursorView = null!;
        private StatsOverlayView? _statsOverlay;
     private UIButton? _screenshotButton;
        private UIButton? _recordButton;

        private string _host;
        private int _port;
        private string? _authToken;
        private StreamInputMode _inputMode = StreamInputMode.Controller;
  private ControllerStyle _controllerStyle = ControllerStyle.Xbox;
        private ControllerSize _controllerSize = ControllerSize.Default;
        private bool _controlsVisible = true;
        private bool _statsVisible = true;
 private bool _gyroEnabled = false;
        private bool _physicalControllerPriority = true;
        private byte[]? _lastFrameData;
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

            _statsOverlay = new StatsOverlayView { TranslatesAutoresizingMaskIntoConstraints = false };
  View.AddSubview(_statsOverlay);

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

_modeButton = CreateOverlayButton(_inputMode == StreamInputMode.Touch ? "hand.tap.fill" : "gamecontroller.fill", UIColor.White);
        _modeButton.TouchUpInside += (s, e) => ToggleInputMode();
            _controlsOverlay.AddSubview(_modeButton);

      _settingsButton = CreateOverlayButton("gearshape.fill", UIColor.White);
 _settingsButton.TouchUpInside += (s, e) => ShowSettingsMenu();
 _controlsOverlay.AddSubview(_settingsButton);

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

     _settingsButton.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
      _settingsButton.TrailingAnchor.ConstraintEqualTo(_closeButton.LeadingAnchor, -8),
 _settingsButton.WidthAnchor.ConstraintEqualTo(44),
                _settingsButton.HeightAnchor.ConstraintEqualTo(44),

     _modeButton.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
   _modeButton.TrailingAnchor.ConstraintEqualTo(_settingsButton.LeadingAnchor, -8),
           _modeButton.WidthAnchor.ConstraintEqualTo(44),
     _modeButton.HeightAnchor.ConstraintEqualTo(44),

         _screenshotButton!.CenterYAnchor.ConstraintEqualTo(_controlsOverlay.CenterYAnchor),
     _screenshotButton.TrailingAnchor.ConstraintEqualTo(_modeButton.LeadingAnchor, -8),
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

 _statsOverlay!.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor, 8),
_statsOverlay.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor, 16),
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

    alert.AddAction(UIAlertAction.Create(_statsVisible ? "Hide Stats ?" : "Show Stats",
    UIAlertActionStyle.Default, _ => { _statsVisible = !_statsVisible; _statsOverlay!.Hidden = !_statsVisible; }));

            if (GyroscopeAimingService.Instance.IsAvailable)
       alert.AddAction(UIAlertAction.Create(_gyroEnabled ? "Disable Gyro ?" : "Enable Gyro",
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
          alert.PopoverPresentationController.SourceView = _settingsButton;
       alert.PopoverPresentationController.SourceRect = _settingsButton.Bounds;
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
                alert.PopoverPresentationController.SourceView = _settingsButton;
    alert.PopoverPresentationController.SourceRect = _settingsButton.Bounds;
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
       _statsOverlay?.RecordFrame(frameData.Length);
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

    public class StatsOverlayView : UIView
    {
        private UILabel _fpsLabel = null!;
    private UILabel _latencyLabel = null!;
        private UILabel _bitrateLabel = null!;
        private int _frameCount;
        private long _totalBytes;
  private DateTime _lastUpdate = DateTime.Now;
private DateTime _lastFrameTime = DateTime.Now;
        private double _currentFps, _currentBitrate, _estimatedLatency;

        public StatsOverlayView()
        {
        BackgroundColor = UIColor.FromRGBA(0, 0, 0, 150);
            Layer.CornerRadius = 8;
       var stack = new UIStackView { Axis = UILayoutConstraintAxis.Vertical, Spacing = 4, TranslatesAutoresizingMaskIntoConstraints = false };
   AddSubview(stack);
       _fpsLabel = CreateLabel("FPS: --");
     _latencyLabel = CreateLabel("Latency: --ms");
      _bitrateLabel = CreateLabel("Bitrate: --");
  stack.AddArrangedSubview(_fpsLabel);
            stack.AddArrangedSubview(_latencyLabel);
          stack.AddArrangedSubview(_bitrateLabel);
NSLayoutConstraint.ActivateConstraints(new[]
            {
      stack.TopAnchor.ConstraintEqualTo(TopAnchor, 8),
     stack.BottomAnchor.ConstraintEqualTo(BottomAnchor, -8),
       stack.LeadingAnchor.ConstraintEqualTo(LeadingAnchor, 12),
        stack.TrailingAnchor.ConstraintEqualTo(TrailingAnchor, -12),
            });
        }

        private UILabel CreateLabel(string text) => new UILabel
        {
            Text = text,
     TextColor = UIColor.White,
   Font = UIFont.SystemFontOfSize(11, UIFontWeight.Medium)
  };

        public void RecordFrame(int frameSize)
    {
     _frameCount++;
      _totalBytes += frameSize;
            var now = DateTime.Now;
     var frameDelta = (now - _lastFrameTime).TotalMilliseconds;
 _lastFrameTime = now;
        _estimatedLatency = _estimatedLatency * 0.9 + frameDelta * 0.1;
 var elapsed = (now - _lastUpdate).TotalSeconds;
       if (elapsed >= 1.0)
     {
           _currentFps = _frameCount / elapsed;
         _currentBitrate = (_totalBytes * 8) / elapsed / 1_000_000;
        _frameCount = 0;
      _totalBytes = 0;
    _lastUpdate = now;
       UpdateLabels();
            }
     }

        private void UpdateLabels()
  {
  _fpsLabel.Text = $"FPS: {_currentFps:F1}";
            _fpsLabel.TextColor = _currentFps >= 55 ? UIColor.SystemGreen : _currentFps >= 30 ? UIColor.SystemYellow : UIColor.SystemRed;
     _latencyLabel.Text = $"Latency: {_estimatedLatency:F0}ms";
            _latencyLabel.TextColor = _estimatedLatency <= 50 ? UIColor.SystemGreen : _estimatedLatency <= 100 ? UIColor.SystemYellow : UIColor.SystemRed;
            _bitrateLabel.Text = $"Bitrate: {(_currentBitrate >= 1 ? $"{_currentBitrate:F1} Mbps" : $"{_currentBitrate * 1000:F0} Kbps")}";
 }
    }

    public class VirtualGamepadView : UIView
    {
        private readonly ControllerStyle _style;
        private readonly ControllerSize _size;
   public event Action<GamepadState>? OnStateChanged;
        private GamepadState _state = new();

        public VirtualGamepadView(ControllerStyle style, ControllerSize size)
   {
       _style = style;
    _size = size;
MultipleTouchEnabled = true;
 // Virtual gamepad controls setup - simplified placeholder
        }

     public override void LayoutSubviews()
  {
      base.LayoutSubviews();
     }

     private void NotifyStateChanged() => OnStateChanged?.Invoke(_state);
    }
}
