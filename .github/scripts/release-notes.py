"""Release notes for the App builds workflow (added Nov 2026): how to install on each device."""
import sys

version = sys.argv[1] if len(sys.argv) > 1 else "1.0.0"
failed = (sys.argv[2] if len(sys.argv) > 2 else "").split()

print(f"""# Frogs-N-Logs {version}

Play in your browser any time: https://rfcowgilliv.github.io/Frogs-N-Logs/

These are free test builds. They aren't in any app store yet, so your device will ask you to
confirm the first time you open them.

| Device | Download | How to install |
|---|---|---|
| **Android** phone or tablet | `Frogs-N-Logs-Android.apk` | Open the file on the device. If asked, allow your browser or Files app to "install unknown apps", then tap Install. |
| **Windows** PC | `Frogs-N-Logs-Windows.zip` | Unzip, then run `Frogs-N-Logs.exe`. If Windows shows "Windows protected your PC", click **More info > Run anyway**. |
| **Mac** (Apple silicon or Intel) | `Frogs-N-Logs-Mac.zip` | Unzip, drag the app to Applications, then **right-click > Open** the first time (or System Settings > Privacy & Security > Open Anyway). |
| **Linux** | `Frogs-N-Logs-Linux.tar.gz` | Extract, then run `./Frogs-N-Logs.x86_64`. |
| **iPhone / iPad** | `Frogs-N-Logs-iOS-Xcode.zip` | An Xcode project, for signing with an Apple Developer account (TestFlight / App Store). |

Apps can run faster than the web version: in Pause > Graphics, choose **Frame rate > Match screen**
for 120 FPS on 120 Hz screens (or 144/240 on fast monitors).

Credits for the art, music and sounds: see CREDITS.md, or Credits on the title screen.
""")
if failed:
    print(f"\n> Not in this release (build failed): {', '.join(failed)}")
