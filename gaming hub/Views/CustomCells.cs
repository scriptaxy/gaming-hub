using gaming_hub.Models;
using gaming_hub.Services;

namespace gaming_hub.Views
{
    public class DealCell : UITableViewCell
    {
        public static readonly string ReuseId = "DealCell";
        private UIImageView _gameImageView = null!;
        private UILabel _titleLabel = null!;
        private UILabel _storeLabel = null!;
        private UILabel _originalPriceLabel = null!;
        private UILabel _salePriceLabel = null!;
        private UILabel _discountLabel = null!;
        private UIView _discountBadge = null!;
        private UILabel _ratingLabel = null!;
        private UIView _cardView = null!;
        private static readonly HttpClient ImageClient = new();
        private string? _currentImageUrl;

        public DealCell(IntPtr handle) : base(handle) => SetupViews();
        public DealCell(UITableViewCellStyle style, string reuseId) : base(style, reuseId) => SetupViews();

        private void SetupViews()
        {
            SelectionStyle = UITableViewCellSelectionStyle.None;
            BackgroundColor = UIColor.Clear;
            
            _cardView = new UIView { BackgroundColor = UIColor.SecondarySystemBackground, ClipsToBounds = true };
            _cardView.Layer.CornerRadius = 12;
            ContentView.AddSubview(_cardView);

            _gameImageView = new UIImageView 
            { 
                ContentMode = UIViewContentMode.ScaleAspectFill, 
                ClipsToBounds = true, 
                BackgroundColor = UIColor.SystemGray5 
            };
            _gameImageView.Layer.CornerRadius = 8;
            _cardView.AddSubview(_gameImageView);

            _titleLabel = new UILabel 
            { 
                Font = UIFont.BoldSystemFontOfSize(15), 
                TextColor = UIColor.Label, 
                Lines = 2, 
                LineBreakMode = UILineBreakMode.TailTruncation 
            };
            _cardView.AddSubview(_titleLabel);

            _storeLabel = new UILabel 
            { 
                Font = UIFont.SystemFontOfSize(11), 
                TextColor = UIColor.SecondaryLabel 
            };
            _cardView.AddSubview(_storeLabel);

            _originalPriceLabel = new UILabel 
            { 
                Font = UIFont.SystemFontOfSize(11), 
                TextColor = UIColor.TertiaryLabel,
                TextAlignment = UITextAlignment.Right
            };
            _cardView.AddSubview(_originalPriceLabel);

            _salePriceLabel = new UILabel 
            { 
                Font = UIFont.BoldSystemFontOfSize(16), 
                TextColor = UIColor.SystemGreen,
                TextAlignment = UITextAlignment.Right
            };
            _cardView.AddSubview(_salePriceLabel);

            _discountBadge = new UIView { BackgroundColor = UIColor.SystemRed };
            _discountBadge.Layer.CornerRadius = 4;
            _cardView.AddSubview(_discountBadge);

            _discountLabel = new UILabel 
            { 
                Font = UIFont.BoldSystemFontOfSize(11), 
                TextColor = UIColor.White, 
                TextAlignment = UITextAlignment.Center 
            };
            _discountBadge.AddSubview(_discountLabel);

            _ratingLabel = new UILabel 
            { 
                Font = UIFont.SystemFontOfSize(10), 
                TextColor = UIColor.TertiaryLabel 
            };
            _cardView.AddSubview(_ratingLabel);
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var bounds = ContentView.Bounds;
            _cardView.Frame = new CGRect(16, 6, bounds.Width - 32, bounds.Height - 12);
            
            var cardBounds = _cardView.Bounds;
            var imageSize = cardBounds.Height - 16;
            _gameImageView.Frame = new CGRect(8, 8, imageSize, imageSize);
      
            var textX = imageSize + 16;
            var priceWidth = 70f;
            var textWidth = cardBounds.Width - textX - priceWidth - 8;
   
            _titleLabel.Frame = new CGRect(textX, 10, textWidth, 36);
            _storeLabel.Frame = new CGRect(textX, 48, textWidth, 14);
            _ratingLabel.Frame = new CGRect(textX, 64, textWidth, 14);
            
            var priceX = cardBounds.Width - priceWidth - 8;
            _originalPriceLabel.Frame = new CGRect(priceX, 12, priceWidth, 14);
            _salePriceLabel.Frame = new CGRect(priceX, 28, priceWidth, 20);
            _discountBadge.Frame = new CGRect(priceX + 10, 52, 50, 18);
            _discountLabel.Frame = new CGRect(0, 0, 50, 18);
        }

        public void Configure(GameDeal deal)
        {
            _titleLabel.Text = deal.GameName;
            _storeLabel.Text = deal.StoreName;
            _salePriceLabel.Text = deal.PriceText;
            _discountLabel.Text = deal.SavingsText;
            
            var originalPriceAttr = new NSAttributedString(
                deal.OriginalPriceText, 
                new UIStringAttributes 
                { 
                    StrikethroughStyle = NSUnderlineStyle.Single, 
                    ForegroundColor = UIColor.TertiaryLabel,
                    Font = UIFont.SystemFontOfSize(11)
                });
            _originalPriceLabel.AttributedText = originalPriceAttr;
            
            if (deal.MetacriticScore.HasValue || deal.SteamRating.HasValue)
            {
                var ratingParts = new List<string>();
                if (deal.MetacriticScore.HasValue) 
                    ratingParts.Add($"MC: {deal.MetacriticScore:0}");
                if (deal.SteamRating.HasValue) 
                    ratingParts.Add($"Steam: {deal.SteamRating:0}%");
                _ratingLabel.Text = string.Join(" • ", ratingParts);
            }
            else 
            { 
                _ratingLabel.Text = ""; 
            }
  
            LoadImageAsync(deal.GameImageUrl);
        }

        private async void LoadImageAsync(string? imageUrl)
        {
            _currentImageUrl = imageUrl;
            _gameImageView.Image = UIImage.GetSystemImage("gamecontroller.fill");
            _gameImageView.TintColor = UIColor.SystemGray3;
            if (string.IsNullOrEmpty(imageUrl)) return;
        
            try
            {
                var data = await ImageClient.GetByteArrayAsync(imageUrl);
                if (_currentImageUrl != imageUrl) return;
                var nsData = Foundation.NSData.FromArray(data);
                var image = UIImage.LoadFromData(nsData);
                InvokeOnMainThread(() => 
                { 
                    if (_currentImageUrl == imageUrl) 
                    { 
                        _gameImageView.Image = image; 
                        _gameImageView.TintColor = null; 
                    } 
                });
            }
            catch { }
        }

        public override void PrepareForReuse()
        {
            base.PrepareForReuse();
            _currentImageUrl = null;
            _gameImageView.Image = UIImage.GetSystemImage("gamecontroller.fill");
            _gameImageView.TintColor = UIColor.SystemGray3;
            _titleLabel.Text = null;
            _storeLabel.Text = null;
            _ratingLabel.Text = null;
        }
    }

    public class ReleaseCell : UITableViewCell
    {
        public static readonly string ReuseId = "ReleaseCell";
        private UIImageView _coverImageView = null!;
        private UILabel _titleLabel = null!;
        private UILabel _dateLabel = null!;
        private UILabel _descriptionLabel = null!;
        private UILabel _platformsLabel = null!;
        private UIButton _wishlistButton = null!;
        private UIView _cardView = null!;
        private static readonly HttpClient ImageClient = new();
        private string? _currentImageUrl;

        public event EventHandler? WishlistToggled;

        public ReleaseCell(IntPtr handle) : base(handle) => SetupViews();
        public ReleaseCell(UITableViewCellStyle style, string reuseId) : base(style, reuseId) => SetupViews();

        private void SetupViews()
        {
            SelectionStyle = UITableViewCellSelectionStyle.None;
            BackgroundColor = UIColor.Clear;
            
            _cardView = new UIView { BackgroundColor = UIColor.SecondarySystemBackground };
            _cardView.Layer.CornerRadius = 12;
            ContentView.AddSubview(_cardView);

            _coverImageView = new UIImageView 
            { 
                ContentMode = UIViewContentMode.ScaleAspectFill, 
                ClipsToBounds = true, 
                BackgroundColor = UIColor.SystemGray5 
            };
            _coverImageView.Layer.CornerRadius = 8;
            _cardView.AddSubview(_coverImageView);

            _titleLabel = new UILabel 
            { 
                Font = UIFont.BoldSystemFontOfSize(15), 
                TextColor = UIColor.Label, 
                Lines = 2,
                LineBreakMode = UILineBreakMode.TailTruncation
            };
            _cardView.AddSubview(_titleLabel);

            _dateLabel = new UILabel 
            { 
                Font = UIFont.SystemFontOfSize(12), 
                TextColor = UIColor.SecondaryLabel 
            };
            _cardView.AddSubview(_dateLabel);

            _descriptionLabel = new UILabel 
            { 
                Font = UIFont.SystemFontOfSize(12), 
                TextColor = UIColor.SystemGreen,
                LineBreakMode = UILineBreakMode.TailTruncation
            };
            _cardView.AddSubview(_descriptionLabel);

            _platformsLabel = new UILabel 
            { 
                Font = UIFont.SystemFontOfSize(10), 
                TextColor = UIColor.TertiaryLabel 
            };
            _cardView.AddSubview(_platformsLabel);

            _wishlistButton = new UIButton(UIButtonType.System);
            _wishlistButton.SetImage(UIImage.GetSystemImage("heart"), UIControlState.Normal);
            _wishlistButton.TintColor = UIColor.SystemRed;
            _wishlistButton.TouchUpInside += (s, e) => WishlistToggled?.Invoke(this, EventArgs.Empty);
            _cardView.AddSubview(_wishlistButton);
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var bounds = ContentView.Bounds;
            _cardView.Frame = new CGRect(16, 6, bounds.Width - 32, bounds.Height - 12);
  
            var cardBounds = _cardView.Bounds;
            var imageWidth = 90f;
            var imageHeight = cardBounds.Height - 16;
            _coverImageView.Frame = new CGRect(8, 8, imageWidth, imageHeight);
         
            var textX = imageWidth + 16;
            var buttonSize = 36f;
            var textWidth = cardBounds.Width - textX - buttonSize - 8;
   
            _titleLabel.Frame = new CGRect(textX, 10, textWidth, 34);
            _dateLabel.Frame = new CGRect(textX, 46, textWidth, 16);
            _descriptionLabel.Frame = new CGRect(textX, 64, textWidth, 16);
            _platformsLabel.Frame = new CGRect(textX, cardBounds.Height - 22, textWidth, 14);
            _wishlistButton.Frame = new CGRect(cardBounds.Width - buttonSize - 8, 8, buttonSize, buttonSize);
        }

        public void Configure(UpcomingRelease release)
        {
            _titleLabel.Text = release.GameName;

          // Show release date or countdown
         if (release.ReleaseDate > DateTime.Today)
   {
         var daysUntil = (release.ReleaseDate - DateTime.Today).Days;
       if (daysUntil == 0)
           _dateLabel.Text = "?? Releases Today!";
   else if (daysUntil == 1)
   _dateLabel.Text = "?? Tomorrow";
           else if (daysUntil <= 7)
   _dateLabel.Text = $"?? In {daysUntil} days";
          else
  _dateLabel.Text = release.IsExactDate 
         ? $"?? {release.ReleaseDate:MMM d, yyyy}" 
     : "?? Coming Soon";
  }
  else
       {
    _dateLabel.Text = "?? Available Now";
            }

_descriptionLabel.Text = release.Description ?? "";
            _platformsLabel.Text = !string.IsNullOrEmpty(release.Platforms) ? $"via {release.Platforms}" : "";

      // Color based on description
   if (release.Description?.Contains("Free") == true)
      _descriptionLabel.TextColor = UIColor.SystemPurple;
         else
          _descriptionLabel.TextColor = UIColor.SystemGreen;

 _wishlistButton.SetImage(
   UIImage.GetSystemImage(release.IsWishlisted ? "heart.fill" : "heart"), 
      UIControlState.Normal);
            
       LoadImageAsync(release.CoverImageUrl);
}

    private async void LoadImageAsync(string? imageUrl)
        {
            _currentImageUrl = imageUrl;
    _coverImageView.Image = UIImage.GetSystemImage("gamecontroller.fill");
        _coverImageView.TintColor = UIColor.SystemGray3;
    if (string.IsNullOrEmpty(imageUrl)) return;
    
try
     {
        var data = await ImageClient.GetByteArrayAsync(imageUrl);
            if (_currentImageUrl != imageUrl) return;
     var nsData = Foundation.NSData.FromArray(data);
         var image = UIImage.LoadFromData(nsData);
           InvokeOnMainThread(() => 
   { 
       if (_currentImageUrl == imageUrl) 
{ 
        _coverImageView.Image = image; 
            _coverImageView.TintColor = null; 
          } 
         });
            }
            catch { }
      }

   public override void PrepareForReuse()
        {
            base.PrepareForReuse();
       _currentImageUrl = null;
        _coverImageView.Image = UIImage.GetSystemImage("gamecontroller.fill");
       _coverImageView.TintColor = UIColor.SystemGray3;
            _titleLabel.Text = null;
      _dateLabel.Text = null;
_descriptionLabel.Text = null;
          _platformsLabel.Text = null;
     }
    }

    public class RemotePCStatusView : UIView
    {
        private UIView _statusIndicator = null!;
        private UILabel _statusLabel = null!;
      private UILabel _hostnameLabel = null!;
     private UILabel _cpuLabel = null!;
        private UILabel _memoryLabel = null!;
        private UILabel _gpuLabel = null!;
        private UILabel _currentGameLabel = null!;
        private UIButton _wakeButton = null!;
        private UIButton _sleepButton = null!;

        public event EventHandler? WakeRequested;
        public event EventHandler? SleepRequested;

        public RemotePCStatusView() => SetupViews();
   public RemotePCStatusView(CGRect frame) : base(frame) => SetupViews();

        private void SetupViews()
        {
    BackgroundColor = UIColor.SecondarySystemBackground;
      Layer.CornerRadius = 16;

    _statusIndicator = new UIView { BackgroundColor = UIColor.SystemRed };
            _statusIndicator.Layer.CornerRadius = 6;
   AddSubview(_statusIndicator);

            _statusLabel = new UILabel 
            { 
       Font = UIFont.BoldSystemFontOfSize(16), 
                TextColor = UIColor.Label, 
                Text = "Offline" 
};
       AddSubview(_statusLabel);

      _hostnameLabel = new UILabel 
     { 
          Font = UIFont.SystemFontOfSize(13), 
       TextColor = UIColor.SecondaryLabel 
  };
       AddSubview(_hostnameLabel);

       _cpuLabel = new UILabel 
            { 
     Font = UIFont.SystemFontOfSize(13), 
                TextColor = UIColor.SecondaryLabel 
            };
     AddSubview(_cpuLabel);

            _memoryLabel = new UILabel 
        { 
        Font = UIFont.SystemFontOfSize(13), 
   TextColor = UIColor.SecondaryLabel 
};
            AddSubview(_memoryLabel);

            _gpuLabel = new UILabel 
          { 
  Font = UIFont.SystemFontOfSize(13), 
  TextColor = UIColor.SecondaryLabel 
      };
          AddSubview(_gpuLabel);

        _currentGameLabel = new UILabel 
            { 
       Font = UIFont.BoldSystemFontOfSize(13), 
       TextColor = UIColor.SystemGreen, 
        Hidden = true 
      };
 AddSubview(_currentGameLabel);

 _wakeButton = new UIButton(UIButtonType.System);
_wakeButton.SetTitle("Wake PC", UIControlState.Normal);
    _wakeButton.SetImage(UIImage.GetSystemImage("power"), UIControlState.Normal);
            _wakeButton.BackgroundColor = UIColor.SystemGreen;
       _wakeButton.TintColor = UIColor.White;
            _wakeButton.SetTitleColor(UIColor.White, UIControlState.Normal);
            _wakeButton.Layer.CornerRadius = 8;
   _wakeButton.TouchUpInside += (s, e) => WakeRequested?.Invoke(this, EventArgs.Empty);
 AddSubview(_wakeButton);

   _sleepButton = new UIButton(UIButtonType.System);
    _sleepButton.SetTitle("Sleep", UIControlState.Normal);
     _sleepButton.SetImage(UIImage.GetSystemImage("moon.fill"), UIControlState.Normal);
            _sleepButton.BackgroundColor = UIColor.SystemOrange;
       _sleepButton.TintColor = UIColor.White;
            _sleepButton.SetTitleColor(UIColor.White, UIControlState.Normal);
 _sleepButton.Layer.CornerRadius = 8;
          _sleepButton.TouchUpInside += (s, e) => SleepRequested?.Invoke(this, EventArgs.Empty);
       _sleepButton.Hidden = true;
            AddSubview(_sleepButton);
        }

        public override void LayoutSubviews()
        {
       base.LayoutSubviews();
   var padding = 16f;
     var width = Bounds.Width - padding * 2;
  
  _statusIndicator.Frame = new CGRect(padding, padding, 12, 12);
            _statusLabel.Frame = new CGRect(padding + 20, padding - 2, width - 20, 20);
   _hostnameLabel.Frame = new CGRect(padding, padding + 24, width, 18);
            _cpuLabel.Frame = new CGRect(padding, padding + 46, width / 2 - 4, 18);
            _memoryLabel.Frame = new CGRect(padding + width / 2, padding + 46, width / 2, 18);
            _gpuLabel.Frame = new CGRect(padding, padding + 68, width, 18);
            _currentGameLabel.Frame = new CGRect(padding, padding + 90, width, 18);
  
        var buttonY = Bounds.Height - 52;
    var buttonWidth = (width - 10) / 2;
         _wakeButton.Frame = new CGRect(padding, buttonY, buttonWidth, 36);
            _sleepButton.Frame = new CGRect(padding + buttonWidth + 10, buttonY, buttonWidth, 36);
        }

    public void UpdateStatus(RemotePCStatus status)
   {
            if (status.IsOnline)
   {
         _statusIndicator.BackgroundColor = UIColor.SystemGreen;
 _statusLabel.Text = "Online";
     _hostnameLabel.Text = $"??? {status.Hostname}";
_cpuLabel.Text = $"CPU: {status.CpuUsage:0}%";
      _memoryLabel.Text = $"RAM: {status.MemoryUsage:0}%";
     
        if (status.GpuUsage.HasValue)
    {
       var gpuText = $"GPU: {status.GpuUsage:0}%";
    if (status.GpuTemperature.HasValue)
     gpuText += $" ({status.GpuTemperature:0}°C)";
           _gpuLabel.Text = gpuText;
 _gpuLabel.Hidden = false;
           }
           else 
      { 
         _gpuLabel.Hidden = true; 
       }
          
            if (!string.IsNullOrEmpty(status.CurrentGame))
    {
     _currentGameLabel.Text = $"?? Playing: {status.CurrentGame}";
            _currentGameLabel.Hidden = false;
     }
      else 
                { 
   _currentGameLabel.Hidden = true; 
             }
     
   _wakeButton.Hidden = true;
 _sleepButton.Hidden = false;
 }
            else
     {
            _statusIndicator.BackgroundColor = UIColor.SystemRed;
         _statusLabel.Text = "Offline";
 _hostnameLabel.Text = "";
              _cpuLabel.Text = "";
   _memoryLabel.Text = "";
                _gpuLabel.Hidden = true;
           _currentGameLabel.Hidden = true;
            _wakeButton.Hidden = false;
      _sleepButton.Hidden = true;
         }
        }
    }
}
