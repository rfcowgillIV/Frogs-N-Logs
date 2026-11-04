# Frogs-N-Logs apps (Nov 2026)

The game builds as a real app for Android, Windows, Mac, Linux and iOS, for free, using
GitHub Actions. The builds go on a GitHub Release page, where anyone with the link can
download them.

## Making a new set of apps

GitHub → **Actions** → **App builds** → **Run workflow**, type a version (e.g. `1.0.1`) and
press the green button. About an hour later the downloads are at
**Releases → Frogs-N-Logs 1.0.1**. (Pushing a tag like `v1.0.1` does the same.)

## What you get

| Platform | File | Notes |
|---|---|---|
| Android | `Frogs-N-Logs-Android.apk` | 64-bit, installs directly (allow "install unknown apps" once) |
| Windows | `Frogs-N-Logs-Windows.zip` | Unsigned: "Windows protected your PC" → More info → Run anyway |
| Mac | `Frogs-N-Logs-Mac.zip` | One app for Apple silicon + Intel; ad-hoc signed, so right-click → Open the first time |
| Linux | `Frogs-N-Logs-Linux.tar.gz` | |
| iOS | `Frogs-N-Logs-iOS-Xcode.zip` | Xcode project; needs an Apple Developer account to sign |

## Frame rate

Pause → Graphics → **Frame rate**: 30 (battery saver), 60, or **Match screen** (default).
In the apps, Match screen runs at the screen's own rate: 120 FPS on ProMotion iPads and
120 Hz Android phones, 144/240 on fast PC monitors. On the web, the browser decides
(iPhone/iPad Safari usually holds pages to 60). The frog's movement is interpolated
between physics steps so high frame rates look smooth.

## App details

- App ID: `com.freddietindlecowgill.frogsnlogs` · Publisher: Freddie Tindle-Cowgill
- Icon: `Assets/Art/Icon/AppIcon.png` (the new cartoon frog, rendered in Blender)
- Landscape only; menus stay clear of phone notches (`SafeAreaFitter.cs`)
- Privacy policy (stores need a link): https://rfcowgilliv.github.io/Frogs-N-Logs/privacy.html
- Build script: `Assets/Editor/AppBuilds.cs`; workflow: `.github/workflows/apps.yml`

## Later (when ready)

- **iPhone/iPad:** with an Apple Developer account, add App Store Connect API key secrets
  so the workflow can sign the app and send it to TestFlight (Mac build machine, free for
  public repos). Also add `CADisableMinimumFrameDurationOnPhone` for 120 Hz on iPhone Pro.
- **Mac:** the same Apple account can notarize the Mac app (no more right-click → Open).
- **Google Play:** $25 one-time account, an upload key, and Android App Bundle (.aab) builds.
- **itch.io:** a free page for Windows/Mac/Linux/Android downloads (needs an itch.io account).
