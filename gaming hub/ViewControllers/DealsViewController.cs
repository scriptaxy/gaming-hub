using gaming_hub.Models;
using gaming_hub.Services;
using gaming_hub.Views;

namespace gaming_hub.ViewControllers
{
    public class DealsViewController : UIViewController, IUITableViewDataSource, IUITableViewDelegate
    {
 private UITableView _tableView = null!;
        private UIRefreshControl _refreshControl = null!;
        private UIActivityIndicatorView _loadingIndicator = null!;
        private UISegmentedControl _storeFilter = null!;
   private List<GameDeal> _deals = [];

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            SetupUI();
      LoadDeals();
        }

        private void SetupUI()
        {
    Title = "Deals";
     View!.BackgroundColor = UIColor.SystemBackground;
        if (NavigationController != null)
           NavigationController.NavigationBar.PrefersLargeTitles = true;

            _storeFilter = new UISegmentedControl(["All", "Steam", "Epic", "GOG", "Humble"]);
  _storeFilter.SelectedSegment = 0;
       _storeFilter.ValueChanged += async (s, e) => await LoadDeals();

    _tableView = new UITableView(CGRect.Empty, UITableViewStyle.Plain)
  {
          DataSource = this,
         Delegate = this,
        BackgroundColor = UIColor.Clear,
            SeparatorStyle = UITableViewCellSeparatorStyle.None,
           RowHeight = 110
   };
            _tableView.RegisterClassForCellReuse(typeof(DealCell), DealCell.ReuseId);

            _refreshControl = new UIRefreshControl();
       _refreshControl.ValueChanged += async (s, e) => await LoadDeals();
            _tableView.RefreshControl = _refreshControl;

            _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large) { HidesWhenStopped = true };

          View.AddSubview(_storeFilter);
            View.AddSubview(_tableView);
          View.AddSubview(_loadingIndicator);
        }

        public override void ViewDidLayoutSubviews()
        {
        base.ViewDidLayoutSubviews();
            var safeArea = View!.SafeAreaInsets;
          var width = View.Bounds.Width;
         var height = View.Bounds.Height;
 _storeFilter.Frame = new CGRect(16, safeArea.Top + 8, width - 32, 32);
     var tableY = _storeFilter.Frame.Bottom + 8;
     _tableView.Frame = new CGRect(0, tableY, width, height - tableY);
         _loadingIndicator.Center = new CGPoint(width / 2, height / 2);
        }

    private async Task LoadDeals()
        {
         _loadingIndicator.StartAnimating();
        try
     {
       var storeId = _storeFilter.SelectedSegment switch
   {
     1 => "1",
         2 => "25",
                3 => "7",
      4 => "11",
           _ => null
                };
                _deals = await GameApiService.Instance.GetDealsAsync(0, 50, storeId);
  await DatabaseService.Instance.SaveDealsAsync(_deals);
          _tableView.ReloadData();
         }
     catch (Exception ex)
 {
                Console.WriteLine($"Error loading deals: {ex.Message}");
      _deals = await DatabaseService.Instance.GetDealsAsync();
 _tableView.ReloadData();
         }
 finally
   {
           _loadingIndicator.StopAnimating();
                _refreshControl.EndRefreshing();
  }
        }

        public nint RowsInSection(UITableView tableView, nint section) => _deals.Count;

    public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
  {
       var cell = tableView.DequeueReusableCell(DealCell.ReuseId, indexPath) as DealCell;
            cell!.Configure(_deals[indexPath.Row]);
    return cell;
   }

 [Export("tableView:didSelectRowAtIndexPath:")]
      public void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
      tableView.DeselectRow(indexPath, true);
         var deal = _deals[indexPath.Row];
            if (!string.IsNullOrEmpty(deal.DealUrl))
  {
            var url = new NSUrl(deal.DealUrl);
  UIApplication.SharedApplication.OpenUrl(url, new UIApplicationOpenUrlOptions(), null);
         }
        }
    }

 public class ReleasesViewController : UIViewController, IUITableViewDataSource, IUITableViewDelegate
    {
        private UITableView _tableView = null!;
        private UIRefreshControl _refreshControl = null!;
        private UIActivityIndicatorView _loadingIndicator = null!;
        private List<UpcomingRelease> _releases = [];

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
        SetupUI();
            LoadReleases();
        }

        private void SetupUI()
        {
  Title = "Upcoming";
          View!.BackgroundColor = UIColor.SystemBackground;
    if (NavigationController != null)
    NavigationController.NavigationBar.PrefersLargeTitles = true;

     _tableView = new UITableView(CGRect.Empty, UITableViewStyle.Plain)
            {
 DataSource = this,
         Delegate = this,
         BackgroundColor = UIColor.Clear,
      SeparatorStyle = UITableViewCellSeparatorStyle.None,
       RowHeight = 120
     };
   _tableView.RegisterClassForCellReuse(typeof(ReleaseCell), ReleaseCell.ReuseId);

         _refreshControl = new UIRefreshControl();
          _refreshControl.ValueChanged += async (s, e) => await LoadReleases();
            _tableView.RefreshControl = _refreshControl;

          _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large) { HidesWhenStopped = true };

        View.AddSubview(_tableView);
        View.AddSubview(_loadingIndicator);
        }

        public override void ViewDidLayoutSubviews()
        {
      base.ViewDidLayoutSubviews();
     var safeArea = View!.SafeAreaInsets;
 _tableView.Frame = new CGRect(0, safeArea.Top, View.Bounds.Width, View.Bounds.Height - safeArea.Top);
            _loadingIndicator.Center = new CGPoint(View.Bounds.Width / 2, View.Bounds.Height / 2);
}

        private async Task LoadReleases()
        {
            _loadingIndicator.StartAnimating();
     try
          {
    _releases = await GameApiService.Instance.GetUpcomingReleasesAsync();
           await DatabaseService.Instance.SaveUpcomingReleasesAsync(_releases);
                _tableView.ReloadData();
   }
      catch (Exception ex)
          {
     Console.WriteLine($"Error loading releases: {ex.Message}");
            _releases = await DatabaseService.Instance.GetUpcomingReleasesAsync();
 _tableView.ReloadData();
            }
         finally
    {
       _loadingIndicator.StopAnimating();
         _refreshControl.EndRefreshing();
          }
   }

      public nint RowsInSection(UITableView tableView, nint section) => _releases.Count;

 public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
          var cell = tableView.DequeueReusableCell(ReleaseCell.ReuseId, indexPath) as ReleaseCell;
            var release = _releases[indexPath.Row];
     cell!.Configure(release);
 cell.WishlistToggled += async (s, e) =>
    {
    await DatabaseService.Instance.ToggleWishlistAsync(release);
        tableView.ReloadRows([indexPath], UITableViewRowAnimation.None);
      };
    return cell;
        }

    [Export("tableView:didSelectRowAtIndexPath:")]
        public void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);
        }
    }
}
