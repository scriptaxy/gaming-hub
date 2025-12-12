using gaming_hub.Models;
using gaming_hub.Services;

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
    cell.DetailTextLabel!.Text = string.IsNullOrEmpty(_userData?.SteamId) ? "Not Connected" : "Connected";
         cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
      }
         else
          {
             cell.TextLabel!.Text = "Epic Games";
          cell.ImageView!.Image = UIImage.GetSystemImage("e.square.fill");
            cell.ImageView.TintColor = UIColor.Black;
        cell.DetailTextLabel!.Text = string.IsNullOrEmpty(_userData?.EpicAccountId) ? "Not Connected" : "Connected";
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
            if (_userData != null) { _userData.DarkModeEnabled = toggle.On; await DatabaseService.Instance.SaveUserDataAsync(_userData); }
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

        public override void ViewDidLoad()
        {
          base.ViewDidLoad();
       Title = "Steam";
          NavigationItem.RightBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Save, SaveSettings);
            LoadData();
        }

        private async void LoadData()
        {
          _userData = await DatabaseService.Instance.GetUserDataAsync();
      TableView.ReloadData();
 }

        public override nint NumberOfSections(UITableView tableView) => 2;
        public override nint RowsInSection(UITableView tableView, nint section) => section == 0 ? 2 : 1;

 public override string TitleForHeader(UITableView tableView, nint section) => section == 0 ? "Steam Credentials" : "Actions";
        public override string TitleForFooter(UITableView tableView, nint section) => section == 0 ? "Get your Steam ID from your profile URL and API key from steamcommunity.com/dev/apikey" : "";

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
    var cell = new UITableViewCell(UITableViewCellStyle.Default, null);
            cell.SelectionStyle = UITableViewCellSelectionStyle.None;

      if (indexPath.Section == 0)
   {
 var textField = new UITextField(new CGRect(100, 12, tableView.Frame.Width - 120, 24))
{
          AutocorrectionType = UITextAutocorrectionType.No,
        AutocapitalizationType = UITextAutocapitalizationType.None
     };

          if (indexPath.Row == 0)
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
    cell.TextLabel!.Text = "Sync Library Now";
cell.TextLabel.TextColor = UIColor.SystemBlue;
            cell.TextLabel.TextAlignment = UITextAlignment.Center;
                cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
            }
        return cell;
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
    {
            tableView.DeselectRow(indexPath, true);
            if (indexPath.Section == 1)
             SyncLibrary();
        }

        private async void SaveSettings(object? sender, EventArgs e)
    {
          if (_userData == null) _userData = new UserData();
      _userData.SteamId = _steamIdField?.Text;
 _userData.SteamApiKey = _apiKeyField?.Text;
        await DatabaseService.Instance.SaveUserDataAsync(_userData);
            NavigationController?.PopViewController(true);
     }

        private async void SyncLibrary()
   {
     if (string.IsNullOrEmpty(_userData?.SteamId) || string.IsNullOrEmpty(_userData?.SteamApiKey))
   {
                ShowAlert("Configuration Required", "Please enter your Steam ID and API key first.");
           return;
   }

     var indicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium) { HidesWhenStopped = true };
  indicator.StartAnimating();
    NavigationItem.RightBarButtonItem = new UIBarButtonItem(indicator);

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
          NavigationItem.RightBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Save, SaveSettings);
   }
        }

        private void ShowAlert(string title, string message)
        {
 var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
       alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
            PresentViewController(alert, true, null);
        }
    }
}
