# MIDNIGHT PURSUIT

Online multiplayer night-time driving game — Computer Graphics project, AAST.
Three modes on one shared city: **Cops & Robbers**, **Midnight Race**, **Survival**.

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
1. **One owner per scene.** Work only in your own scene in `Assets/_Project/Scenes/Work/` (e.g. `A_City.unity`). Never edit someone else's scene.
2. **Make prefabs.** Anything reusable (buildings, props, UI panels, VFX) becomes a prefab in `Assets/_Project/Prefabs/`.
3. **Always commit the `.meta` file** together with its asset. Never delete `.meta` files by hand.
4. **Never commit** `Library/`, `Temp/`, `Logs/`, `UserSettings/` (the `.gitignore` handles this).
5. Imported Asset Store / free packs go in `Assets/ThirdParty/` — never move files inside them.
6. Save your scene (Ctrl+S) and close Unity before switching branches.
7. Commit messages: what you did, e.g. `Add neon signs to downtown block`.

## Folders
```
Assets/_Project/
  Scripts/{Core,Car,Network,GameModes,AI,UI}   C# (team leader)
  Prefabs/                                     reusable objects
  Scenes/                                      real game scenes (team leader integrates)
  Scenes/Work/                                 personal work scenes, one per member
  Art/{Models,Textures}  Materials/  Audio/  UI/  VFX/
Assets/ThirdParty/                             imported packs
```


