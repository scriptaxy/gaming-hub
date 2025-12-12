using gaming_hub.Models;

namespace gaming_hub.Views
{
    public class GameCell : UICollectionViewCell
  {
     public static readonly string ReuseId = "GameCell";
   private UIImageView _coverImageView = null!;
        private UILabel _nameLabel = null!;
     private UILabel _platformLabel = null!;
        private UIImageView _favoriteIcon = null!;
        private UIView _gradientOverlay = null!;
  private UIActivityIndicatorView _loadingIndicator = null!;
     private static readonly HttpClient ImageClient = new();
        private string? _currentImageUrl;

        public GameCell(CGRect frame) : base(frame) => SetupViews();
        public GameCell(IntPtr handle) : base(handle) => SetupViews();

   private void SetupViews()
        {
        _coverImageView = new UIImageView
     {
  ContentMode = UIViewContentMode.ScaleAspectFill,
   ClipsToBounds = true,
 BackgroundColor = UIColor.SystemGray5
        };
         _coverImageView.Layer.CornerRadius = 12;
 ContentView.AddSubview(_coverImageView);

            _gradientOverlay = new UIView();
            ContentView.AddSubview(_gradientOverlay);

   _nameLabel = new UILabel
     {
          Font = UIFont.BoldSystemFontOfSize(14),
        TextColor = UIColor.White,
   Lines = 2,
          LineBreakMode = UILineBreakMode.TailTruncation
  };
  ContentView.AddSubview(_nameLabel);

  _platformLabel = new UILabel
            {
     Font = UIFont.SystemFontOfSize(10, UIFontWeight.Medium),
        TextColor = UIColor.White,
                TextAlignment = UITextAlignment.Center,
         BackgroundColor = UIColor.SystemBlue.ColorWithAlpha(0.8f),
         ClipsToBounds = true
  };
   _platformLabel.Layer.CornerRadius = 4;
 ContentView.AddSubview(_platformLabel);

      _favoriteIcon = new UIImageView
{
      Image = UIImage.GetSystemImage("heart.fill"),
          TintColor = UIColor.SystemRed,
  ContentMode = UIViewContentMode.ScaleAspectFit,
        Hidden = true
        };
  ContentView.AddSubview(_favoriteIcon);

            _loadingIndicator = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium)
 {
       HidesWhenStopped = true,
   Color = UIColor.White
     };
     ContentView.AddSubview(_loadingIndicator);
  }

    public override void LayoutSubviews()
 {
          base.LayoutSubviews();
  var bounds = ContentView.Bounds;
      _coverImageView.Frame = bounds;
 _gradientOverlay.Frame = new CGRect(0, bounds.Height - 60, bounds.Width, 60);
    ApplyGradient(_gradientOverlay);
        _nameLabel.Frame = new CGRect(8, bounds.Height - 44, bounds.Width - 16, 36);
       _platformLabel.SizeToFit();
        var platformWidth = Math.Max(_platformLabel.Frame.Width + 12, 40);
   _platformLabel.Frame = new CGRect(8, 8, platformWidth, 18);
    _favoriteIcon.Frame = new CGRect(bounds.Width - 28, 8, 20, 20);
       _loadingIndicator.Center = new CGPoint(bounds.Width / 2, bounds.Height / 2);
  }

        private void ApplyGradient(UIView view)
        {
       view.Layer.Sublayers?.ToList().ForEach(l => l.RemoveFromSuperLayer());
            var gradient = new CoreAnimation.CAGradientLayer
 {
       Frame = view.Bounds,
       Colors = [UIColor.Clear.CGColor, UIColor.Black.ColorWithAlpha(0.7f).CGColor],
   Locations = [0, 1]
  };
     view.Layer.InsertSublayer(gradient, 0);
        }

  public void Configure(Game game)
        {
         _nameLabel.Text = game.Name;
       _platformLabel.Text = game.PlatformName;
            _favoriteIcon.Hidden = !game.IsFavorite;
  _platformLabel.BackgroundColor = game.Platform switch
    {
      GamePlatform.Steam => UIColor.FromRGB(27, 40, 56).ColorWithAlpha(0.9f),
                GamePlatform.Epic => UIColor.FromRGB(45, 45, 45).ColorWithAlpha(0.9f),
     GamePlatform.GOG => UIColor.FromRGB(131, 48, 224).ColorWithAlpha(0.9f),
      GamePlatform.Xbox => UIColor.FromRGB(16, 124, 16).ColorWithAlpha(0.9f),
           GamePlatform.PlayStation => UIColor.FromRGB(0, 55, 145).ColorWithAlpha(0.9f),
      _ => UIColor.SystemBlue.ColorWithAlpha(0.9f)
      };
LoadImageAsync(game.CoverImageUrl);
}

 private async void LoadImageAsync(string? imageUrl)
        {
   _currentImageUrl = imageUrl;
  _coverImageView.Image = null;
          if (string.IsNullOrEmpty(imageUrl))
 {
   _coverImageView.Image = UIImage.GetSystemImage("gamecontroller.fill");
 _coverImageView.TintColor = UIColor.SystemGray3;
             return;
   }
         _loadingIndicator.StartAnimating();
   try
   {
   var data = await ImageClient.GetByteArrayAsync(imageUrl);
       if (_currentImageUrl != imageUrl) return;
    var nsData = Foundation.NSData.FromArray(data);
   var image = UIImage.LoadFromData(nsData);
     InvokeOnMainThread(() => { if (_currentImageUrl == imageUrl) _coverImageView.Image = image; });
          }
   catch
   {
              InvokeOnMainThread(() =>
  {
     if (_currentImageUrl == imageUrl)
      {
              _coverImageView.Image = UIImage.GetSystemImage("photo");
    _coverImageView.TintColor = UIColor.SystemGray3;
    }
       });
    }
 finally { InvokeOnMainThread(() => _loadingIndicator.StopAnimating()); }
  }

        public override void PrepareForReuse()
        {
     base.PrepareForReuse();
     _currentImageUrl = null;
            _coverImageView.Image = null;
            _nameLabel.Text = null;
       _platformLabel.Text = null;
     _favoriteIcon.Hidden = true;
        }
    }

    public class FeaturedGameCell : UICollectionViewCell
    {
    public static readonly string ReuseId = "FeaturedGameCell";
        private UIImageView _backgroundImageView = null!;
        private UIView _overlay = null!;
        private UILabel _titleLabel = null!;
        private UILabel _descriptionLabel = null!;
    private UILabel _playtimeLabel = null!;
  private UIButton _playButton = null!;
        private static readonly HttpClient ImageClient = new();
        private string? _currentImageUrl;

        public event EventHandler? PlayButtonTapped;

   public FeaturedGameCell(CGRect frame) : base(frame) => SetupViews();
        public FeaturedGameCell(IntPtr handle) : base(handle) => SetupViews();

   private void SetupViews()
        {
     _backgroundImageView = new UIImageView
       {
       ContentMode = UIViewContentMode.ScaleAspectFill,
  ClipsToBounds = true,
         BackgroundColor = UIColor.SystemGray5
};
      _backgroundImageView.Layer.CornerRadius = 16;
  ContentView.AddSubview(_backgroundImageView);

       _overlay = new UIView { BackgroundColor = UIColor.Black.ColorWithAlpha(0.4f) };
    _overlay.Layer.CornerRadius = 16;
        ContentView.AddSubview(_overlay);

  _titleLabel = new UILabel { Font = UIFont.BoldSystemFontOfSize(24), TextColor = UIColor.White, Lines = 2 };
     ContentView.AddSubview(_titleLabel);

            _descriptionLabel = new UILabel
       {
 Font = UIFont.SystemFontOfSize(14),
        TextColor = UIColor.White.ColorWithAlpha(0.8f),
         Lines = 2,
      LineBreakMode = UILineBreakMode.TailTruncation
     };
        ContentView.AddSubview(_descriptionLabel);

   _playtimeLabel = new UILabel { Font = UIFont.SystemFontOfSize(12, UIFontWeight.Medium), TextColor = UIColor.White.ColorWithAlpha(0.7f) };
            ContentView.AddSubview(_playtimeLabel);

  _playButton = new UIButton(UIButtonType.System);
       _playButton.SetTitle("  Play Now", UIControlState.Normal);
    _playButton.SetImage(UIImage.GetSystemImage("play.fill"), UIControlState.Normal);
            _playButton.TintColor = UIColor.White;
 _playButton.BackgroundColor = UIColor.SystemGreen;
            _playButton.Layer.CornerRadius = 20;
_playButton.TitleLabel!.Font = UIFont.BoldSystemFontOfSize(16);
            _playButton.TouchUpInside += (s, e) => PlayButtonTapped?.Invoke(this, EventArgs.Empty);
ContentView.AddSubview(_playButton);
}

        public override void LayoutSubviews()
        {
 base.LayoutSubviews();
     var bounds = ContentView.Bounds;
      var padding = 20f;
     _backgroundImageView.Frame = bounds;
  _overlay.Frame = bounds;
     _titleLabel.Frame = new CGRect(padding, bounds.Height - 140, bounds.Width - padding * 2, 60);
            _descriptionLabel.Frame = new CGRect(padding, bounds.Height - 85, bounds.Width - padding * 2 - 120, 40);
     _playtimeLabel.Frame = new CGRect(padding, bounds.Height - 45, 150, 20);
    _playButton.Frame = new CGRect(bounds.Width - 140, bounds.Height - 60, 120, 40);
        }

        public void Configure(Game game)
     {
         _titleLabel.Text = game.Name;
    _descriptionLabel.Text = game.Description ?? game.Genres;
     _playtimeLabel.Text = game.PlaytimeMinutes > 0 ? $"?? {game.PlaytimeFormatted} played" : "";
            LoadImageAsync(game.BackgroundImageUrl ?? game.CoverImageUrl);
        }

        private async void LoadImageAsync(string? imageUrl)
        {
    _currentImageUrl = imageUrl;
      _backgroundImageView.Image = null;
 if (string.IsNullOrEmpty(imageUrl)) return;
       try
       {
        var data = await ImageClient.GetByteArrayAsync(imageUrl);
    if (_currentImageUrl != imageUrl) return;
    var nsData = Foundation.NSData.FromArray(data);
        var image = UIImage.LoadFromData(nsData);
        InvokeOnMainThread(() => { if (_currentImageUrl == imageUrl) _backgroundImageView.Image = image; });
         }
            catch { }
     }
    }
}
