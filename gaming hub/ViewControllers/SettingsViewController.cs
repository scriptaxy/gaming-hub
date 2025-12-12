using gaming_hub.Models;
using gaming_hub.Services;
using AuthenticationServices;
using WebKit;
using StoreKit;

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
  0 => 3,
      1 => 1,
     2 => 3,
   3 => 3,
            _ => 0
        };

        public override string TitleForHeader(UITableView tableView, nint section) => section switch
        {
            0 => "Connected Accounts",
            1 => "Remote PC",
    2 => "Preferences",
            3 => "About Synktra",
            _ => ""
        };

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            if (section == 3) return "© 2025 Scriptaxy. All rights reserved.";
            return "";
        }

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
   else if (indexPath.Row == 1)
           {
        cell.TextLabel!.Text = "Epic Games";
  cell.ImageView!.Image = UIImage.GetSystemImage("e.square.fill");
   cell.ImageView.TintColor = UIColor.Black;
      var isConnected = !string.IsNullOrEmpty(_userData?.EpicAccountId);
          cell.DetailTextLabel!.Text = isConnected ? "Connected" : "Not Connected";
               cell.DetailTextLabel.TextColor = isConnected ? UIColor.SystemGreen : UIColor.SecondaryLabel;
             cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
    }
               else
   {
     cell.TextLabel!.Text = "GOG Galaxy";
                cell.ImageView!.Image = UIImage.GetSystemImage("g.circle.fill");
        cell.ImageView.TintColor = UIColor.FromRGB(134, 50, 179);
      var isConnected = !string.IsNullOrEmpty(_userData?.GogAccessToken);
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
   if (_userData != null)
         {
        _userData.DealAlertsEnabled = toggle.On;
             await DatabaseService.Instance.SaveUserDataAsync(_userData);
           }
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
  else if (indexPath.Row == 1)
  {
               cell.TextLabel!.Text = "Created by";
               cell.ImageView!.Image = UIImage.GetSystemImage("person.fill");
 cell.ImageView.TintColor = UIColor.SystemIndigo;
              cell.DetailTextLabel!.Text = "Scriptaxy";
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
            else if (indexPath.Row == 1)
   NavigationController?.PushViewController(new EpicSettingsViewController(), true);
      else
     NavigationController?.PushViewController(new GOGSettingsViewController(), true);
  break;
    case 1:
        NavigationController?.PushViewController(new RemotePCSettingsViewController(), true);
        break;
            case 2:
         if (indexPath.Row == 2)
       ShowDealThresholdPicker();
         break;
    case 3:
       if (indexPath.Row == 2)
         RequestAppRating();
           break;
   }
        }

        private void RequestAppRating()
        {
 if (UIDevice.CurrentDevice.CheckSystemVersion(14, 0))
       {
             var scene = View?.Window?.WindowScene;
      if (scene != null)
     SKStoreReviewController.RequestReview(scene);
       }
     else
  {
    var appStoreUrl = "https://apps.apple.com/app/idXXXXXXXXXX";
     var alert = UIAlertController.Create("Rate Synktra", "Would you like to rate us on the App Store?", UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("Rate Now", UIAlertActionStyle.Default, _ =>
                {
    UIApplication.SharedApplication.OpenUrl(new Foundation.NSUrl(appStoreUrl), new UIApplicationOpenUrlOptions(), null);
            }));
              alert.AddAction(UIAlertAction.Create("Later", UIAlertActionStyle.Cancel, null));
         PresentViewController(alert, true, null);
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
            LoadData();
        }

      private async void LoadData()
        {
            _userData = await DatabaseService.Instance.GetUserDataAsync();

    if (!string.IsNullOrEmpty(_userData.SteamId))
            {
       _isLoading = true;
      TableView.ReloadData();

    if (!string.IsNullOrEmpty(_userData.SteamApiKey))
         _profile = await SteamService.Instance.GetPlayerSummaryAsync(_userData.SteamId, _userData.SteamApiKey);
      else
        _profile = await SteamService.Instance.GetPlayerSummaryPublicAsync(_userData.SteamId);

    _isLoading = false;
            }
            TableView.ReloadData();
     }

        public override nint NumberOfSections(UITableView tableView) => _profile != null ? 3 : 2;

public override nint RowsInSection(UITableView tableView, nint section)
        {
    if (_profile != null)
           return section switch { 0 => 1, 1 => 2, 2 => 2, _ => 0 };
   return section switch { 0 => 2, 1 => 2, _ => 0 };
        }

   public override string TitleForHeader(UITableView tableView, nint section)
        {
            if (_profile != null)
    return section switch { 0 => "Connected Account", 1 => "API Key (Optional)", 2 => "Actions", _ => "" };
      return section switch { 0 => "Sign In", 1 => "Or Enter Manually", _ => "" };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            if (_profile != null && section == 1)
    return "API key enables full library sync with playtime. Get one from steamcommunity.com/dev/apikey";
       if (_profile == null && section == 1)
                return "Enter your Steam ID (17-digit number from profile URL).";
    return "";
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
 {
         var cell = new UITableViewCell(UITableViewCellStyle.Subtitle, null);
    cell.SelectionStyle = UITableViewCellSelectionStyle.None;
        var section = indexPath.Section;
            var row = indexPath.Row;

        if (_profile != null && section == 0)
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
LoadAvatarAsync(cell, _profile.AvatarUrl);
             else
    cell.ImageView!.Image = UIImage.GetSystemImage("person.circle.fill");
       return cell;
         }

   var adjustedSection = _profile != null ? section - 1 : section;

          if (adjustedSection == 0 && _profile == null)
            {
            cell = new UITableViewCell(UITableViewCellStyle.Default, null);
 if (row == 0)
   {
  cell.TextLabel!.Text = "Sign in with Steam";
      cell.TextLabel.TextColor = UIColor.SystemBlue;
       cell.TextLabel.TextAlignment = UITextAlignment.Center;
   cell.ImageView!.Image = UIImage.GetSystemImage("arrow.right.circle.fill");
            cell.ImageView.TintColor = UIColor.FromRGB(27, 40, 56);
               cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
          }
        else
         {
    cell.TextLabel!.Text = "Open Steam Profile";
      cell.TextLabel.TextColor = UIColor.SystemBlue;
         cell.TextLabel.TextAlignment = UITextAlignment.Center;
     cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
       }
        }
            else if (adjustedSection == 0 || (_profile != null && section == 1))
     {
      cell = new UITableViewCell(UITableViewCellStyle.Default, null);
          cell.SelectionStyle = UITableViewCellSelectionStyle.None;
      var textField = new UITextField(new CGRect(100, 12, tableView.Frame.Width - 120, 24))
    {
           AutocorrectionType = UITextAutocorrectionType.No,
  AutocapitalizationType = UITextAutocapitalizationType.None
             };

      if (_profile != null)
    {
   if (row == 0)
          {
              cell.TextLabel!.Text = "API Key";
            _apiKeyField = textField;
       textField.Placeholder = "Optional - for full sync";
       textField.Text = _userData?.SteamApiKey ?? "";
    textField.SecureTextEntry = true;
       cell.ContentView.AddSubview(textField);
        }
                  else
    {
            cell.TextLabel!.Text = "Save API Key";
    cell.TextLabel.TextColor = UIColor.SystemBlue;
          cell.TextLabel.TextAlignment = UITextAlignment.Center;
          cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
         }
    }
                else
       {
              if (row == 0)
   {
cell.TextLabel!.Text = "Steam ID";
    _steamIdField = textField;
           textField.Placeholder = "76561198xxxxxxxxx";
                textField.Text = _userData?.SteamId ?? "";
      textField.KeyboardType = UIKeyboardType.NumberPad;
     cell.ContentView.AddSubview(textField);
         }
       else
           {
         cell.TextLabel!.Text = "Connect";
          cell.TextLabel.TextColor = UIColor.SystemBlue;
              cell.TextLabel.TextAlignment = UITextAlignment.Center;
         cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
 }
     }
            }
 else
            {
     cell = new UITableViewCell(UITableViewCellStyle.Default, null);
    if (row == 0)
        {
      cell.TextLabel!.Text = _isLoading ? "Syncing..." : "Sync Library";
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
   InvokeOnMainThread(() =>
 {
        cell.ImageView!.Image = image;
                 cell.ImageView.Layer.CornerRadius = 25;
         cell.ImageView.ClipsToBounds = true;
        cell.SetNeedsLayout();
               });
         }
            catch { }
        }

  public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
  {
            tableView.DeselectRow(indexPath, true);
     var adjustedSection = _profile != null ? indexPath.Section - 1 : indexPath.Section;

         if (_profile == null && indexPath.Section == 0)
    {
      if (indexPath.Row == 0) StartSteamLogin();
     else OpenSteamProfile();
      }
   else if (adjustedSection == 0 && indexPath.Row == 1)
     {
        if (_profile != null) SaveApiKey();
   else ConnectManually();
    }
            else if (adjustedSection == 1 || (_profile != null && indexPath.Section == 2))
            {
          if (indexPath.Row == 0) SyncLibrary();
      else DisconnectSteam();
            }
        }

     private void StartSteamLogin()
        {
    var webVC = new SteamLoginWebViewController();
         webVC.LoginCompleted += OnSteamLoginCompleted;
 var nav = new UINavigationController(webVC);
          PresentViewController(nav, true, null);
        }

        private void OnSteamLoginCompleted(object? sender, SteamOpenIdResult result)
        {
  DismissViewController(true, async () =>
     {
        if (result.Success)
                {
     _userData.SteamId = result.SteamId;
     await DatabaseService.Instance.SaveUserDataAsync(_userData);
       LoadData();
    }
       else
  {
     ShowAlert("Login Failed", "Could not sign in with Steam.");
                }
    });
        }

        private void OpenSteamProfile()
        {
     UIApplication.SharedApplication.OpenUrl(
                new Foundation.NSUrl("https://steamcommunity.com/my/"),
                new UIApplicationOpenUrlOptions(), null);
        }

        private async void ConnectManually()
        {
     var steamId = _steamIdField?.Text?.Trim();
  if (string.IsNullOrEmpty(steamId))
        {
                ShowAlert("Steam ID Required", "Please enter your Steam ID.");
     return;
      }

    if (steamId.Length != 17 || !long.TryParse(steamId, out _))
            {
    ShowAlert("Invalid Steam ID", "Steam ID should be a 17-digit number.");
       return;
            }

            _userData.SteamId = steamId;
         await DatabaseService.Instance.SaveUserDataAsync(_userData);
            LoadData();
    }

        private async void SaveApiKey()
        {
            _userData.SteamApiKey = _apiKeyField?.Text?.Trim();
 await DatabaseService.Instance.SaveUserDataAsync(_userData);
   LoadData();
      ShowAlert("Saved", "API key saved. You can now sync your full library.");
        }

        private async void SyncLibrary()
        {
     if (string.IsNullOrEmpty(_userData?.SteamId))
            {
         ShowAlert("Not Connected", "Please connect your Steam account first.");
 return;
   }

            _isLoading = true;
   TableView.ReloadData();

      try
            {
           int count;
 if (!string.IsNullOrEmpty(_userData.SteamApiKey))
 {
              count = await SteamService.Instance.SyncLibraryAsync(_userData.SteamId, _userData.SteamApiKey);
      ShowAlert("Sync Complete", $"Synced {count} games from Steam!");
    }
             else
           {
   count = await SteamService.Instance.SyncLibraryPublicAsync(_userData.SteamId);
     ShowAlert("Partial Sync", $"Synced {count} items. Add API key for full library.");
       }
     }
     catch (Exception ex) { ShowAlert("Sync Failed", ex.Message); }
     finally { _isLoading = false; TableView.ReloadData(); }
}

  private void DisconnectSteam()
        {
            var alert = UIAlertController.Create("Disconnect Steam", "Disconnect your Steam account?", UIAlertControllerStyle.Alert);
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

    public class SteamLoginWebViewController : UIViewController
    {
        private WKWebView _webView = null!;
        private UIActivityIndicatorView _loadingIndicator = null!;
        private const string ReturnUrl = "https://synktra.app/steam-callback";
        public event EventHandler<SteamOpenIdResult>? LoginCompleted;

        public override void ViewDidLoad()
   {
  base.ViewDidLoad();
   Title = "Sign in with Steam";
   View!.BackgroundColor = UIColor.SystemBackground;
          NavigationItem.LeftBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Cancel, (s, e) => DismissViewController(true, null));

        var config = new WKWebViewConfiguration();
            _webView = new WKWebView(View.Bounds, config);
            _webView.NavigationDelegate = new SteamWebViewDelegate(this);
 View.AddSubview(_webView);

            _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large)
  {
       HidesWhenStopped = true,
       Center = new CGPoint(View.Bounds.Width / 2, View.Bounds.Height / 2)
      };
   View.AddSubview(_loadingIndicator);

      var authUrl = SteamService.Instance.GetOpenIdLoginUrl(ReturnUrl);
     _webView.LoadRequest(new NSUrlRequest(new NSUrl(authUrl)));
        }

        public override void ViewDidLayoutSubviews()
        {
            base.ViewDidLayoutSubviews();
       _webView.Frame = View!.Bounds;
    _loadingIndicator.Center = new CGPoint(View.Bounds.Width / 2, View.Bounds.Height / 2);
        }

        public async void HandleCallback(string url)
        {
 _loadingIndicator.StartAnimating();
            var result = await SteamService.Instance.VerifyOpenIdResponseAsync(url);
            _loadingIndicator.StopAnimating();
       LoginCompleted?.Invoke(this, result ?? new SteamOpenIdResult { Success = false });
        }

        private class SteamWebViewDelegate : WKNavigationDelegate
        {
     private readonly SteamLoginWebViewController _parent;
            public SteamWebViewDelegate(SteamLoginWebViewController parent) => _parent = parent;

      public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
 {
    var url = navigationAction.Request.Url?.AbsoluteString;
           if (url?.StartsWith(ReturnUrl) == true)
                {
        _parent.HandleCallback(url);
         decisionHandler(WKNavigationActionPolicy.Cancel);
        return;
        }
   decisionHandler(WKNavigationActionPolicy.Allow);
        }

            public override void DidStartProvisionalNavigation(WKWebView webView, WKNavigation navigation) =>
      _parent._loadingIndicator.StartAnimating();

    public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation) =>
       _parent._loadingIndicator.StopAnimating();
        }
    }

    public class EpicSettingsViewController : UITableViewController
    {
        private UserData _userData = null!;
        private bool _isLoading;
    private bool _isConnected;

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
  _isConnected = !string.IsNullOrEmpty(_userData.EpicAccountId);
            TableView.ReloadData();
        }

        public override nint NumberOfSections(UITableView tableView) => _isConnected ? 2 : 1;
        public override nint RowsInSection(UITableView tableView, nint section) => _isConnected ? (section == 0 ? 1 : 3) : 2;
public override string TitleForHeader(UITableView tableView, nint section) => _isConnected ? (section == 0 ? "Status" : "Actions") : "Epic Games Store";
        public override string TitleForFooter(UITableView tableView, nint section) => !_isConnected && section == 0 ? "Epic doesn't support third-party library sync. You can sync free games or add games manually." : "";

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
         var cell = new UITableViewCell(UITableViewCellStyle.Subtitle, null);

      if (_isConnected)
            {
     if (indexPath.Section == 0)
      {
               cell.TextLabel!.Text = "Epic Games";
        cell.DetailTextLabel!.Text = _userData.EpicLastSync.HasValue ? $"Last synced: {_userData.EpicLastSync.Value:MMM d, yyyy}" : "Ready to sync";
    cell.ImageView!.Image = UIImage.GetSystemImage("checkmark.circle.fill");
      cell.ImageView.TintColor = UIColor.SystemGreen;
     cell.SelectionStyle = UITableViewCellSelectionStyle.None;
    }
     else
  {
             cell = new UITableViewCell(UITableViewCellStyle.Default, null);
  if (indexPath.Row == 0)
   {
         cell.TextLabel!.Text = _isLoading ? "Syncing..." : "Sync Free Games";
              cell.TextLabel.TextColor = UIColor.SystemBlue;
           cell.TextLabel.TextAlignment = UITextAlignment.Center;
  cell.SelectionStyle = _isLoading ? UITableViewCellSelectionStyle.None : UITableViewCellSelectionStyle.Default;
             if (_isLoading) { var s = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium); s.StartAnimating(); cell.AccessoryView = s; }
      }
            else if (indexPath.Row == 1)
         {
   cell.TextLabel!.Text = "Search Epic Store";
        cell.TextLabel.TextColor = UIColor.SystemBlue;
           cell.TextLabel.TextAlignment = UITextAlignment.Center;
                 }
               else
   {
   cell.TextLabel!.Text = "Disconnect";
          cell.TextLabel.TextColor = UIColor.SystemRed;
            cell.TextLabel.TextAlignment = UITextAlignment.Center;
         }
         }
}
            else
            {
     cell = new UITableViewCell(UITableViewCellStyle.Subtitle, null);
    if (indexPath.Row == 0)
   {
   cell.TextLabel!.Text = "Enable Epic Games";
      cell.TextLabel.TextColor = UIColor.SystemBlue;
    cell.TextLabel.TextAlignment = UITextAlignment.Center;
          cell.ImageView!.Image = UIImage.GetSystemImage("plus.circle.fill");
       cell.ImageView.TintColor = UIColor.SystemBlue;
        }
   else
 {
               cell.TextLabel!.Text = "Search Epic Store";
          cell.DetailTextLabel!.Text = "Find and add games manually";
    cell.ImageView!.Image = UIImage.GetSystemImage("magnifyingglass");
   cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                }
            }
    return cell;
        }

      public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
        tableView.DeselectRow(indexPath, true);
            if (_isConnected && indexPath.Section == 1)
    {
       if (indexPath.Row == 0) SyncFreeGames();
      else if (indexPath.Row == 1) OpenEpicSearch();
     else DisconnectEpic();
      }
            else if (!_isConnected)
            {
         if (indexPath.Row == 0) EnableEpic();
       else OpenEpicSearch();
    }
        }

   private async void EnableEpic()
        {
  _userData.EpicAccountId = "enabled";
            await DatabaseService.Instance.SaveUserDataAsync(_userData);
   _isConnected = true;
        TableView.ReloadData();
 }

  private void OpenEpicSearch()
        {
            NavigationController?.PushViewController(new EpicSearchViewController(), true);
        }

        private async void SyncFreeGames()
        {
            _isLoading = true; TableView.ReloadData();
         try
         {
var count = await EpicGamesService.Instance.SyncLibraryAsync("manual");
     ShowAlert("Sync Complete", count > 0 ? $"Added {count} free games!" : "No new free games found.");
     LoadData();
   }
      catch (Exception ex) { ShowAlert("Sync Failed", ex.Message); }
    finally { _isLoading = false; TableView.ReloadData(); }
        }

        private void DisconnectEpic()
        {
       var alert = UIAlertController.Create("Disconnect", "Remove Epic Games?", UIAlertControllerStyle.Alert);
 alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
            alert.AddAction(UIAlertAction.Create("Disconnect", UIAlertActionStyle.Destructive, async _ =>
        {
         _userData.EpicAccountId = null; _userData.EpicAccessToken = null; _userData.EpicLastSync = null;
     await DatabaseService.Instance.SaveUserDataAsync(_userData);
              _isConnected = false; TableView.ReloadData();
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

    public class EpicSearchViewController : UITableViewController, IUISearchResultsUpdating
    {
      private UISearchController _searchController = null!;
        private List<Game> _results = [];
        private bool _searching;

        public override void ViewDidLoad()
    {
            base.ViewDidLoad();
     Title = "Search Epic Store";
       _searchController = new UISearchController(searchResultsController: null) { SearchResultsUpdater = this, ObscuresBackgroundDuringPresentation = false };
            _searchController.SearchBar.Placeholder = "Search games...";
  NavigationItem.SearchController = _searchController;
      NavigationItem.HidesSearchBarWhenScrolling = false;
    }

        public async void UpdateSearchResultsForSearchController(UISearchController c)
        {
            var q = c.SearchBar.Text;
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2) { _results.Clear(); TableView.ReloadData(); return; }
      _searching = true; TableView.ReloadData();
     try { _results = await EpicGamesService.Instance.SearchCatalogAsync(q); }
            catch { _results.Clear(); }
    finally { _searching = false; TableView.ReloadData(); }
   }

        public override nint RowsInSection(UITableView tableView, nint section) => _searching ? 1 : _results.Count;

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var cell = new UITableViewCell(UITableViewCellStyle.Subtitle, null);
            if (_searching) { cell.TextLabel!.Text = "Searching..."; cell.SelectionStyle = UITableViewCellSelectionStyle.None; }
      else if (indexPath.Row < _results.Count)
            {
                var g = _results[indexPath.Row];
       cell.TextLabel!.Text = g.Name;
 cell.DetailTextLabel!.Text = g.Description ?? "Epic Games";
       }
   return cell;
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
    {
   tableView.DeselectRow(indexPath, true);
     if (_searching || indexPath.Row >= _results.Count) return;
    var game = _results[indexPath.Row];
            var alert = UIAlertController.Create("Add Game", $"Add \"{game.Name}\"?", UIAlertControllerStyle.Alert);
    alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
alert.AddAction(UIAlertAction.Create("Add", UIAlertActionStyle.Default, async _ =>
          {
     await DatabaseService.Instance.SaveGamesAsync([game]);
         var done = UIAlertController.Create("Added", $"\"{game.Name}\" added!", UIAlertControllerStyle.Alert);
done.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
                PresentViewController(done, true, null);
}));
     PresentViewController(alert, true, null);
        }
}

    public class GOGSettingsViewController : UITableViewController
    {
        private UserData _userData = null!;
        private GOGProfile? _profile;
        private bool _isLoading;

        public override void ViewDidLoad()
      {
        base.ViewDidLoad();
       Title = "GOG Galaxy";
  TableView = new UITableView(View!.Bounds, UITableViewStyle.InsetGrouped);
    LoadData();
        }

        private async void LoadData()
        {
 _userData = await DatabaseService.Instance.GetUserDataAsync();
   if (!string.IsNullOrEmpty(_userData.GogAccessToken))
    _profile = await GOGService.Instance.GetUserDataAsync(_userData.GogAccessToken);
            TableView.ReloadData();
        }

        public override nint NumberOfSections(UITableView tableView) => _profile != null ? 2 : 1;
        public override nint RowsInSection(UITableView tableView, nint section) => _profile != null ? (section == 0 ? 1 : 2) : 1;
        public override string TitleForHeader(UITableView tableView, nint section) => _profile != null ? (section == 0 ? "Connected Account" : "Actions") : "Connect GOG";
        public override string TitleForFooter(UITableView tableView, nint section) => _profile == null ? "Sign in with GOG to sync your DRM-free library." : "";

public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
     {
            var cell = new UITableViewCell(UITableViewCellStyle.Subtitle, null);
    if (_profile != null)
          {
  if (indexPath.Section == 0)
     {
   cell.TextLabel!.Text = _profile.Username;
  cell.DetailTextLabel!.Text = _profile.Email ?? _profile.UserId;
      cell.ImageView!.Image = UIImage.GetSystemImage("person.circle.fill");
  cell.ImageView.TintColor = UIColor.FromRGB(134, 50, 179);
  cell.SelectionStyle = UITableViewCellSelectionStyle.None;
        }
    else
     {
         cell = new UITableViewCell(UITableViewCellStyle.Default, null);
         cell.TextLabel!.Text = indexPath.Row == 0 ? (_isLoading ? "Syncing..." : "Sync Library") : "Disconnect";
           cell.TextLabel.TextColor = indexPath.Row == 0 ? UIColor.SystemBlue : UIColor.SystemRed;
           cell.TextLabel.TextAlignment = UITextAlignment.Center;
  if (_isLoading && indexPath.Row == 0) { var s = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium); s.StartAnimating(); cell.AccessoryView = s; }
            }
            }
            else
            {
  cell.TextLabel!.Text = "Sign in with GOG";
       cell.TextLabel.TextColor = UIColor.SystemBlue;
 cell.TextLabel.TextAlignment = UITextAlignment.Center;
       cell.ImageView!.Image = UIImage.GetSystemImage("arrow.right.circle.fill");
            cell.ImageView.TintColor = UIColor.FromRGB(134, 50, 179);
            }
    return cell;
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
 tableView.DeselectRow(indexPath, true);
            if (_profile != null && indexPath.Section == 1)
       {
       if (indexPath.Row == 0) SyncLibrary();
          else DisconnectGOG();
        }
            else if (_profile == null) StartGOGLogin();
        }

        private void StartGOGLogin()
        {
            var webVC = new GOGLoginWebViewController();
            webVC.LoginCompleted += (s, r) => DismissViewController(true, async () =>
            {
       _userData.GogAccessToken = r.AccessToken;
         _userData.GogTokenExpiry = DateTime.UtcNow.AddSeconds(r.ExpiresIn);
       await DatabaseService.Instance.SaveUserDataAsync(_userData);
         LoadData();
            });
  PresentViewController(new UINavigationController(webVC), true, null);
        }

        private async void SyncLibrary()
        {
  if (string.IsNullOrEmpty(_userData?.GogAccessToken)) return;
      _isLoading = true; TableView.ReloadData();
            try
            {
    var count = await GOGService.Instance.SyncLibraryAsync(_userData.GogAccessToken);
      var alert = UIAlertController.Create("Sync Complete", $"Synced {count} games!", UIAlertControllerStyle.Alert);
      alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
     PresentViewController(alert, true, null);
     }
        catch (Exception ex)
{
          var alert = UIAlertController.Create("Sync Failed", ex.Message, UIAlertControllerStyle.Alert);
       alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
     PresentViewController(alert, true, null);
    }
    finally { _isLoading = false; TableView.ReloadData(); }
        }

        private void DisconnectGOG()
        {
var alert = UIAlertController.Create("Disconnect", "Remove GOG?", UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
            alert.AddAction(UIAlertAction.Create("Disconnect", UIAlertActionStyle.Destructive, async _ =>
 {
                _userData.GogAccessToken = null; _userData.GogTokenExpiry = null; _userData.GogLastSync = null;
        await DatabaseService.Instance.SaveUserDataAsync(_userData);
     _profile = null; TableView.ReloadData();
            }));
          PresentViewController(alert, true, null);
}
    }

    public class GOGLoginWebViewController : UIViewController
    {
        private WKWebView _webView = null!;
     private UIActivityIndicatorView _loading = null!;
   public event EventHandler<GOGAuthResult>? LoginCompleted;

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
    Title = "GOG Login";
         View!.BackgroundColor = UIColor.SystemBackground;
   NavigationItem.LeftBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Cancel, (s, e) => DismissViewController(true, null));
    _webView = new WKWebView(View.Bounds, new WKWebViewConfiguration());
         _webView.NavigationDelegate = new GOGWebViewDelegate(this);
   View.AddSubview(_webView);
          _loading = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large) { HidesWhenStopped = true };
            View.AddSubview(_loading);
_webView.LoadRequest(new NSUrlRequest(new NSUrl(GOGService.Instance.GetAuthorizationUrl())));
      }

    public override void ViewDidLayoutSubviews()
        {
      base.ViewDidLayoutSubviews();
         _webView.Frame = View!.Bounds;
            _loading.Center = new CGPoint(View.Bounds.Width / 2, View.Bounds.Height / 2);
        }

        public async void HandleCallback(string code)
        {
 _loading.StartAnimating();
          var result = await GOGService.Instance.ExchangeCodeForTokenAsync(code);
            _loading.StopAnimating();
   if (result != null) LoginCompleted?.Invoke(this, result);
 else
  {
          var alert = UIAlertController.Create("Login Failed", "Could not authenticate", UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, _ => DismissViewController(true, null)));
              PresentViewController(alert, true, null);
         }
        }

        private class GOGWebViewDelegate : WKNavigationDelegate
    {
     private readonly GOGLoginWebViewController _parent;
       public GOGWebViewDelegate(GOGLoginWebViewController p) => _parent = p;

 public override void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, Action<WKNavigationActionPolicy> decisionHandler)
  {
       var url = navigationAction.Request.Url;
    if (url?.AbsoluteString?.Contains("on_login_success") == true)
          {
     var components = new NSUrlComponents(url, true);
        var code = components?.QueryItems?.FirstOrDefault(i => i.Name == "code")?.Value;
    if (code != null) { _parent.HandleCallback(code); decisionHandler(WKNavigationActionPolicy.Cancel); return; }
            }
 decisionHandler(WKNavigationActionPolicy.Allow);
            }

        public override void DidStartProvisionalNavigation(WKWebView webView, WKNavigation navigation) => _parent._loading.StartAnimating();
     public override void DidFinishNavigation(WKWebView webView, WKNavigation navigation) => _parent._loading.StopAnimating();
        }
    }
}
