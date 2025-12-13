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
        private UIActivityIndicatorView _loadMoreIndicator = null!;
  private UISegmentedControl _storeFilter = null!;
  private List<GameDeal> _deals = [];
        private int _currentPage = 0;
        private bool _isLoadingMore = false;
        private bool _hasMoreData = true;
        private const int PageSize = 60;

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
     _storeFilter.ValueChanged += async (s, e) => { _currentPage = 0; _deals.Clear(); _hasMoreData = true; await LoadDeals(); };

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
            _refreshControl.ValueChanged += async (s, e) => { _currentPage = 0; _deals.Clear(); _hasMoreData = true; await LoadDeals(); };
    _tableView.RefreshControl = _refreshControl;

            _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large) { HidesWhenStopped = true };
            
   _loadMoreIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium) { HidesWhenStopped = true };

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
            if (_currentPage == 0)
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
              
     var newDeals = await GameApiService.Instance.GetDealsAsync(_currentPage, PageSize, storeId);
      
      if (newDeals.Count < PageSize)
       _hasMoreData = false;
                
          if (_currentPage == 0)
                {
           _deals = newDeals;
                await DatabaseService.Instance.SaveDealsAsync(_deals);
         }
                else
{
  _deals.AddRange(newDeals);
        }
                
          _tableView.ReloadData();
  }
     catch (Exception ex)
   {
         Console.WriteLine($"Error loading deals: {ex.Message}");
     if (_currentPage == 0)
            {
 _deals = await DatabaseService.Instance.GetDealsAsync();
        _tableView.ReloadData();
         }
            }
    finally
     {
                _loadingIndicator.StopAnimating();
            _refreshControl.EndRefreshing();
    _isLoadingMore = false;
         _loadMoreIndicator.StopAnimating();
     }
        }

        private async Task LoadMoreIfNeeded(nint row)
        {
    // Load more when approaching the end of the list
      if (!_isLoadingMore && _hasMoreData && row >= _deals.Count - 10)
         {
         _isLoadingMore = true;
        _currentPage++;
    await LoadDeals();
 }
        }

     public nint RowsInSection(UITableView tableView, nint section) => _deals.Count;

        public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
   {
       var cell = tableView.DequeueReusableCell(DealCell.ReuseId, indexPath) as DealCell;
     cell!.Configure(_deals[indexPath.Row]);
            
       // Trigger load more
     _ = LoadMoreIfNeeded(indexPath.Row);
            
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
        private UILabel _emptyLabel = null!;
        private UISegmentedControl _viewModeSelector = null!;
        private List<UpcomingRelease> _releases = [];
        private bool _showMostAnticipated = false;

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

            _viewModeSelector = new UISegmentedControl(["Upcoming", "Most Anticipated"]);
            _viewModeSelector.SelectedSegment = 0;
 _viewModeSelector.ValueChanged += async (s, e) => 
            {
      _showMostAnticipated = _viewModeSelector.SelectedSegment == 1;
   await LoadReleases();
       };
            View.AddSubview(_viewModeSelector);

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

_emptyLabel = new UILabel
 {
     Text = "No upcoming releases found.\nPull to refresh.",
            TextColor = UIColor.SecondaryLabel,
    TextAlignment = UITextAlignment.Center,
 Lines = 0,
     Font = UIFont.SystemFontOfSize(16),
            Hidden = true
     };

    View.AddSubview(_tableView);
     View.AddSubview(_loadingIndicator);
   View.AddSubview(_emptyLabel);
        }

        public override void ViewDidLayoutSubviews()
        {
         base.ViewDidLayoutSubviews();
    var safeArea = View!.SafeAreaInsets;
        _viewModeSelector.Frame = new CGRect(16, safeArea.Top + 8, View.Bounds.Width - 32, 32);
 var tableY = _viewModeSelector.Frame.Bottom + 8;
            _tableView.Frame = new CGRect(0, tableY, View.Bounds.Width, View.Bounds.Height - tableY);
       _loadingIndicator.Center = new CGPoint(View.Bounds.Width / 2, View.Bounds.Height / 2);
    _emptyLabel.Frame = new CGRect(32, View.Bounds.Height / 2 - 40, View.Bounds.Width - 64, 80);
        }

        private async Task LoadReleases()
        {
            _loadingIndicator.StartAnimating();
    _emptyLabel.Hidden = true;
     try
            {
 // Load more games - increased limit to 100
                if (_showMostAnticipated)
      {
       _releases = await GameApiService.Instance.GetMostAnticipatedAsync(100);
     }
       else
    {
    _releases = await GameApiService.Instance.GetUpcomingReleasesAsync(1, 100);
  }
  
   await DatabaseService.Instance.SaveUpcomingReleasesAsync(_releases);
        _tableView.ReloadData();

           _emptyLabel.Hidden = _releases.Count > 0;
         }
            catch (Exception ex)
            {
           Console.WriteLine($"Error loading releases: {ex.Message}");
   _releases = await DatabaseService.Instance.GetUpcomingReleasesAsync();
 _tableView.ReloadData();
       _emptyLabel.Hidden = _releases.Count > 0;
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

    var release = _releases[indexPath.Row];
   if (!string.IsNullOrEmpty(release.ExternalId))
    {
            string url;
          if (release.Platforms == "Steam")
   url = $"https://store.steampowered.com/app/{release.ExternalId}";
else if (release.Platforms == "Epic Games")
   url = $"https://store.epicgames.com/en-US/p/{release.ExternalId}";
       else if (release.ExternalId.StartsWith("igdb_"))
        {
     var igdbId = release.ExternalId.Replace("igdb_", "");
         url = $"https://www.igdb.com/games/{release.GameName.ToLower().Replace(" ", "-").Replace(":", "")}";
  }
                else
      url = $"https://www.google.com/search?q={Uri.EscapeDataString(release.GameName)}+game";

                UIApplication.SharedApplication.OpenUrl(new NSUrl(url), new UIApplicationOpenUrlOptions(), null);
      }
        }
 }
}
