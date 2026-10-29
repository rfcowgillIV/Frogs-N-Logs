# Unity 6 Upgrade — October 2026

Frogs-N-Logs was moved from **Unity 2017.3.0f3** to **Unity 6.3 LTS (6000.3.27f1)**.
The upgrade ran on GitHub's servers with your free Personal license, and the game built
successfully for Linux afterward. This file explains what changed and what to check.

## How to open it

1. In **Unity Hub → Installs → Install Editor**, install **Unity 6000.3.27f1** (or any newer
   6000.3 version). Add your platform's build support module if you want to build a playable copy.
2. **Projects → Add → Add project from disk** and pick
   `Documents/Coding Projects/Unity Projects/Frogs-N-Logs`.
3. The first open takes a few minutes while Unity rebuilds its `Library/` cache. That folder
   isn't stored in git anymore, so this is normal.
4. Open `Assets/_Scenes.unity` and press **Play**.

## What changed

### Done by Unity during the upgrade (automated commit)
- Every scene, prefab, material and setting was re-saved in Unity 6 format.
- **Assets are now saved as text instead of binary**, so future changes show up as readable
  diffs on GitHub. A few file types Unity always keeps binary stay that way (terrain data,
  NavMesh, lighting data).
- A modern `Packages/manifest.json` was created. The old `UnityPackageManager/` folder from
  2017 is no longer used.
- `ProjectSettings/ProjectVersion.txt` now says `6000.3.27f1`.
- Your 13 game scripts in `Assets/Scripts/` needed **no code changes**; only their `.meta`
  files were refreshed.

### Code fixes needed before Unity 6 would compile
| File | Problem | Fix |
|---|---|---|
| `Standard Assets/Utility/ForcedReset.cs`, `SimpleActivatorMenu.cs` | Used `GUITexture` / `GUIText`, removed from Unity in 2019 | Deleted. The game never used them (checked every scene and prefab). |
| `Standard Assets/Editor/CrossPlatformInput/CrossPlatformInputInitialize.cs` | Referenced PlayStation Mobile, Tizen and Samsung TV, platforms Unity no longer has | Removed those entries |
| `PostProcessing/Editor/Models/AmbientOcclusionModelEditor.cs` | Missing `using UnityEngine.PostProcessing;` | Added |
| `PostProcessing/Editor/PropertyDrawers/MinDrawer.cs` | Unity 6 added its own `MinAttribute`, which clashed with the package's | Alias picks the post-processing one |

### New: `Assets/Editor/UpgradeAndBuild.cs`
Editor script that re-saves all assets and builds the game. The workflow runs it, and you can
also run it from **Tools → Frogs-N-Logs → Upgrade And Build**.

### New: automatic Unity build check
`.github/workflows/unity-upgrade.yml` now runs a real Unity 6 build on every push and pull request
to `master`, so every new feature or level is checked automatically. It uses the
`UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD` secrets and returns the license seat when done.

## Please check when you play
- **The bird.** `Assets/Prefabs/Bird.prefab` is the one prefab Unity didn't re-save in the new
  format, which can mean it couldn't fully load it. The level still built. If the bird looks or
  acts wrong, open the prefab and check the Console for warnings. Re-saving it (Ctrl/Cmd + S)
  after any fix converts it.
- **Water and visual effects.** The 2017 water (`Standard Assets/Environment/Water`) and the old
  Post Processing Stack v1 still work in Unity 6's Built-in renderer, but both are legacy. If
  water looks pink or effects are missing, that's why, and it's a good candidate for a later
  "visual refresh" (Post Processing v2 package, or moving to URP).
- **Controls, flies, score, Game Over and restart** should all behave as before, including the
  bug fixes from the security update.

## Security note
Builds from this upgraded project are **no longer affected by CVE-2025-59489** (the Unity runtime
flaw in 2017-era versions). Any old builds you shared before should still be taken down or
replaced with a new build.
