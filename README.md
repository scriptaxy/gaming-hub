# ?? Gaming Hub

A comprehensive iOS gaming companion app built with .NET 9 and UIKit.

## Features

- ?? **Game Library** - Track games from Steam, Epic, GOG, and manually added titles
- ?? **Deals** - Find the best game deals across multiple stores (via CheapShark)
- ?? **Upcoming Releases** - Track game release dates with countdown timers
- ??? **Remote PC** - Wake-on-LAN, launch games remotely, view PC status
- ?? **Steam Integration** - Sync your Steam library automatically

## Screenshots

*Coming soon*

## Building the App

### Prerequisites

- .NET 9 SDK
- For iOS: macOS with Xcode 15+ (or use GitHub Actions)

### Build Locally (requires Mac)

```bash
# Restore packages
dotnet restore "gaming hub/gaming hub.csproj"

# Build for simulator
dotnet build "gaming hub/gaming hub.csproj" -c Debug -f net9.0-ios -r iossimulator-arm64

# Build for device
dotnet build "gaming hub/gaming hub.csproj" -c Release -f net9.0-ios -r ios-arm64
```

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

### RAWG API (for game search)

1. Get free API key from [RAWG.io](https://rawg.io/apidocs)
2. The app works without it, but game search will be limited

### Remote PC Feature

Requires a companion server running on your PC. The app expects these endpoints:
- `GET /api/status` - PC status (CPU, RAM, GPU, current game)
- `GET /api/games` - List installed games
- `POST /api/games/{id}/launch` - Launch a game
- `POST /api/system/sleep` - Put PC to sleep
- `POST /api/system/shutdown` - Shutdown PC

## API Credits

- [CheapShark](https://www.cheapshark.com/) - Game deals (free, no key needed)
- [RAWG](https://rawg.io/) - Game database (free tier available)
- [Steam Web API](https://developer.valvesoftware.com/wiki/Steam_Web_API) - Steam integration

## Tech Stack

- **.NET 9** with **iOS workload**
- **UIKit** (native iOS UI)
- **SQLite** for local storage
- **Newtonsoft.Json** for API parsing

## Project Structure

```
gaming hub/
??? Models/
?   ??? Game.cs       # Game entity
?   ??? UserData.cs      # User settings, deals, releases
??? Services/
?   ??? DatabaseService.cs   # SQLite operations
?   ??? GameApiService.cs    # RAWG + CheapShark APIs
?   ??? SteamService.cs      # Steam API integration
?   ??? RemotePCService.cs   # Remote PC control
??? Views/
?   ??? GameCell.cs      # Game collection cell
?   ??? CustomCells.cs   # Deal, Release cells
??? ViewControllers/
?   ??? LibraryViewController.cs
?   ??? DealsViewController.cs
?   ??? GameDetailViewController.cs
?   ??? RemotePCViewController.cs
? ??? SettingsViewController.cs
??? MainTabBarController.cs
```

## License

MIT License - feel free to use and modify!

## Contributing

PRs welcome! Please open an issue first for major changes.
