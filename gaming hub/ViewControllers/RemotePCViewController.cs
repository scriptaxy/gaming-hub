using gaming_hub.Models;
using gaming_hub.Services;
using gaming_hub.Views;

namespace gaming_hub.ViewControllers
{
 public class RemotePCViewController : UIViewController
    {
    private UIScrollView _scrollView = null!;
        private RemotePCStatusView _statusView = null!;
        private UILabel _gamesHeader = null!;
        private UITableView _gamesTableView = null!;
        private UIActivityIndicatorView _loadingIndicator = null!;
        private List<RemoteGame> _installedGames = [];
   private UserData _userData = null!;

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
     View.AddSubview(_scrollView);

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
   _statusView.Frame = new CGRect(padding, padding, width - padding * 2, 180);
     _gamesHeader.Frame = new CGRect(padding, _statusView.Frame.Bottom + padding, width - padding * 2, 24);

   var tableHeight = Math.Max(_installedGames.Count * 60, 200);
     _gamesTableView.Frame = new CGRect(padding, _gamesHeader.Frame.Bottom + 8, width - padding * 2, tableHeight);

 _scrollView.ContentSize = new CGSize(width, _gamesTableView.Frame.Bottom + padding);
     _loadingIndicator.Center = new CGPoint(width / 2, height / 2);
        }

        private async void LoadData()
        {
    _userData = await DatabaseService.Instance.GetUserDataAsync();
            await RefreshStatus();
   }

   private async Task RefreshStatus()
        {
       if (_userData == null || string.IsNullOrEmpty(_userData.RemotePCHost))
   {
   _statusView.UpdateStatus(new RemotePCStatus { IsOnline = false });
         return;
  }

    var status = await RemotePCService.Instance.GetStatusAsync(_userData.RemotePCHost, _userData.RemotePCPort, _userData.RemotePCAuthToken);
    _statusView.UpdateStatus(status);

  if (status.IsOnline)
            {
     _installedGames = await RemotePCService.Instance.GetInstalledGamesAsync(_userData.RemotePCHost, _userData.RemotePCPort, _userData.RemotePCAuthToken);
    _gamesTableView.ReloadData();
    ViewDidLayoutSubviews();
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
   ShowAlert("Wake Signal Sent", "Wake-on-LAN signal sent. Your PC should start in a few moments.");
      await Task.Delay(5000);
    await RefreshStatus();
       }
            else
    {
     ShowAlert("Wake Failed", "Could not send wake signal. Check your network settings.");
     }
            _loadingIndicator.StopAnimating();
     }

 private async void OnSleepRequested(object? sender, EventArgs e)
        {
 if (_userData == null) return;
   var success = await RemotePCService.Instance.SleepAsync(_userData.RemotePCHost, _userData.RemotePCPort, _userData.RemotePCAuthToken);
 if (success)
 {
         await Task.Delay(2000);
   await RefreshStatus();
        }
    }

        private async void LaunchGame(RemoteGame game)
        {
   if (_userData == null) return;
   _loadingIndicator.StartAnimating();
           var success = await RemotePCService.Instance.LaunchGameAsync(_userData.RemotePCHost, _userData.RemotePCPort, game.Id, _userData.RemotePCAuthToken);
  ShowAlert(success ? "Game Launched" : "Launch Failed", success ? $"{game.Name} is starting!" : "Could not launch the game.");
        _loadingIndicator.StopAnimating();
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
          public RemoteGamesDataSource(RemotePCViewController parent) { _parent = parent; }
    public override nint RowsInSection(UITableView tableView, nint section) => _parent._installedGames.Count;
     public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
{
      var cell = tableView.DequeueReusableCell("RemoteGameCell", indexPath);
       var game = _parent._installedGames[indexPath.Row];
     var config = UIListContentConfiguration.SubtitleCellConfiguration;
         config.Text = game.Name;
        config.SecondaryText = game.Platform;
        config.Image = UIImage.GetSystemImage("gamecontroller.fill");
                cell.ContentConfiguration = config;
                cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
      return cell;
   }
        }

        private class RemoteGamesDelegate : UITableViewDelegate
        {
   private readonly RemotePCViewController _parent;
     public RemoteGamesDelegate(RemotePCViewController parent) { _parent = parent; }
      public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
      {
         tableView.DeselectRow(indexPath, true);
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

        public event EventHandler<UserData>? SettingsSaved;

    public override void ViewDidLoad()
   {
    base.ViewDidLoad();
        Title = "Remote PC Settings";
    NavigationItem.RightBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Save, SaveSettings);
 LoadData();
    }

  private async void LoadData()
        {
            _userData = await DatabaseService.Instance.GetUserDataAsync();
     TableView.ReloadData();
   }

     public override nint NumberOfSections(UITableView tableView) => 1;
        public override nint RowsInSection(UITableView tableView, nint section) => 4;

    public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var cell = new UITableViewCell(UITableViewCellStyle.Default, null);
       cell.SelectionStyle = UITableViewCellSelectionStyle.None;

         var textField = new UITextField(new CGRect(120, 12, tableView.Frame.Width - 140, 24))
 {
        Placeholder = indexPath.Row switch { 0 => "192.168.1.100", 1 => "5000", 2 => "AA:BB:CC:DD:EE:FF", _ => "optional" },
    KeyboardType = indexPath.Row == 1 ? UIKeyboardType.NumberPad : UIKeyboardType.Default,
        AutocorrectionType = UITextAutocorrectionType.No,
    AutocapitalizationType = UITextAutocapitalizationType.None
  };

     switch (indexPath.Row)
 {
              case 0:
          cell.TextLabel!.Text = "Host";
       _hostField = textField;
       textField.Text = _userData?.RemotePCHost ?? "";
         break;
       case 1:
  cell.TextLabel!.Text = "Port";
     _portField = textField;
       textField.Text = _userData?.RemotePCPort.ToString() ?? "5000";
     break;
       case 2:
   cell.TextLabel!.Text = "MAC Address";
        _macField = textField;
       textField.Text = _userData?.RemotePCMacAddress ?? "";
     break;
     case 3:
     cell.TextLabel!.Text = "Auth Token";
       _tokenField = textField;
      textField.Text = _userData?.RemotePCAuthToken ?? "";
         textField.SecureTextEntry = true;
        break;
            }

            cell.ContentView.AddSubview(textField);
   return cell;
    }

    private async void SaveSettings(object? sender, EventArgs e)
        {
   if (_userData == null) _userData = new UserData();
   _userData.RemotePCHost = _hostField?.Text;
  _userData.RemotePCPort = int.TryParse(_portField?.Text, out var port) ? port : 5000;
      _userData.RemotePCMacAddress = _macField?.Text;
  _userData.RemotePCAuthToken = _tokenField?.Text;

  await DatabaseService.Instance.SaveUserDataAsync(_userData);
         SettingsSaved?.Invoke(this, _userData);
  NavigationController?.PopViewController(true);
        }
    }
}
