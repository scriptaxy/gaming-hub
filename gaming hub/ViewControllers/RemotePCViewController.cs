using gaming_hub.Models;
using gaming_hub.Services;
using gaming_hub.Views;

namespace gaming_hub.ViewControllers
{
    public class RemotePCViewController : UIViewController
    {
   private UIScrollView _scrollView = null!;
        private RemotePCStatusView _statusView = null!;
   private UILabel _setupLabel = null!;
   private UILabel _gamesHeader = null!;
      private UITableView _gamesTableView = null!;
  private UIActivityIndicatorView _loadingIndicator = null!;
        private UIRefreshControl _refreshControl = null!;
  private List<RemoteGame> _installedGames = [];
        private UserData _userData = null!;
   private bool _isConfigured;

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
       NavigationItem.RightBarButtonItem = new UIBarButtonItem(UIImage.GetSystemImage("gear"), UIBarButtonItemStyle.Plain, ShowSettings);

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
   Text = "No PC Configured\n\nTap the gear icon to configure your gaming PC.\n\nYou'll need:\n• Your PC's IP address\n• MAC address (for Wake-on-LAN)\n• A companion app running on your PC",
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
   _scrollView.AddSubview(_statusView);

 _gamesHeader = new UILabel
     {
          Text = "Installed Games",
   Font = UIFont.BoldSystemFontOfSize(20),
   TextColor = UIColor.Label
    };
        _scrollView.AddSubview(_gamesHeader);

    _gamesTableView = new UITableView(CGRect.Empty, UITableViewStyle.Plain)
         {
  BackgroundColor = UIColor.Clear,
            SeparatorStyle = UITableViewCellSeparatorStyle.None,
      ScrollEnabled = false,
RowHeight = 60
  };
     _gamesTableView.RegisterClassForCellReuse(typeof(UITableViewCell), "RemoteGameCell");
     _gamesTableView.DataSource = new RemoteGamesDataSource(this);
            _gamesTableView.Delegate = new RemoteGamesDelegate(this);
         _scrollView.AddSubview(_gamesTableView);

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
   _gamesHeader.Hidden = true;
    _gamesTableView.Hidden = true;
_setupLabel.Frame = new CGRect(padding, 100, width - padding * 2, 300);
          _scrollView.ContentSize = new CGSize(width, 400);
   }
     else
   {
_setupLabel.Hidden = true;
   _statusView.Hidden = false;
 _gamesHeader.Hidden = false;
       _gamesTableView.Hidden = false;
      
          _statusView.Frame = new CGRect(padding, padding, width - padding * 2, 180);
     _gamesHeader.Frame = new CGRect(padding, _statusView.Frame.Bottom + padding, width - padding * 2, 24);
  
      var tableHeight = Math.Max(_installedGames.Count * 60, 60);
       _gamesTableView.Frame = new CGRect(padding, _gamesHeader.Frame.Bottom + 8, width - padding * 2, tableHeight);
  
    _scrollView.ContentSize = new CGSize(width, _gamesTableView.Frame.Bottom + padding);
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

          if (status.IsOnline)
    {
         _installedGames = await RemotePCService.Instance.GetInstalledGamesAsync(
          _userData.RemotePCHost!, 
            _userData.RemotePCPort, 
    _userData.RemotePCAuthToken);
   _gamesTableView.ReloadData();
      ViewDidLayoutSubviews();
   }
          else
      {
       _installedGames.Clear();
_gamesTableView.ReloadData();
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
              ShowAlert("MAC Address Required", "Configure your PC's MAC address in settings to use Wake-on-LAN.\n\nYou can find your MAC address by running 'ipconfig /all' on Windows or checking your network adapter settings.");
    return;
 }

   _loadingIndicator.StartAnimating();
          
  var success = await RemotePCService.Instance.WakeOnLanAsync(_userData.RemotePCMacAddress);

      if (success)
  {
     ShowAlert("Wake Signal Sent", "Wake-on-LAN magic packet sent!\n\nYour PC should start within 10-30 seconds. Make sure Wake-on-LAN is enabled in your BIOS/UEFI settings.");
       
   // Wait and check status
      await Task.Delay(10000);
      await RefreshStatus();
      }
   else
   {
       ShowAlert("Wake Failed", "Could not send wake signal.\n\nMake sure:\n• Your iPhone is on the same network as your PC\n• The MAC address is correct\n• Wake-on-LAN is enabled on your PC");
  }
         
          _loadingIndicator.StopAnimating();
        }

    private async void OnSleepRequested(object? sender, EventArgs e)
     {
      if (_userData == null) return;
    
  var alert = UIAlertController.Create("Sleep PC?", "This will put your gaming PC to sleep.", UIAlertControllerStyle.Alert);
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
      ShowAlert("Sleep Failed", "Could not put PC to sleep. The companion app may not be responding.");
 }
 }));
     PresentViewController(alert, true, null);
}

        private async void LaunchGame(RemoteGame game)
        {
    if (_userData == null) return;
   
      var alert = UIAlertController.Create($"Launch {game.Name}?", "This will start the game on your PC.", UIAlertControllerStyle.Alert);
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
   ShowAlert("Game Launching", $"{game.Name} is starting on your PC!");
         else
             ShowAlert("Launch Failed", "Could not launch the game. Make sure your PC is on and the companion app is running.");
            
         _loadingIndicator.StopAnimating();
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

        private class RemoteGamesDataSource : UITableViewDataSource
        {
  private readonly RemotePCViewController _parent;
          public RemoteGamesDataSource(RemotePCViewController parent) => _parent = parent;
         
  public override nint RowsInSection(UITableView tableView, nint section) => 
      _parent._installedGames.Count == 0 ? 1 : _parent._installedGames.Count;
  
    public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
    {
  var cell = tableView.DequeueReusableCell("RemoteGameCell", indexPath);
    
     if (_parent._installedGames.Count == 0)
   {
      var config = UIListContentConfiguration.SubtitleCellConfiguration;
        config.Text = "No games available";
      config.SecondaryText = "Games will appear when your PC is online";
       config.Image = UIImage.GetSystemImage("gamecontroller");
      config.ImageProperties.TintColor = UIColor.SystemGray;
        cell.ContentConfiguration = config;
  cell.SelectionStyle = UITableViewCellSelectionStyle.None;
  }
      else
 {
    var game = _parent._installedGames[indexPath.Row];
        var config = UIListContentConfiguration.SubtitleCellConfiguration;
       config.Text = game.Name;
       config.SecondaryText = game.Platform ?? "Unknown Platform";
        config.Image = UIImage.GetSystemImage(game.IsRunning ? "play.circle.fill" : "gamecontroller.fill");
      config.ImageProperties.TintColor = game.IsRunning ? UIColor.SystemGreen : UIColor.SystemBlue;
          cell.ContentConfiguration = config;
      cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
       cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
    }

   return cell;
        }
  }

  private class RemoteGamesDelegate : UITableViewDelegate
        {
  private readonly RemotePCViewController _parent;
         public RemoteGamesDelegate(RemotePCViewController parent) => _parent = parent;
    
 public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
         {
        tableView.DeselectRow(indexPath, true);
       if (_parent._installedGames.Count > 0 && indexPath.Row < _parent._installedGames.Count)
    _parent.LaunchGame(_parent._installedGames[indexPath.Row]);
   }
        }
  }

    public class RemotePCSettingsViewController : UITableViewController
    {
        private UserData _userData = null!;
  private UITextField _hostField = null!;
        private UITextField _portField = null!;
        private UITextField _macField = null!;
        private UITextField _tokenField = null!;
     private UIButton _testButton = null!;
   private UIActivityIndicatorView _testIndicator = null!;

      public event EventHandler<UserData>? SettingsSaved;

 public override void ViewDidLoad()
    {
    base.ViewDidLoad();
      Title = "Remote PC Settings";
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
0 => 2, // Host, Port
            1 => 2, // MAC, Token
       2 => 1, // Test connection
      _ => 0
        };

        public override string TitleForHeader(UITableView tableView, nint section) => section switch
        {
            0 => "Connection",
   1 => "Wake-on-LAN & Security",
  2 => "",
    _ => ""
        };

   public override string TitleForFooter(UITableView tableView, nint section) => section switch
    {
  0 => "Enter your PC's local IP address (e.g., 192.168.1.100) and the port your companion app uses.",
    1 => "MAC address is required for Wake-on-LAN. Format: AA:BB:CC:DD:EE:FF\nAuth token is optional for secured connections.",
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
  textField.Placeholder = "5000";
textField.Text = _userData?.RemotePCPort.ToString() ?? "5000";
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
     port = 5000;

     var alert = UIAlertController.Create("Testing Connection...", $"Connecting to {host}:{port}", UIAlertControllerStyle.Alert);
   PresentViewController(alert, true, null);

   var status = await RemotePCService.Instance.GetStatusAsync(host, port, _tokenField?.Text);
            
    await DismissViewControllerAsync(true);

     if (status.IsOnline)
            {
     ShowAlert("Connection Successful! ?", $"Connected to: {status.Hostname}\nCPU: {status.CpuUsage:0}%\nRAM: {status.MemoryUsage:0}%");
 }
 else
  {
     // Try just a ping
 var pingOk = await RemotePCService.Instance.PingAsync(host, port);
    if (pingOk)
        ShowAlert("Partial Connection", $"Can reach {host}:{port} but the companion app is not responding correctly.\n\nMake sure the companion app is running and configured properly.");
     else
      ShowAlert("Connection Failed", $"Cannot reach {host}:{port}\n\nMake sure:\n• Your PC is turned on\n• The companion app is running\n• You're on the same network\n• The IP address and port are correct");
   }
        }

    private async void SaveSettings(object? sender, EventArgs e)
      {
   if (_userData == null) _userData = new UserData();
    
     _userData.RemotePCHost = _hostField?.Text?.Trim();
      _userData.RemotePCPort = int.TryParse(_portField?.Text?.Trim(), out var port) ? port : 5000;
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
