using gaming_hub.Services;

namespace gaming_hub
{
    [Register("SceneDelegate")]
    public class SceneDelegate : UIResponder, IUIWindowSceneDelegate
    {
      [Export("window")]
        public UIWindow? Window { get; set; }

        public static SceneDelegate? Current { get; private set; }

        [Export("scene:willConnectToSession:options:")]
        public async void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
        {
     Current = this;
         
    if (scene is UIWindowScene windowScene)
         {
                Window ??= new UIWindow(windowScene);
 
           // Load user preferences and apply theme
         await ApplyThemeAsync();
        
   // Set up the main tab bar controller
  Window.RootViewController = new MainTabBarController();
           Window.MakeKeyAndVisible();
 }
      }

        public async Task ApplyThemeAsync()
     {
       try
 {
   await DatabaseService.Instance.InitializeAsync();
                var userData = await DatabaseService.Instance.GetUserDataAsync();
ApplyTheme(userData.DarkModeEnabled);
          }
         catch
            {
        // Default to system appearance if we can't load preferences
        ApplyTheme(true);
          }
        }

        public void ApplyTheme(bool darkMode)
        {
            if (Window == null) return;
          
          Window.OverrideUserInterfaceStyle = darkMode 
            ? UIUserInterfaceStyle.Dark 
  : UIUserInterfaceStyle.Light;
        }

        [Export("sceneDidDisconnect:")]
public void DidDisconnect(UIScene scene)
        {
  }

        [Export("sceneDidBecomeActive:")]
    public void DidBecomeActive(UIScene scene)
    {
        }

        [Export("sceneWillResignActive:")]
 public void WillResignActive(UIScene scene)
   {
        }

        [Export("sceneWillEnterForeground:")]
        public void WillEnterForeground(UIScene scene)
      {
        }

        [Export("sceneDidEnterBackground:")]
        public void DidEnterBackground(UIScene scene)
        {
        }
    }
}
