# ?? Synktra

**By Scriptaxy**

A comprehensive iOS gaming companion app built with .NET 9 and UIKit.

## Features

- ?? **Game Library** - Track games from Steam, Epic, GOG, and manually added titles
- ?? **Deals** - Find the best game deals across multiple stores (via CheapShark)
- ?? **Upcoming Releases** - Track game release dates with countdown timers
- ??? **Remote PC** - Wake-on-LAN, launch games remotely, view PC status
- ?? **Game Streaming** - Stream games from your PC with low-latency video
- ?? **Virtual Controller** - Games recognize your iPhone as an Xbox 360 controller
- ?? **Steam Integration** - Sync your Steam library automatically
- ?? **Epic Games Integration** - Connect and sync your Epic Games library
- ?? **Dark/Light Mode** - Full theme support

## SynktraCompanion (Windows PC App)

The companion app runs on your Windows gaming PC and enables:
- Remote game launching
- System monitoring (CPU, GPU, Memory)
- Game streaming to your iPhone
- **Virtual Xbox 360 Controller** - Your iPhone becomes a real game controller

### Virtual Controller Setup

The virtual controller feature uses **ViGEmBus** to create a virtual Xbox 360 controller that Windows and games recognize as a real gamepad.

#### Automatic Installation (Recommended)

SynktraCompanion can **automatically download and install** the ViGEmBus driver:

1. Launch SynktraCompanion
2. If ViGEmBus is not detected, click "Install Driver" or call the API endpoint
3. Accept the administrator permission prompt
4. The driver will install automatically - no restart usually required!

**API Endpoint:** `POST /api/controller/install`

#### Manual Installation

If automatic installation doesn't work:

1. Download the latest ViGEmBus driver from: https://github.com/ViGEm/ViGEmBus/releases
2. Run the installer as Administrator
3. Restart your PC if prompted
4. Launch SynktraCompanion - it will automatically detect and use the virtual controller

#### How It Works

- When you use the virtual gamepad on your iPhone, the input is sent to SynktraCompanion
- If ViGEmBus is installed, inputs are sent as a **real Xbox 360 controller**
- Games with controller support will automatically detect and use the controller
- If ViGEmBus is not installed, inputs fall back to keyboard/mouse emulation

#### Why Can't the Driver Be Bundled?

ViGEmBus is a **Windows kernel driver** that requires:
- System-level installation with administrator privileges
- Microsoft code signing (WHQL certification)
- Cannot be loaded dynamically by applications for security reasons

That's why we provide automatic download and installation instead.

#### Controller Status

Check the controller status via:
- The SynktraCompanion UI shows "Xbox 360 Controller (Virtual)" when active
- API endpoint: `GET /api/controller/status`
- Discovery response includes `SupportsVirtualController` and `VirtualControllerActive`

#### Troubleshooting Virtual Controller

- **"ViGEmBus not installed"**: Download and install from the link above
- **Games don't detect controller**: Make sure ViGEmBus is installed and SynktraCompanion shows "Virtual controller connected"
- **Controller not responding**: Try `POST /api/controller/reconnect` to reconnect
- **Want keyboard/mouse instead**: `POST /api/controller/disable` to switch to fallback mode

## Screenshots

*Coming soon*

## Building the App

### Prerequisites

- .NET 9 SDK
- For iOS: macOS with Xcode 15+ (or use GitHub Actions)
- For Windows Companion: Windows 10/11

### Build Locally (requires Mac for iOS)

```bash
# Restore packages
dotnet restore "gaming hub/gaming hub.csproj"

# Build for simulator
dotnet build "gaming hub/gaming hub.csproj" -c Debug -f net9.0-ios -r iossimulator-arm64

# Build for device
dotnet build "gaming hub/gaming hub.csproj" -c Release -f net9.0-ios -r ios-arm64
```

### Build Windows Companion

#### Option 1: Download Pre-built Installer (Recommended)

Download the latest installer from the [Releases](https://github.com/scriptaxy/gaming-hub/releases) page.

The installer includes:
- ? Synktra Companion application
- ? Optional ViGEmBus driver installation (for virtual controller)
- ? Automatic Windows Firewall configuration
- ? Start with Windows option
- ? Desktop/Quick Launch shortcuts

#### Option 2: Build from Source

```bash
# Navigate to companion directory
cd SynktraCompanion

# Build release
dotnet build -c Release

# Publish as self-contained
dotnet publish -c Release -r win-x64 --self-contained
```

#### Option 3: Build Installer from Source

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) installed.

```powershell
# Run the build script
cd SynktraCompanion
.\Build-Installer.ps1

# Or with custom version
.\Build-Installer.ps1 -Version "1.2.0"
```

The installer will be created at `SynktraCompanion/Installer/Output/`

### Build with GitHub Actions (no Mac needed!)

1. Fork this repository
2. Go to **Actions** tab
3. Click **Run workflow**
4. Download the build artifacts when complete

## Installing on iPhone

### Option 1: AltStore (FREE)

1. Download [AltStore](https://altstore.io) on your PC/Mac
2. Install AltStore on your iPhone (follow their guide)
3. Download the IPA from GitHub Actions artifacts
4. Open the IPA with AltStore

**Note:** Free Apple ID = re-sign every 7 days, max 3 apps

### Option 2: Sideloadly (FREE)

1. Download [Sideloadly](https://sideloadly.io)
2. Connect iPhone to PC
3. Download IPA from GitHub Actions
4. Drag IPA into Sideloadly
5. Enter Apple ID and install

### Option 3: Apple Developer Account ($99/year)

With a paid developer account, you can:
- Install apps that last 1 year
- No 3-app limit
- TestFlight distribution

## Configuration

### Steam Integration

1. Get your Steam ID from your profile URL
2. Get API key from [Steam Web API](https://steamcommunity.com/dev/apikey)
3. Enter both in Settings > Steam

### Epic Games Integration

1. Go to Settings > Epic Games
2. Sign in with your Epic Games account
3. Sync your library automatically

### RAWG API (for game search)

1. Get free API key from [RAWG.io](https://rawg.io/apidocs)
2. The app works without it, but game search will be limited

### Remote PC Feature

The SynktraCompanion Windows app provides these API endpoints:

#### System & Status
- `GET /api/discover` - PC discovery info (supports UDP broadcast on port 5001)
- `GET /api/status` - PC status (CPU, RAM, GPU, current game, controller status)

#### Games
- `GET /api/games` - List installed games
- `POST /api/games/{id}/launch` - Launch a game
- `POST /api/games/close` - Close current game

#### System Power
- `POST /api/system/sleep` - Put PC to sleep
- `POST /api/system/shutdown` - Shutdown PC
- `POST /api/system/restart` - Restart PC

#### Streaming
- `POST /api/stream/start` - Start streaming
- `POST /api/stream/stop` - Stop streaming and release inputs
- `GET /api/stream/status` - Get streaming status
- WebSocket on port 19501 for video frames
- UDP on port 19502 for low-latency frames

#### Virtual Controller
- `GET /api/controller/status` - Get controller status (includes `canAutoInstall`)
- `POST /api/controller/enable` - Enable virtual controller
- `POST /api/controller/disable` - Disable (use keyboard/mouse fallback)
- `POST /api/controller/reconnect` - Reconnect virtual controller
- `POST /api/controller/install` - **Auto-install ViGEmBus driver** (downloads and installs automatically)

## API Credits

- [CheapShark](https://www.cheapshark.com/) - Game deals (free, no key needed)
- [RAWG](https://rawg.io/) - Game database (free tier available)
- [Steam Web API](https://developer.valvesoftware.com/wiki/Steam_Web_API) - Steam integration
- [Epic Games](https://dev.epicgames.com/) - Epic Games integration
- [ViGEmBus](https://github.com/ViGEm/ViGEmBus) - Virtual gamepad emulation

## Tech Stack

### iOS App (gaming hub)
- **.NET 9** with **iOS workload**
- **UIKit** (native iOS UI)
- **SQLite** for local storage
- **Newtonsoft.Json** for API parsing
- **WebKit** for OAuth authentication

### Windows Companion (SynktraCompanion)
- **.NET 9** with **WPF**
- **ViGEm.Client** for virtual controller emulation
- **SharpDX** for DirectX screen capture
- **Discord Rich Presence** for Discord integration

## Project Structure

```
gaming hub/
??? Models/
?   ??? Game.cs         # Game entity
?   ??? UserData.cs       # User settings, deals, releases
??? Services/
?   ??? DatabaseService.cs    # SQLite operations
?   ??? GameApiService.cs  # RAWG + CheapShark APIs
?   ??? SteamService.cs       # Steam API integration
?   ??? EpicGamesService.cs   # Epic Games API integration
?   ??? RemotePCService.cs    # Remote PC control
?   ??? StreamingClient.cs    # Game streaming client
??? Views/
?   ??? GameCell.cs       # Game collection cell
?   ??? CustomCells.cs    # Deal, Release cells
??? ViewControllers/
?   ??? LibraryViewController.cs
?   ??? DealsViewController.cs
?   ??? GameDetailViewController.cs
?   ??? GameStreamViewController.cs
?   ??? RemotePCViewController.cs
?   ??? SettingsViewController.cs
??? Assets.xcassets/      # App icons and images
??? MainTabBarController.cs

SynktraCompanion/
??? Models/
?   ??? Models.cs             # Shared models
??? Services/
?   ??? ApiServer.cs      # HTTP API server
?   ??? GameScanner.cs        # Detect installed games
?   ??? InputSimulator.cs     # Input handling
?   ??? VirtualControllerService.cs  # ViGEm integration
?   ??? LowLatencyStreamService.cs   # Screen streaming
?   ??? SystemMonitor.cs      # CPU/GPU/RAM monitoring
?   ??? SettingsManager.cs    # App settings
??? MainWindow.xaml           # Main UI
??? App.xaml       # WPF app entry
```

## Author

**Scriptaxy** - [GitHub](https://github.com/scriptaxy)

## License

MIT License - feel free to use and modify!

## Contributing

PRs welcome! Please open an issue first for major changes.

---

*© 2025 Scriptaxy. All rights reserved.*
