# Changelog

## Unreleased

### 0.2.0 - Habaneros

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
