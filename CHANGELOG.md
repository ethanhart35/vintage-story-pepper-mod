# Changelog

## Unreleased

### 0.3.1 - Biomes Compatibility

- Added optional support for Biomes 2.2.0 using its existing regional pepper rules, with no required dependency or separate compatibility download.
- Wild jalapenos can spawn in Pacific Nearctic, Pacific Neotropic, and Atlantic Neotropic. Wild habaneros can spawn in Atlantic Neotropic. Both are eligible near and away from rivers.
- Fixed missing habanero world-generation support caused by Biomes' existing rules targeting a different plant ID.
- Kept normal Pepper Mod spawning unchanged without Biomes, including rarity, suitable-soil requirements, and the disabled unfinished varieties. Player planting and seasonal growth are unchanged.
- Added tests against Biomes' released configuration loader and plant filter across all realms and river states, including asset loading order and unchanged unrelated crops.

### 0.3.0 - Habaneros, Baking & Pepper Bundles

- Added a small iron ceiling hook for hanging bundles and lowered the string loop onto its curved seat. Portable and oven models, recipes, and drying behavior are unchanged.
- Fixed individual raw, baked, and dried pepper positioning in clay ovens with flat, centered, slot-sized display transforms. Hand and inventory positioning is unchanged.
- Added ceiling placement for all six jalapeno/habanero bundle variants. Right-click an underside to hang one, then right-click the bundle to take it down. No special hook or ceiling material is required.
- Fresh and baked bundles air-dry into dried bundles after 168 in-game hours (7 days) hanging, preserving variety, pepper count, and carried-over freshness. Progress survives save/reload and pauses while carried.
- Raw and baked bundles spoil at half speed while hanging so fresh bundles can finish the seven-day drying process. Carried bundles and dried bundles retain normal spoilage rates.
- Hanging bundles continue to spoil; spoiling food cannot be rescued by drying. Missing supports and flooding drop the bundle, full inventories drop picked-up bundles, and fully rotten bundles drop two rot.
- Added regression checks for loose-pepper oven alignment and the full hanging, pickup, drying, spoilage, and persistence lifecycle.
- Fixed see-through baked/dried pepper skins on loose items and bundles by exporting the shared texture at atlas-safe dimensions. Added a pixel-for-pixel regression check using the game's texture atlas loader.
- Added raw, baked, and dried jalapeno/habanero bundles with custom eight-pepper ristra models, flax-fiber ties, hanging loops, and flat oven/dropped positioning.
- Refined all six bundle models with staggered, all-around fruit placement and direct stem-to-cord attachments instead of outward strings and paired rows.
- Added shaped recipes using one flax fiber surrounded by eight matching peppers, plus shapeless unpacking into eight peppers of the same variety and preparation. Fiber is consumed, not returned.
- Four bundles use the vanilla oven's four slots to bake or dry 32 peppers at once, without modifying the oven or other foods.
- Bundles retain the corresponding pepper shelf life and per-pepper Scoville heat. Tying and untying preserve the oldest perish timer without a crafting freshness bonus. Bundles must be unpacked before eating.
- Added an offline bundle preview and tests for recipe matching/consumption, preservation state, repeated crafting, full oven batches, attachments, and display bounds.
- Reduced raw jalapeno and habanero base freshness to 7 days (168 hours, no random variance); other varieties and prepared forms are unchanged.
- Added Scoville heat units to raw, baked, and dried pepper tooltips while preserving normal food and spoilage information.
- Added clay-oven baking from raw to baked peppers, then a longer second bake to dried peppers. Each stage halves Scoville units and spice-meter gain.
- Set baked freshness to 7 days (+/- 1) and dried freshness to 120 days, before storage modifiers. All forms still rot and use vanilla freshness carryover when baked.
- Added baked and dried skin textures and 3D item variants, preserving existing fruit geometry and held positioning. Raw habaneros still fill the spice meter in two bites.
- Added a preparation preview and regression tests exercising the installed game's oven, terminal dried state, tooltips, spice reduction, spoilage definitions, and model assets.
- Replaced all 11 habanero plant placeholders with a broad, asymmetric bush, wider leaves, flowers, and attached lobed peppers.
- Added green unripe fruit, mixed green/orange ripening fruit, orange ripe fruit, and green regrowth at fixed attachment points.
- Preserved the mature canopy through harvesting and regrowth and its branch skeleton during dormancy.
- Added a matching 3D habanero item with a green calyx and bent stem, plus inventory, dropped-item, and hand transforms.
- Enabled very rare wild habanero patches alongside jalapenos in any biome with suitable soil. The other six varieties remain disabled in world generation.
- Reduced raw habanero satiety to 20 to match jalapenos and set its spice dose to 50; two eaten back-to-back fill the meter from empty.
- Added offline plant/item previews and regression checks for shape loading, texture mapping, attachments, wind, hand placement, ripening, harvesting, and eating.
- Retained the existing 1.5-second harvest hold, 16-24 pepper yield, seasonal dormancy, and perennial regrowth behavior.

### Previously Completed

- Temporarily limited wild spawning to jalapenos only; other pepper varieties remain available but no longer generate wild.

- Extreme spice now slowly drains hunger at 0.5 satiety per real-time second, stopping immediately below Extreme. Nutrition levels are unchanged, and Creative/Spectator players are exempt.
- Allowed all wild pepper varieties in any biome by removing climate and altitude restrictions, retaining rarity and soil-only surface placement.
- Corrected wild patch fertility bounds to the game's normalized 0-1 range. Seasonal growth and dormancy are unchanged; new spawning rules apply to newly generated terrain.
- Prevented the Extreme red overlay from writing depth and hiding the spice meter; the meter remains visible until spice reaches zero.
- Reduced raw jalapeno satiety from 80 to 20; other pepper food values are unchanged.
- Added a temporary, three-segment spice HUD: Mild, Hot, and Extreme. It appears only while spice remains.
- Eating spicy peppers builds heat, with different strengths for each variety and no heat from bell peppers.
- Hot and Extreme gently raise body temperature, capped at 2 degrees above normal; Extreme adds a soft red vignette without applying damage or triggering hurt events.
- Spice begins cooling five seconds after the last spicy bite, drains through the levels, persists with the player across saves, and clears on death.
- Corrected the jalapeno's held position, angle, and size using the vanilla food grip; the current first-person renderer shares this third-person hand transform.
- Harvesting ripe peppers now requires holding right-click for 1.5 seconds; releasing early or changing targets cancels the harvest.
- Harvested peppers fall close to the plant with no sideways or upward launch, preserving the existing yield and small drop stacks.
- Replaced the jalapeno food item's placeholder with a textured 3D fruit model, bent stem, and green calyx.
- Added jalapeno-specific inventory, dropped-item, and first/third-person hand transforms while preserving other pepper items and food behavior.
- Replaced all 11 active jalapeno plant stages with the approved bush models.
- Added a complete sprout-to-ripe progression and mature dormant, harvested, and regrowing appearances.
- Kept the mature branch structure and leaf canopy consistent through harvest and fruit regrowth, with attached peppers and gently anchored leaves.
- Mature plants now resume at the full-size harvested stage after dormancy instead of shrinking to stage 4.
- Excluded candidate shapes and model documentation from release packages.
- Stopped packaging and installation when the C# build fails, preventing stale DLLs from being installed.

## 0.1.0 - Perennial Pepper System

- Converted the mod from a simple content crop into a code/content mod.
- Added custom perennial pepper plant logic with persistent plants, right-click harvesting, winter dormancy, and mature regrowth.
- Expanded pepper plant stages to include dormant, harvested, and regrowing states.
- Added eight pepper types: jalapeno, habanero, serrano, cayenne, poblano, bell pepper, banana pepper, and ghost pepper.
- Added rare warm-climate wild pepper patches for survival discovery.
- Added 3D item model placeholders for harvested pepper items.
- Added generated jalapeno and habanero seed textures.
- Added plant shape scaling tooling for VSMC2 model iteration.
- Fixed adjacent pepper planting by replacing vanilla unstable behavior with a custom below-block support check.
- Added random rotated crop shape alternates for more natural planting variation.
- Adjusted leaf and flower wind flags so plant parts stay anchored while tips sway lightly.
- Updated harvest behavior to drop peppers onto the ground in small scattered stacks.
- Retargeted the C# project to .NET 10 for Vintage Story 1.22.3 and added a compiled package build/install script.
