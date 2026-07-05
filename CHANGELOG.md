# Changelog

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
