# Pepper Mod

A small Vintage Story code/content mod for perennial pepper plants.

## Status

Work in progress. The pepper plant wiring and custom growth behavior are built out, and the remaining main task is replacing placeholder models in VSMC2.

## Current Content

- Pepper plants with 8 initial growth stages, 2 mature regrowth stages, and 1 dormant winter stage
- Seeds for each pepper type
- Fresh vegetable items for each pepper type
- Right-click harvest on ripe plants without breaking the plant
- Seasonal dormancy when temperatures are outside the growing range
- Rare wild pepper plant patches in warm climates
- Placeholder textures/icons for each pepper type

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

Zip the contents of this folder and place the zip in your Vintage Story `Mods` folder. This is now a code mod, so `modinfo.json` uses `"type": "code"` and the C# source lives in `src`.

## VSMC2 Workflow

Each plant is wired to load one shape per stage:

`assets/peppermod/shapes/block/plant/crop/{pepper}/stage{stage}.json`

Open the stage file you want in VSMC2, replace the placeholder geometry with your own model, and keep the texture keys named `stem`, `leaf`, `flower`, and `pepper` unless you also update the plant block JSON.

See `docs/pepper-vsmc2-guide.md` for the stage plan and file map.

## Roadmap

- Replace placeholder pepper growth and dormant stage models
- Test planting, wild spawning, seasonal growth, right-click harvest, dormancy, and food item behavior in-game
- Add cooking, drying, and spice recipes
