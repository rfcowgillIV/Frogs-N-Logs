# Web Version & Touch Controls — October 2026

**Play it:** https://rfcowgilliv.github.io/Frogs-N-Logs/ (works on computers, iPads, and phones)

## Controls
| | Keyboard | Mouse | Touch (iPad / phone) |
|---|---|---|---|
| Start | Space | Click | Tap |
| Move | Arrow keys / WASD | Hold the button down; the frog hops toward the pointer | Hold a finger down; the frog hops toward it |

The touch and mouse code lives in `Assets/Scripts/PointerInput.cs`. Unity treats a finger like a
mouse, so one piece of code covers both. The keyboard always takes priority if both are used.

## How it gets built and published
Everything happens in `.github/workflows/unity-upgrade.yml`, in one Unity session:
1. Build the game and run the automated play tests.
2. If they pass, build the web version (`UpgradeAndBuild.BuildWebGL`, about 40 MB, gzip-compressed
   with "decompression fallback" so any plain web host works).
3. `.github/scripts/web-fullscreen.py` tweaks Unity's page so the game fills any screen (iPads
   report as Macs, so Unity's phone mode never kicked in) and finger drags don't scroll or zoom.
4. Publish to the `gh-pages` branch, which GitHub Pages serves at the link above.

This runs on every push to `master` and on pull requests, so a PR can be tried on an iPad before
merging. Note that the most recent run, PR or master, is what's live.

**One-time setup (already done):** Settings → Pages → Deploy from a branch → `gh-pages` / root.

## Why the web build shares the play-test job
A Unity Personal license only signed in reliably once per run on GitHub's servers. A separate web
job's second sign-in was refused ("0 free entitlements"), so both builds share one session.
