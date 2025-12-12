namespace gaming_hub
{
    [Register("SceneDelegate")]
    public class SceneDelegate : UIResponder, IUIWindowSceneDelegate
    {
        [Export("window")]
        public UIWindow? Window { get; set; }

        [Export("scene:willConnectToSession:options:")]
        public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
        {
          if (scene is UIWindowScene windowScene)
            {
   Window ??= new UIWindow(windowScene);
      
         // Set up the main tab bar controller
       Window.RootViewController = new MainTabBarController();
            Window.MakeKeyAndVisible();
            }
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
