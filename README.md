# Character View starter

Start a **Character View** game on the [IT Kamianets 3D Engine](https://3d.itkamianets.com): 1st + 3rd person, games like GTA V, Cyberpunk 2077, Elden Ring.

The camera and controls for this style are ready.

## Start

1. Clone this repository under your game's name.
2. Open it in Unity Hub with Unity 6000.6: it is an empty Universal 3D project.
3. Follow [AGENTS.md](AGENTS.md): add the engine packages, build the game. Hand it to your AI agent, or follow it yourself.

Engine packages for this style: `3d-unity-core`, `3d-unity-scene`, `3d-unity-input`, `3d-unity-character`, `3d-unity-assets`, `3d-unity-audio`, `3d-constructor-character-view-camera`, `3d-constructor-character-view-controls`.

Every package, with what it does and how to install it: https://3d.itkamianets.com/packages.md

## Changing an engine package

The `3d-*` packages are shared by every game. To change one, clone it into `Packages/` (Unity uses that copy instead of the git URL; git ignores it), work on a branch or your fork, and open a pull request to its `master` branch. [AGENTS.md](AGENTS.md) has the steps.

## AI agents

[AGENTS.md](AGENTS.md) is the guide for any AI agent (Codex, Cursor, Copilot and others read it automatically); [CLAUDE.md](CLAUDE.md) points Claude Code to it.
