# Frogs-N-Logs — November 2026 game update

Everything from the to-do list, in three rounds. Each round was checked by the automated
play tests on GitHub before going live at **https://rfcowgilliv.github.io/Frogs-N-Logs/**.

## Round 1: feel, speed and menus
| What | How it works | Code |
|---|---|---|
| **Lag fix** | The frog moves at a steady speed the moment you press a key and stops when you let go. (In 2017 it only moved when its hop animation pushed it, and finished each hop before stopping.) | `PlayerMovement.cs`, `Animators/PlayerController.controller` |
| **Graphics quality** | Auto / Low / Medium / High. Auto watches the frame rate for ~5 s of play and picks for your device. Low = simple water, no shadows or post effects; Medium = small reflection, short shadows; High = the full 2017 look. | `GraphicsManager.cs` |
| **Sharper-but-lighter on Retina** | iPads/Macs were drawing 4x the pixels. The page caps the pixel ratio at 1.25x when it loads. | `.github/scripts/web-fullscreen.py` |
| **Smaller download** | Web textures capped at 1024 px (masks 512), mono sound effects, code stripping, unused packages removed (in-app purchases, analytics, collab, timeline). Scenery is batched. | `Editor/BuildOptimizations.cs` |
| **Flies are reused** | Eaten flies hide and pop up elsewhere instead of being destroyed and re-created (no mid-game hitches). | `FlySpawner.cs`, `FlyPickUp.cs` |
| **Title screen** | How to play with keyboard **and** touch/mouse, difficulty, best score, Play. | `FrogsUI.cs` |
| **Difficulty** | Easy (slower bird, more flies, lower goals) / Medium (classic) / Hard (faster bird, fewer flies, higher goals). Separate best score for each. | `GameSettings.cs` (`DifficultyRules`) |
| **Pause menu** | Esc, P, gamepad Start or the II button. Tabs: Shop, Sound, Graphics, Controls, Game. Auto-pauses if the browser tab loses focus. | `FrogsUI.cs` |
| **Sound** | Master, Music & swamp sounds, Sound effects: sliders plus on/off. | `AudioManager.cs` |
| **Filters** | Retro, B&W, Night, Cartoon, Colour-blind friendly, Vintage. | `ScreenFilter.cs`, `Resources/FrogsFilters.shader` |
| **Key remapping** | Change up/down/left/right/pause/laser keys. Arrow keys always work too. | `KeyBindings.cs` |
| **FPS counter** | Graphics tab > Show frame rate. | `GraphicsManager.cs` |
| **Loading screen** | Frogs-N-Logs branded, with a green progress bar. | `web-fullscreen.py` |

## Round 2: levels, hazards, power-ups
| What | How it works | Code |
|---|---|---|
| **Levels** | The Pond → The Swamp → Night, changing in place (no loading). Fly goals: Easy 8/18, Medium 10/24, Hard 12/28. | `LevelManager.cs` |
| **Snake** | Wakes up in the Swamp, chases when you're within ~11 m, slower than the bird. | `SnakeHazard.cs` |
| **Drifting logs** | Roll across the Swamp and Night. They shove the frog, and the bird must go around them. | `DriftingLog.cs`, `HazardSpawner.cs` |
| **Faster bird** | 15% faster at Night, +8% every 10 flies after that (up to 1.5x). | `LevelManager.cs` |
| **Golden fly** | Rare, +5 (10 with Double flies), gone in 8 s. | `PowerUpSpawner.cs` |
| **Speed boost** | Blue orb: 60% faster for 6 s. | `PowerUps.cs` |
| **Lily-pad shield** | Blocks one catch, knocks the attacker dizzy, 1.5 s safe time. | `PowerUps.cs`, `PlayerHealth.cs` |

## Round 3: shop and extras
| What | How it works | Code |
|---|---|---|
| **Fly wallet** | Every fly eaten is also shop money. Scores are never spent. | `Shop.cs` |
| **Shop** (Pause > Shop) | Strong legs ×3 (+8% each), Fly magnet ×3 (3.5/5.5/8 m), Double flies, Laser eyes, 4 skins, top hat, crown. | `Shop.cs`, `GameDirector.cs` |
| **Laser eyes** | L / gamepad X / ZAP button: stuns the bird (or snake) within 18 m for 4 s, recharges in 20 s. | `GameDirector.cs` |
| **Achievements** | 8 goals, listed in Pause > Game, with pop-ups. | `Shop.cs` (`Achievements`) |
| **Tutorial** | Tips on your first round. | `GameDirector.cs` |
| **Effects** | Screen shake when caught, a home-made "ribbit" (generated in code). | `GameDirector.cs` |
| **Gamepad** | Left stick moves, A starts, Start pauses, X fires the laser. | `KeyBindings.cs`, `ProjectSettings/InputManager.asset` |

## Saved between visits
Difficulty, volumes, graphics, filter, key bindings, best scores, fly wallet, upgrades, skin,
hat, achievements, and whether you've seen the tutorial (`PlayerPrefs`, stored in the browser).

## Play tests
`Assets/Tests/PlayMode/FrogsPlayTests.cs` now covers the title screen, responsiveness, pause,
difficulty, saved settings, levels/hazards, every power-up, the shop, magnet, laser and hats,
on top of the original level/start/fly/Game Over checks.
