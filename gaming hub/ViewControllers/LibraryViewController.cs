using gaming_hub.Models;
using gaming_hub.Services;
using gaming_hub.Views;

namespace gaming_hub.ViewControllers
{
    public class LibraryViewController : UIViewController, IUICollectionViewDataSource, IUICollectionViewDelegate, IUISearchResultsUpdating
    {
        private UICollectionView _collectionView = null!;
    private UIRefreshControl _refreshControl = null!;
   private UISearchController _searchController = null!;
        private UISegmentedControl _filterSegment = null!;
  private UILabel _emptyLabel = null!;
 private UIActivityIndicatorView _loadingIndicator = null!;
     private List<Game> _allGames = [];
   private List<Game> _filteredGames = [];
        private GamePlatform? _currentFilter = null;
        private string _searchText = "";

  public override void ViewDidLoad()
        {
  base.ViewDidLoad();
  SetupUI();
   LoadGames();
   }

      private void SetupUI()
        {
   Title = "My Library";
            View!.BackgroundColor = UIColor.SystemBackground;
    if (NavigationController != null)
        NavigationController.NavigationBar.PrefersLargeTitles = true;
     NavigationItem.LargeTitleDisplayMode = UINavigationItemLargeTitleDisplayMode.Always;
         NavigationItem.RightBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Add, OnAddGameTapped);

    _searchController = new UISearchController(searchResultsController: null)
            {
    ObscuresBackgroundDuringPresentation = false,
       HidesNavigationBarDuringPresentation = false
        };
  _searchController.SearchResultsUpdater = this;
   _searchController.SearchBar.Placeholder = "Search games...";
            NavigationItem.SearchController = _searchController;
            NavigationItem.HidesSearchBarWhenScrolling = false;

    _filterSegment = new UISegmentedControl(["All", "Steam", "Epic", "GOG", "Favorites"]);
         _filterSegment.SelectedSegment = 0;
      _filterSegment.ValueChanged += OnFilterChanged;

     var layout = new UICollectionViewFlowLayout
         {
              ScrollDirection = UICollectionViewScrollDirection.Vertical,
    MinimumInteritemSpacing = 12,
      MinimumLineSpacing = 16,
      SectionInset = new UIEdgeInsets(16, 16, 16, 16)
 };
  _collectionView = new UICollectionView(CGRect.Empty, layout)
{
              BackgroundColor = UIColor.Clear,
   DataSource = this,
         Delegate = this
        };
  _collectionView.RegisterClassForCell(typeof(GameCell), GameCell.ReuseId);

        _refreshControl = new UIRefreshControl();
            _refreshControl.ValueChanged += async (s, e) => await RefreshLibrary();
     _collectionView.RefreshControl = _refreshControl;

  _emptyLabel = new UILabel
 {
       Text = "No games in your library.\nConnect Steam or Epic Games to sync your games,\nor add games manually.",
      TextColor = UIColor.SecondaryLabel,
     TextAlignment = UITextAlignment.Center,
        Lines = 0,
         Font = UIFont.SystemFontOfSize(16),
        Hidden = true
         };

    _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large) { HidesWhenStopped = true };

          View.AddSubview(_filterSegment);
  View.AddSubview(_collectionView);
   View.AddSubview(_emptyLabel);
            View.AddSubview(_loadingIndicator);
     }

        public override void ViewDidLayoutSubviews()
        {
 base.ViewDidLayoutSubviews();
     var safeArea = View!.SafeAreaInsets;
  var width = View.Bounds.Width;
         var height = View.Bounds.Height;
            _filterSegment.Frame = new CGRect(16, safeArea.Top + 8, width - 32, 32);
            var collectionY = _filterSegment.Frame.Bottom + 8;
     _collectionView.Frame = new CGRect(0, collectionY, width, height - collectionY);
     _emptyLabel.Frame = new CGRect(32, height / 2 - 60, width - 64, 120);
_loadingIndicator.Center = new CGPoint(width / 2, height / 2);

            if (_collectionView.CollectionViewLayout is UICollectionViewFlowLayout flowLayout)
     {
        var columns = width > 500 ? 4 : 2;
 var spacing = flowLayout.MinimumInteritemSpacing;
        var insets = flowLayout.SectionInset.Left + flowLayout.SectionInset.Right;
  var availableWidth = width - insets - (spacing * (columns - 1));
      var cellWidth = availableWidth / columns;
     var cellHeight = cellWidth * 1.4f;
     flowLayout.ItemSize = new CGSize(cellWidth, cellHeight);
            }
   }

    private async void LoadGames()
 {
   _loadingIndicator.StartAnimating();
try
       {
   await DatabaseService.Instance.InitializeAsync();
     _allGames = await DatabaseService.Instance.GetAllGamesAsync();
  ApplyFilters();
}
    catch (Exception ex) { ShowError($"Failed to load games: {ex.Message}"); }
   finally { _loadingIndicator.StopAnimating(); }
   }

    private async Task RefreshLibrary()
        {
         try
          {
   var userData = await DatabaseService.Instance.GetUserDataAsync();
      if (!string.IsNullOrEmpty(userData.SteamId) && !string.IsNullOrEmpty(userData.SteamApiKey))
       {
         var count = await SteamService.Instance.SyncLibraryAsync(userData.SteamId, userData.SteamApiKey);
       Console.WriteLine($"Synced {count} Steam games");
  }
      _allGames = await DatabaseService.Instance.GetAllGamesAsync();
  ApplyFilters();
            }
   catch (Exception ex) { ShowError($"Sync failed: {ex.Message}"); }
     finally { _refreshControl.EndRefreshing(); }
    }

      private void OnFilterChanged(object? sender, EventArgs e)
        {
         _currentFilter = _filterSegment.SelectedSegment switch
   {
     1 => GamePlatform.Steam,
     2 => GamePlatform.Epic,
        3 => GamePlatform.GOG,
 _ => null
     };
       ApplyFilters();
        }

        private void ApplyFilters()
        {
     var isFavorites = _filterSegment.SelectedSegment == 4;
  _filteredGames = _allGames.Where(g =>
 {
      if (_currentFilter.HasValue && g.Platform != _currentFilter.Value) return false;
    if (isFavorites && !g.IsFavorite) return false;
 if (!string.IsNullOrEmpty(_searchText))
    {
    var searchLower = _searchText.ToLower();
   if (!g.Name.ToLower().Contains(searchLower) && !(g.Genres?.ToLower().Contains(searchLower) ?? false))
  return false;
     }
 return true;
     }).ToList();
   _emptyLabel.Hidden = _filteredGames.Count > 0;
         _collectionView.ReloadData();
     }

        public void UpdateSearchResults(UISearchController searchController)
        {
  _searchText = searchController.SearchBar.Text ?? "";
       ApplyFilters();
        }

        private void OnAddGameTapped(object? sender, EventArgs e)
        {
       var alert = UIAlertController.Create("Add Game", null, UIAlertControllerStyle.ActionSheet);
        alert.AddAction(UIAlertAction.Create("Search Online", UIAlertActionStyle.Default, _ =>
         {
          var searchVC = new GameSearchViewController();
      searchVC.GameSelected += OnGameSelectedFromSearch;
       var nav = new UINavigationController(searchVC);
        PresentViewController(nav, true, null);
   }));
     alert.AddAction(UIAlertAction.Create("Add Manually", UIAlertActionStyle.Default, _ => ShowManualAddDialog()));
   alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
      if (alert.PopoverPresentationController != null)
  alert.PopoverPresentationController.BarButtonItem = NavigationItem.RightBarButtonItem;
    PresentViewController(alert, true, null);
        }

private async void OnGameSelectedFromSearch(object? sender, Game game)
   {
  DismissViewController(true, async () =>
    {
      await DatabaseService.Instance.SaveGameAsync(game);
        _allGames = await DatabaseService.Instance.GetAllGamesAsync();
      ApplyFilters();
    });
      }

        private void ShowManualAddDialog()
        {
   var alert = UIAlertController.Create("Add Game", "Enter game name", UIAlertControllerStyle.Alert);
  alert.AddTextField(tf => tf.Placeholder = "Game name");
    alert.AddAction(UIAlertAction.Create("Add", UIAlertActionStyle.Default, async _ =>
            {
          var name = alert.TextFields?[0].Text;
         if (!string.IsNullOrWhiteSpace(name))
  {
     var game = new Game { Name = name, Platform = GamePlatform.Manual, DateAdded = DateTime.UtcNow };
    await DatabaseService.Instance.SaveGameAsync(game);
        _allGames = await DatabaseService.Instance.GetAllGamesAsync();
  ApplyFilters();
    }
     }));
      alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
   PresentViewController(alert, true, null);
   }

      private void ShowError(string message)
    {
     var alert = UIAlertController.Create("Error", message, UIAlertControllerStyle.Alert);
 alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
    PresentViewController(alert, true, null);
  }

        public nint GetItemsCount(UICollectionView collectionView, nint section) => _filteredGames.Count;

        public UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
      {
       var cell = collectionView.DequeueReusableCell(GameCell.ReuseId, indexPath) as GameCell;
     cell!.Configure(_filteredGames[indexPath.Row]);
   return cell;
   }

        [Export("collectionView:didSelectItemAtIndexPath:")]
  public void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
   {
 var game = _filteredGames[indexPath.Row];
     var detailVC = new GameDetailViewController(game);
      detailVC.GameUpdated += async (s, g) =>
       {
_allGames = await DatabaseService.Instance.GetAllGamesAsync();
    ApplyFilters();
    };
     NavigationController?.PushViewController(detailVC, true);
        }
    }

  public class GameSearchViewController : UIViewController, IUISearchBarDelegate, IUITableViewDataSource, IUITableViewDelegate
    {
  private UISearchBar _searchBar = null!;
        private UITableView _tableView = null!;
        private UIActivityIndicatorView _loadingIndicator = null!;
      private List<Game> _searchResults = [];

  public event EventHandler<Game>? GameSelected;

  public override void ViewDidLoad()
  {
        base.ViewDidLoad();
  Title = "Search Games";
            View!.BackgroundColor = UIColor.SystemBackground;
 NavigationItem.LeftBarButtonItem = new UIBarButtonItem(UIBarButtonSystemItem.Cancel, (s, e) => DismissViewController(true, null));

 _searchBar = new UISearchBar { Placeholder = "Search for a game...", SearchBarStyle = UISearchBarStyle.Minimal };
  _searchBar.Delegate = this;
          View.AddSubview(_searchBar);

        _tableView = new UITableView { DataSource = this, Delegate = this };
            _tableView.RegisterClassForCellReuse(typeof(UITableViewCell), "SearchCell");
     View.AddSubview(_tableView);

            _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Large) { HidesWhenStopped = true };
  View.AddSubview(_loadingIndicator);
     }

        public override void ViewDidLayoutSubviews()
     {
   base.ViewDidLayoutSubviews();
      var safeArea = View!.SafeAreaInsets;
   _searchBar.Frame = new CGRect(0, safeArea.Top, View.Bounds.Width, 56);
            _tableView.Frame = new CGRect(0, _searchBar.Frame.Bottom, View.Bounds.Width, View.Bounds.Height - _searchBar.Frame.Bottom);
        _loadingIndicator.Center = new CGPoint(View.Bounds.Width / 2, View.Bounds.Height / 2);
    }

 [Export("searchBarSearchButtonClicked:")]
     public async void SearchButtonClicked(UISearchBar searchBar)
  {
    searchBar.ResignFirstResponder();
     var query = searchBar.Text;
          if (string.IsNullOrWhiteSpace(query)) return;
          _loadingIndicator.StartAnimating();
         try
      {
         _searchResults = await GameApiService.Instance.SearchGamesAsync(query);
_tableView.ReloadData();
 }
    catch (Exception ex) { Console.WriteLine($"Search error: {ex.Message}"); }
      finally { _loadingIndicator.StopAnimating(); }
        }

        public nint RowsInSection(UITableView tableView, nint section) => _searchResults.Count;

        public UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
  var cell = tableView.DequeueReusableCell("SearchCell", indexPath);
  var game = _searchResults[indexPath.Row];
            var config = UIListContentConfiguration.SubtitleCellConfiguration;
     config.Text = game.Name;
        config.SecondaryText = game.ReleaseDate?.ToString("yyyy") ?? "";
  cell.ContentConfiguration = config;
   return cell;
        }

        [Export("tableView:didSelectRowAtIndexPath:")]
        public void RowSelected(UITableView tableView, NSIndexPath indexPath)
   {
           tableView.DeselectRow(indexPath, true);
            GameSelected?.Invoke(this, _searchResults[indexPath.Row]);
    }
    }
}
