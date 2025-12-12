using gaming_hub.Models;
using gaming_hub.Services;

namespace gaming_hub.ViewControllers
{
 public class GameDetailViewController : UIViewController
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
     private UIButton _playButton = null!;
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

       _favoriteButton = new UIButton(UIButtonType.System);
         _favoriteButton.SetImage(UIImage.GetSystemImage("heart"), UIControlState.Normal);
   _favoriteButton.TintColor = UIColor.SystemRed;
   _favoriteButton.TouchUpInside += OnFavoriteTapped;
     _scrollView.AddSubview(_favoriteButton);

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
            _titleLabel.Frame = new CGRect(infoX, padding, infoWidth, 80);
 _titleLabel.SizeToFit();
      _titleLabel.Frame = new CGRect(infoX, padding, infoWidth, _titleLabel.Frame.Height);

         _platformLabel.SizeToFit();
     var platformWidth = Math.Max(_platformLabel.Frame.Width + 16, 50);
     _platformLabel.Frame = new CGRect(infoX, _titleLabel.Frame.Bottom + 8, platformWidth, 22);
_playtimeLabel.Frame = new CGRect(infoX, _platformLabel.Frame.Bottom + 8, infoWidth, 20);
 _releaseDateLabel.Frame = new CGRect(infoX, _playtimeLabel.Frame.Bottom + 4, infoWidth, 20);
    _favoriteButton.Frame = new CGRect(width - padding - 44, padding, 44, 44);

   if (_game.MetacriticScore.HasValue)
   _metacriticLabel.Frame = new CGRect(infoX, _releaseDateLabel.Frame.Bottom + 8, 44, 44);

       var contentY = Math.Max(_coverImageView.Frame.Bottom, _releaseDateLabel.Frame.Bottom + 60) + padding;
       _genresLabel.Frame = new CGRect(padding, contentY, width - padding * 2, 50);
      _genresLabel.SizeToFit();
   contentY = _genresLabel.Frame.Bottom + padding;
   _descriptionLabel.Frame = new CGRect(padding, contentY, width - padding * 2, 1000);
 _descriptionLabel.SizeToFit();
            contentY = _descriptionLabel.Frame.Bottom + padding;
 _developersLabel.Frame = new CGRect(padding, contentY, width - padding * 2, 50);
   _developersLabel.SizeToFit();
   contentY = _developersLabel.Frame.Bottom + padding * 2;
  _playButton.Frame = new CGRect(padding, contentY, width - padding * 2, 50);
    _scrollView.ContentSize = new CGSize(width, _playButton.Frame.Bottom + padding * 2);
        }

private void LoadGameDetails()
     {
  _titleLabel.Text = _game.Name;
    _platformLabel.Text = _game.PlatformName;
 UpdateFavoriteButton();

            _playtimeLabel.Text = _game.PlaytimeMinutes > 0 ? $"?? {_game.PlaytimeFormatted} played" : "";
            _playtimeLabel.Hidden = _game.PlaytimeMinutes == 0;

  _releaseDateLabel.Text = _game.ReleaseDate.HasValue ? $"?? {_game.ReleaseDate.Value:MMMM d, yyyy}" : "";
      _releaseDateLabel.Hidden = !_game.ReleaseDate.HasValue;

           _genresLabel.Text = !string.IsNullOrEmpty(_game.Genres) ? $"?? {_game.Genres}" : "";
    _genresLabel.Hidden = string.IsNullOrEmpty(_game.Genres);

      _descriptionLabel.Text = !string.IsNullOrEmpty(_game.Description) ? _game.Description : "No description available.";
     _descriptionLabel.TextColor = string.IsNullOrEmpty(_game.Description) ? UIColor.TertiaryLabel : UIColor.Label;

   if (!string.IsNullOrEmpty(_game.Developers))
     {
     _developersLabel.Text = $"????? {_game.Developers}";
  if (!string.IsNullOrEmpty(_game.Publishers))
       _developersLabel.Text += $"\n?? {_game.Publishers}";
          }
            _developersLabel.Hidden = string.IsNullOrEmpty(_game.Developers);

    if (_game.MetacriticScore.HasValue)
     {
  var score = _game.MetacriticScore.Value;
       _metacriticLabel.Text = $"{score:0}";
 _metacriticLabel.BackgroundColor = score >= 75 ? UIColor.SystemGreen : score >= 50 ? UIColor.SystemYellow : UIColor.SystemRed;
        }
       _metacriticLabel.Hidden = !_game.MetacriticScore.HasValue;

      LoadImagesAsync();
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

    private async void OnFavoriteTapped(object? sender, EventArgs e)
        {
     _game.IsFavorite = !_game.IsFavorite;
     UpdateFavoriteButton();
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
    }
}
