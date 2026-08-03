# Pepper VSMC2 Guide

## Files To Edit

Every pepper crop uses this shape path:

```text
assets/peppermod/shapes/block/plant/crop/{pepper}/stage{stage}.json
```

For example:

```text
crop-jalapeno-1 -> shapes/block/plant/crop/jalapeno/stage1.json
crop-habanero-8 -> shapes/block/plant/crop/habanero/stage8.json
crop-bell-pepper-6 -> shapes/block/plant/crop/bell-pepper/stage6.json
crop-jalapeno-9 -> shapes/block/plant/crop/jalapeno/stage9.json
crop-jalapeno-10 -> shapes/block/plant/crop/jalapeno/stage10.json
crop-jalapeno-11 -> shapes/block/plant/crop/jalapeno/stage11.json
```

Every harvested pepper item uses this shape path:

```text
assets/peppermod/shapes/item/food/vegetable/{pepper}.json
```

For example:

```text
vegetable-jalapeno -> shapes/item/food/vegetable/jalapeno.json
vegetable-habanero -> shapes/item/food/vegetable/habanero.json
```

## Pepper Folders

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

The jalapeno files contain your current in-progress models. The other pepper folders contain simple placeholder models so the mod has valid files until you replace them in VSMC2.

## Suggested Visual Stages

1. Tiny sprout with two small leaves
2. Short seedling with more leaf mass
3. Young leafy plant
4. Taller bush, no fruit yet
5. Fuller plant with small white flowers
6. First tiny peppers
7. More visible peppers
8. Ripe harvest-ready plant
9. Dormant winter plant
10. Mature plant after harvest, same size as stage 8 but with harvested pepper spots
11. Mature plant with peppers regrowing in the stage 8 pepper positions

Stage 9 currently starts as a copy of stage 4 so the mod has valid files. Stages 10 and 11 currently start as copies of stage 8 so the plant does not shrink after harvest. Replace those later with mature models that keep the same stem and leaf structure as stage 8 while changing only the peppers.

## Growth Behavior

The pepper plants now use custom code instead of the vanilla one-shot crop behavior.

- Seeds place stage 1 plants.
- Stages 1 through 8 advance only during growing temperatures.
- Stage 8 is harvested by right-clicking the plant, then it moves to stage 10 and 11 while peppers regrow on the mature plant.
- Outside the growing temperature range, the plant switches to stage 9 dormant.
- When growing temperatures return, stage 9 wakes back up at its saved young stage, or stage 4 for mature plants.

Current growth timing:

```text
stage 1 -> 8: about 31 in-game hours per stage
stage 10 -> 11 -> 8 after harvest: about 24 in-game hours per step
growing temperature range: 8C to 38C
harvest yield: 16 to 24 peppers, with a small seed chance
```

## Wild Spawning

Wild pepper plants generate as rare block patches in new warm-climate chunks.

- Jalapeno and serrano: warm fertile regions
- Bell pepper, banana pepper, and poblano: warm fertile regions
- Cayenne: hotter and somewhat drier regions
- Habanero and ghost pepper: very rare hot, wetter regions

Wild patches use stages 4 through 8. Stage 8 plants can be right-click harvested, and breaking any wild plant can return seeds.

## Texture Keys

Keep these texture keys in every model:

```text
stem
leaf
flower
pepper
```

Assign faces to `#stem`, `#leaf`, `#flower`, or `#pepper` in VSMC2. The crop JSON maps those keys to each pepper's texture folder.

For harvested pepper item models, use this texture key:

```text
base
```

Assign faces to `#base`. The vegetable item JSON maps that key to `textures/item/food/vegetable/{pepper}.png`.

## Pepper Item Models

The item model placeholders are only there so the peppers render as 3D held/eaten items instead of flat icons. Replace the placeholder body, tip, and stem pieces with your own pepper model in VSMC2.

Keep the hidden root element and keep your visible item elements attached with `stepParentName`, just like the crop stage files.

After the model looks right, use the in-game `.tfedit` tool to fine-tune how it appears in the GUI, on the ground, and in first/third person hands. The current transforms are starter values.

## VSMC2 Copy Fix

Each placeholder file includes a hidden root element and `stepParentName` values on the visible elements. That is intentional. It prevents the VSMC2 copy button from crashing on these plant models.

When replacing geometry, keep one root element in the file and keep visible elements attached to it with `stepParentName`.

## Wind Sway

The stage files include per-face `windMode` values so the plants sway in the wind:

```text
stem -> NoWind
leaf -> anchored base vertices with ExtraWeakWind tips
flower -> anchored base vertices with ExtraWeakWind tips
pepper -> NoWind
```

Leaf and flower faces use mixed `windMode` arrays such as `0,0,7,7`, where `0` means pinned and `7` means ExtraWeakWind. This keeps the branch/base side steadier while the outer edge moves.

If you add brand-new leaf or flower elements in VSMC2 and they float too much, set the two vertices closest to the stem/branch to `0` and leave only the two outside vertices at `7`.

## Random Orientation

Pepper crop blocks use rotated shape alternates at 0, 90, 180, and 270 degrees. This keeps the same block codes and model files while making placed plants vary their facing direction in-game.
