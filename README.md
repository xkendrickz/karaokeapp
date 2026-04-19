# KaraokeApp — Desktop Karaoke Player

A full-featured desktop karaoke application built with **WPF (.NET Framework 4.8)** and **LibVLCSharp**. Designed with a dark karaoke-themed UI featuring neon cyan accents, smooth animations, and a focus on the singing experience.

---

## Screenshots

> Add your screenshots here after taking them from the running app.

---

## Features

### Playback
- Plays all major karaoke formats via LibVLC:
  **MP4, MKV, MPG, AVI, DAT, M4A, MP3, CDG, ZIP (CDG+MP3), FLV, WMV, VOB, TS**, and more
- **Vocal ON/OFF** — defaults to right channel only (instrumental)
  - Toggles to stereo for full mix
  - Switches audio tracks for multi-track files
  - Auto-detects companion `_vocal.mp3` files
- **Pitch control** — semitone adjustment (−12 to +12)
- **Volume control** — 0–200% with live slider
- **Repeat mode** — loop current song
- **Live progress bar** — neon animated playback position with time display
- **Auto-sets system volume to 100%** on launch

### Playlist / Queue
- Songs are **removed from queue when they start playing**
- Insert a song → **plays immediately** if nothing is currently loaded
- Reorder queue with Up / Down buttons
- Double-click any queued song to play it next

### Song Library
- Filter by **language / category** (Indonesia, English/Barat, Mandarin, Jepang, Korea + custom)
- **Instant search** by Title or Artist
- **On-screen keyboard** for touch / kiosk use — keys animate with cyan glow on press

### Database
- **SQLite** embedded — no server setup needed
- **Bulk import** from folder — recursively scans all supported file types
- **Auto-parses filenames** using a configurable separator (`#` by default)
  - Format: `TITLE#ARTIST#CATEGORY_KEYWORD#VOCAL_HINT.ext`
  - Example: `PERFECT#ED SHEERAN#BARAT#LEFT.dat`
    → Title: Perfect | Artist: Ed Sheeran | Category: English | Vocal: Left channel
- **Category keyword mapping** — e.g. category "English" with keywords
  `english,inggris,barat,western` auto-tags matching filenames on import
- Song records include: Title, Artist, Category, File Path, Vocal Track, Date Added

### YouTube
- Search YouTube via **YouTube Data API v3** (API key required)
- Download and play YouTube videos via **yt-dlp + LibVLC**
  - Full pitch, volume, and vocal control works on YouTube too
- Save YouTube songs to local library
- Add YouTube URLs manually with custom Title / Artist / Category

### UI / UX
- **Animated splash screen** with progress bar on startup
- **Dark karaoke theme** — deep blue-black gradient with neon cyan accents
- **Animated buttons** — hover glow, press scale-down, spring-back
- **Animated keyboard keys** — cyan flash on press
- **Now Playing banner** — pulsing dot indicator, border flash on song change
- **Category pills** — glowing animated pill buttons
- Resizable panels via drag splitter

---

## Tech Stack

| Layer | Technology |
|---|---|
| UI Framework | WPF (Windows Presentation Foundation) |
| Language | C# / .NET Framework 4.8 |
| Media Engine | LibVLCSharp 3.x + VideoLAN.LibVLC.Windows |
| YouTube Download | yt-dlp + ffmpeg |
| Database | SQLite via System.Data.SQLite |
| Audio API | NAudio (system volume control) |
| JSON Parsing | Newtonsoft.Json |

---

## Requirements

### Runtime
- **Windows 10 / 11 (x64)**
- **.NET Framework 4.8** — usually pre-installed; [download here](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) if needed
- **Microsoft Edge WebView2 Runtime** — usually pre-installed on Windows 11; [download here](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) if needed

### Development
- **Visual Studio 2022** (Community edition is free) with **.NET desktop development** workload
- **.NET Framework 4.8 Developer Pack**

---

## Setup After Clone

Follow these steps in order after cloning the repository.

### Step 1 — Restore NuGet Packages

Open the solution in Visual Studio 2022, then in the **Package Manager Console**
(Tools → NuGet Package Manager → Package Manager Console):

```
Install-Package System.Data.SQLite -Version 1.0.118
Install-Package LibVLCSharp -Version 3.8.2
Install-Package LibVLCSharp.WPF -Version 3.8.2
Install-Package VideoLAN.LibVLC.Windows -Version 3.0.21
Install-Package Microsoft.Web.WebView2 -Version 1.0.2739.15
Install-Package Newtonsoft.Json -Version 13.0.3
Install-Package NAudio -Version 2.2.1
```

Or right-click the solution → **Restore NuGet Packages**.

---

### Step 2 — Download yt-dlp.exe

Required for YouTube playback.

1. Go to https://github.com/yt-dlp/yt-dlp/releases/latest
2. Download `yt-dlp.exe`
3. Place it here:
```
KaraokeApp\bin\Debug\net48\yt-dlp.exe
```

---

### Step 3 — Download ffmpeg.exe

Required for YouTube audio/video merging.

1. Go to https://www.gyan.dev/ffmpeg/builds/
2. Download `ffmpeg-release-essentials.zip`
3. Extract the zip, open the `bin` folder inside
4. Copy `ffmpeg.exe` to:
```
KaraokeApp\bin\Debug\net48\ffmpeg.exe
```

---

### Step 4 — Set Build Platform to x64

Required because LibVLC only ships as 64-bit.

- In the top toolbar dropdown, change **Any CPU** → **x64**
- Or: **Build → Configuration Manager** → set Platform to **x64**

---

### Step 5 — Build and Run

Press **Ctrl+F5** (run without debugger).

The SQLite database will be created automatically at:
```
%APPDATA%\KaraokeApp\karaoke.db
```

---

### Step 6 — Add YouTube API Key (Optional)

Only needed if you want YouTube search to work.

1. Go to [Google Cloud Console](https://console.cloud.google.com)
2. Create a project → Enable **YouTube Data API v3**
3. Create credentials → **API Key**
4. In the app: **⚙ SETTING → YouTube tab** → paste key → Save

> Free quota: ~100 searches/day (100 units per search, 10,000 units/day free)

---

## Project Structure

```
KaraokeApp/
├── Models/
│   ├── Song.cs               # Song entity with DateAdded
│   ├── Category.cs           # Category with keyword mapping
│   ├── PlaylistItem.cs       # Queue item
│   └── AppSettings.cs        # Persisted settings
├── Data/
│   └── DatabaseService.cs    # SQLite CRUD + safe schema migration
├── Services/
│   ├── VlcPlayerService.cs   # LibVLC wrapper + YoutubeHelper (yt-dlp)
│   └── YoutubeService.cs     # YouTube Data API v3 search
├── Converters/
│   └── ValueConverters.cs    # WPF value converters
├── Windows/
│   ├── SplashScreenWindow.xaml   # Animated startup splash screen
│   ├── SettingsWindow.xaml       # Database manager
│   └── Dialogs.cs                # Song/Category edit dialogs
├── Resources/
│   └── Styles.xaml           # Full dark theme + animations
├── App.xaml / App.xaml.cs    # App entry point, splash → main flow
├── MainWindow.xaml           # Main UI
└── MainWindow.xaml.cs        # All application logic
```

---

## Adding Songs

### Method 1 — Import Folder (Recommended)
1. Click **⚙ SETTING → Songs tab → 📁 Import Folder**
2. Select your karaoke folder — scans all subfolders recursively
3. Choose a default category (or auto-detect from filename)

### Method 2 — Add Manually
1. SETTING → Songs → **+ Add Song**
2. Browse for the file — title/artist/category auto-fills from filename
3. Optionally browse for a separate vocal track file

### Filename Convention
For best auto-parsing, name your files using `#` as separator:
```
PERFECT#ED SHEERAN FT BEYONCE#BARAT#LEFT.mp4
│         │                   │     └── vocal hint (LEFT / RIGHT)
│         │                   └──────── category keyword
│         └──────────────────────────── artist
└────────────────────────────────────── title
```

You can change the separator in **SETTING → Import Settings**.

---

## Known Limitations

| Feature | Status |
|---|---|
| True pitch shifting | ⚠️ Rate-based approximation — changes pitch AND tempo together |
| Vocal removal | ⚠️ Channel switching only — doesn't work on mono or mixed stereo |
| YouTube volume/pitch | ✅ Works via yt-dlp + LibVLC (requires yt-dlp.exe) |
| YouTube autoplay to next | ❌ Not implemented |
| Progress bar seeking | ❌ Display only — clicking does not seek |
| Dual monitor support | ❌ Not implemented |
| Drag-and-drop playlist | ❌ Not implemented |
