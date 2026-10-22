# Character View starter: agent guide

This repository starts a **Character View** game on the **IT Kamianets 3D Engine**: 1st + 3rd person, games like GTA V, Cyberpunk 2077, Elden Ring. A developer clones it under the game's name, and it becomes their game.

For games where the player directly controls a character from first-person or third-person perspective.

It holds an empty Unity project (Universal 3D template, Unity **6000.6**): one scene, `Assets/Scenes/Main.unity`, the URP settings, `Packages/manifest.json` and `ProjectSettings/`. Everything else is built from this guide.

## Read first

- **https://3d.itkamianets.com/packages.md**: every engine package with its purpose, usage, dependencies, install order and git URL. Fetch it before adding, removing or writing a package; it is the current list, this file is not.
- https://3d.itkamianets.com/llms.txt: short index of the docs.
- https://3d.itkamianets.com/constructors/character-view: this constructor on the website.

## 1. Open the Unity project

1. In Unity Hub: Add › this repository's folder, and open it with Unity **6000.6** (the first import takes a minute; `Library/` is rebuilt locally and never committed).
2. In `Project Settings › Player`, set Product Name and the application identifier (`com.itkamianets.<game>`, letters and dots only) for the game.
3. The project has no project-wide input actions asset on purpose: all input comes from `3d-unity-input`'s `EngineInput` (step 2).

## 2. Add the engine packages

Merge these into `dependencies` in `Packages/manifest.json`. Unity installs each package's Unity dependencies (such as the Input System) itself, but not other git packages, so every engine package below is listed, including indirect ones.

```json
{
  "com.itkamianets.3d-constructor-character-view-camera": "https://github.com/IT-Kamianets/3d-constructor-character-view-camera.git",
  "com.itkamianets.3d-constructor-character-view-controls": "https://github.com/IT-Kamianets/3d-constructor-character-view-controls.git",
  "com.itkamianets.3d-unity-assets": "https://github.com/IT-Kamianets/3d-unity-assets.git",
  "com.itkamianets.3d-unity-audio": "https://github.com/IT-Kamianets/3d-unity-audio.git",
  "com.itkamianets.3d-unity-character": "https://github.com/IT-Kamianets/3d-unity-character.git",
  "com.itkamianets.3d-unity-core": "https://github.com/IT-Kamianets/3d-unity-core.git",
  "com.itkamianets.3d-unity-input": "https://github.com/IT-Kamianets/3d-unity-input.git",
  "com.itkamianets.3d-unity-scene": "https://github.com/IT-Kamianets/3d-unity-scene.git"
}
```

| Package | Why |
|---|---|
| [`3d-unity-core`](https://3d.itkamianets.com/packages/3d-unity-core) | The shared base for every Unity package: configuration, services, events, logging and startup. |
| [`3d-unity-scene`](https://3d.itkamianets.com/packages/3d-unity-scene) | Loads Scene JSON into live Unity scenes and saves changes back to the shared format. |
| [`3d-unity-input`](https://3d.itkamianets.com/packages/3d-unity-input) | One set of actions for mouse, keyboard, touch, gamepad and VR controllers. |
| [`3d-unity-character`](https://3d.itkamianets.com/packages/3d-unity-character) | A reusable player character: walking, running, jumping, ground, slopes and steps. |
| [`3d-unity-assets`](https://3d.itkamianets.com/packages/3d-unity-assets) | Loads models, materials and textures by id, locally or from a server, with caching. |
| [`3d-unity-audio`](https://3d.itkamianets.com/packages/3d-unity-audio) | Positional 3D sound, sound events with variations, footsteps by surface, volume groups and music. |

**Camera and controls for this style** (included above):

| Package | What it does |
|---|---|
| [`3d-constructor-character-view-camera`](https://3d.itkamianets.com/packages/3d-constructor-character-view-camera) | First- and third-person camera for the Character View constructor. |
| [`3d-constructor-character-view-controls`](https://3d.itkamianets.com/packages/3d-constructor-character-view-controls) | Connects input to the character: forward is where the camera looks, looking turns the character. |

**Optional**, add when the game needs it (with its dependencies; packages.md lists the install order):

| Package | Add for |
|---|---|
| [`3d-unity-network`](https://3d.itkamianets.com/packages/3d-unity-network) | multiplayer: arena shooters, co-op. Its session messages follow [`3d-network-schema`](https://3d.itkamianets.com/packages/3d-network-schema) |
| [`3d-unity-firebase`](https://3d.itkamianets.com/packages/3d-unity-firebase) | accounts, saved progress, ranks |
| [`3d-unity-xr`](https://3d.itkamianets.com/packages/3d-unity-xr) | a VR version played in first person |
| [`3d-unity-interaction`](https://3d.itkamianets.com/packages/3d-unity-interaction) | players build or rearrange objects in the world |

## 3. Build the game

1. Build `Assets/Scenes/Main.unity` up with a floor, a few steps and a ramp to test movement.
2. Make `Assets/Prefabs/Player.prefab`: a `CharacterController`, `CharacterMotor` (from `3d-unity-character`) and `CharacterViewControls` (from `3d-constructor-character-view-controls`), with a capsule body. Put `CharacterViewCamera` (from `3d-constructor-character-view-camera`) on the main camera and point it at the player. Press Play: WASD or the left stick moves, the mouse or right stick looks, V switches first and third person.
3. Add the interface packages (`<game>-menus`, `<game>-hud`), then the gameplay (`<game>-core`, `<game>-maps`, plus what your game is about: weapons, spells, quests).

**Name game packages `<game>-<part>`**, package id `com.itkamianets.<game>-<part>`, e.g. `temnorid-arena-core`. Suggested for this kind of game: `<game>-menus`, `<game>-hud`, `<game>-core`, `<game>-maps`, weapons or spells.

- **Interface first, then gameplay.** Menus and HUD give a playable loop early; rules and content follow.
- Game packages live in their own repos (cloned into `Packages/`, like the engine ones), so several games can share one; while the game is young, keep them as folders with their own asmdef under `Assets/<Game>/`.
- Scenes, prefabs that wire packages together, and build settings stay in this project; reusable code goes into packages.
- Rewrite `README.md` for the game and this file's first paragraph once the game has a name: what it is, its packages, how to play.

## Rules

- **Read input only through `3d-unity-input`.** Never read devices directly.
- **No game rules in engine or constructor packages.** If more than one game could use something, propose it as an engine package instead of keeping it in the game.
- **Dependencies point one way:** game → constructor → engine → schemas. An engine package never references a game.
- **Schemas are the source of truth** for scenes (`3d-scene-schema`), assets (`3d-asset-schema`) and multiplayer messages (`3d-network-schema`).
- Commit `.meta` files with their assets. Never hand-write them; Unity generates them.

## This repository is the game's workspace

The game's Unity project sits at the root of this repository, laid out like the engine's own workspace (where the game Temnorid Arena is the root). Package repos you need to read or change are cloned **into `Packages/`**: Unity loads each one there as an embedded package, in place of its git URL in the manifest, and git ignores them (`.gitignore` ignores `Packages/*` except `manifest.json` and `packages-lock.json`), so they never end up in the game's history:

```text
<game-name>/                       this repo: the game
  Assets/ ProjectSettings/
  Packages/manifest.json           tracked: Unity packages and the engine packages by git URL
  Packages/3d-unity-character/     a cloned engine repo, git-ignored, used instead of its git URL
  AGENTS.md  README.md
```

Normally there is nothing to clone: Unity downloads the engine packages by their git URLs.

## Changing an engine package

Engine packages (`3d-*`) are shared by every game, so changes go back to them through a pull request, never as a copy inside the game.

1. Clone the package into `Packages/`: `git clone https://github.com/IT-Kamianets/3d-unity-character Packages/3d-unity-character`. Without write access to IT-Kamianets, fork it on GitHub first and clone your fork. Unity now uses this copy instead of the git URL; the manifest stays as it is.
2. Create a branch for the change.
3. Open a pull request to the package's `master` branch. Keep game rules out of it: the change must make sense for every game.
4. After it is merged, delete the clone (Unity goes back to the git URL) and pin the manifest to the new tag.

**Pin versions** before a release: `https://github.com/IT-Kamianets/3d-unity-core.git#v0.1.0`.

## Git

- `.gitignore` is Unity's standard set plus OS and editor files: never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or builds.
- `.gitattributes` marks Unity scenes and prefabs as text (merged with Unity Smart Merge once configured; the command is in the file) and art and audio as binary. Enable the Git LFS lines before the first large asset.
