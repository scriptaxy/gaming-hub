using Foundation;
using gaming_hub.ViewControllers;

namespace gaming_hub.Services
{
    /// <summary>
    /// Actions that can be mapped to buttons
 /// </summary>
    public enum MappableAction
    {
        // Standard gamepad
   ButtonA,
      ButtonB,
        ButtonX,
      ButtonY,
        LeftBumper,
        RightBumper,
        LeftTrigger,
        RightTrigger,
     LeftStickClick,
        RightStickClick,
        DPadUp,
  DPadDown,
        DPadLeft,
     DPadRight,
    Start,
        Back,
        
   // Quick actions
        VolumeUp,
   VolumeDown,
        VolumeMute,
     MediaPlayPause,
 MediaNext,
        MediaPrevious,
        Screenshot,
        ToggleRecording,
        
        // Keyboard shortcuts
     AltTab,
  AltF4,
        Escape,
    Enter,
        Space,
        Tab,
        
        // Special
   None,
        ToggleGyro,
        CalibrateGyro,
        ShowQuickMenu
    }

    /// <summary>
    /// A single button mapping configuration
    /// </summary>
    public class ButtonMapping
    {
  public GamepadButtons SourceButton { get; set; }
        public MappableAction Action { get; set; }
        public string? CustomKeyCombo { get; set; } // For custom keyboard shortcuts
        
        public ButtonMapping() { }
        
        public ButtonMapping(GamepadButtons source, MappableAction action)
        {
         SourceButton = source;
    Action = action;
        }
    }

  /// <summary>
    /// A complete button mapping profile
    /// </summary>
    public class MappingProfile
    {
      public string Name { get; set; } = "Default";
        public string? GameId { get; set; } // Optional: specific game this profile is for
 public List<ButtonMapping> Mappings { get; set; } = new();
        public bool SwapSticksEnabled { get; set; }
        public bool SwapTriggersEnabled { get; set; }
    public float LeftStickDeadzone { get; set; } = 0.1f;
        public float RightStickDeadzone { get; set; } = 0.1f;
        public float TriggerDeadzone { get; set; } = 0.05f;
        
 public static MappingProfile CreateDefault()
      {
   return new MappingProfile
        {
       Name = "Default",
    Mappings = new List<ButtonMapping>
       {
     new(GamepadButtons.A, MappableAction.ButtonA),
    new(GamepadButtons.B, MappableAction.ButtonB),
      new(GamepadButtons.X, MappableAction.ButtonX),
        new(GamepadButtons.Y, MappableAction.ButtonY),
       new(GamepadButtons.LeftBumper, MappableAction.LeftBumper),
            new(GamepadButtons.RightBumper, MappableAction.RightBumper),
        new(GamepadButtons.LeftStick, MappableAction.LeftStickClick),
      new(GamepadButtons.RightStick, MappableAction.RightStickClick),
              new(GamepadButtons.DPadUp, MappableAction.DPadUp),
     new(GamepadButtons.DPadDown, MappableAction.DPadDown),
          new(GamepadButtons.DPadLeft, MappableAction.DPadLeft),
   new(GamepadButtons.DPadRight, MappableAction.DPadRight),
          new(GamepadButtons.Start, MappableAction.Start),
    new(GamepadButtons.Back, MappableAction.Back),
     }
            };
        }
    }

    /// <summary>
/// Service for managing custom button mappings
  /// </summary>
    public class ButtonMappingService
    {
      private static ButtonMappingService? _instance;
        public static ButtonMappingService Instance => _instance ??= new ButtonMappingService();

 private const string ProfilesKey = "button_mapping_profiles";
    private const string ActiveProfileKey = "active_mapping_profile";
        
        private List<MappingProfile> _profiles = new();
        private MappingProfile _activeProfile;
private Dictionary<GamepadButtons, MappableAction> _quickLookup = new();
        
     public MappingProfile ActiveProfile => _activeProfile;
        public IReadOnlyList<MappingProfile> Profiles => _profiles;
        
        public event Action<GamepadState>? OnRemappedInput;
        public event Action<MappableAction>? OnSpecialAction;

        private ButtonMappingService()
        {
          LoadProfiles();
            _activeProfile = _profiles.FirstOrDefault() ?? MappingProfile.CreateDefault();
            BuildQuickLookup();
 }

        /// <summary>
        /// Process input through the mapping system
        /// </summary>
  public GamepadState ProcessInput(GamepadState input)
        {
  var output = new GamepadState();
  
         // Apply stick swapping
            if (_activeProfile.SwapSticksEnabled)
      {
         output.LeftStickX = input.RightStickX;
 output.LeftStickY = input.RightStickY;
     output.RightStickX = input.LeftStickX;
    output.RightStickY = input.LeftStickY;
            }
          else
    {
           output.LeftStickX = input.LeftStickX;
   output.LeftStickY = input.LeftStickY;
        output.RightStickX = input.RightStickX;
       output.RightStickY = input.RightStickY;
            }
      
 // Apply deadzones
        output.LeftStickX = ApplyDeadzone(output.LeftStickX, _activeProfile.LeftStickDeadzone);
    output.LeftStickY = ApplyDeadzone(output.LeftStickY, _activeProfile.LeftStickDeadzone);
 output.RightStickX = ApplyDeadzone(output.RightStickX, _activeProfile.RightStickDeadzone);
         output.RightStickY = ApplyDeadzone(output.RightStickY, _activeProfile.RightStickDeadzone);
            
            // Apply trigger swapping
            if (_activeProfile.SwapTriggersEnabled)
            {
      output.LeftTrigger = ApplyDeadzone(input.RightTrigger, _activeProfile.TriggerDeadzone);
      output.RightTrigger = ApplyDeadzone(input.LeftTrigger, _activeProfile.TriggerDeadzone);
    }
     else
      {
       output.LeftTrigger = ApplyDeadzone(input.LeftTrigger, _activeProfile.TriggerDeadzone);
         output.RightTrigger = ApplyDeadzone(input.RightTrigger, _activeProfile.TriggerDeadzone);
            }
 
     // Process button mappings
    foreach (GamepadButtons button in Enum.GetValues<GamepadButtons>())
            {
      if (button == GamepadButtons.None) continue;
    if ((input.Buttons & button) == 0) continue;
       
  var action = GetMappedAction(button);
 
     // Handle special actions
     if (IsSpecialAction(action))
            {
        OnSpecialAction?.Invoke(action);
 continue;
      }
       
        // Map to output button
         var outputButton = ActionToButton(action);
 if (outputButton != GamepadButtons.None)
     {
         output.Buttons |= outputButton;
    }
  }
         
    OnRemappedInput?.Invoke(output);
            return output;
        }

        public MappableAction GetMappedAction(GamepadButtons button)
        {
 return _quickLookup.TryGetValue(button, out var action) ? action : MappableAction.None;
  }

     public void SetMapping(GamepadButtons source, MappableAction action)
    {
      var existing = _activeProfile.Mappings.FirstOrDefault(m => m.SourceButton == source);
            if (existing != null)
            {
            existing.Action = action;
            }
    else
     {
     _activeProfile.Mappings.Add(new ButtonMapping(source, action));
   }
BuildQuickLookup();
      SaveProfiles();
        }

        public void SetActiveProfile(string name)
        {
            var profile = _profiles.FirstOrDefault(p => p.Name == name);
            if (profile != null)
            {
     _activeProfile = profile;
          BuildQuickLookup();
                NSUserDefaults.StandardUserDefaults.SetString(name, ActiveProfileKey);
  }
     }

        public void CreateProfile(string name, string? basedOn = null)
        {
            var newProfile = basedOn != null
           ? CloneProfile(_profiles.FirstOrDefault(p => p.Name == basedOn) ?? MappingProfile.CreateDefault())
    : MappingProfile.CreateDefault();
     
 newProfile.Name = name;
  _profiles.Add(newProfile);
   SaveProfiles();
        }

        public void DeleteProfile(string name)
        {
 if (name == "Default") return; // Can't delete default
      _profiles.RemoveAll(p => p.Name == name);
    
            if (_activeProfile.Name == name)
   {
     _activeProfile = _profiles.FirstOrDefault() ?? MappingProfile.CreateDefault();
            }
            SaveProfiles();
        }

        public void ResetToDefault()
        {
            _activeProfile = MappingProfile.CreateDefault();
            BuildQuickLookup();
    }

      private void BuildQuickLookup()
  {
            _quickLookup.Clear();
            foreach (var mapping in _activeProfile.Mappings)
    {
     _quickLookup[mapping.SourceButton] = mapping.Action;
 }
        }

  private float ApplyDeadzone(float value, float deadzone)
        {
    if (Math.Abs(value) < deadzone) return 0;
       var sign = Math.Sign(value);
          return sign * (Math.Abs(value) - deadzone) / (1 - deadzone);
        }

        private bool IsSpecialAction(MappableAction action)
        {
            return action switch
     {
      MappableAction.VolumeUp or
    MappableAction.VolumeDown or
       MappableAction.VolumeMute or
           MappableAction.MediaPlayPause or
   MappableAction.MediaNext or
 MappableAction.MediaPrevious or
       MappableAction.Screenshot or
 MappableAction.ToggleRecording or
         MappableAction.AltTab or
        MappableAction.AltF4 or
         MappableAction.Escape or
     MappableAction.ToggleGyro or
    MappableAction.CalibrateGyro or
    MappableAction.ShowQuickMenu => true,
       _ => false
       };
        }

        private GamepadButtons ActionToButton(MappableAction action)
        {
            return action switch
            {
     MappableAction.ButtonA => GamepadButtons.A,
     MappableAction.ButtonB => GamepadButtons.B,
     MappableAction.ButtonX => GamepadButtons.X,
     MappableAction.ButtonY => GamepadButtons.Y,
  MappableAction.LeftBumper => GamepadButtons.LeftBumper,
          MappableAction.RightBumper => GamepadButtons.RightBumper,
       MappableAction.LeftStickClick => GamepadButtons.LeftStick,
     MappableAction.RightStickClick => GamepadButtons.RightStick,
          MappableAction.DPadUp => GamepadButtons.DPadUp,
       MappableAction.DPadDown => GamepadButtons.DPadDown,
    MappableAction.DPadLeft => GamepadButtons.DPadLeft,
    MappableAction.DPadRight => GamepadButtons.DPadRight,
        MappableAction.Start => GamepadButtons.Start,
      MappableAction.Back => GamepadButtons.Back,
                _ => GamepadButtons.None
 };
      }

  private MappingProfile CloneProfile(MappingProfile source)
   {
            return new MappingProfile
            {
     Name = source.Name + " (Copy)",
         Mappings = source.Mappings.Select(m => new ButtonMapping(m.SourceButton, m.Action)).ToList(),
              SwapSticksEnabled = source.SwapSticksEnabled,
           SwapTriggersEnabled = source.SwapTriggersEnabled,
         LeftStickDeadzone = source.LeftStickDeadzone,
    RightStickDeadzone = source.RightStickDeadzone,
  TriggerDeadzone = source.TriggerDeadzone
            };
   }

  private void LoadProfiles()
        {
var defaults = NSUserDefaults.StandardUserDefaults;
      var data = defaults.StringForKey(ProfilesKey);
   
     if (!string.IsNullOrEmpty(data))
        {
    try
          {
      _profiles = System.Text.Json.JsonSerializer.Deserialize<List<MappingProfile>>(data) ?? new();
    }
     catch
      {
       _profiles = new List<MappingProfile> { MappingProfile.CreateDefault() };
     }
     }
            else
     {
      _profiles = new List<MappingProfile> { MappingProfile.CreateDefault() };
        }
        
  var activeProfileName = defaults.StringForKey(ActiveProfileKey);
            _activeProfile = _profiles.FirstOrDefault(p => p.Name == activeProfileName) 
        ?? _profiles.FirstOrDefault() 
              ?? MappingProfile.CreateDefault();
     }

        private void SaveProfiles()
        {
try
       {
     var data = System.Text.Json.JsonSerializer.Serialize(_profiles);
     NSUserDefaults.StandardUserDefaults.SetString(data, ProfilesKey);
       NSUserDefaults.StandardUserDefaults.SetString(_activeProfile.Name, ActiveProfileKey);
         }
         catch (Exception ex)
       {
    Console.WriteLine($"Failed to save profiles: {ex.Message}");
            }
        }
    }
}
