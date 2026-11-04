# Art & audio upgrade (Nov 2026)

What changed, in plain words:

- **New renderer.** The game now runs on Unity's Universal Render Pipeline (URP 17.3) instead
  of the old built-in renderer. `Assets/Editor/FrogsURPSetup.cs` creates the pipeline assets,
  converts every material and fixes the scene; CI runs it automatically.
- **Cartoon look.** A toon shader (`Resources/FrogsToon.shader`) with soft banded light and a
  rim glow, cartoon water, and a gentle "look" (bloom, colour grading, vignette) set up by
  `Stylizer.cs`. Each level tints the look: green for the Swamp, cool blue for Night.
- **Screen filters** are now a URP renderer feature (`FrogsFilterFeature.cs`) using Render Graph.
- **New characters.** An animated cartoon frog and eagle replace the 2017 models
  (`CharacterVisual.cs` swaps them in and plays walk / idle / flying clips).
- **New scenery.** Trees, bushes, flowers, mushrooms and rocks from Quaternius's Stylized
  Nature MegaKit (`SceneDressing.cs`). On Low graphics, half of the small plants are hidden.
- **New music and sounds.** Four Kevin MacLeod tracks (one per level plus the title) stream in
  after the game loads, so the first download stays small. New CC0 ribbits, gulps, clicks,
  jingles, zaps and night crickets (`AudioDirector.cs`).
- **Credits.** A Credits button on the title screen, and the credits roll when you survive the
  Night (reach the final fly goal: Easy 30, Medium 40, Hard 48). You can keep playing after.
  Achievement: *Night survivor*.
- **Cleanup.** The unused 2017 Standard Assets and Post-Processing v1 folders are gone (the few
  pieces still used live in `Assets/Art/Legacy`). Packages are updated to their latest versions.

Everything used is free: CC0 models and sounds, and CC BY 4.0 music with attribution
(see [CREDITS.md](CREDITS.md)).

Later idea: a "realistic nature" skin as an alternative to the cartoon look.
