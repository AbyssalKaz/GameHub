# Game Hub

One library for every PC game you have installed, whichever launcher it came from.

Game Hub has two parts:

- **Game Shortcut Sync** (`GameShortcutSync.ps1`) finds your games and puts a shortcut for each one in the Start Menu, so they all show up in Windows search. It runs at logon and every hour.
- **Game Hub** (`Hub\`) is a desktop launcher that shows the whole library in one place.

## Supported sources

Steam, Epic Games, GOG, Xbox / Microsoft Store, EA, Ubisoft, Riot, Rockstar, HoYoPlay, Battle.net, Amazon Games, itch.io, Hydra, emulators (one entry per ROM), and any folder you point it at.

## Features

- Real on-disk sizes (launchers often under-report), plus a storage breakdown by drive and folder
- Cover, hero and logo art from Steam, Epic and SteamGridDB, with a picker to change it
- Search filters such as `platform:steam size:>30gb played:<7d genre:rpg`
- Favourites, collections, play status, ratings and notes
- Playtime tracking for every game, including ones launched outside the hub
- Game info: descriptions, screenshots, trailers, HowLongToBeat times and news
- Free game alerts (Epic, Steam, itch.io), plus a wishlist with sale alerts
- Built-in store browser with tabs, an ad blocker and download handling
- Uninstall through each game's own launcher, a leftovers cleaner, moving games between drives, and save backups
- A full-screen console mode for controllers, with an on-screen keyboard
- Light and dark themes, an accent colour, a Discord status and jump-list shortcuts

## Requirements

- Windows 10 or 11
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/), which is already installed on most PCs
- [Visual Studio Build Tools](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio) with the ".NET desktop build tools" workload, to compile. No .NET SDK is needed.

## Getting started

1. Download or clone this repository.
2. Install the Start Menu sync. This also creates the scheduled task:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\GameShortcutSync.ps1 -Install
   ```

3. Build and start the hub. The first build downloads the WebView2 SDK from NuGet.

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\Hub\build.ps1
   .\Hub\bin\GameHub.exe
   ```

4. In the hub, open **Settings → About** to add Game Hub to the Start Menu.

## Configuration

On first run, `config.example.json` is copied to `config.json`. Most of these options are also in the hub's Settings page.

| Key | Purpose |
| --- | --- |
| `LibraryFolders` | Folders whose subfolders are each a game |
| `SearchFolders` | Folders to scan for loose games (for example, Downloads) |
| `ExtraGames` | Individual game folders to always include |
| `IgnoreNames` | Game names to skip |
| `ShortcutFolderName` | Start Menu folder that holds the shortcuts |

Your library, settings and caches stay on your PC in `library.json`, `config.json` and `Hub\cache\`. None of them are uploaded anywhere.

## Command line

`GameShortcutSync.ps1` takes these switches:

- `-Install`, `-Uninstall`: set up or remove the shortcuts and the scheduled task
- `-Preview`: show what would change without changing anything
- `-Quiet`, `-Pause`: control console output

`GameHub.exe` takes these switches:

- `--launch <id>`: start a game
- `--home`: open console mode
- `--fullscreen`, `--minimized`: window state on start

## Credits

Created by Claude, an AI model made by Anthropic. Every line of Game Hub's code was written by AI, and all of it has been proofread by people.

## License

[MIT](LICENSE)
