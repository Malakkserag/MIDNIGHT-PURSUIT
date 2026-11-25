# MIDNIGHT PURSUIT

Night-time police chase driving game — Computer Graphics project, AAST. Made in **Unity 6 (URP)**.

Drive a sports car through a neon city at night and escape three police cars for as long as you can.
Every ram fills the **BUSTING** bar — when it's full, you're busted. Your **escape time** is your score.

**Features:** night city map · police AI that follows the streets (NavMesh) · 3 police cars incl. 288 km/h interceptors ·
civilian traffic · nitro / repair / time pickups · OpenGL-style radar drawn with Unity's `GL` API ·
minimap · 4 camera views · start, pause, options and busted menus · sounds generated in code · crash sparks.

**Play:** open `Assets/_Project/Scenes/City_Chase.unity` and press Play.
Controls: **WASD** drive · **Space** drift · **C** camera · **R** restart · **Esc** pause.

Full write-up with screenshots: [`Docs/Project_Document.md`](Docs/Project_Document.md)

## Setup (everyone)
1. Install **Unity 6000.3.28f1** exactly (Unity Hub → Installs → Install Editor → Archive). No other version.
2. Install **Git**, **Git LFS** and **GitHub Desktop**.
3. Clone this repo with GitHub Desktop, then open the folder in Unity Hub (Add → Add project from disk).
4. First open takes several minutes (Unity rebuilds `Library/`). That is normal.

> Windows "Part of this app has been blocked" pop-ups about Unity DLLs are a known Smart App Control
> behavior with unsigned Unity assemblies. They are harmless — do not disable Windows security.

## Branches
| Branch | Purpose | Who merges |
|---|---|---|
| `main` | Always playable. TA demos are built from here. | Team leader only |
| `feature/<name>-<task>` | One branch per task, e.g. `feature/omar-city-blockout` | Owner opens a PR into `main` |

Daily flow in GitHub Desktop: **Fetch → switch to your branch → work → Commit → Push → open Pull Request**.

## Golden rules (prevent Unity merge disasters)
1. **One owner per scene.** Work only in your own scene in `Assets/_Project/Scenes/Work/` (`Sandbox_Malak`, `Sandbox_Habiba`, `Sandbox_Chantal`, `Sandbox_Adham`). Never edit someone else's scene — and never commit changes to `City_Chase.unity` without asking the team leader.
2. **Make prefabs.** Anything reusable (buildings, props, UI panels, VFX) becomes a prefab in `Assets/_Project/Prefabs/`.
3. **Always commit the `.meta` file** together with its asset. Never delete `.meta` files by hand.
4. **Never commit** `Library/`, `Temp/`, `Logs/`, `UserSettings/` (the `.gitignore` handles this).
5. Imported Asset Store / free packs go in `Assets/ThirdParty/` — never move files inside them.
6. Save your scene (Ctrl+S) and close Unity before switching branches.
7. Commit messages: what you did, e.g. `Add neon signs to downtown block`.

## Folders
```
Assets/_Project/
  Scripts/{Core,Car,AI,Audio,Gameplay,UI,VFX,Editor}   C# (team leader)
  Prefabs/                                     reusable objects
  Scenes/                                      real game scenes (team leader integrates)
  Scenes/Work/                                 personal work scenes, one per member
  Art/{Models,Textures}  Materials/  Audio/  UI/  VFX/
Assets/ThirdParty/                             imported packs
```


