# Temnorid Dungeons

**Clear the ruins. Beat the boss. Keep the loot.**

Adventures, dungeons and the hunt for artefacts. A [Temnorid](https://temnorid.com) game for **Windows, Android and iOS**, built on the [IT Kamianets 3D Engine](https://3d.itkamianets.com) with Unity 6000.6.

**Status:** version 0.2.0, playable alone or with up to three friends. Website: https://dungeons.temnorid.com · Download: [Temnorid-Dungeons-Setup.exe](https://github.com/WebArtWork/dungeons.temnorid.com/releases/latest/download/Temnorid-Dungeons-Setup.exe)

## What it is

Explore dangerous ruins, fight monsters, defeat bosses and find rare treasure. Win gear and magical artefacts for your heroes.

## The first playable

The first playable is one compact dungeon: fight through a few monsters, face a boss with a visible attack warning, and take the treasure it guards.

### How a session goes

1. Enter the dungeon with a sword and one special ability, the shockwave.
2. Move through corridors and rooms. Monsters notice you from a distance, chase you and attack. A few drop a green orb that heals you when you walk over it.
3. Defeat the regular monsters to open the way to the boss room.
4. Fight the boss. It has much more health and telegraphs its big attack, so you can dodge it.
5. Open the chest the boss guarded (**E**): gold, the Ember Blade and a Boss Sigil go into your stash. Loot dropped by monsters is picked up by walking over it.
6. Step into the glowing exit to finish the run, see what you won, and play again.

### Controls

| Key | Action |
| --- | --- |
| W A S D | Move |
| Mouse | Look around |
| Shift | Run |
| Space | Jump |
| E | Interact |
| Left mouse (hold) | Sword attack in front of you |
| Right mouse | Shockwave: hurts everything around you, recharges in 6 seconds |
| E | Open the chest |
| V | Switch between third and first person |
| Esc | Pause menu (resume, save, quit) |

### Win and lose

- **Win:** Defeat the boss, collect the loot and leave through the exit.
- **Lose:** If your health reaches zero the run ends. Retry restarts the dungeon.

### Playing with others

Up to four heroes can fight through the same dungeon. On the landing screen choose **Play with others**, then **Host a game here** (friends on the same network type the address it shows) or **Host online** (friends type a short code; needs the game linked to Unity Cloud). Everybody presses **Ready** and the host starts. The monsters get tougher with each extra hero (50% more health per hero; a single hero is unchanged). Each hero keeps its own loot: drops go to the hero that walks over them (a green orb only to a hero who needs it), and everybody opens the chest for their own stash. A fallen hero stays down and watches; if every hero falls the run is lost for all. Once the boss is beaten, the run is won when the living heroes have left through the exit (or 45 seconds after the first one did). If the host leaves, the game ends for everybody.

### Updates

When a newer release is published on GitHub, the Windows game downloads and installs it by itself the next time it starts, and starts again. Turn it off in **Profile** if you prefer. On Android and iOS the update comes from the store or the release page.

### Phones

Android and iOS builds are made from Unity Build Profiles (the Boot, Menu and Main scenes, landscape). On a phone there is a stick on the left to move, a drag anywhere else to look, and buttons on the right: **Attack**, **Wave** (shockwave), **Jump**, **Use** (open the chest) and **Run**. The places (Dungeon, Hero, Profile) are the tabs along the bottom.

### The scene

One small dungeon made of blocks: an entrance, corridors, a few rooms and a boss room. Monsters are capsules, the boss is a larger shape.

## How it connects to Lords

Gear and magical artefacts found here will go to the heroes of Lords, which already send heroes into dungeons for loot.

This is for a later phase: [Lords](https://lords.temnorid.com) is the central strategy game and every Temnorid game will be connected to it. **This version is fully offline and does not talk to Lords**; progress is saved locally on the player's PC. Keep identifiers and save data simple and stable so the connection is cheap to add later.

## Scope of this version

- One dungeon, 3 to 5 regular monsters, one boss, loot and a replay loop.
- No random dungeons, levels or full character progression yet.

Everything is drawn with Unity primitives (blocks, capsules, spheres), simple colours and text. No models, no paid assets. Every visible object has a stable asset id (for example `resources/wood-node`) so real models can replace the placeholders without touching gameplay.

## For developers and agents

- **Unity:** 6000.6, Universal 3D (URP). Open the folder in Unity Hub. `Library/` is rebuilt locally and not committed.
- **Platform:** Windows (with self-update), Android and iOS (touch controls). One map, local saves, co-op for up to four.
- **Shell:** the scenes are `Boot` (splash, profile, update check), `Menu` (landing, onboarding, lobby, profile) and `Main` (the dungeon), from `3d-unity-shell`. `Assets/Scripts/DungeonShell.cs` is the game's `ShellConfig` (game id `dungeons`, repo `WebArtWork/dungeons.temnorid.com`, 4 players, places Dungeon and Hero). `DungeonHud` makes the `ShellChrome` bars and keeps the game screen between them.
- **Starts from:** the IT Kamianets 3D Engine Character View starter. [AGENTS.md](AGENTS.md) is still the starter's generic guide; this README is the game's own.
- **Engine pieces this game uses:** the Character View set (`3d-unity-character`, the camera and controls), `3d-unity-core`, `3d-unity-input`, `3d-unity-inventory` (the loot stash), `3d-unity-save`, and two new shared packages written for this game: `3d-unity-combat` (health, teams, melee cone and area attacks) and `3d-unity-ai` (noticing, chasing over a NavMesh, attack requests). The monsters, the boss, loot, gates, the chest, the exit and the screens are written in this game (`Assets/Scripts`).
- **Where code lives:**
  - `3d-*` packages: generic, engine-level behaviour with no Temnorid content (combat, AI, inventory, save, ...).
  - `temnorid-*` packages: Temnorid content shared by **several games** (for example `temnorid-character`). Never `temnorid-<game>-*`.
  - `Assets/Scripts` in this project: this game's own rules, scene builders, HUD and menus.
- **Packages while developing:** the packages sit in the shared workspace folder next to the games (`../3d-unity-core`, `../3d-unity-input`, `../3d-unity-character`, the two Character View packages, `../3d-unity-inventory`, `../3d-unity-save`, `../3d-unity-combat`, `../3d-unity-ai`) and `Packages/manifest.json` points at them with `file:../../<package>`. When they get their own repositories, only these paths change.
- **Level building:** menu **Temnorid > Dungeons > Build Scene** (or `-executeMethod Temnorid.Dungeons.EditorTools.DungeonSceneBuilder.Build`) rebuilds `Assets/Scenes/Main.unity` from scratch: the rooms and corridors, the gates, 5 monsters and the boss, the chest, the exit, the hero, camera and lights, all from primitives. Do not edit that scene by hand. The NavMesh is baked at game start from the floor and wall colliders, and the monsters are switched on after it exists.
- **Code map (`Assets/Scripts`):**
  - `DungeonSession`: the rewards rules with no Unity in them (the stash of everything looted, the run's loot, completions, best time) and `DungeonSaveStore`.
  - `DungeonGame`: the run (gates open when a room is cleared, loot drops, the chest, victory, defeat, pause, saving). `PlayerFighter`: sword, shockwave, chest. `Monster`: what a monster's attack does, its drop, the boss's slam with a red warning on the floor. `RoomGate`, `LootPickup`, `Chest`, `ExitPortal`, `HealthBar3D`, `DungeonHud` (all screens, plain uGUI), `Fx`, `AssetIdTag` (stable ids like `monsters/goblin` for future models).
- **Tests:** `Assets/Tests/EditMode` covers the rewards and the save file (and `3d-unity-inventory` and `3d-unity-combat` have their own tests, run through `testables` in the manifest); `Assets/Tests/PlayMode` (`OnlineDungeonTests`: a host on the real scene and a guest session in the same process; the touch controls) loads the scene, checks every monster stands on the NavMesh, that monsters chase and hurt the player, that clearing rooms opens the gates and that the boss, the chest and the exit finish the run.
- **Save file:** `<persistent data path>/dungeons/default.json` (on Windows `%USERPROFILE%\AppData\LocalLow\Temnorid\Temnorid Dungeons\dungeons\default.json`): the stash, dungeons cleared and the best time.
- **Builds:** the scene builder (`Temnorid > Dungeons > Build Scene`) also writes the `Boot` and `Menu` scenes, the build order and the Android and iOS settings (`com.itkamianets.dungeons`, version 0.2.0). Android and iOS packages: Unity Build Profiles, or `ShellSceneKit.BuildAndroid` / `ExportIos`.
- **Windows build:** **Temnorid > Dungeons > Build Windows Player** (or `-executeMethod Temnorid.Dungeons.EditorTools.DungeonReleaseBuilder.BuildWindows`) writes `Builds/Windows/TemnoridDungeons` and a portable zip. `installer/Dungeons.iss` turns the folder into `Builds/Installer/Temnorid-Dungeons-Setup.exe` with Inno Setup.
- **Online code:** `DungeonNet` (on the Game object, only when `NetSession.IsOnline`) and `RemoteHero`. The host is the authority: only it runs the monsters, the boss, drops, pickups, who a monster hits, and the end of the run (`NetSession.IsAuthority`; the AI of a client's monsters is switched off, `Monster.MakeProxy`). Heroes are owner-simulated and published as poses (15 per second); monsters and drops are published by the host as entities (10 per second): ids 1 to 6 are the monsters in the scene's order (kind 1 goblin, 2 skeleton, 3 boss), ids from 1000 drops (kind 10 and up). Events: `dg.hit` (client to host: monster id and sword or shockwave; the host checks reach and rate and uses its own damage numbers), `dg.kill`, `dg.gate`, `dg.loot`, `dg.dmg` (host to one hero's owner), `dg.finish`, `dg.end`. There is no catch-up help of any kind against people. The test port is 7801 (`OnlineDungeonTests`).
- **Do not add:** Firebase, accounts, or any link to Lords in this version.

## Website and downloads

The `docs/` folder is this game's public website (`docs/index.html`, served at https://dungeons.temnorid.com through GitHub Pages; `docs/CNAME` holds the domain). It repeats the information in this README: what the game is, how to play, controls and the download button.

The download button points to the latest GitHub Release asset:

`https://github.com/WebArtWork/dungeons.temnorid.com/releases/latest/download/Temnorid-Dungeons-Setup.exe`

Release steps (when the first playable is ready):

1. Build the Windows player from Unity (File > Build Profiles > Windows).
2. Wrap the build in a Windows installer named `Temnorid-Dungeons-Setup.exe`.
3. Create a GitHub Release (tag `v0.2.0`; installed games update to it by themselves) and attach that file.
4. Open the website, click the button, install, play, save, restart, uninstall.

When you change the game's description, controls or rules, update this README and `docs/index.html` together.

## License

See [LICENSE](LICENSE).
