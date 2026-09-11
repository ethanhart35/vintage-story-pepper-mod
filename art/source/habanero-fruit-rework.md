# Habanero Fruit Rework

Completed September 11, 2026. This is a visual update, not a balance change.

## Models

- Replaced the fruit and calyx in stages 6, 7, 8, and 11. All nonfruit elements are preserved from the previous models, including branches, leaves, flowers, petioles, stalks, and wind settings. Stages 1-5, 9, and 10 are unchanged.
- Rebuilt the picked Habanero as a shorter, full-bodied lantern with broad, uneven shoulders, four bent longitudinal folds, and a broad, staggered lower end. The September 11 refinement removes the previous conical tip while retaining its original bent stem and hand transforms.
- The raw, charred, and dried loose items share identical geometry. Bundles use eight scaled and rotated copies, with each stem endpoint pinned to the central cord. The cord, hanging loop, and ceiling hook are unchanged.
- Oven transforms and hanging selection bounds were recalculated for the new fruit dimensions. Recipes, yields, seasonal behavior, spice, Scoville values, and preservation settings are unchanged.
- Other varieties and all seed artwork are unaffected by this rework.

The first shape direction was informed by [Stokes Seeds' orange Habanero description](https://www.stokeseeds.com/products/habanero-orange-hot-pepper-seed). The blunter revision was based on direct visual inspection of [Bohica Pepper Hut's orange Habanero photographs](https://bohicapepperhut.com/products/habanero-orange-seeds): fuller lower bodies and blunt, irregular ends. The photos were used for shape reference only, never as game textures.

Original models, definitions, and texture are backed up locally under `backups/habanero-before-2026-09-10/`. Backups and scratch modeling scripts are not packaged or committed.

The preceding pointed revision and package are also preserved under `backups/habanero-before-charred-2026-09-11/`. Models were edited directly as native shape JSON; browser control was used only to inspect the reference photographs, not to operate VSMC2.

## Texture

Final runtime asset: `assets/peppermod/textures/block/plant/habanero/pepper.png`.

One dedicated opaque 1024x1024 atlas, shared by the Habanero plant, loose items, and bundles:

| Quarter | Surface |
| --- | --- |
| Upper left | Green unripe skin |
| Upper right | Bright orange ripe skin |
| Lower left | Blackened, blistered charred skin |
| Lower right | Rusty red-orange dried skin |

UVs stay inside padded quarters. Material scale follows each cuboid's face dimensions to avoid stretched grain. Runtime texture overrides are Habanero-specific; the shared prepared-pepper atlas and other varieties' mappings are unchanged.

Generation mode: built-in image generation, no CLI/API fallback. The source was generated September 10 as `exec-59cd0988-9211-4880-84db-cd2b7e1c1bd1.png` in the Codex generated-images directory. The user explicitly approved resizing on September 11. Sharp resized the original 1254x1254 RGB image to 1024x1024 with Lanczos3, without recoloring or replacing the artwork.

### Initial Atlas Prompt

```text
Use case: stylized-concept.
Asset type: one opaque square 2-by-2 texture atlas for the skin of low-poly habanero peppers in a Vintage Story mod. This is flat material color/albedo, NOT an illustration of peppers.
Primary request: exactly four equally sized square material swatches filling the image edge-to-edge with no gutters: upper-left fresh unripe leaf-green habanero skin, upper-right fresh ripe vivid tangerine-orange habanero skin, lower-left baked habanero warm deeper orange with a few subtle toasted freckles, lower-right dried habanero deep rusty red-orange with fine irregular leathery creases.
Style: restrained hand-painted game texture, small painterly pixel-like marks at a medium resolution, natural irregular soft mottling; understated handcrafted Vintage Story-compatible material detail. Fresh swatches smooth waxy skin with very subtle color differences and sparse fine pores. Dried swatch a little more wrinkled but still orange-red, never purple.
Lighting: uniformly illuminated flat albedo; no directional lighting, no cast shadows, no large specular highlights, no vignettes. Each swatch should be nearly uniform overall color, interesting subtle fine detail only.
Constraints: image size 1024 by 1024. Four equal quarters, with boundaries exactly halfway across and halfway down. Fully opaque entire image. No transparent margins. No objects, silhouettes, stems, leaves, seeds, fruit illustrations, words, labels, borders, dividers, checkerboards, gradients, wood grain, vertical stripes, horizontal stripes, grooves, black char marks or dramatic shadows. Do not paint the 3D shape; the game's geometry supplies that.
```

## Charred Texture Revision

On September 11 the user approved generating the charred skin separately, resizing it, and replacing only the lower-left quarter of the atlas. Built-in image generation produced `exec-16cf9ce9-d12f-4dbf-b005-76e318ca74d8.png`. Sharp resized it to 512x512 using Lanczos3 and composited it at pixel (0, 512) into the previous 1024x1024 sheet. Raw RGB comparisons confirmed that every pixel in the fresh, green, and dried quarters is unchanged. The final asset remains the single opaque runtime texture listed above.

The cooked Habanero item and both bundle forms now display Charred Habanero names. Internal `baked` variant IDs remain unchanged to preserve existing items, recipes, and saves. Cooking still progresses from raw to charred to dried; no temperature, duration, Scoville, spice, or spoilage balance was changed. Other varieties retain Baked names and their existing textures.

### Charred Skin Prompt

```text
Use case: stylized-concept.
Asset type: a single square fully opaque albedo texture of CHARRED ORANGE HABANERO PEPPER SKIN for a Vintage Story low-poly food model.
Primary request: a seamless-looking flat closeup material swatch, covering the entire square, of orange habanero skin after fire roasting. The cooked surface must read immediately as CHARRED: irregular matte charcoal-black and very dark brown blistered patches covering about 45 percent of the area, some dark toasted amber edges, and still-recognizable saturated roasted orange skin exposed between them. Use varied medium and small irregular patches distributed across the entire image, including the center and edges, with some tiny wrinkles and blisters. NOT solid black ash; NOT only brown freckles. Do not depict a whole pepper.
Style: restrained hand-painted game albedo texture with fine painterly marks, natural irregular detail, Vintage Story-compatible handcrafted material. Char is uneven organic burned skin, not neat dots, stripes, bricks, stones, holes, cracks or camouflage shapes.
Lighting: completely flat diffuse illumination. No shadows from geometry, no shine, no perspective or directional shading. The game's mesh provides the form.
Composition: edge-to-edge material, every pixel filled, all regions the same material at the same scale. One material only, no grids or quadrants. No words, stems, leaves, plate, fire, smoke, background, frame, border, watermark or checkerboard. Fully opaque. Requested dimensions 1024 by 1024.
```

### Pixel-Painted Charred Skin

The first charred revision above was rejected as too photorealistic. Its source is
retained for provenance, but it is no longer the runtime artwork.

The replacement uses the existing shared prepared-pepper atlas as a style reference:
flat pixel-like marks, warm toasted orange, dark brown scorch clusters, and no
realistic creases, shine, or raised blister detail. Built-in image generation
produced `exec-3cdf217f-b27f-4e3b-a50b-24b869dca537.png`; the approved quarter-only
workflow resized it to 512x512 with nearest-neighbor sampling and inserted it at
(0, 512) in the existing atlas. The final runtime path is unchanged.

Pixel comparisons confirm that the other three quarters remain identical. Hashes
of all 193 other runtime asset files also remain unchanged. No geometry, UVs,
balance settings, recipes, or names were edited in this texture-only correction.
Dark scorch pixels cover approximately 20.4% of the new quarter. All 105 regression
tests pass, and the loose-pepper and bundle previews were refreshed and checked.

#### Replacement Prompt

```text
Use case: stylized-concept.
Asset type: a single flat opaque square game albedo texture, CHARRED ORANGE PEPPER SKIN.
Input image 1: STYLE REFERENCE ONLY. This is the existing four-quarter cooked-pepper texture atlas from the user's Vintage Story mod. Match its understated pixel-painted material style, especially the upper-right orange quarter. Do not copy the four-quarter layout.
Primary request: create ONE orange charred-pepper material swatch filling the entire image. Muted ochre-orange and warm toasted orange base, scattered irregular dark brown to charcoal scorch patches covering about 25 percent of the material. The patches should read as burns but remain flat painted color, not raised blisters or 3D crevices.
Style: deliberately low-resolution voxel-game pixel art. Think a 64 by 64 pixel texture enlarged with nearest-neighbor: clearly defined simple square pixel clusters, limited palette of about 8 earthy colors, broad quiet areas, a little understated vertical pixel mottling like the reference. Orange remains the dominant color. Sparse medium scorch clusters distributed across the whole square, not a polka-dot pattern. Simpler and less busy than the reference.
Critical constraints: NO PHOTOGRAPHIC DETAIL. No pores, wrinkles, flakes, cracks, glossy highlights, bump mapping, realistic skin, specular reflections, ambient occlusion, tiny noise, soft gradients or painterly brush hairs. All shading comes from the game mesh, not this texture. Burn patches are matte dark umber with some charcoal centers, not glowing orange lava or stone camouflage.
Composition: exactly one material, edge-to-edge, square, every pixel opaque. No illustrated peppers, objects, backgrounds, stems, leaves, text, labels, borders, grid lines, quadrants or checkerboard. Do not reproduce the reference atlas. Requested output 1024 by 1024 with coarse pixel-art artwork.
```

## Verification

The regression suite exercises Vintage Story 1.22.3's native shape transforms, hand placement, oven bounds, bundle attachments, and texture-atlas loader. The skin is checked pixel-for-pixel after atlas insertion, including opacity. Dedicated checks guard the full-bodied proportions, broad lower end, visible charcoal blistering, correct color quarters, localized Charred names, stable saved item IDs, and unchanged fallback textures for other varieties.

Offline previews use the native shape matrices, with desktop/mobile, rotation, and nonblank canvas checks. A native in-game visual playtest is still required; preview lighting is not identical to the game's lighting.
