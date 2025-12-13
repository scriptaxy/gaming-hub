using UIKit;
using CoreGraphics;
using Foundation;

namespace gaming_hub.ViewControllers
{
    /// <summary>
    /// On-screen keyboard for text input during game streaming
    /// </summary>
    public class StreamKeyboardViewController : UIViewController
    {
        public event Action<int, bool>? OnKeyPress; // keyCode, isDown
        public event Action? OnDismiss;

        private UIVisualEffectView? _blurView;
        private UIView? _keyboardContainer;
        private UITextField? _hiddenTextField;
      private UISegmentedControl? _layoutSelector;
        private bool _shiftActive;
 private bool _capsLock;

      // Common key codes (Windows Virtual Key codes)
        private static readonly Dictionary<string, int> KeyCodes = new()
   {
// Letters
    ["A"] = 0x41, ["B"] = 0x42, ["C"] = 0x43, ["D"] = 0x44, ["E"] = 0x45,
 ["F"] = 0x46, ["G"] = 0x47, ["H"] = 0x48, ["I"] = 0x49, ["J"] = 0x4A,
   ["K"] = 0x4B, ["L"] = 0x4C, ["M"] = 0x4D, ["N"] = 0x4E, ["O"] = 0x4F,
   ["P"] = 0x50, ["Q"] = 0x51, ["R"] = 0x52, ["S"] = 0x53, ["T"] = 0x54,
       ["U"] = 0x55, ["V"] = 0x56, ["W"] = 0x57, ["X"] = 0x58, ["Y"] = 0x59,
            ["Z"] = 0x5A,
// Numbers
          ["0"] = 0x30, ["1"] = 0x31, ["2"] = 0x32, ["3"] = 0x33, ["4"] = 0x34,
 ["5"] = 0x35, ["6"] = 0x36, ["7"] = 0x37, ["8"] = 0x38, ["9"] = 0x39,
            // Function keys
 ["F1"] = 0x70, ["F2"] = 0x71, ["F3"] = 0x72, ["F4"] = 0x73,
  ["F5"] = 0x74, ["F6"] = 0x75, ["F7"] = 0x76, ["F8"] = 0x77,
            ["F9"] = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A, ["F12"] = 0x7B,
       // Special keys
            ["Space"] = 0x20, ["Enter"] = 0x0D, ["Tab"] = 0x09, ["Backspace"] = 0x08,
  ["Escape"] = 0x1B, ["Delete"] = 0x2E, ["Insert"] = 0x2D,
     ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22,
  // Arrow keys
      ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28,
     // Modifiers
  ["Shift"] = 0x10, ["Ctrl"] = 0x11, ["Alt"] = 0x12, ["Win"] = 0x5B,
     ["CapsLock"] = 0x14,
            // Symbols
            ["-"] = 0xBD, ["="] = 0xBB, ["["] = 0xDB, ["]"] = 0xDD,
            ["\\"] = 0xDC, [";"] = 0xBA, ["'"] = 0xDE, [","] = 0xBC,
            ["."] = 0xBE, ["/"] = 0xBF, ["`"] = 0xC0
        };

        public override void ViewDidLoad()
  {
            base.ViewDidLoad();
            SetupUI();
  }

        private void SetupUI()
        {
     View!.BackgroundColor = UIColor.Clear;

 // Blur background
var blurEffect = UIBlurEffect.FromStyle(UIBlurEffectStyle.Dark);
_blurView = new UIVisualEffectView(blurEffect)
            {
       TranslatesAutoresizingMaskIntoConstraints = false
          };
    View.AddSubview(_blurView);

 // Keyboard container
    _keyboardContainer = new UIView
      {
 TranslatesAutoresizingMaskIntoConstraints = false,
      BackgroundColor = UIColor.FromWhiteAlpha(0.1f, 0.9f)
            };
     _keyboardContainer.Layer.CornerRadius = 16;
            View.AddSubview(_keyboardContainer);

        // Layout selector
            _layoutSelector = new UISegmentedControl("QWERTY", "Function", "Gaming")
    {
            TranslatesAutoresizingMaskIntoConstraints = false,
          SelectedSegment = 0
 };
            _layoutSelector.ValueChanged += (s, e) => UpdateKeyboardLayout();
    _keyboardContainer.AddSubview(_layoutSelector);

  // Close button
        var closeButton = new UIButton(UIButtonType.System)
 {
             TranslatesAutoresizingMaskIntoConstraints = false
         };
 closeButton.SetTitle("?", UIControlState.Normal);
         closeButton.TitleLabel!.Font = UIFont.SystemFontOfSize(20);
            closeButton.TouchUpInside += (s, e) =>
            {
     OnDismiss?.Invoke();
      DismissViewController(true, null);
            };
          _keyboardContainer.AddSubview(closeButton);

            // Hidden text field for native keyboard
        _hiddenTextField = new UITextField
            {
   TranslatesAutoresizingMaskIntoConstraints = false,
       Alpha = 0,
   AutocorrectionType = UITextAutocorrectionType.No,
              AutocapitalizationType = UITextAutocapitalizationType.None,
                SpellCheckingType = UITextSpellCheckingType.No
         };
     _hiddenTextField.EditingChanged += OnTextChanged;
   View.AddSubview(_hiddenTextField);

    SetupConstraints(closeButton);
 UpdateKeyboardLayout();

            // Tap to dismiss
  var tapGesture = new UITapGestureRecognizer(() =>
  {
       if (_hiddenTextField?.IsFirstResponder == true)
              _hiddenTextField.ResignFirstResponder();
            });
            tapGesture.CancelsTouchesInView = false;
      _blurView.AddGestureRecognizer(tapGesture);
        }

        private void SetupConstraints(UIButton closeButton)
        {
       NSLayoutConstraint.ActivateConstraints([
      _blurView!.TopAnchor.ConstraintEqualTo(View!.TopAnchor),
         _blurView.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),
      _blurView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
              _blurView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),

                _keyboardContainer!.CenterXAnchor.ConstraintEqualTo(View.CenterXAnchor),
         _keyboardContainer.CenterYAnchor.ConstraintEqualTo(View.CenterYAnchor),
 _keyboardContainer.WidthAnchor.ConstraintEqualTo(View.WidthAnchor, 0.9f),
      _keyboardContainer.HeightAnchor.ConstraintEqualTo(280),

        _layoutSelector!.TopAnchor.ConstraintEqualTo(_keyboardContainer.TopAnchor, 12),
      _layoutSelector.LeadingAnchor.ConstraintEqualTo(_keyboardContainer.LeadingAnchor, 16),

     closeButton.TopAnchor.ConstraintEqualTo(_keyboardContainer.TopAnchor, 8),
    closeButton.TrailingAnchor.ConstraintEqualTo(_keyboardContainer.TrailingAnchor, -8),
      closeButton.WidthAnchor.ConstraintEqualTo(40),
      closeButton.HeightAnchor.ConstraintEqualTo(40)
         ]);
        }

        private void UpdateKeyboardLayout()
        {
            // Remove existing key buttons
            foreach (var subview in _keyboardContainer!.Subviews)
{
 if (subview is UIButton btn && btn.Tag >= 100)
     subview.RemoveFromSuperview();
            }

            var layout = (int)_layoutSelector!.SelectedSegment;
            switch (layout)
       {
    case 0: CreateQwertyLayout(); break;
           case 1: CreateFunctionLayout(); break;
     case 2: CreateGamingLayout(); break;
  }
        }

      private void CreateQwertyLayout()
      {
   var rows = new[]
  {
      new[] { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" },
        new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\" },
           new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Enter" },
       new[] { "Shift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "Backspace" },
   new[] { "Ctrl", "Win", "Alt", "Space", "Alt", "Ctrl", "Left", "Up", "Down", "Right" }
          };

         CreateKeyRows(rows, 50);
        }

        private void CreateFunctionLayout()
   {
      var rows = new[]
            {
 new[] { "Escape", "F1", "F2", "F3", "F4", "F5", "F6" },
    new[] { "Tab", "F7", "F8", "F9", "F10", "F11", "F12" },
         new[] { "Insert", "Delete", "Home", "End", "PageUp", "PageDown" },
           new[] { "Ctrl", "Alt", "Shift", "Win", "Enter", "Backspace" }
          };

          CreateKeyRows(rows, 60);
        }

        private void CreateGamingLayout()
        {
            var rows = new[]
            {
     new[] { "Escape", "1", "2", "3", "4", "5", "Tab" },
new[] { "Q", "W", "E", "R", "T", "F", "G" },
                new[] { "A", "S", "D", "F", "Shift", "Ctrl", "Space" },
    new[] { "Z", "X", "C", "V", "Alt", "Enter", "Backspace" }
  };

            CreateKeyRows(rows, 55);
        }

        private void CreateKeyRows(string[][] rows, nfloat keyHeight)
        {
        var startY = 50;
       var padding = 4;
            var tag = 100;

      for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
   {
     var row = rows[rowIndex];
      var totalWidth = _keyboardContainer!.Bounds.Width - 32;
            var keyWidth = (totalWidth - (row.Length - 1) * padding) / row.Length;
           var y = startY + rowIndex * (keyHeight + padding);

  for (int colIndex = 0; colIndex < row.Length; colIndex++)
       {
        var key = row[colIndex];
    var x = 16 + colIndex * (keyWidth + padding);

          // Wider keys for special keys
       var width = keyWidth;
         if (key == "Space") width = keyWidth * 3;
       else if (key == "Shift" || key == "Enter" || key == "Backspace") width = keyWidth * 1.5f;

         var button = CreateKeyButton(key, new CGRect(x, y, width, keyHeight));
     button.Tag = tag++;
          _keyboardContainer.AddSubview(button);
         }
            }
      }

        private UIButton CreateKeyButton(string key, CGRect frame)
        {
        var button = new UIButton(UIButtonType.Custom)
            {
       Frame = frame,
                BackgroundColor = IsModifierKey(key) ? UIColor.FromWhiteAlpha(0.3f, 1) : UIColor.FromWhiteAlpha(0.2f, 1)
    };
            button.Layer.CornerRadius = 6;
            button.Layer.BorderColor = UIColor.FromWhiteAlpha(0.4f, 1).CGColor;
 button.Layer.BorderWidth = 1;

          var displayText = key.Length > 6 ? GetShortKeyName(key) : key;
       button.SetTitle(displayText, UIControlState.Normal);
 button.TitleLabel!.Font = UIFont.SystemFontOfSize(key.Length > 2 ? 11 : 14, UIFontWeight.Medium);
        button.SetTitleColor(UIColor.White, UIControlState.Normal);

     button.TouchDown += (s, e) =>
          {
button.BackgroundColor = UIColor.SystemBlue;
     SendKey(key, true);
            };

          button.TouchUpInside += (s, e) =>
 {
         button.BackgroundColor = IsModifierKey(key) ? UIColor.FromWhiteAlpha(0.3f, 1) : UIColor.FromWhiteAlpha(0.2f, 1);
            SendKey(key, false);
            };

            button.TouchUpOutside += (s, e) =>
{
        button.BackgroundColor = IsModifierKey(key) ? UIColor.FromWhiteAlpha(0.3f, 1) : UIColor.FromWhiteAlpha(0.2f, 1);
     SendKey(key, false);
          };

          return button;
        }

        private void SendKey(string key, bool isDown)
  {
            if (KeyCodes.TryGetValue(key, out var keyCode))
            {
      OnKeyPress?.Invoke(keyCode, isDown);
            }
        }

   private bool IsModifierKey(string key)
        {
      return key is "Shift" or "Ctrl" or "Alt" or "Win" or "CapsLock";
    }

private string GetShortKeyName(string key) => key switch
     {
            "Backspace" => "?",
     "Enter" => "?",
            "Space" => "Space",
     "Escape" => "Esc",
  "Delete" => "Del",
        "Insert" => "Ins",
      "PageUp" => "PgUp",
        "PageDown" => "PgDn",
   "CapsLock" => "Caps",
    _ => key
    };

        private void OnTextChanged(object? sender, EventArgs e)
   {
       // Handle text input from native keyboard
      var text = _hiddenTextField?.Text ?? "";
     if (text.Length > 0)
      {
   var lastChar = text[^1].ToString().ToUpper();
       if (KeyCodes.TryGetValue(lastChar, out var keyCode))
     {
     OnKeyPress?.Invoke(keyCode, true);
            OnKeyPress?.Invoke(keyCode, false);
                }
                _hiddenTextField!.Text = "";
   }
    }

        /// <summary>
        /// Show native iOS keyboard for text input
/// </summary>
        public void ShowNativeKeyboard()
        {
       _hiddenTextField?.BecomeFirstResponder();
     }
    }
}
