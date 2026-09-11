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

The active jalapeno, habanero, and serrano folders contain complete 11-stage bush models. Habanero has a broader canopy, wider leaves, and lobed fruit that ripens from green to orange. Serrano has a taller, narrower canopy and slender green fruit. The other five folders contain placeholder models. Earlier handmade jalapeno models and replaced habanero placeholders are preserved in the local `backups` folder.

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

For jalapeno, habanero, and serrano, stages 6, 7, 8, 10, and 11 share the same mature branches and leaf canopy. Stage 9 keeps the mature branches but removes leaves and fruit. Stage 10 has no peppers, and stage 11 grows small peppers at the stage 8 attachment points. Other varieties still use placeholder geometry.

## Growth Behavior

The pepper plants now use custom code instead of the vanilla one-shot crop behavior.

- Seeds place stage 1 plants.
- Stages 1 through 8 advance only during growing temperatures.
- Hold right-click on stage 8 for 1.5 seconds to harvest. Releasing early or changing targets cancels the hold. Peppers fall close to the plant, then it moves to stage 10 and 11 while peppers regrow on the mature plant.
- Outside the growing temperature range, the plant switches to stage 9 dormant.
- When growing temperatures return, stage 9 wakes back up at its saved young stage, or stage 10 for plants that had reached the full-size canopy at stage 6 or later.

Current growth timing:

```text
stage 1 -> 5: about 31 in-game hours per step
stage 5 -> 8: about 24 in-game hours per step
stage 10 -> 11 -> 8 after harvest: about 24 in-game hours per step
growing temperature range: 8C to 38C
harvest yield: 16 to 24 peppers, with a small seed chance
```

## Wild Spawning

Wild pepper plants generate as rare block patches in newly generated terrain in any biome with suitable soil.

- Jalapeno: rare patches
- Habanero: very rare patches
- Serrano: rare patches, less common than jalapenos
- Other varieties: wild spawning disabled until their artwork is complete

Wild patches use stages 4 through 8. Stage 8 plants use the same hold-to-harvest interaction, and breaking any wild plant can return seeds.

With Biomes installed, regional restrictions also apply. Jalapenos and serranos use Pacific Nearctic, Pacific Neotropic, and Atlantic Neotropic; habaneros use Atlantic Neotropic. These restrictions do not affect player planting.

## Texture Keys

Keep these texture keys in every model:

```text
stem
leaf
flower
pepper
```

Assign faces to `#stem`, `#leaf`, `#flower`, or `#pepper` in VSMC2. The crop JSON maps those keys to each pepper's texture folder. Habanero additionally uses `#peppergreen`, mapped to the jalapeno skin atlas, for unripe fruit in stages 6, 7, and 11.

For harvested pepper item models, use this texture key:

```text
base
```

For the completed jalapeno, habanero, and serrano items, assign skin faces to `#base` and cap/stem faces to `#stem`. These reuse `textures/block/plant/{pepper}/pepper.png` and `stem.png` so the harvested fruit matches the plant. Other pepper items still map `#base` to `textures/item/food/vegetable/{pepper}.png`.

## Pepper Item Models

The jalapeno item has a complete tapered 3D fruit model. Habanero has a wider, lobed orange fruit. Serrano is slimmer with a gently curved tip. All three have a green calyx and bent stem. The other five item models remain placeholders.

Prepared models live in `shapes/item/food/prepared/{baked|dried}/{pepper}.json`, and bundles in `shapes/item/food/bundle/{raw|baked|dried}/{pepper}.json`. Baking changes skin UVs without rearranging the geometry. Hanging variants reuse bundle shapes with a separate ceiling-hook overlay.

Keep the hidden root element and keep your visible item elements attached with `stepParentName`, just like the crop stage files.

Use the in-game `.tfedit` tool to fine-tune how an item appears in the GUI, on the ground, and in first/third person hands. The vegetable item JSON now uses per-variety transforms: jalapeno has its own settings, while the `*` entries preserve the other items' starter values.

## VSMC2 Copy Fix

Each placeholder file includes a hidden root element and `stepParentName` values on the visible elements. That is intentional. It prevents the VSMC2 copy button from crashing on these plant models.

When replacing geometry, keep one root element in the file and keep visible elements attached to it with `stepParentName`.

## Wind Sway

The stage files include per-face `windMode` values so the plants sway in the wind:

```text
stem -> NoWind
leaf -> anchored base vertices with ExtraWeakWind tips
flower -> NoWind in the approved jalapeno models
pepper -> NoWind
```

Leaf faces use mixed `windMode` arrays such as `0,7,7,0` in the approved jalapeno models, where `0` means pinned and `7` means ExtraWeakWind. The vertex order depends on the face orientation. This keeps the branch/base side steadier while the outer edge moves. Other varieties may also use mixed wind flags on their placeholder flowers.

If you add brand-new leaf or flower elements in VSMC2 and they float too much, set the two vertices closest to the stem/branch to `0` and leave only the two outside vertices at `7`.

## Random Orientation

Pepper crop blocks use rotated shape alternates at 0, 90, 180, and 270 degrees. This keeps the same block codes and model files while making placed plants vary their facing direction in-game.
