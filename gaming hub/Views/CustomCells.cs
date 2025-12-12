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

  _gameImageView = new UIImageView { ContentMode = UIViewContentMode.ScaleAspectFill, ClipsToBounds = true, BackgroundColor = UIColor.SystemGray5 };
         _gameImageView.Layer.CornerRadius = 8;
        _cardView.AddSubview(_gameImageView);

      _titleLabel = new UILabel { Font = UIFont.BoldSystemFontOfSize(16), TextColor = UIColor.Label, Lines = 2, LineBreakMode = UILineBreakMode.TailTruncation };
       _cardView.AddSubview(_titleLabel);

     _storeLabel = new UILabel { Font = UIFont.SystemFontOfSize(12), TextColor = UIColor.SecondaryLabel };
   _cardView.AddSubview(_storeLabel);

  _originalPriceLabel = new UILabel { Font = UIFont.SystemFontOfSize(12), TextColor = UIColor.TertiaryLabel };
  _cardView.AddSubview(_originalPriceLabel);

    _salePriceLabel = new UILabel { Font = UIFont.BoldSystemFontOfSize(18), TextColor = UIColor.SystemGreen };
   _cardView.AddSubview(_salePriceLabel);

     _discountBadge = new UIView { BackgroundColor = UIColor.SystemRed };
     _discountBadge.Layer.CornerRadius = 4;
      _cardView.AddSubview(_discountBadge);

    _discountLabel = new UILabel { Font = UIFont.BoldSystemFontOfSize(12), TextColor = UIColor.White, TextAlignment = UITextAlignment.Center };
  _discountBadge.AddSubview(_discountLabel);

         _ratingLabel = new UILabel { Font = UIFont.SystemFontOfSize(11), TextColor = UIColor.SecondaryLabel };
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
         var textX = imageSize + 20;
         var textWidth = cardBounds.Width - textX - 80;
   _titleLabel.Frame = new CGRect(textX, 12, textWidth, 40);
     _storeLabel.Frame = new CGRect(textX, 54, textWidth, 16);
            _ratingLabel.Frame = new CGRect(textX, 72, textWidth, 14);
    var priceX = cardBounds.Width - 75;
       _originalPriceLabel.Frame = new CGRect(priceX, 20, 60, 16);
  _salePriceLabel.Frame = new CGRect(priceX, 38, 60, 22);
     _discountBadge.Frame = new CGRect(priceX, 64, 50, 20);
      _discountLabel.Frame = new CGRect(0, 0, 50, 20);
  }

  public void Configure(GameDeal deal)
 {
  _titleLabel.Text = deal.GameName;
  _storeLabel.Text = deal.StoreName;
  _salePriceLabel.Text = deal.PriceText;
      _discountLabel.Text = deal.SavingsText;
        var originalPriceAttr = new NSAttributedString(deal.OriginalPriceText, new UIStringAttributes { StrikethroughStyle = NSUnderlineStyle.Single, ForegroundColor = UIColor.TertiaryLabel });
            _originalPriceLabel.AttributedText = originalPriceAttr;
         if (deal.MetacriticScore.HasValue || deal.SteamRating.HasValue)
   {
           var ratingText = "";
     if (deal.MetacriticScore.HasValue) ratingText += $"MC: {deal.MetacriticScore:0}";
      if (deal.SteamRating.HasValue) ratingText += (ratingText.Length > 0 ? " | " : "") + $"Steam: {deal.SteamRating:0}%";
       _ratingLabel.Text = ratingText;
  }
  else { _ratingLabel.Text = ""; }
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
  InvokeOnMainThread(() => { if (_currentImageUrl == imageUrl) { _gameImageView.Image = image; _gameImageView.TintColor = null; } });
            }
      catch { }
        }

        public override void PrepareForReuse()
     {
        base.PrepareForReuse();
   _currentImageUrl = null;
         _gameImageView.Image = UIImage.GetSystemImage("gamecontroller.fill");
     _titleLabel.Text = null;
         _storeLabel.Text = null;
 }
    }

    public class ReleaseCell : UITableViewCell
    {
        public static readonly string ReuseId = "ReleaseCell";
        private UIImageView _coverImageView = null!;
    private UILabel _titleLabel = null!;
        private UILabel _dateLabel = null!;
        private UILabel _countdownLabel = null!;
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

          _coverImageView = new UIImageView { ContentMode = UIViewContentMode.ScaleAspectFill, ClipsToBounds = true, BackgroundColor = UIColor.SystemGray5 };
         _coverImageView.Layer.CornerRadius = 8;
   _cardView.AddSubview(_coverImageView);

 _titleLabel = new UILabel { Font = UIFont.BoldSystemFontOfSize(16), TextColor = UIColor.Label, Lines = 2 };
 _cardView.AddSubview(_titleLabel);

    _dateLabel = new UILabel { Font = UIFont.SystemFontOfSize(13), TextColor = UIColor.SecondaryLabel };
          _cardView.AddSubview(_dateLabel);

     _countdownLabel = new UILabel { Font = UIFont.BoldSystemFontOfSize(14), TextColor = UIColor.SystemOrange };
 _cardView.AddSubview(_countdownLabel);

   _platformsLabel = new UILabel { Font = UIFont.SystemFontOfSize(11), TextColor = UIColor.TertiaryLabel };
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
var imageWidth = 100f;
      _coverImageView.Frame = new CGRect(8, 8, imageWidth, cardBounds.Height - 16);
            var textX = imageWidth + 20;
       var textWidth = cardBounds.Width - textX - 50;
            _titleLabel.Frame = new CGRect(textX, 12, textWidth, 40);
    _dateLabel.Frame = new CGRect(textX, 54, textWidth, 18);
    _countdownLabel.Frame = new CGRect(textX, 74, textWidth, 18);
 _platformsLabel.Frame = new CGRect(textX, cardBounds.Height - 28, textWidth, 16);
     _wishlistButton.Frame = new CGRect(cardBounds.Width - 44, 12, 36, 36);
        }

    public void Configure(UpcomingRelease release)
  {
     _titleLabel.Text = release.GameName;
       _dateLabel.Text = $"?? {release.ReleaseDateText}";
        _countdownLabel.Text = release.DaysUntilRelease >= 0 ? $"? {release.CountdownText}" : "?? Out Now!";
 _platformsLabel.Text = release.Platforms;
      _wishlistButton.SetImage(UIImage.GetSystemImage(release.IsWishlisted ? "heart.fill" : "heart"), UIControlState.Normal);
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
        InvokeOnMainThread(() => { if (_currentImageUrl == imageUrl) { _coverImageView.Image = image; _coverImageView.TintColor = null; } });
     }
    catch { }
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

  _statusLabel = new UILabel { Font = UIFont.BoldSystemFontOfSize(16), TextColor = UIColor.Label, Text = "Offline" };
     AddSubview(_statusLabel);

  _hostnameLabel = new UILabel { Font = UIFont.SystemFontOfSize(13), TextColor = UIColor.SecondaryLabel };
         AddSubview(_hostnameLabel);

     _cpuLabel = new UILabel { Font = UIFont.SystemFontOfSize(13), TextColor = UIColor.SecondaryLabel };
       AddSubview(_cpuLabel);

   _memoryLabel = new UILabel { Font = UIFont.SystemFontOfSize(13), TextColor = UIColor.SecondaryLabel };
    AddSubview(_memoryLabel);

            _gpuLabel = new UILabel { Font = UIFont.SystemFontOfSize(13), TextColor = UIColor.SecondaryLabel };
         AddSubview(_gpuLabel);

     _currentGameLabel = new UILabel { Font = UIFont.BoldSystemFontOfSize(14), TextColor = UIColor.SystemGreen, Hidden = true };
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
_cpuLabel.Frame = new CGRect(padding, padding + 46, width / 2, 18);
_memoryLabel.Frame = new CGRect(padding + width / 2, padding + 46, width / 2, 18);
     _gpuLabel.Frame = new CGRect(padding, padding + 68, width, 18);
 _currentGameLabel.Frame = new CGRect(padding, padding + 92, width, 20);
  var buttonY = Bounds.Height - 52;
           _wakeButton.Frame = new CGRect(padding, buttonY, (width - 10) / 2, 36);
         _sleepButton.Frame = new CGRect(padding + (width + 10) / 2, buttonY, (width - 10) / 2, 36);
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
    _gpuLabel.Text = $"GPU: {status.GpuUsage:0}%" + (status.GpuTemperature.HasValue ? $" ({status.GpuTemperature:0}°C)" : "");
     _gpuLabel.Hidden = false;
        }
     else { _gpuLabel.Hidden = true; }
      if (!string.IsNullOrEmpty(status.CurrentGame))
    {
     _currentGameLabel.Text = $"?? Playing: {status.CurrentGame}";
   _currentGameLabel.Hidden = false;
      }
    else { _currentGameLabel.Hidden = true; }
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
