# Pepper Mod

A small Vintage Story code/content mod for perennial pepper plants.

## Status

Work in progress. Version 0.2.0 adds complete habanero plant and item models alongside the finished jalapenos. Both varieties have 11 plant states, picked fruit models, seeds, perennial growth, and spice effects. The new habanero artwork and gameplay integration have automated checks but still need a native in-game playtest. The other six varieties remain unfinished.

## Current Content

- Pepper plants with 8 initial growth stages, 2 mature regrowth stages, and 1 dormant winter stage
- Seeds for each pepper type
- Fresh vegetable items for each pepper type
- Hold right-click for 1.5 seconds to harvest ripe plants without breaking them; peppers fall to the ground nearby
- Seasonal dormancy when temperatures are outside the growing range
- Rare wild jalapeno patches and rarer habanero patches in any biome with suitable soil
- Complete jalapeno and habanero growth, dormant, harvested, and regrowing models
- Matching 3D jalapeno and orange habanero fruit for inventory, held, and dropped items
- Habanero fruit progresses from green to orange on a broad, leafy bush
- Placeholder models for the six remaining pepper types

Current pepper types:

```text
jalapeno
habanero
serrano
cayenne
poblano
bell-pepper
banana-pepper
ghost-pepper
```

## Install For Testing

Only jalapenos and habaneros currently spawn wild, in any biome on suitable soil in newly generated terrain.
The remaining six varieties' wild spawning is disabled for now; their items and existing plants remain intact.
Existing terrain is not repopulated. Cold-weather dormancy and growing-season
temperature requirements still apply.

Build and install the compiled test package with:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Build-PepperMod.ps1 -VintageStory "E:\Vintagestory" -Install
```

This is a code mod, so the release zip needs the compiled `peppermod.dll` at the root of the package. The build script creates `peppermod-0.2.0.zip` and copies it into your Vintage Story `Mods` folder when `-Install` is used. Keep only one version of Pepper Mod in that folder; move the old version outside it before testing this update.

## Spice Effects

Raw jalapenos and habaneros provide 20 satiety instead of 80. Each completed bite adds spice:
banana pepper 10, poblano 15, jalapeno 25, serrano 40, cayenne 50, habanero 50,
and ghost pepper 100. Bell peppers add no spice. The remaining varieties retain 80 satiety.
Two habaneros eaten back-to-back fill the spice meter from empty.

Spice is capped at 100 and appears in a three-segment HUD near the lower-right:

- Mild: above 0 and below 34, with no gameplay effect.
- Hot: 34 to below 67, gently warming the player's body.
- Extreme: 67 to 100, retaining warmth, adding a slowly pulsing red edge tint,
  and draining 0.5 satiety per real-time second (1 point every 2 seconds).

The additional hunger drain stops below Extreme, does not change nutrition
levels, and does not apply in Creative or Spectator mode. Hunger cannot fall
below zero; ordinary starvation rules still apply when it is empty.

Spice never applies health damage directly or fires damage events. Warming adds up to 0.12
degrees per real-time second, capped at 2 degrees above normal body temperature.
It stops below Hot; ordinary temperature simulation then controls cooling.

After five seconds without another spicy bite, spice decays by one point per
real-time second. The HUD disappears at zero. Spice pauses while offline, is
saved with the player, and clears on death. The effect currently applies to raw
pepper items; cooked dishes do not yet inherit spice from their ingredients.

## VSMC2 Workflow

Each plant is wired to load one shape per stage:

`assets/peppermod/shapes/block/plant/crop/{pepper}/stage{stage}.json`

Open the stage file you want in VSMC2, replace the placeholder geometry with your own model, and keep the texture keys named `stem`, `leaf`, `flower`, and `pepper` unless you also update the plant block JSON.

The active jalapeno and habanero files contain complete bush models. Habanero also
uses `peppergreen` for unripe fruit, mapped to the existing green pepper skin.
The separate
`jalapeno-candidate` folder and interactive previews are retained for reference
but excluded from the game package. Previous handmade stages are preserved in
the local, Git-ignored `backups` folder.

See `docs/pepper-vsmc2-guide.md` for the stage plan and file map.

## Regression Tests

The regression tests use the installed game's assemblies and assets and require no additional test packages:

```powershell
$env:VINTAGE_STORY = "E:\Vintagestory"
dotnet run --project .\tests\PepperMod.Tests\PepperMod.Tests.csproj -c Release
```

They cover hold timing, cancellation, changed targets, concurrent players, claims,
seasonal restrictions, harvest yield, downward-only item drops, both finished varieties' hand
placement, completed bites, spice tiers and cooldowns, body warming, no-damage
behavior, per-player state, habanero model/texture wiring, connected fruit and leaves,
mature canopy consistency, and ripening colors. Native in-game testing is still needed for final
HUD appearance, animation, and interaction feel.

## Roadmap

- Replace the remaining varieties' placeholder growth and dormant stage models
- Test planting, wild spawning, seasonal growth, right-click harvest, dormancy, and food item behavior in-game
- Add cooking, drying, and spice recipes
