using gaming_hub.ViewControllers;

namespace gaming_hub
{
    public class MainTabBarController : UITabBarController
    {
  public override void ViewDidLoad()
  {
          base.ViewDidLoad();
       SetupTabs();
            SetupAppearance();
        }

     private void SetupTabs()
        {
   // Library Tab
 var libraryVC = new LibraryViewController();
    var libraryNav = new UINavigationController(libraryVC);
       libraryNav.TabBarItem = new UITabBarItem("Library", UIImage.GetSystemImage("books.vertical.fill"), 0);

// Deals Tab
 var dealsVC = new DealsViewController();
     var dealsNav = new UINavigationController(dealsVC);
    dealsNav.TabBarItem = new UITabBarItem("Deals", UIImage.GetSystemImage("tag.fill"), 1);

   // Upcoming Releases Tab
        var releasesVC = new ReleasesViewController();
 var releasesNav = new UINavigationController(releasesVC);
       releasesNav.TabBarItem = new UITabBarItem("Upcoming", UIImage.GetSystemImage("calendar"), 2);

     // Remote PC Tab
 var remotePCVC = new RemotePCViewController();
  var remotePCNav = new UINavigationController(remotePCVC);
       remotePCNav.TabBarItem = new UITabBarItem("Remote PC", UIImage.GetSystemImage("desktopcomputer"), 3);

    // Settings Tab
 var settingsVC = new SettingsViewController();
      var settingsNav = new UINavigationController(settingsVC);
     settingsNav.TabBarItem = new UITabBarItem("Settings", UIImage.GetSystemImage("gear"), 4);

     ViewControllers = [libraryNav, dealsNav, releasesNav, remotePCNav, settingsNav];
  }

        private void SetupAppearance()
        {
  // Tab bar appearance
var tabBarAppearance = new UITabBarAppearance();
     tabBarAppearance.ConfigureWithDefaultBackground();
      TabBar.StandardAppearance = tabBarAppearance;
   TabBar.ScrollEdgeAppearance = tabBarAppearance;
 TabBar.TintColor = UIColor.SystemBlue;

 // Navigation bar appearance
      var navBarAppearance = new UINavigationBarAppearance();
    navBarAppearance.ConfigureWithDefaultBackground();

  foreach (var nav in ViewControllers!.OfType<UINavigationController>())
            {
     nav.NavigationBar.StandardAppearance = navBarAppearance;
              nav.NavigationBar.ScrollEdgeAppearance = navBarAppearance;
               nav.NavigationBar.TintColor = UIColor.SystemBlue;
  }
      }
    }
}
