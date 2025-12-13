using UIKit;
using CoreGraphics;
using Foundation;
using gaming_hub.Services;
using CoreHaptics;
using AudioToolbox;

namespace gaming_hub.ViewControllers
{
    public enum StreamInputMode { Touch, Controller }
    public enum ControllerStyle { Xbox, PlayStation }
    public enum ControllerSize { Minimal, Default, Large }

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
        private bool _audioEnabled = true;
        private float _audioVolume = 1.0f;
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
            _ = StartAudioStreamAsync();
        }

        public override void ViewWillDisappear(bool animated)
        {
            base.ViewWillDisappear(animated);
            _hideTimer?.Invalidate();
            CleanupNewFeatures();
            _ = StopStreamingAsync();
            _ = AudioStreamClient.Instance.DisconnectAsync();
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

        private UIButton CreateOverlayButton(string iconName, UIColor tint)
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
                _touchView = new TouchInputView(StreamingClient.Instance.StreamWidth, StreamingClient.Instance.StreamHeight)
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

            // Add tap gesture to show controls when hidden
            var showControlsTap = new UITapGestureRecognizer(() => ShowControls())
            {
                NumberOfTapsRequired = 1,
                NumberOfTouchesRequired = 1
            };
            _streamView.UserInteractionEnabled = true;
            _streamView.AddGestureRecognizer(showControlsTap);
        }

        private void StartHideTimer()
        {
            _hideTimer = NSTimer.CreateRepeatingScheduledTimer(3.0, t =>
        {
    if ((DateTime.Now - _lastInteraction).TotalSeconds > 8 && _controlsVisible)
        HideControls();
            });
        }

        private void ShowControls()
        {
            _lastInteraction = DateTime.Now;
            if (!_controlsVisible)
            {
      _controlsVisible = true;
         _controlsOverlay.Hidden = false;
 UIView.Animate(0.3, () => _controlsOverlay.Alpha = 1);
 }
        }

        private void HideControls()
        {
  _controlsVisible = false;
     UIView.Animate(0.3, () => _controlsOverlay.Alpha = 0);
            // Don't set Hidden = true so tap gesture still works
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
            var modeText = _inputMode == StreamInputMode.Touch ? "Switch to Controller" : "Switch to Touch";
            alert.AddAction(UIAlertAction.Create(modeText, UIAlertActionStyle.Default, _ => ToggleInputMode()));

            // Audio toggle
            var audioText = _audioEnabled ? "?? Audio: On" : "?? Audio: Off";
            alert.AddAction(UIAlertAction.Create(audioText, UIAlertActionStyle.Default, _ => ToggleAudio()));

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

            // Updated size options with Large
            var sizeText = $"Size: {_controllerSize}";
            alert.AddAction(UIAlertAction.Create(sizeText, UIAlertActionStyle.Default, _ =>
            {
                _controllerSize = _controllerSize switch
                {
                    ControllerSize.Minimal => ControllerSize.Default,
                    ControllerSize.Default => ControllerSize.Large,
                    ControllerSize.Large => ControllerSize.Minimal,
                    _ => ControllerSize.Default
                };
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

        private void ToggleAudio()
        {
            _audioEnabled = !_audioEnabled;

            if (_audioEnabled)
            {
                _ = StartAudioStreamAsync();
                ShowToast("Audio enabled");
            }
            else
            {
                _ = AudioStreamClient.Instance.DisconnectAsync();
                ShowToast("Audio disabled");
            }
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

        private async Task StartAudioStreamAsync()
        {
       if (!_audioEnabled) return;

        try
            {
          // Audio stream is on port + 1 (e.g., 5002 video -> 5003 audio)
         var audioPort = _port + 1;
        var connected = await AudioStreamClient.Instance.ConnectAsync(_host, audioPort);
 if (connected)
     {
AudioStreamClient.Instance.SetVolume(_audioVolume);
          Console.WriteLine("Audio stream connected");
      }
           else
        {
       Console.WriteLine("Audio stream not available");
      }
   }
      catch (Exception ex)
            {
         Console.WriteLine($"Failed to connect audio stream: {ex.Message}");
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
    /// Touch input view for mouse control mode - Completely rewritten for reliable tracking
    /// </summary>
    public class TouchInputView : UIView
    {
   public event Action<float, float>? OnMouseMove;
   public event Action<bool, bool, bool>? OnMouseClick;
        public event Action<CGPoint>? OnCursorMoved;

        // Cursor state
      private CGPoint _virtualCursorPos;
        private CGPoint _previousTouchPos;
        private bool _isMoving;

        // Settings
        private readonly float _sensitivity = 1.5f;
 private readonly float _acceleration = 1.2f;

        // Click handling
        private DateTime _lastTapTime = DateTime.MinValue;
        private bool _isDragging;
        private NSTimer? _longPressTimer;

        // Stream dimensions for coordinate mapping
        private readonly int _streamWidth;
        private readonly int _streamHeight;

        public TouchInputView(int streamWidth = 1920, int streamHeight = 1080)
        {
       _streamWidth = streamWidth > 0 ? streamWidth : 1920;
            _streamHeight = streamHeight > 0 ? streamHeight : 1080;

     MultipleTouchEnabled = true;
    UserInteractionEnabled = true;
  BackgroundColor = UIColor.Clear;
        }

  public override void LayoutSubviews()
      {
      base.LayoutSubviews();
          
       // Initialize cursor at center if not set
            if (_virtualCursorPos == CGPoint.Empty)
          {
  _virtualCursorPos = new CGPoint(Bounds.Width / 2, Bounds.Height / 2);
OnCursorMoved?.Invoke(_virtualCursorPos);
            }
        }

        public override void TouchesBegan(NSSet touches, UIEvent? evt)
        {
            base.TouchesBegan(touches, evt);

     if (touches.AnyObject is UITouch touch)
       {
           _previousTouchPos = touch.LocationInView(this);
       _isMoving = true;

                var touchCount = evt?.AllTouches?.Count ?? 1;

       // Two finger tap = right click
       if (touchCount >= 2)
     {
    PerformRightClick();
        return;
     }

       // Start long press timer for drag
     _longPressTimer?.Invalidate();
 _longPressTimer = NSTimer.CreateScheduledTimer(0.4, false, _ =>
         {
     if (_isMoving)
    {
               _isDragging = true;
OnMouseClick?.Invoke(true, false, true); // Left down
              PlayHaptic(UIImpactFeedbackStyle.Medium);
  }
       });
        }
        }

        public override void TouchesMoved(NSSet touches, UIEvent? evt)
        {
         base.TouchesMoved(touches, evt);

        if (!_isMoving) return;
 if (touches.AnyObject is not UITouch touch) return;

       var currentPos = touch.LocationInView(this);
            
    // Calculate delta with sensitivity and acceleration
  var rawDeltaX = currentPos.X - _previousTouchPos.X;
   var rawDeltaY = currentPos.Y - _previousTouchPos.Y;
            
    // Apply acceleration based on movement speed
      var speed = Math.Sqrt((double)(rawDeltaX * rawDeltaX + rawDeltaY * rawDeltaY));
     var accelFactor = 1.0 + (speed / 50.0) * (_acceleration - 1.0);
      accelFactor = Math.Min(accelFactor, 2.5);

   var deltaX = rawDeltaX * _sensitivity * (nfloat)accelFactor;
var deltaY = rawDeltaY * _sensitivity * (nfloat)accelFactor;

    // Update virtual cursor position with bounds clamping
            var newX = Math.Clamp(_virtualCursorPos.X + deltaX, 0, Bounds.Width);
            var newY = Math.Clamp(_virtualCursorPos.Y + deltaY, 0, Bounds.Height);
       _virtualCursorPos = new CGPoint(newX, newY);

            _previousTouchPos = currentPos;

 // Notify cursor moved (for visual feedback)
   OnCursorMoved?.Invoke(_virtualCursorPos);

            // Send normalized mouse position to PC
 var normalizedX = (float)(_virtualCursorPos.X / Bounds.Width);
            var normalizedY = (float)(_virtualCursorPos.Y / Bounds.Height);
            
            normalizedX = Math.Clamp(normalizedX, 0f, 1f);
  normalizedY = Math.Clamp(normalizedY, 0f, 1f);

       OnMouseMove?.Invoke(normalizedX, normalizedY);

   // Cancel long press if moved too much
     if (speed > 10)
          {
      _longPressTimer?.Invalidate();
         _longPressTimer = null;
      }
        }

    public override void TouchesEnded(NSSet touches, UIEvent? evt)
        {
      base.TouchesEnded(touches, evt);

            _longPressTimer?.Invalidate();
          _longPressTimer = null;

    // Release drag if active
     if (_isDragging)
       {
    _isDragging = false;
   OnMouseClick?.Invoke(true, false, false); // Left up
 PlayHaptic(UIImpactFeedbackStyle.Light);
           _isMoving = false;
    return;
        }

            _isMoving = false;

            // Check for tap (quick release without much movement)
       if (touches.AnyObject is UITouch touch)
            {
  var endPos = touch.LocationInView(this);
    var moveDistance = Math.Sqrt(
             Math.Pow((double)(endPos.X - _previousTouchPos.X), 2) +
   Math.Pow((double)(endPos.Y - _previousTouchPos.Y), 2));

    // Single tap - left click
          if (moveDistance < 15)
    {
   var now = DateTime.Now;
   var timeSinceLastTap = (now - _lastTapTime).TotalMilliseconds;

 if (timeSinceLastTap < 300)
           {
 // Double tap - double click
      PerformDoubleClick();
           _lastTapTime = DateTime.MinValue;
               }
else
  {
           // Single tap - left click
    PerformLeftClick();
            _lastTapTime = now;
           }
       }
            }
        }

  public override void TouchesCancelled(NSSet touches, UIEvent? evt)
        {
         base.TouchesCancelled(touches, evt);

     _longPressTimer?.Invalidate();
            _longPressTimer = null;

  if (_isDragging)
{
                _isDragging = false;
                OnMouseClick?.Invoke(true, false, false); // Left up
  }

            _isMoving = false;
        }

  private void PerformLeftClick()
      {
        PlayHaptic(UIImpactFeedbackStyle.Light);
   OnMouseClick?.Invoke(true, false, true);  // Left down
         
            Task.Delay(30).ContinueWith(_ =>
            {
     InvokeOnMainThread(() => OnMouseClick?.Invoke(true, false, false)); // Left up
            });
        }

        private void PerformDoubleClick()
        {
          PlayHaptic(UIImpactFeedbackStyle.Medium);
    
            // First click
            OnMouseClick?.Invoke(true, false, true);
            OnMouseClick?.Invoke(true, false, false);
    
          // Second click after short delay
            Task.Delay(50).ContinueWith(_ =>
   {
                InvokeOnMainThread(() =>
     {
         OnMouseClick?.Invoke(true, false, true);
              OnMouseClick?.Invoke(true, false, false);
                });
   });
        }

        private void PerformRightClick()
        {
            PlayHaptic(UIImpactFeedbackStyle.Medium);
 OnMouseClick?.Invoke(false, true, true);  // Right down
          
            Task.Delay(30).ContinueWith(_ =>
   {
                InvokeOnMainThread(() => OnMouseClick?.Invoke(false, true, false)); // Right up
         });
        }

        private void PlayHaptic(UIImpactFeedbackStyle style)
     {
var generator = new UIImpactFeedbackGenerator(style);
            generator.Prepare();
    generator.ImpactOccurred();
    }
    }

  /// <summary>
    /// Modern virtual gamepad view with glass-morphic design
    /// </summary>
    public class VirtualGamepadView : UIView
    {
        private readonly ControllerStyle _style;
        private readonly ControllerSize _size;
        public event Action<GamepadState>? OnStateChanged;
     private GamepadState _state = new();

        private ModernStickView? _leftStick;
    private ModernStickView? _rightStick;
        private ModernFaceButton? _buttonA, _buttonB, _buttonX, _buttonY;
        private ModernDPadView? _dpad;
    private ModernShoulderButton? _leftBumper, _rightBumper;
        private ModernTriggerButton? _leftTrigger, _rightTrigger;
        private UIButton? _startButton, _backButton, _guideButton;
        private UIImpactFeedbackGenerator? _lightHaptic;
        private UIImpactFeedbackGenerator? _mediumHaptic;

        public VirtualGamepadView(ControllerStyle style, ControllerSize size)
  {
    _style = style;
    _size = size;
      MultipleTouchEnabled = true;
            _lightHaptic = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Light);
          _mediumHaptic = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Medium);
   _lightHaptic.Prepare();
    _mediumHaptic.Prepare();
      SetupControls();
   }

        private (int stickSize, int buttonSize, int dpadSize, nfloat margin) GetSizes() => _size switch
    {
  ControllerSize.Minimal => (80, 38, 100, 12),
       ControllerSize.Large => (140, 58, 150, 30),
            _ => (110, 48, 125, 20)
        };

        private void SetupControls()
        {
    var (stickSize, buttonSize, dpadSize, margin) = GetSizes();

  _leftStick = new ModernStickView(stickSize);
            _leftStick.OnStickMoved += (x, y) => { _state.LeftStickX = x; _state.LeftStickY = y; NotifyStateChanged(); };
  _leftStick.OnStickPressed += p => { if (p) _state.Buttons |= GamepadButtons.LeftStick; else _state.Buttons &= ~GamepadButtons.LeftStick; NotifyStateChanged(); _mediumHaptic?.ImpactOccurred(); };
            AddSubview(_leftStick);

      _rightStick = new ModernStickView(stickSize);
   _rightStick.OnStickMoved += (x, y) => { _state.RightStickX = x; _state.RightStickY = y; NotifyStateChanged(); };
            _rightStick.OnStickPressed += p => { if (p) _state.Buttons |= GamepadButtons.RightStick; else _state.Buttons &= ~GamepadButtons.RightStick; NotifyStateChanged(); _mediumHaptic?.ImpactOccurred(); };
       AddSubview(_rightStick);

  var colors = GetFaceButtonColors();
            _buttonA = CreateFaceButton("A", colors.a, buttonSize, GamepadButtons.A);
            _buttonB = CreateFaceButton("B", colors.b, buttonSize, GamepadButtons.B);
            _buttonX = CreateFaceButton("X", colors.x, buttonSize, GamepadButtons.X);
 _buttonY = CreateFaceButton("Y", colors.y, buttonSize, GamepadButtons.Y);
  AddSubview(_buttonA); AddSubview(_buttonB); AddSubview(_buttonX); AddSubview(_buttonY);

            _dpad = new ModernDPadView(dpadSize);
            _dpad.OnDirectionChanged += (up, down, left, right) =>
 {
     _state.Buttons &= ~(GamepadButtons.DPadUp | GamepadButtons.DPadDown | GamepadButtons.DPadLeft | GamepadButtons.DPadRight);
    if (up) _state.Buttons |= GamepadButtons.DPadUp;
      if (down) _state.Buttons |= GamepadButtons.DPadDown;
     if (left) _state.Buttons |= GamepadButtons.DPadLeft;
           if (right) _state.Buttons |= GamepadButtons.DPadRight;
     NotifyStateChanged();
          };
   AddSubview(_dpad);

            _leftBumper = new ModernShoulderButton("LB", 70, 36);
            _leftBumper.OnPressed += p => { if (p) _state.Buttons |= GamepadButtons.LeftBumper; else _state.Buttons &= ~GamepadButtons.LeftBumper; NotifyStateChanged(); _lightHaptic?.ImpactOccurred(); };
        AddSubview(_leftBumper);

    _rightBumper = new ModernShoulderButton("RB", 70, 36);
            _rightBumper.OnPressed += p => { if (p) _state.Buttons |= GamepadButtons.RightBumper; else _state.Buttons &= ~GamepadButtons.RightBumper; NotifyStateChanged(); _lightHaptic?.ImpactOccurred(); };
            AddSubview(_rightBumper);

     _leftTrigger = new ModernTriggerButton("LT", 60, 44);
_leftTrigger.OnValueChanged += v => { _state.LeftTrigger = v; NotifyStateChanged(); };
            AddSubview(_leftTrigger);

_rightTrigger = new ModernTriggerButton("RT", 60, 44);
            _rightTrigger.OnValueChanged += v => { _state.RightTrigger = v; NotifyStateChanged(); };
            AddSubview(_rightTrigger);

 _startButton = CreateMenuButton("?", GamepadButtons.Start);
  _backButton = CreateMenuButton("?", GamepadButtons.Back);
    _guideButton = CreateGuideButton();
            AddSubview(_startButton); AddSubview(_backButton); AddSubview(_guideButton);
        }

        private (UIColor a, UIColor b, UIColor x, UIColor y) GetFaceButtonColors() => _style == ControllerStyle.Xbox
      ? (UIColor.FromRGB(16, 124, 16), UIColor.FromRGB(180, 40, 40), UIColor.FromRGB(30, 100, 180), UIColor.FromRGB(200, 160, 30))
  : (UIColor.FromRGB(70, 130, 200), UIColor.FromRGB(200, 70, 90), UIColor.FromRGB(200, 80, 160), UIColor.FromRGB(80, 190, 160));

        private ModernFaceButton CreateFaceButton(string label, UIColor color, int size, GamepadButtons button)
  {
          var displayLabel = _style == ControllerStyle.PlayStation ? GetPlayStationLabel(button) : label;
         var btn = new ModernFaceButton(displayLabel, color, size);
            btn.OnPressed += p => { if (p) _state.Buttons |= button; else _state.Buttons &= ~button; NotifyStateChanged(); _lightHaptic?.ImpactOccurred(); };
            return btn;
        }

        private string GetPlayStationLabel(GamepadButtons button) => button switch { GamepadButtons.A => "?", GamepadButtons.B => "?", GamepadButtons.X => "?", GamepadButtons.Y => "?", _ => "" };

        private UIButton CreateMenuButton(string label, GamepadButtons button)
 {
  var size = _size == ControllerSize.Minimal ? 32 : (_size == ControllerSize.Large ? 44 : 38);
  var btn = new UIButton(UIButtonType.Custom) { Frame = new CGRect(0, 0, size, size) };
       btn.BackgroundColor = UIColor.FromWhiteAlpha(0.15f, 0.6f);
      btn.Layer.CornerRadius = size / 2;
            btn.Layer.BorderColor = UIColor.FromWhiteAlpha(0.3f, 0.4f).CGColor;
    btn.Layer.BorderWidth = 1;
     btn.SetTitle(label, UIControlState.Normal);
    btn.SetTitleColor(UIColor.FromWhiteAlpha(0.9f, 1f), UIControlState.Normal);
            btn.TitleLabel!.Font = UIFont.SystemFontOfSize(size * 0.4f, UIFontWeight.Medium);
          btn.TouchDown += (s, e) => { _state.Buttons |= button; NotifyStateChanged(); _lightHaptic?.ImpactOccurred(); btn.BackgroundColor = UIColor.FromWhiteAlpha(0.35f, 0.8f); btn.Transform = CGAffineTransform.MakeScale(0.92f, 0.92f); };
            void Release() { _state.Buttons &= ~button; NotifyStateChanged(); btn.BackgroundColor = UIColor.FromWhiteAlpha(0.15f, 0.6f); btn.Transform = CGAffineTransform.MakeIdentity(); }
            btn.TouchUpInside += (s, e) => Release();
            btn.TouchUpOutside += (s, e) => Release();
       btn.TouchCancel += (s, e) => Release();
            return btn;
        }

        private UIButton CreateGuideButton()
        {
        var size = _size == ControllerSize.Minimal ? 36 : (_size == ControllerSize.Large ? 52 : 44);
            var btn = new UIButton(UIButtonType.Custom) { Frame = new CGRect(0, 0, size, size) };
 btn.BackgroundColor = _style == ControllerStyle.Xbox ? UIColor.FromRGB(16, 124, 16) : UIColor.FromRGB(0, 55, 145);
         btn.Layer.CornerRadius = size / 2;
   btn.ClipsToBounds = true;
        var config = UIImageSymbolConfiguration.Create(size * 0.45f, UIImageSymbolWeight.Regular);
            btn.SetImage(UIImage.GetSystemImage("circle.fill", config), UIControlState.Normal);
            btn.TintColor = UIColor.White;
            btn.TouchDown += (s, e) => { _state.Buttons |= GamepadButtons.Guide; NotifyStateChanged(); _mediumHaptic?.ImpactOccurred(); btn.Transform = CGAffineTransform.MakeScale(0.9f, 0.9f); };
        void Release() { _state.Buttons &= ~GamepadButtons.Guide; NotifyStateChanged(); btn.Transform = CGAffineTransform.MakeIdentity(); }
            btn.TouchUpInside += (s, e) => Release();
     btn.TouchUpOutside += (s, e) => Release();
btn.TouchCancel += (s, e) => Release();
    return btn;
     }

 public override void LayoutSubviews()
     {
 base.LayoutSubviews();
     var safeArea = SafeAreaInsets;
      var (stickSize, buttonSize, dpadSize, margin) = GetSizes();
       var bottomMargin = safeArea.Bottom + 15;

   _leftStick?.SetFrame(new CGRect(safeArea.Left + margin, Bounds.Height - stickSize - bottomMargin, stickSize, stickSize));
         var dpadX = safeArea.Left + margin + (stickSize - dpadSize) / 2;
        var dpadY = Bounds.Height - stickSize - bottomMargin - dpadSize - 20;
    _dpad?.SetFrame(new CGRect(dpadX, dpadY, dpadSize, dpadSize));
            _rightStick?.SetFrame(new CGRect(Bounds.Width - stickSize - safeArea.Right - margin, Bounds.Height - stickSize - bottomMargin, stickSize, stickSize));

            var faceCenterX = Bounds.Width - stickSize / 2 - safeArea.Right - margin;
         var faceCenterY = Bounds.Height - stickSize - bottomMargin - buttonSize - 40;
    var faceSpacing = buttonSize + 6;
     _buttonA?.SetCenter(new CGPoint(faceCenterX, faceCenterY + faceSpacing / 2 + 5));
            _buttonB?.SetCenter(new CGPoint(faceCenterX + faceSpacing / 2 + 5, faceCenterY));
            _buttonX?.SetCenter(new CGPoint(faceCenterX - faceSpacing / 2 - 5, faceCenterY));
            _buttonY?.SetCenter(new CGPoint(faceCenterX, faceCenterY - faceSpacing / 2 - 5));

            var shoulderY = safeArea.Top + 50;
  var triggerY = safeArea.Top + 100;
      _leftBumper?.SetCenter(new CGPoint(safeArea.Left + 55, shoulderY));
            _rightBumper?.SetCenter(new CGPoint(Bounds.Width - safeArea.Right - 55, shoulderY));
       _leftTrigger?.SetCenter(new CGPoint(safeArea.Left + 50, triggerY));
   _rightTrigger?.SetCenter(new CGPoint(Bounds.Width - safeArea.Right - 50, triggerY));

            var menuY = safeArea.Top + 75;
       var guideSize = _size == ControllerSize.Minimal ? 36 : (_size == ControllerSize.Large ? 52 : 44);
     _guideButton!.Frame = new CGRect(0, 0, guideSize, guideSize);
            _guideButton.Center = new CGPoint(Bounds.Width / 2, menuY);
     var menuSize = _size == ControllerSize.Minimal ? 32 : (_size == ControllerSize.Large ? 44 : 38);
            _backButton!.Frame = new CGRect(0, 0, menuSize, menuSize);
            _backButton.Center = new CGPoint(Bounds.Width / 2 - guideSize - 15, menuY);
     _startButton!.Frame = new CGRect(0, 0, menuSize, menuSize);
          _startButton.Center = new CGPoint(Bounds.Width / 2 + guideSize + 15, menuY);
    }

        private void NotifyStateChanged() => OnStateChanged?.Invoke(_state);
    }

 public class ModernStickView : UIView
    {
        public event Action<float, float>? OnStickMoved;
      public event Action<bool>? OnStickPressed;
        private UIView _trackView, _thumbView;
private CGPoint _centerPoint;
    private readonly nfloat _maxDistance;
     private bool _isTracking;
  private DateTime _touchStartTime;

        public ModernStickView(int size)
     {
 _maxDistance = size / 2 - 22;
            Frame = new CGRect(0, 0, size, size);
  UserInteractionEnabled = true;

            _trackView = new UIView { Frame = new CGRect(0, 0, size, size), BackgroundColor = UIColor.FromWhiteAlpha(0.08f, 0.5f) };
            _trackView.Layer.CornerRadius = size / 2;
            _trackView.Layer.BorderColor = UIColor.FromWhiteAlpha(0.25f, 0.6f).CGColor;
            _trackView.Layer.BorderWidth = 2;
       AddSubview(_trackView);

  var thumbSize = size * 0.5;
        _thumbView = new UIView { Frame = new CGRect(0, 0, thumbSize, thumbSize), BackgroundColor = UIColor.FromWhiteAlpha(0.85f, 0.95f) };
      _thumbView.Layer.CornerRadius = (nfloat)(thumbSize / 2);
          _thumbView.Layer.ShadowColor = UIColor.Black.CGColor;
    _thumbView.Layer.ShadowOffset = new CGSize(0, 3);
          _thumbView.Layer.ShadowRadius = 6;
            _thumbView.Layer.ShadowOpacity = 0.4f;
   AddSubview(_thumbView);
   }

public void SetFrame(CGRect frame) { Frame = frame; SetNeedsLayout(); }
        public override void LayoutSubviews() { base.LayoutSubviews(); _centerPoint = new CGPoint(Bounds.Width / 2, Bounds.Height / 2); if (!_isTracking) _thumbView.Center = _centerPoint; }
        public override void TouchesBegan(NSSet touches, UIEvent? evt) { _isTracking = true; _touchStartTime = DateTime.Now; UpdateThumbPosition(touches); UIView.Animate(0.1, () => { _thumbView.Transform = CGAffineTransform.MakeScale(1.1f, 1.1f); }); }
     public override void TouchesMoved(NSSet touches, UIEvent? evt) { if (_isTracking) UpdateThumbPosition(touches); }
        public override void TouchesEnded(NSSet touches, UIEvent? evt) { if ((DateTime.Now - _touchStartTime).TotalMilliseconds < 200 && _thumbView.Center.DistanceTo(_centerPoint) < 15) { OnStickPressed?.Invoke(true); Task.Delay(100).ContinueWith(_ => InvokeOnMainThread(() => OnStickPressed?.Invoke(false))); } _isTracking = false; ResetThumb(); }
        public override void TouchesCancelled(NSSet touches, UIEvent? evt) { _isTracking = false; ResetThumb(); }

        private void UpdateThumbPosition(NSSet touches)
        {
   if (touches.AnyObject is not UITouch touch) return;
          var location = touch.LocationInView(this);
       var deltaX = location.X - _centerPoint.X;
    var deltaY = location.Y - _centerPoint.Y;
    var distance = (nfloat)Math.Sqrt((double)(deltaX * deltaX + deltaY * deltaY));
            if (distance > _maxDistance) { var scale = _maxDistance / distance; deltaX *= scale; deltaY *= scale; }
            _thumbView.Center = new CGPoint(_centerPoint.X + deltaX, _centerPoint.Y + deltaY);
     var normalizedX = (float)(deltaX / _maxDistance);
 var normalizedY = (float)(deltaY / _maxDistance);
            if (Math.Abs(normalizedX) < 0.1f) normalizedX = 0;
    if (Math.Abs(normalizedY) < 0.1f) normalizedY = 0;
OnStickMoved?.Invoke(normalizedX, normalizedY);
 }

        private void ResetThumb() { UIView.Animate(0.2, () => { _thumbView.Center = _centerPoint; _thumbView.Transform = CGAffineTransform.MakeIdentity(); }); OnStickMoved?.Invoke(0, 0); }
    }

    public class ModernFaceButton : UIView
    {
     public event Action<bool>? OnPressed;
   private readonly UIColor _color;
        private readonly UIView _glowView;

        public ModernFaceButton(string text, UIColor color, int size)
        {
    _color = color;
     Frame = new CGRect(0, 0, size, size);
            UserInteractionEnabled = true;

    _glowView = new UIView { Frame = new CGRect(-4, -4, size + 8, size + 8), BackgroundColor = color.ColorWithAlpha(0.3f), Alpha = 0 };
            _glowView.Layer.CornerRadius = (size + 8) / 2;
            AddSubview(_glowView);

            BackgroundColor = color.ColorWithAlpha(0.85f);
Layer.CornerRadius = size / 2;
 Layer.BorderColor = UIColor.FromWhiteAlpha(0.4f, 0.5f).CGColor;
     Layer.BorderWidth = 1.5f;

    var label = new UILabel { Text = text, TextColor = UIColor.White, Font = UIFont.BoldSystemFontOfSize(size * 0.4f), TextAlignment = UITextAlignment.Center, Frame = new CGRect(0, 0, size, size) };
      AddSubview(label);
        }

        public void SetCenter(CGPoint center) => Center = center;
        public override void TouchesBegan(NSSet touches, UIEvent? evt) { OnPressed?.Invoke(true); UIView.Animate(0.08, () => { Transform = CGAffineTransform.MakeScale(0.88f, 0.88f); _glowView.Alpha = 1; BackgroundColor = _color; }); }
        public override void TouchesEnded(NSSet touches, UIEvent? evt) => Release();
        public override void TouchesCancelled(NSSet touches, UIEvent? evt) => Release();
        private void Release() { OnPressed?.Invoke(false); UIView.Animate(0.15, () => { Transform = CGAffineTransform.MakeIdentity(); _glowView.Alpha = 0; BackgroundColor = _color.ColorWithAlpha(0.85f); }); }
    }

    public class ModernDPadView : UIView
    {
 public event Action<bool, bool, bool, bool>? OnDirectionChanged;
    private readonly UIView[] _arrows = new UIView[4];
        private bool _up, _down, _left, _right;
        private readonly int _size;

     public ModernDPadView(int size)
        {
            _size = size;
            Frame = new CGRect(0, 0, size, size);
            UserInteractionEnabled = true;
   MultipleTouchEnabled = true;

     BackgroundColor = UIColor.FromWhiteAlpha(0.1f, 0.5f);
   Layer.CornerRadius = size / 2;
 Layer.BorderColor = UIColor.FromWhiteAlpha(0.2f, 0.5f).CGColor;
          Layer.BorderWidth = 1.5f;

            var arrowOffset = size * 0.28;
        _arrows[0] = CreateArrow("chevron.up", new CGPoint(size / 2, arrowOffset));
  _arrows[1] = CreateArrow("chevron.down", new CGPoint(size / 2, size - arrowOffset));
            _arrows[2] = CreateArrow("chevron.left", new CGPoint(arrowOffset, size / 2));
          _arrows[3] = CreateArrow("chevron.right", new CGPoint(size - arrowOffset, size / 2));
        }

        private UIView CreateArrow(string icon, CGPoint center)
        {
        var size = _size * 0.2;
            var view = new UIImageView { Frame = new CGRect(0, 0, size, size), ContentMode = UIViewContentMode.Center, TintColor = UIColor.FromWhiteAlpha(0.7f, 1f) };
            var config = UIImageSymbolConfiguration.Create((nfloat)(size * 0.7), UIImageSymbolWeight.Bold);
            view.Image = UIImage.GetSystemImage(icon, config);
            view.Center = center;
   AddSubview(view);
   return view;
  }

        public void SetFrame(CGRect frame) { Frame = frame; SetNeedsLayout(); }
        public override void TouchesBegan(NSSet touches, UIEvent? evt) => UpdateDirection(touches);
        public override void TouchesMoved(NSSet touches, UIEvent? evt) => UpdateDirection(touches);
      public override void TouchesEnded(NSSet touches, UIEvent? evt) { _up = _down = _left = _right = false; UpdateVisuals(); OnDirectionChanged?.Invoke(false, false, false, false); }
        public override void TouchesCancelled(NSSet touches, UIEvent? evt) => TouchesEnded(touches, evt);

        private void UpdateDirection(NSSet touches)
        {
   if (touches.AnyObject is not UITouch touch) return;
    var location = touch.LocationInView(this);
     var deadzone = Bounds.Width * 0.15;
        _up = (location.Y - Bounds.Height / 2) < -deadzone;
        _down = (location.Y - Bounds.Height / 2) > deadzone;
          _left = (location.X - Bounds.Width / 2) < -deadzone;
      _right = (location.X - Bounds.Width / 2) > deadzone;
         UpdateVisuals();
            OnDirectionChanged?.Invoke(_up, _down, _left, _right);
          if (_up || _down || _left || _right) new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Light).ImpactOccurred();
        }

    private void UpdateVisuals() { UIView.Animate(0.08, () => { for (int i = 0; i < 4; i++) { var active = i == 0 ? _up : i == 1 ? _down : i == 2 ? _left : _right; _arrows[i].TintColor = active ? UIColor.White : UIColor.FromWhiteAlpha(0.7f, 1f); _arrows[i].Transform = active ? CGAffineTransform.MakeScale(1.2f, 1.2f) : CGAffineTransform.MakeIdentity(); } }); }
    }

    public class ModernShoulderButton : UIView
    {
        public event Action<bool>? OnPressed;
public ModernShoulderButton(string label, int width, int height)
        {
        Frame = new CGRect(0, 0, width, height);
          UserInteractionEnabled = true;
      BackgroundColor = UIColor.FromWhiteAlpha(0.15f, 0.7f);
Layer.CornerRadius = height / 2;
       Layer.BorderColor = UIColor.FromWhiteAlpha(0.3f, 0.5f).CGColor;
            Layer.BorderWidth = 1;
       var lbl = new UILabel { Text = label, TextColor = UIColor.White, Font = UIFont.BoldSystemFontOfSize(13), TextAlignment = UITextAlignment.Center, Frame = new CGRect(0, 0, width, height) };
            AddSubview(lbl);
        }
        public void SetCenter(CGPoint center) => Center = center;
        public override void TouchesBegan(NSSet touches, UIEvent? evt) { OnPressed?.Invoke(true); UIView.Animate(0.08, () => { BackgroundColor = UIColor.FromWhiteAlpha(0.4f, 0.85f); Transform = CGAffineTransform.MakeScale(0.95f, 0.95f); }); }
  public override void TouchesEnded(NSSet touches, UIEvent? evt) => Release();
        public override void TouchesCancelled(NSSet touches, UIEvent? evt) => Release();
        private void Release() { OnPressed?.Invoke(false); UIView.Animate(0.12, () => { BackgroundColor = UIColor.FromWhiteAlpha(0.15f, 0.7f); Transform = CGAffineTransform.MakeIdentity(); }); }
    }

    public class ModernTriggerButton : UIView
    {
        public event Action<float>? OnValueChanged;
private readonly UIView _fillView;
        private readonly int _height;

        public ModernTriggerButton(string label, int width, int height)
        {
  _height = height;
       Frame = new CGRect(0, 0, width, height);
  UserInteractionEnabled = true;
   BackgroundColor = UIColor.FromWhiteAlpha(0.12f, 0.6f);
         Layer.CornerRadius = 10;
   Layer.BorderColor = UIColor.FromWhiteAlpha(0.25f, 0.5f).CGColor;
          Layer.BorderWidth = 1;
     ClipsToBounds = true;

  _fillView = new UIView { Frame = new CGRect(0, height, width, 0), BackgroundColor = UIColor.FromRGB(80, 160, 255).ColorWithAlpha(0.6f) };
        AddSubview(_fillView);
       var lbl = new UILabel { Text = label, TextColor = UIColor.White, Font = UIFont.BoldSystemFontOfSize(12), TextAlignment = UITextAlignment.Center, Frame = new CGRect(0, 0, width, height) };
      AddSubview(lbl);
    }

     public void SetCenter(CGPoint center) => Center = center;
        public override void TouchesBegan(NSSet touches, UIEvent? evt) => SetValue(1.0f);
        public override void TouchesEnded(NSSet touches, UIEvent? evt) => SetValue(0f);
        public override void TouchesCancelled(NSSet touches, UIEvent? evt) => SetValue(0f);
        private void SetValue(float value) { OnValueChanged?.Invoke(value); UIView.Animate(0.1, () => { var fillHeight = _height * value; _fillView.Frame = new CGRect(0, _height - fillHeight, Frame.Width, fillHeight); }); }
    }

    public static class CGPointExtensions
    {
        public static nfloat DistanceTo(this CGPoint point, CGPoint other)
        {
            var dx = point.X - other.X;
            var dy = point.Y - other.Y;
  return (nfloat)Math.Sqrt((double)(dx * dx + dy * dy));
      }
    }
}
