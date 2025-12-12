using gaming_hub.Models;
using gaming_hub.Services;
using AuthenticationServices;
using WebKit;

namespace gaming_hub.ViewControllers
{
  public class SettingsViewController : UITableViewController
    {
        private UserData _userData = null!;

      public override void ViewDidLoad()
        {
            base.ViewDidLoad();
      Title = "Settings";
            if (NavigationController != null)
      NavigationController.NavigationBar.PrefersLargeTitles = true;
     TableView = new UITableView(View!.Bounds, UITableViewStyle.InsetGrouped);
            LoadData();
    }

        public override void ViewWillAppear(bool animated)
        {
       base.ViewWillAppear(animated);
       LoadData();
   }

        private async void LoadData()
  {
   _userData = await DatabaseService.Instance.GetUserDataAsync();
    TableView.ReloadData();
     }

        public override nint NumberOfSections(UITableView tableView) => 4;

    public override nint RowsInSection(UITableView tableView, nint section) => section switch
   {
     0 => 2,
            1 => 1,
     2 => 3,
   3 => 2,
            _ => 0
        };

  public override string TitleForHeader(UITableView tableView, nint section) => section switch
        {
       0 => "Connected Accounts",
            1 => "Remote PC",
   2 => "Preferences",
         3 => "About",
          _ => ""
    };

 public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
      {
  var cell = new UITableViewCell(UITableViewCellStyle.Value1, null);

            switch (indexPath.Section)
     {
    case 0:
                if (indexPath.Row == 0)
     {
    cell.TextLabel!.Text = "Steam";
   cell.ImageView!.Image = UIImage.GetSystemImage("gamecontroller.fill");
       cell.ImageView.TintColor = UIColor.FromRGB(27, 40, 56);
      var isConnected = !string.IsNullOrEmpty(_userData?.SteamId);
  cell.DetailTextLabel!.Text = isConnected ? "Connected" : "Not Connected";
           cell.DetailTextLabel.TextColor = isConnected ? UIColor.SystemGreen : UIColor.SecondaryLabel;
    cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
              }
         else
          {
    cell.TextLabel!.Text = "Epic Games";
        cell.ImageView!.Image = UIImage.GetSystemImage("e.square.fill");
           cell.ImageView.TintColor = UIColor.Black;
    var isConnected = !string.IsNullOrEmpty(_userData?.EpicAccountId);
  cell.DetailTextLabel!.Text = isConnected ? "Connected" : "Not Connected";
         cell.DetailTextLabel.TextColor = isConnected ? UIColor.SystemGreen : UIColor.SecondaryLabel;
          cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
           }
    break;

       case 1:
    cell.TextLabel!.Text = "Configure Remote PC";
     cell.ImageView!.Image = UIImage.GetSystemImage("desktopcomputer");
              cell.ImageView.TintColor = UIColor.SystemBlue;
            cell.DetailTextLabel!.Text = string.IsNullOrEmpty(_userData?.RemotePCHost) ? "Not Configured" : _userData.RemotePCHost;
      cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
              break;

      case 2:
             if (indexPath.Row == 0)
        {
         cell.TextLabel!.Text = "Dark Mode";
         cell.ImageView!.Image = UIImage.GetSystemImage("moon.fill");
     cell.ImageView.TintColor = UIColor.SystemPurple;
      var toggle = new UISwitch { On = _userData?.DarkModeEnabled ?? true };
         toggle.ValueChanged += async (s, e) =>
         {
          if (_userData != null)
          {
      _userData.DarkModeEnabled = toggle.On;
        await DatabaseService.Instance.SaveUserDataAsync(_userData);
           SceneDelegate.Current?.ApplyTheme(toggle.On);
       }
      };
          cell.AccessoryView = toggle;
     cell.SelectionStyle = UITableViewCellSelectionStyle.None;
  }
else if (indexPath.Row == 1)
 {
         cell.TextLabel!.Text = "Deal Alerts";
        cell.ImageView!.Image = UIImage.GetSystemImage("bell.fill");
 cell.ImageView.TintColor = UIColor.SystemOrange;
  var toggle = new UISwitch { On = _userData?.DealAlertsEnabled ?? true };
             toggle.ValueChanged += async (s, e) =>
      {
       if (_userData != null) { _userData.DealAlertsEnabled = toggle.On; await DatabaseService.Instance.SaveUserDataAsync(_userData); }
  };
    cell.AccessoryView = toggle;
             cell.SelectionStyle = UITableViewCellSelectionStyle.None;
          }
       else
          {
      cell.TextLabel!.Text = "Deal Threshold";
 cell.ImageView!.Image = UIImage.GetSystemImage("tag.fill");
              cell.ImageView.TintColor = UIColor.SystemGreen;
     cell.DetailTextLabel!.Text = $"{_userData?.DealThreshold ?? 50}% off";
    cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
     }
       break;

                case 3:
        if (indexPath.Row == 0)
             {
   cell.TextLabel!.Text = "Version";
   cell.ImageView!.Image = UIImage.GetSystemImage("info.circle.fill");
      cell.ImageView.TintColor = UIColor.SystemGray;
         cell.DetailTextLabel!.Text = "1.0.0";
            cell.SelectionStyle = UITableViewCellSelectionStyle.None;
            }
 else
            {
        cell.TextLabel!.Text = "Rate App";
       cell.ImageView!.Image = UIImage.GetSystemImage("star.fill");
cell.ImageView.TintColor = UIColor.SystemYellow;
          cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
        }
         break;
    }

       return cell;
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
     {
            tableView.DeselectRow(indexPath, true);

      switch (indexPath.Section)
         {
  case 0:
       if (indexPath.Row == 0)
    NavigationController?.PushViewController(new SteamSettingsViewController(), true);
         else
                 NavigationController?.PushViewController(new EpicSettingsViewController(), true);
           break;
     case 1:
       var remotePCSettings = new RemotePCSettingsViewController();
              NavigationController?.PushViewController(remotePCSettings, true);
 break;
                case 2:
     if (indexPath.Row == 2)
        ShowDealThresholdPicker();
      break;
                case 3:
   if (indexPath.Row == 1)
          {
       // Open App Store for rating
      }
        break;
            }
    }

      private void ShowDealThresholdPicker()
        {
       var alert = UIAlertController.Create("Deal Alert Threshold", "Notify when discount is at least:", UIAlertControllerStyle.ActionSheet);
  foreach (var threshold in new[] { 25, 50, 75, 90 })
        {
        alert.AddAction(UIAlertAction.Create($"{threshold}%", UIAlertActionStyle.Default, async _ =>
         {
     if (_userData != null)
        {
       _userData.DealThreshold = threshold;
                await DatabaseService.Instance.SaveUserDataAsync(_userData);
             TableView.ReloadData();
    }
            }));
            }
    alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
          PresentViewController(alert, true, null);
        }
    }

    public class SteamSettingsViewController : UITableViewController
    {
        private UserData _userData = null!;
 private UITextField _steamIdField = null!;
  private UITextField _apiKeyField = null!;
        private SteamProfile? _profile;
        private bool _isLoading;

      public override void ViewDidLoad()
        {
   base.ViewDidLoad();
   Title = "Steam";
    TableView = new UITableView(View!.Bounds, UITableViewStyle.InsetGrouped);
            NavigationItem.RightBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Save, SaveSettings);
      LoadData();
        }

        private async void LoadData()
        {
       _userData = await DatabaseService.Instance.GetUserDataAsync();
            
 if (!string.IsNullOrEmpty(_userData.SteamId) && !string.IsNullOrEmpty(_userData.SteamApiKey))
            {
        _isLoading = true;
       TableView.ReloadData();
    _profile = await SteamService.Instance.GetPlayerSummaryAsync(_userData.SteamId, _userData.SteamApiKey);
                _isLoading = false;
       }
       TableView.ReloadData();
      }

        public override nint NumberOfSections(UITableView tableView) => _profile != null ? 3 : 2;
        
        public override nint RowsInSection(UITableView tableView, nint section)
     {
    if (_profile != null)
            {
     return section switch
    {
              0 => 1,
          1 => 2,
    2 => 2,
      _ => 0
                };
            }
       return section switch
            {
   0 => 2,
              1 => 1,
          _ => 0
            };
        }

        public override string TitleForHeader(UITableView tableView, nint section)
      {
          if (_profile != null)
        {
         return section switch
       {
               0 => "Connected Account",
        1 => "Steam Credentials",
              2 => "Actions",
     _ => ""
                };
          }
 return section switch
  {
    0 => "Steam Credentials",
        1 => "Actions",
      _ => ""
       };
 }

    public override string TitleForFooter(UITableView tableView, nint section)
        {
      var credSection = _profile != null ? 1 : 0;
 if (section == credSection)
   return "Get your Steam ID from your profile URL and API key from steamcommunity.com/dev/apikey";
      return "";
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
 {
            var cell = new UITableViewCell(UITableViewCellStyle.Subtitle, null);
            cell.SelectionStyle = UITableViewCellSelectionStyle.None;

    var section = indexPath.Section;
        var row = indexPath.Row;

    if (_profile != null)
            {
     if (section == 0)
        {
    cell.TextLabel!.Text = _profile.PersonaName;
      cell.DetailTextLabel!.Text = _profile.IsOnline ? "Online" : "Offline";
       cell.DetailTextLabel.TextColor = _profile.IsOnline ? UIColor.SystemGreen : UIColor.SecondaryLabel;
           
  if (!string.IsNullOrEmpty(_profile.CurrentGame))
         {
                 cell.DetailTextLabel.Text = $"Playing: {_profile.CurrentGame}";
     cell.DetailTextLabel.TextColor = UIColor.SystemGreen;
  }
              
          if (!string.IsNullOrEmpty(_profile.AvatarUrl))
     {
          LoadAvatarAsync(cell, _profile.AvatarUrl);
  }
         else
        {
      cell.ImageView!.Image = UIImage.GetSystemImage("person.circle.fill");
 }
      return cell;
  }
          section--;
    }

      if (section == 0)
            {
      cell = new UITableViewCell(UITableViewCellStyle.Default, null);
       cell.SelectionStyle = UITableViewCellSelectionStyle.None;
       
            var textField = new UITextField(new CGRect(100, 12, tableView.Frame.Width - 120, 24))
   {
          AutocorrectionType = UITextAutocorrectionType.No,
  AutocapitalizationType = UITextAutocapitalizationType.None
     };

      if (row == 0)
 {
                cell.TextLabel!.Text = "Steam ID";
         _steamIdField = textField;
                    textField.Placeholder = "76561198xxxxxxxxx";
           textField.Text = _userData?.SteamId ?? "";
          textField.KeyboardType = UIKeyboardType.NumberPad;
           }
       else
            {
           cell.TextLabel!.Text = "API Key";
      _apiKeyField = textField;
textField.Placeholder = "Your API key";
             textField.Text = _userData?.SteamApiKey ?? "";
       textField.SecureTextEntry = true;
          }
       cell.ContentView.AddSubview(textField);
            }
   else
 {
      cell = new UITableViewCell(UITableViewCellStyle.Default, null);
   if (row == 0)
          {
   cell.TextLabel!.Text = _isLoading ? "Syncing..." : "Sync Library Now";
        cell.TextLabel.TextColor = UIColor.SystemBlue;
       cell.TextLabel.TextAlignment = UITextAlignment.Center;
             cell.SelectionStyle = _isLoading ? UITableViewCellSelectionStyle.None : UITableViewCellSelectionStyle.Default;
             
              if (_isLoading)
      {
           var spinner = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium);
     spinner.StartAnimating();
      cell.AccessoryView = spinner;
   }
       }
     else
              {
   cell.TextLabel!.Text = "Disconnect Steam";
 cell.TextLabel.TextColor = UIColor.SystemRed;
   cell.TextLabel.TextAlignment = UITextAlignment.Center;
      cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
  }
            }
    return cell;
        }

        private async void LoadAvatarAsync(UITableViewCell cell, string url)
        {
        try
        {
using var client = new HttpClient();
        var data = await client.GetByteArrayAsync(url);
     var image = UIImage.LoadFromData(NSData.FromArray(data));
       if (image != null)
      {
           InvokeOnMainThread(() =>
         {
       cell.ImageView!.Image = image;
       cell.ImageView.Layer.CornerRadius = 25;
    cell.ImageView.ClipsToBounds = true;
    cell.SetNeedsLayout();
       });
  }
            }
            catch { }
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
    tableView.DeselectRow(indexPath, true);
         
   var section = _profile != null ? indexPath.Section - 1 : indexPath.Section;
   
        if (section == 1)
         {
         if (indexPath.Row == 0)
  SyncLibrary();
         else
          DisconnectSteam();
}
        }

        private async void SaveSettings(object? sender, EventArgs e)
        {
        if (_userData == null) _userData = new UserData();
     _userData.SteamId = _steamIdField?.Text;
            _userData.SteamApiKey = _apiKeyField?.Text;
        await DatabaseService.Instance.SaveUserDataAsync(_userData);
            LoadData();
        }

   private async void SyncLibrary()
   {
            if (string.IsNullOrEmpty(_userData?.SteamId) || string.IsNullOrEmpty(_userData?.SteamApiKey))
   {
       ShowAlert("Configuration Required", "Please enter your Steam ID and API key first.");
    return;
            }

         _isLoading = true;
    TableView.ReloadData();

       try
     {
      var count = await SteamService.Instance.SyncLibraryAsync(_userData.SteamId, _userData.SteamApiKey);
                ShowAlert("Sync Complete", $"Successfully synced {count} games from Steam!");
  }
            catch (Exception ex)
       {
   ShowAlert("Sync Failed", ex.Message);
            }
            finally
            {
     _isLoading = false;
 TableView.ReloadData();
            }
        }

        private void DisconnectSteam()
        {
            var alert = UIAlertController.Create("Disconnect Steam", "Are you sure you want to disconnect your Steam account?", UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
       alert.AddAction(UIAlertAction.Create("Disconnect", UIAlertActionStyle.Destructive, async _ =>
        {
       _userData.SteamId = null;
 _userData.SteamApiKey = null;
       _userData.SteamLastSync = null;
     await DatabaseService.Instance.SaveUserDataAsync(_userData);
       _profile = null;
       TableView.ReloadData();
       }));
            PresentViewController(alert, true, null);
        }

    private void ShowAlert(string title, string message)
        {
     var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
    PresentViewController(alert, true, null);
        }
    }

    public class EpicSettingsViewController : UITableViewController
    {
 private UserData _userData = null!;
        private EpicProfile? _profile;
        private bool _isLoading;

        public override void ViewDidLoad()
      {
            base.ViewDidLoad();
            Title = "Epic Games";
            TableView = new UITableView(View!.Bounds, UITableViewStyle.InsetGrouped);
            LoadData();
 }

        private async void LoadData()
   {
            _userData = await DatabaseService.Instance.GetUserDataAsync();
       
            if (!string.IsNullOrEmpty(_userData.EpicAccountId) && !string.IsNullOrEmpty(_userData.EpicAccessToken))
     {
      _profile = await EpicGamesService.Instance.GetAccountInfoAsync(_userData.EpicAccessToken, _userData.EpicAccountId);
            }
    TableView.ReloadData();
        }

        public override nint NumberOfSections(UITableView tableView) => _profile != null ? 2 : 1;
   
        public override nint RowsInSection(UITableView tableView, nint section)
        {
     if (_profile != null)
      {
      return section switch
     {
     0 => 1,
        1 => 2,
     _ => 0
                };
            }
            return 1;
        }

        public override string TitleForHeader(UITableView tableView, nint section)
        {
            if (_profile != null)
            {
       return section switch
 {
               0 => "Connected Account",
          1 => "Actions",
     _ => ""
      };
 }
  return "Connect Epic Games";
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            if (_profile == null && section == 0)
         return "Sign in with your Epic Games account to sync your game library.";
   return "";
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var cell = new UITableViewCell(UITableViewCellStyle.Subtitle, null);

            if (_profile != null)
     {
                if (indexPath.Section == 0)
  {
           cell.TextLabel!.Text = _profile.DisplayName;
     cell.DetailTextLabel!.Text = _profile.Email ?? _profile.AccountId;
        cell.ImageView!.Image = UIImage.GetSystemImage("person.circle.fill");
     cell.ImageView.TintColor = UIColor.SystemBlue;
             cell.SelectionStyle = UITableViewCellSelectionStyle.None;
  }
         else
                {
              if (indexPath.Row == 0)
  {
          cell.TextLabel!.Text = _isLoading ? "Syncing..." : "Sync Library Now";
                  cell.TextLabel.TextColor = UIColor.SystemBlue;
         cell.TextLabel.TextAlignment = UITextAlignment.Center;
     cell.SelectionStyle = _isLoading ? UITableViewCellSelectionStyle.None : UITableViewCellSelectionStyle.Default;
      
       if (_isLoading)
       {
      var spinner = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium);
         spinner.StartAnimating();
    cell.AccessoryView = spinner;
     }
 }
      else
  {
        cell.TextLabel!.Text = "Disconnect Epic Games";
    cell.TextLabel.TextColor = UIColor.SystemRed;
 cell.TextLabel.TextAlignment = UITextAlignment.Center;
    }
     }
            }
        else
          {
    cell.TextLabel!.Text = "Sign in with Epic Games";
  cell.TextLabel.TextColor = UIColor.SystemBlue;
         cell.TextLabel.TextAlignment = UITextAlignment.Center;
   cell.ImageView!.Image = UIImage.GetSystemImage("arrow.right.circle.fill");
        cell.ImageView.TintColor = UIColor.SystemBlue;
        }

  return cell;
        }

public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
      tableView.DeselectRow(indexPath, true);

if (_profile != null)
            {
 if (indexPath.Section == 1)
              {
               if (indexPath.Row == 0)
      SyncLibrary();
               else
 DisconnectEpic();
    }
            }
    else
 {
             StartEpicLogin();
}
        }

        private void StartEpicLogin()
        {
            var webVC = new EpicLoginWebViewController();
            webVC.LoginCompleted += OnLoginCompleted;
            var nav = new UINavigationController(webVC);
        PresentViewController(nav, true, null);
        }

      private async void OnLoginCompleted(object? sender, EpicAuthResult result)
        {
        DismissViewController(true, async () =>
       {
_userData.EpicAccountId = result.AccountId;
 _userData.EpicAccessToken = result.AccessToken;
          _userData.EpicTokenExpiry = DateTime.UtcNow.AddSeconds(result.ExpiresIn);
          await DatabaseService.Instance.SaveUserDataAsync(_userData);
      LoadData();
   });
    }

     private async void SyncLibrary()
        {
      if (string.IsNullOrEmpty(_userData?.EpicAccessToken))
            {
       ShowAlert("Not Connected", "Please connect your Epic Games account first.");
   return;
            }

            _isLoading = true;
            TableView.ReloadData();

          try
        {
         var count = await EpicGamesService.Instance.SyncLibraryAsync(_userData.EpicAccessToken);
       ShowAlert("Sync Complete", $"Successfully synced {count} games from Epic Games!");
       }
            catch (Exception ex)
            {
   ShowAlert("Sync Failed", ex.Message);
     }
     finally
            {
       _isLoading = false;
 TableView.ReloadData();
     }
        }

    private void DisconnectEpic()
        {
   var alert = UIAlertController.Create("Disconnect Epic Games", "Are you sure you want to disconnect your Epic Games account?", UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
            alert.AddAction(UIAlertAction.Create("Disconnect", UIAlertActionStyle.Destructive, async _ =>
          {
                _userData.EpicAccountId = null;
 _userData.EpicAccessToken = null;
    _userData.EpicTokenExpiry = null;
  _userData.EpicLastSync = null;
             await DatabaseService.Instance.SaveUserDataAsync(_userData);
   _profile = null;
                TableView.ReloadData();
      }));
  PresentViewController(alert, true, null);
 }

        private void ShowAlert(string title, string message)
        {
          var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
   PresentViewController(alert, true, null);
        }
    }

 public class EpicLoginWebViewController : UIViewController
    {
        private WKWebView _webView = null!;
        private UIActivityIndicatorView _loadingIndicator = null!;
    private const string RedirectUri = "com.gaminghub.app://epic-callback";

        public event EventHandler<EpicAuthResult>? LoginCompleted;

      public override void ViewDidLoad()
        {
            base.ViewDidLoad();
      Title = "Epic Games Login";
     View!.BackgroundColor = UIColor.SystemBackground;
            
            NavigationItem.LeftBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Cancel, (s, e) => DismissViewController(true, null));

         var config = new WKWebViewConfiguration();
     _webView = new WKWebView(View.Bounds, config);
        _webView.NavigationDelegate = new EpicWebViewDelegate(this);
         View.AddSubview(_webView);

          _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large)
  {
            HidesWhenStopped = true,
          Center = new CGPoint(View.Bounds.Width / 2, View.Bounds.Height / 2)
 };
       View.AddSubview(_loadingIndicator);

  var authUrl = EpicGamesService.Instance.GetAuthorizationUrl(RedirectUri);
            _webView.LoadRequest(new NSUrlRequest(new NSUrl(authUrl)));
        }

      public override void ViewDidLayoutSubviews()
        {
    base.ViewDidLayoutSubviews();
    _webView.Frame = View!.Bounds;
          _loadingIndicator.Center = new CGPoint(View.Bounds.Width / 2, View.Bounds.Height / 2);
        }

        public async void HandleCallback(NSUrl url)
        {
     var components = new NSUrlComponents(url, true);
            var codeItem = components?.QueryItems?.FirstOrDefault(i => i.Name == "code");

 if (codeItem?.Value != null)
     {
      _loadingIndicator.StartAnimating();
 
        var result = await EpicGamesService.Instance.ExchangeCodeForTokenAsync(codeItem.Value);
          
      _loadingIndicator.StopAnimating();

             if (result != null)
   {
        LoginCompleted?.Invoke(this, result);
    }
         else
       {
           ShowError("Failed to authenticate with Epic Games");
     }
     }
        }

   private void ShowError(string message)
     {
  var alert = UIAlertController.Create("Login Failed", message, UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, _ => DismissViewController(true, null)));
         PresentViewController(alert, true, null);
 }

        private class EpicWebViewDelegate : WKNavigationDelegate
     {
 private readonly EpicLoginWebViewController _parent;

   public EpicWebViewDelegate(EpicLoginWebViewController parent)
     {
     _parent = parent;
            }

      public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
       {
        var url = navigationAction.Request.Url;
        
  if (url?.Scheme == "com.gaminghub.app")
           {
      _parent.HandleCallback(url);
    decisionHandler(WKNavigationActionPolicy.Cancel);
  return;
 }

     decisionHandler(WKNavigationActionPolicy.Allow);
            }

  public override void DidStartProvisionalNavigation(WKWebView webView, WKNavigation navigation)
            {
 _parent._loadingIndicator.StartAnimating();
          }

 public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation)
{
         _parent._loadingIndicator.StopAnimating();
    }
   }
    }
}
