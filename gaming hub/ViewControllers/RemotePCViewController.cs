using gaming_hub.Models;
using gaming_hub.Services;
using gaming_hub.Views;

namespace gaming_hub.ViewControllers
{
    public class RemotePCViewController : UIViewController
    {
    private UIScrollView _scrollView = null!;
  private UIView _headerView = null!;
        private RemotePCStatusView _statusView = null!;
        private UILabel _setupLabel = null!;
      private UILabel _gamesHeader = null!;
        private UICollectionView _gamesCollectionView = null!;
        private UIActivityIndicatorView _loadingIndicator = null!;
     private UIRefreshControl _refreshControl = null!;
        private UIButton _streamButton = null!;
        private List<RemoteGame> _installedGames = [];
        private UserData _userData = null!;
private bool _isConfigured;
        private bool _isDiscovering;

    public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            SetupUI();
      LoadData();
 }

        public override void ViewWillAppear(bool animated)
        {
      base.ViewWillAppear(animated);
            RefreshStatus();
        }

        private void SetupUI()
        {
   Title = "Remote PC";
            View!.BackgroundColor = UIColor.SystemBackground;
     
     if (NavigationController != null)
   NavigationController.NavigationBar.PrefersLargeTitles = true;

            var settingsBtn = new UIBarButtonItem(UIImage.GetSystemImage("gear"), UIBarButtonItemStyle.Plain, ShowSettings);
            var discoverBtn = new UIBarButtonItem(UIImage.GetSystemImage("antenna.radiowaves.left.and.right"), UIBarButtonItemStyle.Plain, DiscoverPCs);
          NavigationItem.RightBarButtonItems = new[] { settingsBtn, discoverBtn };

    _scrollView = new UIScrollView { AlwaysBounceVertical = true };
            _refreshControl = new UIRefreshControl();
    _refreshControl.ValueChanged += async (s, e) =>
  {
                await RefreshStatus();
 _refreshControl.EndRefreshing();
            };
            _scrollView.RefreshControl = _refreshControl;
          View.AddSubview(_scrollView);

 _setupLabel = new UILabel
            {
      Text = "No PC Connected\n\nTap the antenna icon to scan for PCs running Synktra Companion on your network.\n\nOr tap the gear icon to manually configure.",
  TextColor = UIColor.SecondaryLabel,
      TextAlignment = UITextAlignment.Center,
        Lines = 0,
    Font = UIFont.SystemFontOfSize(15),
            Hidden = true
            };
     _scrollView.AddSubview(_setupLabel);

    _statusView = new RemotePCStatusView();
            _statusView.WakeRequested += OnWakeRequested;
   _statusView.SleepRequested += OnSleepRequested;
     _statusView.RestartRequested += OnRestartRequested;
   _statusView.ShutdownRequested += OnShutdownRequested;
            _scrollView.AddSubview(_statusView);

        // Stream button
            _streamButton = new UIButton(UIButtonType.System);
        _streamButton.SetTitle("  Stream & Play", UIControlState.Normal);
    _streamButton.SetImage(UIImage.GetSystemImage("play.rectangle.fill"), UIControlState.Normal);
            _streamButton.TintColor = UIColor.White;
            _streamButton.BackgroundColor = UIColor.FromRGB(124, 58, 237); // Purple
 _streamButton.Layer.CornerRadius = 12;
        _streamButton.TitleLabel!.Font = UIFont.BoldSystemFontOfSize(17);
 _streamButton.TouchUpInside += StartStreaming;
  _streamButton.Hidden = true;
        _scrollView.AddSubview(_streamButton);

_gamesHeader = new UILabel
         {
       Text = "Installed Games",
              Font = UIFont.BoldSystemFontOfSize(20),
     TextColor = UIColor.Label
         };
       _scrollView.AddSubview(_gamesHeader);

 var layout = new UICollectionViewFlowLayout
  {
         ItemSize = new CGSize(160, 200),
      MinimumInteritemSpacing = 12,
       MinimumLineSpacing = 12,
       ScrollDirection = UICollectionViewScrollDirection.Vertical
  };

  _gamesCollectionView = new UICollectionView(CGRect.Empty, layout)
            {
      BackgroundColor = UIColor.Clear,
    AllowsSelection = true
       };
   _gamesCollectionView.RegisterClassForCell(typeof(RemoteGameCell), RemoteGameCell.ReuseId);
            _gamesCollectionView.DataSource = new RemoteGamesCollectionSource(this);
      _gamesCollectionView.Delegate = new RemoteGamesCollectionDelegate(this);
     _scrollView.AddSubview(_gamesCollectionView);

            _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large) { HidesWhenStopped = true };
      View.AddSubview(_loadingIndicator);
        }

    public override void ViewDidLayoutSubviews()
{
            base.ViewDidLayoutSubviews();
     var safeArea = View!.SafeAreaInsets;
            var width = View.Bounds.Width;
            var height = View.Bounds.Height;
         var padding = 16f;

      _scrollView.Frame = new CGRect(0, safeArea.Top, width, height - safeArea.Top - safeArea.Bottom);

            if (!_isConfigured)
            {
          _setupLabel.Hidden = false;
   _statusView.Hidden = true;
      _streamButton.Hidden = true;
            _gamesHeader.Hidden = true;
   _gamesCollectionView.Hidden = true;
    _setupLabel.Frame = new CGRect(padding, 80, width - padding * 2, 200);
                _scrollView.ContentSize = new CGSize(width, 300);
            }
            else
            {
         _setupLabel.Hidden = true;
       _statusView.Hidden = false;
                _streamButton.Hidden = !_statusView.IsOnline;
          _gamesHeader.Hidden = false;
       _gamesCollectionView.Hidden = false;

         _statusView.Frame = new CGRect(padding, padding, width - padding * 2, 200);
          
        // Stream button below status
         _streamButton.Frame = new CGRect(padding, _statusView.Frame.Bottom + 16, width - padding * 2, 50);
                
         var gamesHeaderY = _streamButton.Hidden ? _statusView.Frame.Bottom + 20 : _streamButton.Frame.Bottom + 20;
_gamesHeader.Frame = new CGRect(padding, gamesHeaderY, width - padding * 2, 24);

             var columns = Math.Max(1, (int)((width - padding * 2 + 12) / 172));
         var rows = Math.Max(1, (int)Math.Ceiling((double)_installedGames.Count / columns));
            var collectionHeight = rows * 212;

   _gamesCollectionView.Frame = new CGRect(padding, _gamesHeader.Frame.Bottom + 12, width - padding * 2, collectionHeight);
           _scrollView.ContentSize = new CGSize(width, _gamesCollectionView.Frame.Bottom + padding);
   }

        _loadingIndicator.Center = new CGPoint(width / 2, height / 2);
 }

        private async void LoadData()
        {
       _userData = await DatabaseService.Instance.GetUserDataAsync();
       _isConfigured = !string.IsNullOrEmpty(_userData?.RemotePCHost);
            ViewDidLayoutSubviews();
            await RefreshStatus();
   }

     private void StartStreaming(object? sender, EventArgs e)
        {
         if (_userData == null || string.IsNullOrEmpty(_userData.RemotePCHost)) return;

  var alert = UIAlertController.Create(
     "Start Streaming?",
        "This will stream your PC screen with a virtual gamepad.\n\nNote: Works best on LAN with low latency.",
    UIAlertControllerStyle.Alert);

      alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
     alert.AddAction(UIAlertAction.Create("Start", UIAlertActionStyle.Default, _ =>
          {
 var streamVC = new GameStreamViewController(
         _userData.RemotePCHost!,
       19501, // Stream WebSocket port
               _userData.RemotePCAuthToken);
     PresentViewController(streamVC, true, null);
     }));

  PresentViewController(alert, true, null);
        }

   private async void DiscoverPCs(object? sender, EventArgs e)
        {
 if (_isDiscovering) return;
         _isDiscovering = true;
         _loadingIndicator.StartAnimating();

      try
 {
         var discovered = await RemotePCService.Instance.DiscoverPCsOnNetworkAsync(4000);

             if (discovered.Count == 0)
             {
  ShowAlert("No PCs Found", "Make sure Synktra Companion is running on your PC and both devices are on the same network.");
      }
  else if (discovered.Count == 1)
{
           var pc = discovered[0];
       var alert = UIAlertController.Create("PC Found", $"Found: {pc.Hostname}\nIP: {pc.IpAddress}:{pc.Port}\n\nConnect to this PC?", UIAlertControllerStyle.Alert);
   alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
           alert.AddAction(UIAlertAction.Create("Connect", UIAlertActionStyle.Default, async _ =>
  {
         await ConnectToDiscoveredPC(pc);
     }));
       PresentViewController(alert, true, null);
           }
   else
        {
       var alert = UIAlertController.Create("Select PC", "Multiple PCs found on network:", UIAlertControllerStyle.ActionSheet);
         foreach (var pc in discovered)
                    {
            alert.AddAction(UIAlertAction.Create($"{pc.Hostname} ({pc.IpAddress})", UIAlertActionStyle.Default, async _ =>
      {
          await ConnectToDiscoveredPC(pc);
  }));
          }
      alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
         PresentViewController(alert, true, null);
      }
     }
            catch (Exception ex)
            {
         ShowAlert("Discovery Failed", $"Error: {ex.Message}");
    }
            finally
       {
            _isDiscovering = false;
    _loadingIndicator.StopAnimating();
        }
        }

        private async Task ConnectToDiscoveredPC(DiscoveredPC pc)
      {
            if (_userData == null) _userData = new UserData();

   _userData.RemotePCHost = pc.IpAddress;
_userData.RemotePCPort = pc.Port;

     if (pc.RequiresAuth)
          {
      var tokenAlert = UIAlertController.Create("Auth Required", "This PC requires an authentication token. Enter it below:", UIAlertControllerStyle.Alert);
        tokenAlert.AddTextField(tf => tf.Placeholder = "Auth Token");
    tokenAlert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
      tokenAlert.AddAction(UIAlertAction.Create("Connect", UIAlertActionStyle.Default, async _ =>
       {
       _userData.RemotePCAuthToken = tokenAlert.TextFields?[0].Text;
      await DatabaseService.Instance.SaveUserDataAsync(_userData);
    _isConfigured = true;
                    ViewDidLayoutSubviews();
           await RefreshStatus();
            }));
                PresentViewController(tokenAlert, true, null);
   }
else
            {
    await DatabaseService.Instance.SaveUserDataAsync(_userData);
      _isConfigured = true;
       ViewDidLayoutSubviews();
  await RefreshStatus();
         }
 }

    private async Task RefreshStatus()
        {
            if (_userData == null)
         {
    _userData = await DatabaseService.Instance.GetUserDataAsync();
          }

   _isConfigured = !string.IsNullOrEmpty(_userData?.RemotePCHost);
  ViewDidLayoutSubviews();

       if (!_isConfigured)
        {
      _statusView.UpdateStatus(new RemotePCStatus { IsOnline = false });
         return;
            }

            _loadingIndicator.StartAnimating();

    try
         {
      var status = await RemotePCService.Instance.GetStatusAsync(
     _userData.RemotePCHost!,
       _userData.RemotePCPort,
   _userData.RemotePCAuthToken);

       _statusView.UpdateStatus(status);
      _streamButton.Hidden = !status.IsOnline;

    if (status.IsOnline)
   {
     _installedGames = await RemotePCService.Instance.GetInstalledGamesAsync(
            _userData.RemotePCHost!,
   _userData.RemotePCPort,
     _userData.RemotePCAuthToken);
          _gamesCollectionView.ReloadData();
        ViewDidLayoutSubviews();
          }
                else
       {
    _installedGames.Clear();
         _gamesCollectionView.ReloadData();
     }
 }
catch (Exception ex)
          {
          Console.WriteLine($"RefreshStatus error: {ex.Message}");
     _statusView.UpdateStatus(new RemotePCStatus { IsOnline = false });
          }
     finally
   {
_loadingIndicator.StopAnimating();
          }
 }

        private async void OnWakeRequested(object? sender, EventArgs e)
        {
  if (_userData == null || string.IsNullOrEmpty(_userData.RemotePCMacAddress))
            {
    ShowAlert("MAC Address Required", "Configure your PC's MAC address in settings to use Wake-on-LAN.");
      return;
            }

  _loadingIndicator.StartAnimating();

            var success = await RemotePCService.Instance.WakeOnLanAsync(_userData.RemotePCMacAddress);

            if (success)
            {
      ShowAlert("Wake Signal Sent", "Magic packet sent. Your PC should start within 10-30 seconds.");
  await Task.Delay(10000);
              await RefreshStatus();
    }
 else
        {
        ShowAlert("Wake Failed", "Could not send wake signal.");
            }

 _loadingIndicator.StopAnimating();
        }

   private async void OnSleepRequested(object? sender, EventArgs e)
        {
    if (_userData == null) return;

      var alert = UIAlertController.Create("Sleep PC?", "This will put your PC to sleep.", UIAlertControllerStyle.Alert);
     alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
            alert.AddAction(UIAlertAction.Create("Sleep", UIAlertActionStyle.Default, async _ =>
            {
     var success = await RemotePCService.Instance.SleepAsync(
           _userData.RemotePCHost!,
         _userData.RemotePCPort,
          _userData.RemotePCAuthToken);

     if (success)
         {
   await Task.Delay(2000);
                    await RefreshStatus();
                }
  else
             {
           ShowAlert("Failed", "Could not put PC to sleep.");
    }
     }));
          PresentViewController(alert, true, null);
        }

        private async void OnRestartRequested(object? sender, EventArgs e)
        {
            if (_userData == null) return;

      var alert = UIAlertController.Create("Restart PC?", "This will restart your PC.", UIAlertControllerStyle.Alert);
   alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
        alert.AddAction(UIAlertAction.Create("Restart", UIAlertActionStyle.Destructive, async _ =>
            {
                await RemotePCService.Instance.RestartAsync(
      _userData.RemotePCHost!,
 _userData.RemotePCPort,
      _userData.RemotePCAuthToken);
            }));
   PresentViewController(alert, true, null);
        }

        private async void OnShutdownRequested(object? sender, EventArgs e)
        {
            if (_userData == null) return;

  var alert = UIAlertController.Create("Shutdown PC?", "This will shutdown your PC.", UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
  alert.AddAction(UIAlertAction.Create("Shutdown", UIAlertActionStyle.Destructive, async _ =>
    {
                await RemotePCService.Instance.ShutdownAsync(
        _userData.RemotePCHost!,
              _userData.RemotePCPort,
     _userData.RemotePCAuthToken);
            }));
       PresentViewController(alert, true, null);
        }

      private async void LaunchGame(RemoteGame game)
   {
            if (_userData == null) return;

        var alert = UIAlertController.Create($"Launch {game.Name}?", "Start this game on your PC.", UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
          alert.AddAction(UIAlertAction.Create("Launch", UIAlertActionStyle.Default, async _ =>
   {
        _loadingIndicator.StartAnimating();
     var success = await RemotePCService.Instance.LaunchGameAsync(
 _userData.RemotePCHost!,
               _userData.RemotePCPort,
 game.Id,
       _userData.RemotePCAuthToken);

   if (success)
      ShowAlert("Launching", $"{game.Name} is starting on your PC.");
    else
        ShowAlert("Failed", "Could not launch the game.");

          _loadingIndicator.StopAnimating();
            }));

   // Add option to launch and stream
            alert.AddAction(UIAlertAction.Create("Launch & Stream", UIAlertActionStyle.Default, async _ =>
        {
      _loadingIndicator.StartAnimating();
  var success = await RemotePCService.Instance.LaunchGameAsync(
    _userData.RemotePCHost!,
    _userData.RemotePCPort,
  game.Id,
       _userData.RemotePCAuthToken);

          _loadingIndicator.StopAnimating();

     if (success)
       {
   // Wait a bit for game to start, then open stream
   await Task.Delay(2000);
        var streamVC = new GameStreamViewController(
         _userData.RemotePCHost!,
   19501, // Stream WebSocket port
     _userData.RemotePCAuthToken);
            PresentViewController(streamVC, true, null);
     }
    else
     {
        ShowAlert("Failed", "Could not launch the game.");
     }
      }));

      PresentViewController(alert, true, null);
        }

        private void ShowSettings(object? sender, EventArgs e)
        {
            var settingsVC = new RemotePCSettingsViewController();
     settingsVC.SettingsSaved += async (s, userData) =>
            {
        _userData = userData;
       await RefreshStatus();
   };
         NavigationController?.PushViewController(settingsVC, true);
        }

        private void ShowAlert(string title, string message)
        {
 var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
   PresentViewController(alert, true, null);
}

        private class RemoteGamesCollectionSource : UICollectionViewDataSource
        {
            private readonly RemotePCViewController _parent;
 public RemoteGamesCollectionSource(RemotePCViewController parent) => _parent = parent;

      public override nint GetItemsCount(UICollectionView collectionView, nint section) =>
    _parent._installedGames.Count == 0 ? 0 : _parent._installedGames.Count;

  public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
      {
          var cell = (RemoteGameCell)collectionView.DequeueReusableCell(RemoteGameCell.ReuseId, indexPath);
   if (indexPath.Row < _parent._installedGames.Count)
      cell.Configure(_parent._installedGames[(int)indexPath.Row]);
  return cell;
            }
        }

     private class RemoteGamesCollectionDelegate : UICollectionViewDelegate
        {
  private readonly RemotePCViewController _parent;
            public RemoteGamesCollectionDelegate(RemotePCViewController parent) => _parent = parent;

    public override void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
{
         collectionView.DeselectItem(indexPath, true);
    if (_parent._installedGames.Count > 0 && indexPath.Row < _parent._installedGames.Count)
             _parent.LaunchGame(_parent._installedGames[(int)indexPath.Row]);
            }
        }
 }

    public class RemoteGameCell : UICollectionViewCell
    {
 public static readonly string ReuseId = "RemoteGameCell";
        private UIView _cardView = null!;
        private UIView _iconContainer = null!;
        private UILabel _iconLabel = null!;
        private UILabel _nameLabel = null!;
        private UILabel _platformLabel = null!;
  private UIView _runningIndicator = null!;

        [Export("initWithFrame:")]
     public RemoteGameCell(CGRect frame) : base(frame) => SetupViews();

        private void SetupViews()
        {
            _cardView = new UIView { BackgroundColor = UIColor.SecondarySystemBackground };
  _cardView.Layer.CornerRadius = 12;
            ContentView.AddSubview(_cardView);

  _iconContainer = new UIView { BackgroundColor = UIColor.SystemGray5 };
            _iconContainer.Layer.CornerRadius = 12;
         _iconContainer.Layer.MaskedCorners = (CoreAnimation.CACornerMask)3;
  _cardView.AddSubview(_iconContainer);

            _iconLabel = new UILabel
      {
   Font = UIFont.BoldSystemFontOfSize(32),
            TextColor = UIColor.SystemGray3,
          TextAlignment = UITextAlignment.Center
    };
            _iconContainer.AddSubview(_iconLabel);

          _nameLabel = new UILabel
{
         Font = UIFont.BoldSystemFontOfSize(13),
    TextColor = UIColor.Label,
                Lines = 2,
       LineBreakMode = UILineBreakMode.TailTruncation
        };
            _cardView.AddSubview(_nameLabel);

  _platformLabel = new UILabel
            {
    Font = UIFont.SystemFontOfSize(11),
       TextColor = UIColor.SecondaryLabel
       };
            _cardView.AddSubview(_platformLabel);

   _runningIndicator = new UIView { BackgroundColor = UIColor.SystemGreen, Hidden = true };
    _runningIndicator.Layer.CornerRadius = 4;
            _cardView.AddSubview(_runningIndicator);
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            _cardView.Frame = ContentView.Bounds;
          _iconContainer.Frame = new CGRect(0, 0, _cardView.Bounds.Width, 110);
         _iconLabel.Frame = _iconContainer.Bounds;
     _nameLabel.Frame = new CGRect(10, 118, _cardView.Bounds.Width - 20, 36);
            _platformLabel.Frame = new CGRect(10, 156, _cardView.Bounds.Width - 20, 16);
            _runningIndicator.Frame = new CGRect(_cardView.Bounds.Width - 16, 8, 8, 8);
        }

      public void Configure(RemoteGame game)
        {
         _nameLabel.Text = game.Name;
  _platformLabel.Text = game.Platform ?? "Unknown";
     _runningIndicator.Hidden = !game.IsRunning;

            var initial = string.IsNullOrEmpty(game.Name) ? "G" : game.Name[..1].ToUpper();
  _iconLabel.Text = initial;

            _iconContainer.BackgroundColor = game.Platform?.ToLower() switch
        {
    "steam" => UIColor.FromRGB(27, 40, 56),
                "epic" => UIColor.FromRGB(42, 42, 42),
 "gog" => UIColor.FromRGB(134, 50, 155),
             _ => UIColor.SystemGray5
    };

        _iconLabel.TextColor = game.Platform?.ToLower() switch
      {
   "steam" or "epic" or "gog" => UIColor.White,
             _ => UIColor.SystemGray3
            };
   }

        public override void PrepareForReuse()
    {
            base.PrepareForReuse();
         _nameLabel.Text = null;
            _platformLabel.Text = null;
         _runningIndicator.Hidden = true;
   }
    }

    public class RemotePCSettingsViewController : UITableViewController
    {
   private UserData _userData = null!;
   private UITextField _hostField = null!;
        private UITextField _portField = null!;
 private UITextField _macField = null!;
  private UITextField _tokenField = null!;

        public event EventHandler<UserData>? SettingsSaved;

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
      Title = "PC Settings";
            TableView = new UITableView(View!.Bounds, UITableViewStyle.InsetGrouped);
 NavigationItem.RightBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Save, SaveSettings);
         LoadData();
        }

        private async void LoadData()
   {
            _userData = await DatabaseService.Instance.GetUserDataAsync();
       TableView.ReloadData();
        }

        public override nint NumberOfSections(UITableView tableView) => 3;
      public override nint RowsInSection(UITableView tableView, nint section) => section switch
   {
      0 => 2,
      1 => 2,
            2 => 1,
            _ => 0
        };

        public override string TitleForHeader(UITableView tableView, nint section) => section switch
        {
    0 => "Connection",
        1 => "Advanced",
    2 => "",
            _ => ""
      };

    public override string TitleForFooter(UITableView tableView, nint section) => section switch
    {
   0 => "Enter your PC's IP address and port (default: 19500).",
      1 => "MAC address enables Wake-on-LAN. Auth token is optional.",
         _ => ""
     };

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
   {
            if (indexPath.Section == 2)
            {
             var buttonCell = new UITableViewCell(UITableViewCellStyle.Default, null);
          buttonCell.TextLabel!.Text = "Test Connection";
  buttonCell.TextLabel.TextColor = UIColor.SystemBlue;
                buttonCell.TextLabel.TextAlignment = UITextAlignment.Center;
         return buttonCell;
            }

       var cell = new UITableViewCell(UITableViewCellStyle.Default, null);
    cell.SelectionStyle = UITableViewCellSelectionStyle.None;

      var textField = new UITextField(new CGRect(120, 12, tableView.Frame.Width - 140, 24))
    {
       KeyboardType = (indexPath.Section == 0 && indexPath.Row == 1) ? UIKeyboardType.NumberPad : UIKeyboardType.Default,
    AutocorrectionType = UITextAutocorrectionType.No,
         AutocapitalizationType = UITextAutocapitalizationType.None,
          ClearButtonMode = UITextFieldViewMode.WhileEditing
       };

            if (indexPath.Section == 0)
            {
                if (indexPath.Row == 0)
                {
cell.TextLabel!.Text = "Host / IP";
            _hostField = textField;
            textField.Placeholder = "192.168.1.100";
       textField.Text = _userData?.RemotePCHost ?? "";
           }
     else
 {
   cell.TextLabel!.Text = "Port";
    _portField = textField;
               textField.Placeholder = "19500";
                    textField.Text = _userData?.RemotePCPort.ToString() ?? "19500";
        }
   }
 else if (indexPath.Section == 1)
            {
      if (indexPath.Row == 0)
        {
        cell.TextLabel!.Text = "MAC Address";
              _macField = textField;
   textField.Placeholder = "AA:BB:CC:DD:EE:FF";
             textField.Text = _userData?.RemotePCMacAddress ?? "";
           }
     else
      {
    cell.TextLabel!.Text = "Auth Token";
     _tokenField = textField;
         textField.Placeholder = "Optional";
   textField.Text = _userData?.RemotePCAuthToken ?? "";
            textField.SecureTextEntry = true;
              }
      }

        cell.ContentView.AddSubview(textField);
    return cell;
        }

  public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
         tableView.DeselectRow(indexPath, true);
            if (indexPath.Section == 2)
    TestConnection();
        }

        private async void TestConnection()
   {
            var host = _hostField?.Text?.Trim();
         var portText = _portField?.Text?.Trim();

       if (string.IsNullOrEmpty(host))
         {
    ShowAlert("Missing Host", "Please enter your PC's IP address.");
          return;
            }

   if (!int.TryParse(portText, out var port))
     port = 19500;

            var alert = UIAlertController.Create("Testing...", $"Connecting to {host}:{port}", UIAlertControllerStyle.Alert);
       PresentViewController(alert, true, null);

            var status = await RemotePCService.Instance.GetStatusAsync(host, port, _tokenField?.Text);

      await DismissViewControllerAsync(true);

          if (status.IsOnline)
            {
   ShowAlert("Connected", $"Host: {status.Hostname}\nCPU: {status.CpuUsage:0}%\nRAM: {status.MemoryUsage:0}%");
 }
            else
         {
      var pingOk = await RemotePCService.Instance.PingAsync(host, port);
        if (pingOk)
   ShowAlert("Partial Connection", $"Can reach {host}:{port} but the app is not responding correctly.");
   else
   ShowAlert("Connection Failed", $"Cannot reach {host}:{port}. Check that Synktra Companion is running.");
      }
        }

 private async void SaveSettings(object? sender, EventArgs e)
      {
    if (_userData == null) _userData = new UserData();

 _userData.RemotePCHost = _hostField?.Text?.Trim();
   _userData.RemotePCPort = int.TryParse(_portField?.Text?.Trim(), out var port) ? port : 19500;
      _userData.RemotePCMacAddress = _macField?.Text?.Trim()?.ToUpper();
            _userData.RemotePCAuthToken = _tokenField?.Text?.Trim();

     await DatabaseService.Instance.SaveUserDataAsync(_userData);
            SettingsSaved?.Invoke(this, _userData);
       NavigationController?.PopViewController(true);
        }

        private void ShowAlert(string title, string message)
     {
            var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
     PresentViewController(alert, true, null);
        }
    }
}
