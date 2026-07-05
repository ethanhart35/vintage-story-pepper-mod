# Pepper Mod

A Vintage Story content mod for growable pepper crops.

## Status

Work in progress. The first crop being built is jalapeno.

## Current Content

- Fresh jalapeno item: `peppermod:vegetable-jalapeno`
- Jalapeno seeds: `peppermod:seeds-jalapeno`
- Jalapeno crop stages: `peppermod:crop-jalapeno-1` through `peppermod:crop-jalapeno-8`

## Install For Testing

Copy this folder into your Vintage Story `Mods` folder, or zip the contents of this folder and place the zip in `Mods`.

## VSMC2 Workflow

The crop is already wired to load one shape per stage:

`assets/peppermod/shapes/block/plant/crop/jalapeno/stage1.json`

Start with `stage1.json` for the sprout. Open it in VSMC2, replace the placeholder geometry with your own sprout model, and keep the texture keys named `stem` and `leaf` unless you also update the crop block JSON.

See `docs/jalapeno-vsmc2-guide.md` for the stage plan and file map.

## Roadmap

- Finish jalapeno growth stage models
- Test planting, growth, drops, and food item behavior in-game
- Add more pepper varieties
- Add cooking, drying, and spice recipes
