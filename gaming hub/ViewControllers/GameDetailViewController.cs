using gaming_hub.Models;
using gaming_hub.Services;
using StoreKit;

namespace gaming_hub.ViewControllers
{
    public class GameDetailViewController : UIViewController, IUICollectionViewDataSource, IUICollectionViewDelegateFlowLayout
 {
        private Game _game;
     private UIScrollView _scrollView = null!;
        private UIImageView _backgroundImageView = null!;
    private UIImageView _coverImageView = null!;
  private UILabel _titleLabel = null!;
        private UILabel _platformLabel = null!;
  private UILabel _playtimeLabel = null!;
     private UILabel _releaseDateLabel = null!;
     private UILabel _genresLabel = null!;
  private UILabel _descriptionLabel = null!;
        private UILabel _developersLabel = null!;
        private UILabel _metacriticLabel = null!;
   private UIButton _favoriteButton = null!;
        private UIButton _wishlistButton = null!;
   private UIButton _playButton = null!;
        
        // New features
        private UILabel _screenshotsLabel = null!;
        private UICollectionView _screenshotsCollectionView = null!;
     private UILabel _notesLabel = null!;
     private UITextView _notesTextView = null!;
   private UILabel _userRatingLabel = null!;
     private UIStackView _ratingStarsView = null!;
        private List<string> _screenshots = [];
     
   private static readonly HttpClient ImageClient = new();

        public event EventHandler<Game>? GameUpdated;

        public GameDetailViewController(Game game) { _game = game; }

        public override void ViewDidLoad()
      {
          base.ViewDidLoad();
 SetupUI();
            LoadGameDetails();
        }

  private void SetupUI()
        {
View!.BackgroundColor = UIColor.SystemBackground;
 NavigationItem.LargeTitleDisplayMode = UINavigationItemLargeTitleDisplayMode.Never;
            NavigationItem.RightBarButtonItem = new UIBarButtonItem(UIImage.GetSystemImage("ellipsis.circle"), UIBarButtonItemStyle.Plain, ShowMenu);

            _backgroundImageView = new UIImageView { ContentMode = UIViewContentMode.ScaleAspectFill, ClipsToBounds = true, Alpha = 0.3f };
            View.AddSubview(_backgroundImageView);

 _scrollView = new UIScrollView { AlwaysBounceVertical = true };
            View.AddSubview(_scrollView);

            _coverImageView = new UIImageView { ContentMode = UIViewContentMode.ScaleAspectFill, ClipsToBounds = true, BackgroundColor = UIColor.SystemGray5 };
            _coverImageView.Layer.CornerRadius = 12;
            _coverImageView.Layer.ShadowColor = UIColor.Black.CGColor;
            _coverImageView.Layer.ShadowOffset = new CGSize(0, 4);
            _coverImageView.Layer.ShadowRadius = 8;
            _coverImageView.Layer.ShadowOpacity = 0.3f;
  _scrollView.AddSubview(_coverImageView);

            _titleLabel = new UILabel { Font = UIFont.BoldSystemFontOfSize(28), TextColor = UIColor.Label, Lines = 0 };
            _scrollView.AddSubview(_titleLabel);

        _platformLabel = new UILabel
            {
       Font = UIFont.SystemFontOfSize(12, UIFontWeight.Medium),
    TextColor = UIColor.White,
     TextAlignment = UITextAlignment.Center,
     BackgroundColor = UIColor.SystemBlue,
   ClipsToBounds = true
      };
        _platformLabel.Layer.CornerRadius = 4;
  _scrollView.AddSubview(_platformLabel);

       _playtimeLabel = new UILabel { Font = UIFont.SystemFontOfSize(14), TextColor = UIColor.SecondaryLabel };
   _scrollView.AddSubview(_playtimeLabel);

     _releaseDateLabel = new UILabel { Font = UIFont.SystemFontOfSize(14), TextColor = UIColor.SecondaryLabel };
      _scrollView.AddSubview(_releaseDateLabel);

         _genresLabel = new UILabel { Font = UIFont.SystemFontOfSize(14), TextColor = UIColor.SecondaryLabel, Lines = 0 };
        _scrollView.AddSubview(_genresLabel);

       _descriptionLabel = new UILabel { Font = UIFont.SystemFontOfSize(15), TextColor = UIColor.Label, Lines = 0 };
    _scrollView.AddSubview(_descriptionLabel);

 _developersLabel = new UILabel { Font = UIFont.SystemFontOfSize(14), TextColor = UIColor.SecondaryLabel, Lines = 0 };
            _scrollView.AddSubview(_developersLabel);

         _metacriticLabel = new UILabel
            {
  Font = UIFont.BoldSystemFontOfSize(16),
      TextColor = UIColor.White,
          TextAlignment = UITextAlignment.Center,
       ClipsToBounds = true
  };
            _metacriticLabel.Layer.CornerRadius = 4;
      _scrollView.AddSubview(_metacriticLabel);

     // Favorite & Wishlist buttons
            _favoriteButton = new UIButton(UIButtonType.System);
            _favoriteButton.SetImage(UIImage.GetSystemImage("heart"), UIControlState.Normal);
       _favoriteButton.TintColor = UIColor.SystemRed;
            _favoriteButton.TouchUpInside += OnFavoriteTapped;
            _scrollView.AddSubview(_favoriteButton);

            _wishlistButton = new UIButton(UIButtonType.System);
  _wishlistButton.SetImage(UIImage.GetSystemImage("bookmark"), UIControlState.Normal);
         _wishlistButton.TintColor = UIColor.SystemOrange;
       _wishlistButton.TouchUpInside += OnWishlistTapped;
         _scrollView.AddSubview(_wishlistButton);

            // User Rating Section
        _userRatingLabel = new UILabel { Text = "Your Rating", Font = UIFont.BoldSystemFontOfSize(18), TextColor = UIColor.Label };
 _scrollView.AddSubview(_userRatingLabel);

       _ratingStarsView = new UIStackView { Axis = UILayoutConstraintAxis.Horizontal, Spacing = 8, Distribution = UIStackViewDistribution.FillEqually };
       for (int i = 1; i <= 5; i++)
            {
       var starButton = new UIButton(UIButtonType.System);
   starButton.Tag = i;
       starButton.SetImage(UIImage.GetSystemImage("star"), UIControlState.Normal);
              starButton.TintColor = UIColor.SystemYellow;
 starButton.TouchUpInside += OnRatingTapped;
     _ratingStarsView.AddArrangedSubview(starButton);
     }
            _scrollView.AddSubview(_ratingStarsView);

// Screenshots Section
          _screenshotsLabel = new UILabel { Text = "Screenshots", Font = UIFont.BoldSystemFontOfSize(18), TextColor = UIColor.Label };
    _scrollView.AddSubview(_screenshotsLabel);

     var screenshotsLayout = new UICollectionViewFlowLayout
      {
           ScrollDirection = UICollectionViewScrollDirection.Horizontal,
   ItemSize = new CGSize(200, 120),
       MinimumInteritemSpacing = 10
       };
       _screenshotsCollectionView = new UICollectionView(CGRect.Empty, screenshotsLayout)
 {
                BackgroundColor = UIColor.Clear,
          DataSource = this,
           Delegate = this,
            ShowsHorizontalScrollIndicator = false
    };
            _screenshotsCollectionView.RegisterClassForCell(typeof(ScreenshotCell), "ScreenshotCell");
  _scrollView.AddSubview(_screenshotsCollectionView);

            // Notes Section
            _notesLabel = new UILabel { Text = "Your Notes", Font = UIFont.BoldSystemFontOfSize(18), TextColor = UIColor.Label };
            _scrollView.AddSubview(_notesLabel);

    _notesTextView = new UITextView
        {
     Font = UIFont.SystemFontOfSize(15),
   TextColor = UIColor.Label,
    BackgroundColor = UIColor.SecondarySystemBackground,
    Text = "Tap to add notes...",
    Editable = true
            };
    _notesTextView.Layer.CornerRadius = 8;
 _notesTextView.Delegate = new NotesTextViewDelegate(this);
       _scrollView.AddSubview(_notesTextView);

         // Play Button
         _playButton = new UIButton(UIButtonType.System);
    _playButton.SetTitle("  Play on PC", UIControlState.Normal);
            _playButton.SetImage(UIImage.GetSystemImage("play.fill"), UIControlState.Normal);
      _playButton.BackgroundColor = UIColor.SystemGreen;
  _playButton.TintColor = UIColor.White;
            _playButton.SetTitleColor(UIColor.White, UIControlState.Normal);
    _playButton.Layer.CornerRadius = 12;
     _playButton.TitleLabel!.Font = UIFont.BoldSystemFontOfSize(16);
            _playButton.TouchUpInside += OnPlayTapped;
     _scrollView.AddSubview(_playButton);
        }

        public override void ViewDidLayoutSubviews()
        {
            base.ViewDidLayoutSubviews();
            var width = View!.Bounds.Width;
 var height = View.Bounds.Height;
 var safeArea = View.SafeAreaInsets;
 var padding = 20f;

 _backgroundImageView.Frame = new CGRect(0, 0, width, height / 2);
      _scrollView.Frame = new CGRect(0, safeArea.Top, width, height - safeArea.Top);

            var coverWidth = width * 0.4f;
            var coverHeight = coverWidth * 1.4f;
       _coverImageView.Frame = new CGRect(padding, padding, coverWidth, coverHeight);

            var infoX = coverWidth + padding * 2;
       var infoWidth = width - infoX - padding;

            _titleLabel.Frame = new CGRect(infoX, padding, infoWidth - 100, 80);
            _titleLabel.SizeToFit();
     _titleLabel.Frame = new CGRect(infoX, padding, infoWidth - 100, _titleLabel.Frame.Height);

    _platformLabel.SizeToFit();
     var platformWidth = Math.Max(_platformLabel.Frame.Width + 16, 50);
            _platformLabel.Frame = new CGRect(infoX, _titleLabel.Frame.Bottom + 8, platformWidth, 22);

        _playtimeLabel.Frame = new CGRect(infoX, _platformLabel.Frame.Bottom + 8, infoWidth, 20);
 _releaseDateLabel.Frame = new CGRect(infoX, _playtimeLabel.Frame.Bottom + 4, infoWidth, 20);

            _favoriteButton.Frame = new CGRect(width - padding - 44, padding, 44, 44);
        _wishlistButton.Frame = new CGRect(width - padding - 88, padding, 44, 44);

            if (_game.MetacriticScore.HasValue)
     _metacriticLabel.Frame = new CGRect(infoX, _releaseDateLabel.Frame.Bottom + 8, 44, 44);

var contentY = Math.Max(_coverImageView.Frame.Bottom, _releaseDateLabel.Frame.Bottom + 60) + padding;

  // User Rating
     _userRatingLabel.Frame = new CGRect(padding, contentY, width - padding * 2, 24);
            contentY = _userRatingLabel.Frame.Bottom + 8;
            _ratingStarsView.Frame = new CGRect(padding, contentY, 200, 40);
            contentY = _ratingStarsView.Frame.Bottom + padding;

        // Genres
     _genresLabel.Frame = new CGRect(padding, contentY, width - padding * 2, 50);
            _genresLabel.SizeToFit();
      if (!_genresLabel.Hidden)
   contentY = _genresLabel.Frame.Bottom + padding;

       // Description
        _descriptionLabel.Frame = new CGRect(padding, contentY, width - padding * 2, 1000);
            _descriptionLabel.SizeToFit();
contentY = _descriptionLabel.Frame.Bottom + padding;

        // Developers
        _developersLabel.Frame = new CGRect(padding, contentY, width - padding * 2, 50);
       _developersLabel.SizeToFit();
         if (!_developersLabel.Hidden)
   contentY = _developersLabel.Frame.Bottom + padding;

   // Screenshots
            if (_screenshots.Count > 0)
        {
         _screenshotsLabel.Frame = new CGRect(padding, contentY, width - padding * 2, 24);
        contentY = _screenshotsLabel.Frame.Bottom + 8;
                _screenshotsCollectionView.Frame = new CGRect(0, contentY, width, 130);
         _screenshotsCollectionView.ContentInset = new UIEdgeInsets(0, padding, 0, padding);
                contentY = _screenshotsCollectionView.Frame.Bottom + padding;
            }
       _screenshotsLabel.Hidden = _screenshots.Count == 0;
            _screenshotsCollectionView.Hidden = _screenshots.Count == 0;

         // Notes
         _notesLabel.Frame = new CGRect(padding, contentY, width - padding * 2, 24);
          contentY = _notesLabel.Frame.Bottom + 8;
     _notesTextView.Frame = new CGRect(padding, contentY, width - padding * 2, 100);
     contentY = _notesTextView.Frame.Bottom + padding * 2;

       // Play Button
            _playButton.Frame = new CGRect(padding, contentY, width - padding * 2, 50);

   _scrollView.ContentSize = new CGSize(width, _playButton.Frame.Bottom + padding * 2);
        }

        private void LoadGameDetails()
   {
       _titleLabel.Text = _game.Name;
      _platformLabel.Text = _game.PlatformName;
     UpdateFavoriteButton();
  UpdateWishlistButton();
   UpdateRatingStars();

      _playtimeLabel.Text = _game.PlaytimeMinutes > 0 ? $"{_game.PlaytimeFormatted} played" : "";
 _playtimeLabel.Hidden = _game.PlaytimeMinutes == 0;

        _releaseDateLabel.Text = _game.ReleaseDate.HasValue ? $"{_game.ReleaseDate.Value:MMMM d, yyyy}" : "";
          _releaseDateLabel.Hidden = !_game.ReleaseDate.HasValue;

            _genresLabel.Text = !string.IsNullOrEmpty(_game.Genres) ? _game.Genres : "";
            _genresLabel.Hidden = string.IsNullOrEmpty(_game.Genres);

            _descriptionLabel.Text = !string.IsNullOrEmpty(_game.Description) ? _game.Description : "No description available.";
   _descriptionLabel.TextColor = string.IsNullOrEmpty(_game.Description) ? UIColor.TertiaryLabel : UIColor.Label;

    if (!string.IsNullOrEmpty(_game.Developers))
     {
       _developersLabel.Text = $"Dev: {_game.Developers}";
    if (!string.IsNullOrEmpty(_game.Publishers))
      _developersLabel.Text += $"\nPub: {_game.Publishers}";
 }
            _developersLabel.Hidden = string.IsNullOrEmpty(_game.Developers);

            if (_game.MetacriticScore.HasValue)
          {
      var score = _game.MetacriticScore.Value;
    _metacriticLabel.Text = $"{score:0}";
    _metacriticLabel.BackgroundColor = score >= 75 ? UIColor.SystemGreen : score >= 50 ? UIColor.SystemYellow : UIColor.SystemRed;
}
    _metacriticLabel.Hidden = !_game.MetacriticScore.HasValue;

            // Load notes
    if (!string.IsNullOrEmpty(_game.Notes))
      {
   _notesTextView.Text = _game.Notes;
      _notesTextView.TextColor = UIColor.Label;
  }

   // Load screenshots
        _screenshots = _game.ScreenshotList;
       _screenshotsCollectionView.ReloadData();

     LoadImagesAsync();
    ViewDidLayoutSubviews();
     }

        private void UpdateRatingStars()
        {
    var rating = (int)(_game.UserRating ?? 0);
   for (int i = 0; i < _ratingStarsView.ArrangedSubviews.Length; i++)
            {
if (_ratingStarsView.ArrangedSubviews[i] is UIButton starButton)
        {
         var isFilled = i < rating;
    starButton.SetImage(UIImage.GetSystemImage(isFilled ? "star.fill" : "star"), UIControlState.Normal);
            }
            }
        }

        private async void OnRatingTapped(object? sender, EventArgs e)
        {
       if (sender is UIButton button)
            {
         _game.UserRating = button.Tag;
      UpdateRatingStars();
     await DatabaseService.Instance.SaveGameAsync(_game);
     GameUpdated?.Invoke(this, _game);
 }
        }

     private async void LoadImagesAsync()
      {
  if (string.IsNullOrEmpty(_game.CoverImageUrl)) return;
            try
            {
      var data = await ImageClient.GetByteArrayAsync(_game.CoverImageUrl);
    var nsData = Foundation.NSData.FromArray(data);
    var image = UIImage.LoadFromData(nsData);
      InvokeOnMainThread(() =>
       {
          _coverImageView.Image = image;
           _backgroundImageView.Image = image;
     var blurEffect = UIBlurEffect.FromStyle(UIBlurEffectStyle.SystemUltraThinMaterial);
         var blurView = new UIVisualEffectView(blurEffect) { Frame = _backgroundImageView.Bounds, AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight };
     _backgroundImageView.AddSubview(blurView);
 });
            }
            catch { }
        }

 private void UpdateFavoriteButton()
 {
      _favoriteButton.SetImage(UIImage.GetSystemImage(_game.IsFavorite ? "heart.fill" : "heart"), UIControlState.Normal);
        }

  private void UpdateWishlistButton()
    {
       _wishlistButton.SetImage(UIImage.GetSystemImage(_game.IsWishlisted ? "bookmark.fill" : "bookmark"), UIControlState.Normal);
        }

        private async void OnFavoriteTapped(object? sender, EventArgs e)
 {
      _game.IsFavorite = !_game.IsFavorite;
        UpdateFavoriteButton();
            await DatabaseService.Instance.SaveGameAsync(_game);
      GameUpdated?.Invoke(this, _game);
        }

    private async void OnWishlistTapped(object? sender, EventArgs e)
        {
            _game.IsWishlisted = !_game.IsWishlisted;
            UpdateWishlistButton();
       await DatabaseService.Instance.SaveGameAsync(_game);
   GameUpdated?.Invoke(this, _game);
        }

        private async void OnPlayTapped(object? sender, EventArgs e)
        {
            var userData = await DatabaseService.Instance.GetUserDataAsync();
       if (string.IsNullOrEmpty(userData.RemotePCHost))
 {
      ShowAlert("Remote PC Not Configured", "Set up your PC connection in Settings to play games remotely.");
   return;
            }

        _playButton.Enabled = false;
            _playButton.SetTitle("  Launching...", UIControlState.Normal);
     try
     {
        var status = await RemotePCService.Instance.GetStatusAsync(userData.RemotePCHost, userData.RemotePCPort, userData.RemotePCAuthToken);
       if (!status.IsOnline)
                {
        ShowAlert("PC Offline", "Your PC appears to be offline. Wake it up first?");
          return;
      }
     var success = await RemotePCService.Instance.LaunchGameAsync(userData.RemotePCHost, userData.RemotePCPort, _game.ExternalId ?? _game.Name, userData.RemotePCAuthToken);
     ShowAlert(success ? "Game Launched" : "Launch Failed", success ? $"{_game.Name} is starting on your PC!" : "Could not start the game. Make sure it's installed on your PC.");
  }
            catch (Exception ex) { ShowAlert("Error", ex.Message); }
      finally
      {
       _playButton.Enabled = true;
                _playButton.SetTitle("  Play on PC", UIControlState.Normal);
      }
        }

        private void ShowMenu(object? sender, EventArgs e)
        {
 var alert = UIAlertController.Create(null, null, UIAlertControllerStyle.ActionSheet);
 alert.AddAction(UIAlertAction.Create(_game.IsFavorite ? "Remove from Favorites" : "Add to Favorites", UIAlertActionStyle.Default, _ => OnFavoriteTapped(null, EventArgs.Empty)));
        alert.AddAction(UIAlertAction.Create(_game.IsWishlisted ? "Remove from Wishlist" : "Add to Wishlist", UIAlertActionStyle.Default, _ => OnWishlistTapped(null, EventArgs.Empty)));
            if (!string.IsNullOrEmpty(_game.Website))
       {
        alert.AddAction(UIAlertAction.Create("Open Website", UIAlertActionStyle.Default, _ =>
              {
       UIApplication.SharedApplication.OpenUrl(new Foundation.NSUrl(_game.Website!), new UIApplicationOpenUrlOptions(), null);
     }));
            }
   alert.AddAction(UIAlertAction.Create("Delete from Library", UIAlertActionStyle.Destructive, async _ =>
      {
       await DatabaseService.Instance.DeleteGameAsync(_game);
  GameUpdated?.Invoke(this, _game);
   NavigationController?.PopViewController(true);
          }));
   alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
            if (alert.PopoverPresentationController != null)
     alert.PopoverPresentationController.BarButtonItem = NavigationItem.RightBarButtonItem;
        PresentViewController(alert, true, null);
     }

        private void ShowAlert(string title, string message)
        {
    var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
          PresentViewController(alert, true, null);
        }

public async Task SaveNotesAsync()
        {
if (_notesTextView.Text != "Tap to add notes..." && _notesTextView.Text != _game.Notes)
            {
             _game.Notes = _notesTextView.Text;
           await DatabaseService.Instance.SaveGameAsync(_game);
        GameUpdated?.Invoke(this, _game);
  }
        }

        // UICollectionView DataSource
        public nint GetItemsCount(UICollectionView collectionView, nint section) => _screenshots.Count;

        public UICollectionViewCell GetCell(UICollectionView collectionView, Foundation.NSIndexPath indexPath)
        {
          var cell = collectionView.DequeueReusableCell("ScreenshotCell", indexPath) as ScreenshotCell;
            cell!.Configure(_screenshots[indexPath.Row]);
         return cell;
  }

   // Notes TextView Delegate
        private class NotesTextViewDelegate : UITextViewDelegate
        {
 private readonly GameDetailViewController _parent;
 public NotesTextViewDelegate(GameDetailViewController parent) => _parent = parent;

        public override void EditingStarted(UITextView textView)
        {
                if (textView.Text == "Tap to add notes...")
           {
    textView.Text = "";
 textView.TextColor = UIColor.Label;
        }
            }

     public override void EditingEnded(UITextView textView)
            {
        if (string.IsNullOrWhiteSpace(textView.Text))
                {
         textView.Text = "Tap to add notes...";
          textView.TextColor = UIColor.TertiaryLabel;
 }
                _ = _parent.SaveNotesAsync();
     }
        }
    }

    // Screenshot Cell
    public class ScreenshotCell : UICollectionViewCell
    {
        private UIImageView _imageView = null!;
        private static readonly HttpClient ImageClient = new();

   [Foundation.Export("initWithFrame:")]
        public ScreenshotCell(CGRect frame) : base(frame)
        {
         _imageView = new UIImageView
      {
      ContentMode = UIViewContentMode.ScaleAspectFill,
            ClipsToBounds = true,
      BackgroundColor = UIColor.SystemGray5
       };
  _imageView.Layer.CornerRadius = 8;
            ContentView.AddSubview(_imageView);
        }

        public override void LayoutSubviews()
        {
         base.LayoutSubviews();
    _imageView.Frame = ContentView.Bounds;
        }

        public async void Configure(string imageUrl)
        {
            _imageView.Image = null;
            if (string.IsNullOrEmpty(imageUrl)) return;

         try
     {
            var data = await ImageClient.GetByteArrayAsync(imageUrl);
       var nsData = Foundation.NSData.FromArray(data);
         var image = UIImage.LoadFromData(nsData);
         InvokeOnMainThread(() => _imageView.Image = image);
            }
            catch { }
        }
    }
}
