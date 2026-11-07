# Bow Arc

![version](https://img.shields.io/badge/version-0.1.0-blue) ![valheim](https://img.shields.io/badge/Valheim-1.0.21-orange) ![client](https://img.shields.io/badge/client--side-only-green)

A BepInEx plugin for Valheim.

A shot-aiming assist for bows. As soon as you start drawing your bow, a faint dotted arc appears showing where the arrow will fly, with a small ring where it will hit.

## Features
- Uses the game's own arrow physics (draw strength, bow and arrow speed, gravity and drag), so the arc follows your actual shot.
- Appears almost as soon as you start drawing and gets slightly stronger as you reach full draw. The arc grows as the draw gets stronger.
- Briefly shows the icon of your equipped arrows just above the crosshair when you start a draw.
- Stops at the first thing the arrow would hit (ground, buildings, creatures, water) and marks it with a ring.
- Works with every bow and arrow type, including modded ones that use the vanilla projectile system.
- The arc follows the middle of your shot. At low skill the arrow's random spread can still pull it a little off the line.
- Client-side only: nothing is patched or sent to the server, so it works on vanilla servers.

## In-game settings
**Settings > Gameplay** has two Bow Arc rows at the bottom:
- **Bow Arc: arrow icon flashes**: flash the arrow icon instead of showing it steadily.
- **Bow Arc: icon flash time**: how long it flashes (0.2-5 s).

Changes are saved when you press OK. Everything else is in the config file below.

## Configuration (`BepInEx/config/jordan.valheim.bowarc.cfg`)
| Section | Key | Default | What it does |
|---|---|---|---|
| General | Enabled | true | Show the arc. |
| General | ShowAtDrawPercent | 0.05 | How far the bow must be drawn (0-1) before the arc appears. |
| General | MaxDistance | 150 | Longest arc to draw, in metres. |
| Look | Color | white, alpha 0.35 | Dot colour; the alpha is how see-through the arc is when it first appears. |
| Look | FullDrawAlpha | 0.55 | Alpha of the dots at full draw. |
| Look | DotSize | 0.06 | Dot diameter in metres. |
| Look | DotSpacing | 0.5 | Distance between dots in metres. |
| Look | ShowImpactMarker | true | Draw a ring where the arc hits something. |
| Ammo Icon | Enabled | true | Show the equipped arrow icon when a draw starts. |
| Ammo Icon | Seconds | 1.0 | How long the icon stays fully visible before fading. |
| Ammo Icon | Flash | false | Flash the icon on and off instead of showing it steadily. |
| Ammo Icon | FlashSeconds | 1.5 | With Flash on: how long it keeps flashing before disappearing (replaces Seconds). |
| Ammo Icon | FlashesPerSecond | 4 | With Flash on: how fast it flashes. |
| Ammo Icon | Size | 80 | Icon size in pixels. |
| Ammo Icon | OffsetAboveCrosshair | 45 | How far above the crosshair the icon sits, in pixels. |

## Install

### Easiest: Valheim Mod Installer
Download the [**Valheim Mod Installer**](https://github.com/Glutoblop/ValheimPlugins) and run it (`Start-Installer.bat` on Windows, `start-installer.sh` on Linux/macOS), point it at your Valheim folder, tick **Bow Arc** and click **Install / Update**. It installs BepInEx for you if needed.

### Manual
Download from the [latest release](https://github.com/Glutoblop/BowArc/releases/latest) ([`BowArc.zip`](https://github.com/Glutoblop/BowArc/releases/latest/download/BowArc.zip): unzip into your Valheim folder), or:

1. Install [BepInExPack_Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) (5.4.2360 or newer) into your Valheim folder.
2. Download [`dist/BowArc.dll`](https://github.com/Glutoblop/BowArc/raw/main/dist/BowArc.dll).
3. Copy it to `<Valheim>/BepInEx/plugins/BowArc/BowArc.dll`.
4. Launch the game once; settings are written to `BepInEx/config/jordan.valheim.bowarc.cfg`.

## Compatibility

| | |
|---|---|
| Plugin version | **0.1.0** |
| Last tested on Valheim | **1.0.21** (network version 41, Steam build 26249040) on 2026-11-06 |
| BepInExPack_Valheim | 5.4.2360 |
| Multiplayer | Client-side only: works on vanilla servers, other players don't need it |

Machine-readable versions live in [`plugin.json`](plugin.json). The installer reads it straight from `main` to tell users when an update is available, or when the mod hasn't been tested against their game version.

## Building

Requires the .NET SDK (8+) and a Valheim install with BepInEx.

```
dotnet build -c Release -p:GameDir="C:\Path\To\Valheim"
```
