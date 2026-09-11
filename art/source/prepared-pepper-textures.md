# Prepared Pepper Textures

`assets/peppermod/textures/item/food/prepared/pepper-skins.png` is an AI-generated
raster material atlas created for this mod. It contains baked green, baked orange,
dried green, and dried orange pepper skins in four equal quadrants. The prepared
models reuse the existing fruit geometry with adjusted skin UVs and original stems.

Generation direction: a flat hand-painted voxel-game material sheet, no objects,
text, borders, lighting gradients, or perspective. Upper-left: muted olive-green
baked pepper skin with small toasted blisters. Upper-right: golden orange baked
skin with browned blisters. Lower-left: dark olive and rust dried skin with fine
leathery vertical wrinkles. Lower-right: russet red-orange dried skin with leathery
wrinkles. Exact equal quadrants, edge-to-edge opaque material, subtle color variation.

The generated atlas was visually reviewed and exported at 1024x1024 for runtime use.
Its original 1254-pixel width triggered a row-copy bug in Vintage Story 1.22.3's
texture atlas loader, corrupting pixels and leaving transparent gaps. The runtime
export preserves the artwork and quadrant layout while using a width divisible by
four. A regression test loads the actual PNG through the game's bitmap decoder
and checks every pixel after insertion into the game's texture atlas.
Prepared shapes reference it directly with padded quadrant UVs. No existing raw
texture or handmade model was changed.

Habanero now uses its dedicated atlas at
`assets/peppermod/textures/block/plant/habanero/pepper.png`, including the new
Charred Habanero skin. Other varieties continue using the shared prepared atlas.
See [Habanero Fruit Rework](habanero-fruit-rework.md) for the current artwork,
processing details, and preservation checks.
