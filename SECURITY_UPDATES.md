# Security & Bug-Fix Update — October 2026

Everything changed in the `security-updates-2026` branch, and why.
The project lives in `Documents/Coding Projects/Unity Projects/Frogs-N-Logs/`.

## 1. Security: Unity engine vulnerability (CVE-2025-59489)

The game's own scripts have no security problems (no networking, no saved files, no user
accounts). However, **the Unity version the project uses, 2017.3.0f3, is affected by
CVE-2025-59489**, a high-severity flaw Unity disclosed in October 2025. A game *built* with an
affected version can be tricked into loading malicious code on Windows, macOS, Linux, and Android.

- This only matters for **built copies of the game** that you've shared (e.g. a `.exe` or `.app`
  posted online). The source code in this repo is not affected.
- Unity lists 2017.1 and later as affected and lists **no patched 2017.x editor**.

**What to do if you ever shared a build:**

1. **Preferred:** open the project in a currently supported Unity version (Unity Hub will offer to
   upgrade it), fix any upgrade warnings, and rebuild. Unity 2017 projects usually upgrade with
   minor changes; old Standard Assets and the PostProcessing stack may need replacing.
2. **Alternative:** Unity offers a binary patch tool that swaps the runtime library inside an
   existing build. Check Unity's advisory to confirm 2017.3 is supported before relying on it.
3. Take down old builds you can't patch.

Source: [Unity Security Advisory, Sept 2025](https://unity.com/security/sept-2025-01)

## 2. Bugs fixed (all in `Assets/Scripts/`)

| Script | Bug | Fix |
|---|---|---|
| `GameState.cs` | After the frog died, pressing **Space** on the "Game Over!" screen called `StartGame()` again. It hid the Game Over text and re-enabled controls for a frog that was gone, while the restart countdown kept running. | New `gameOver` flag; Space is ignored after Game Over until the level reloads |
| `GameState.cs` | `EndGame()` ran **every frame** after dying (resetting text and hiding the player ~60× a second) | Runs once |
| `FlyPickUp.cs` | A fly touched by two colliders in the same frame was counted twice (double score, and the spawner's fly count went wrong) | `eaten` flag so each fly counts once |
| `RandomSoundPlayer.cs` | With no sound clips assigned, it threw an "index out of range" error every 3 seconds | Stays quiet if there are no clips or no AudioSource |
| `DontDestroy.cs` | After destroying a duplicate, it still called `DontDestroyOnLoad` on it | `return` right after `Destroy` |
| `FollowCamera.cs` | Camera moved in `Update`, before the frog moved, causing a one-frame lag and jitter | `LateUpdate` (Unity's recommended spot for follow cameras) |

Nothing was renamed, so scene and prefab references in the Unity editor keep working. (For
example, `totalyFlyMinimum` in `FlySpawner.cs` keeps its typo on purpose: renaming a serialized
field would reset its value in the scene.)

## 3. Repository cleanup: `.gitignore` added

The repo includes Unity's **`Library/`** folder (about 13,750 generated files), plus `Temp`/`obj`
build output and Visual Studio project files. Unity regenerates all of these when the project
opens, so they make the repo huge and cause conflicts.

A `.gitignore` was added so new copies of these files won't be committed. To remove the existing
ones from the repo **without deleting them from your computer**, run this once from the repo folder:

```bash
git rm -r --cached "Documents/Coding Projects/Unity Projects/Frogs-N-Logs/Library" \
                   "Documents/Coding Projects/Unity Projects/Frogs-N-Logs/obj"
git commit -m "Stop tracking Unity-generated folders"
```

This wasn't done automatically because it creates a ~13,000-file change that would bury the real
code fixes during review.

## 4. How to verify

Open the project in Unity and play:

1. Press Space to start, eat some flies, and check that the score goes up by exactly 1 per fly.
2. Let the bird catch you, then press Space during "Game Over!" The text should stay up and the
   level should restart after 3 seconds.
3. The camera should follow smoothly without jitter.
4. The Console should show no red errors.

No Unity editor was available where these changes were made, so step 4 is the compile check.
