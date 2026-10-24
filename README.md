# RoadSense Patrol

**Spot road hazards. React early. Keep your streak alive.** RoadSense Patrol is a Windows-only, top-down C# arcade driving game designed for short booth sessions. Dodge major potholes and traffic, brake for minor damage, road patches, and humps, use the scanner for earlier warnings, and compete on a local Top 10 board. You have three lives; traffic and speed increase as you survive. Motorcycles join later and swerve suddenly between lanes.

Before each run, choose The King, Lightning McQueen, or Doc Hudson on the animated car-selection screen. The selected top-down car also appears on the road. Use A/D or Left/Right to choose and Enter to start, or select with a mouse or touch screen.

Road hazards and scanner detections are simulated for demonstration. The game does not run the RoadSense AI model or connect to the iOS app.

The Windows executable and game window use the RoadSense logo. When the third life is lost, the game shows a crash replay for the selected car with its matching crash sound, then fades smoothly into results. The King uses the first supplied clip and sound, Lightning McQueen uses `crash scene 2.mp4` and `crash sound 2.mp3`, and Doc Hudson uses `crash scene 3.mp4` and `crash sound 3.mp3`. Tap, Space, or Enter skips to results. The videos are bundled as three frame archives in `assets/` for offline playback; the sounds are embedded as WAV files.

Hold **W** or **Up Arrow** to build speed gradually and move toward the top of the road. Releasing it gradually returns the car and speed to the run's current cruise level. The HUD starts at 80 km/h, rises toward 100 km/h as the run progresses, and shows the temporary boost separately. Road scrolling uses screen units, so the displayed km/h is an arcade scale. Use **A/D** or Left/Right to steer, **S** or Down to brake, and **B** to scan farther ahead and reveal hazards in side lanes. Normal radar reveals road distress only in your current lane; detected hazards remain visible after you change lanes.

The scanner has an energy bar and a cooldown bar. Releasing Scan Boost starts a 3-second cooldown; draining its energy starts a 5-second cooldown. Scan Boost cannot run again until the cooldown finishes, and its energy recharges while it rests.

## Download

Packaged Windows x64 builds are on the [GitHub Releases page](https://github.com/clouddddd34/RoadSense-Patrol/releases). For the newest source changes, build from `main` using the instructions below. The installer adds a Start Menu shortcut and an optional desktop shortcut. It runs offline and includes the .NET runtime.

## Run

Requires Windows 10/11 and the .NET 8 SDK for development.

```powershell
dotnet run
```

Build and test:

```powershell
dotnet build -c Release
dotnet run --project tests/RoadSensePatrol.Tests.csproj -c Release
```

Make a portable, self-contained Windows x64 release:

```powershell
dotnet publish RoadSensePatrol.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Copy the **entire `publish` folder** to the booth PC. Run `RoadSensePatrol.exe`. The build is offline; no account or network is needed.

Build the Windows installer with Inno Setup 6 after publishing:

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\RoadSensePatrol.iss
```

The installer is written to `dist/RoadSensePatrol-Setup-x64.exe`.

## License

RoadSense Patrol is released under the [MIT License](LICENSE).

## Controls

| Action | Keyboard | Touch / mouse |
| --- | --- | --- |
| Start or replay | Space or Enter | Tap the screen |
| Open or close menu | Esc | MENU / CLOSE button |
| Rankings and hazard guide | R / H while menu is open | Menu tabs |
| Change lane | A/D or Left/Right | Tap left/right side of road |
| Temporary speed boost and move forward | Hold W or Up Arrow | Keyboard only |
| Brake | Hold S or Down | Hold BRAKE button |
| Scan Boost | Hold B | Hold SCAN BOOST button |
| Mute | M | — |
| Fullscreen | F11 | — |
| Quit | Ctrl+Shift+Q | — |

On the results screen, type a name of up to 20 characters and press Enter or click **SAVE SCORE** to save a qualifying Top 10 score. Letters, numbers, spaces, hyphens, apostrophes, and periods are supported. Backspace edits the entry. The game returns to the attract screen after 20 seconds. Qualifying scores left blank at timeout are saved as `PLAYER`. Existing three-letter scores remain on the board.

Hidden booth admin shortcut: **Ctrl+Shift+R** opens a confirmation to clear local scores. Scores are stored in `%LOCALAPPDATA%\RoadSensePatrol\leaderboard.json` and survive restarts. A missing or invalid leaderboard file starts with an empty board.

## Booth settings

Edit `settings.json` beside the executable:

- `boothSeed`: set to an integer for the same route for every player; use `null` for randomized routes.
- `fullscreenOnStart`: set to `true` for a fullscreen booth display.
- `startingLives`: default 3.
- `baseSpeed`, `speedIncreasePerSecond`, `maxSpeed`: tune the endless speed curve. Holding W or Up Arrow ramps effective speed up to 75% above cruise over about 1.3 seconds; releasing it eases back to the current progression speed.
- `prizeScore`: score target, default 5000.
- `showPrizeTarget`: set to `true` only when the booth actually offers a prize.

Game scoring values, multiplier cap, hazard timing, and difficulty progression are in `GameConfig.cs` and `GameModel.cs`. The default speed is 205 and rises by 2.1 per second of survival, reaching 331 after a minute and capping at 520. Brake, scan, and rough road temporarily reduce effective movement speed.

## Booth test checklist

- Play until all three lives are gone with A/D, S, and B; check rising speed, cars, trucks, warnings, score chain, hits, crash animation, pickups, results, and replay.
- Check Left/Right, Up, and Down arrow controls.
- On a touch display, tap the road and hold both on-screen buttons. The Top 10 entry requires a keyboard.
- Save a score, restart the app, and verify the Top 10 persists.
- Leave results untouched for 20 seconds and verify return to attract mode.
- Test F11 fullscreen, mute, and hidden admin reset on the booth PC.
