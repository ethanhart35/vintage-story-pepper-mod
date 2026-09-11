# Serrano Assets

The completed Serrano plant builds on the approved jalapeno branching layout with
a taller, narrower canopy, slimmer leaves, and new tapered green fruit geometry.
Mature, harvested, and regrowing stages share the same canopy and attachment
points. The dormant stage retains the mature branch skeleton.

The picked fruit uses the same sectioned construction with a bent stem. Each of
the eight bundle fruits is attached by its actual stem endpoint to the shared
cord. Bundles reuse the existing ceiling hook and preparation textures.

Plant and raw fruit textures reuse the existing Serrano artwork. The seed icon
reuses the finished jalapeno seed artwork; no new raster artwork was generated.
Baked and dried skins use the green quadrants of the shared prepared-pepper atlas.

All runtime models are ordinary editable Vintage Story shape JSON files. There
is no procedural generator or additional runtime dependency in the mod. Working
scripts and intermediate render checks remain outside the project.

Previews:
- `art/previews/serrano-stages.html`: all 11 states, with layer and camera controls.
- `art/previews/serrano-item.html`: the picked fruit.
- `art/previews/prepared-peppers.html`: all three finished varieties and preparations.
- `art/previews/pepper-bundles.html`: all nine bundles.
- `art/previews/serrano-growing.gif`: fixed-scale growth animation.

The native game API is used to verify model transforms, leaf and fruit
attachments, hand placement, oven bounds, cord contact, and hanging clearance.
Playwright checks the offline previews at desktop and mobile sizes. A final
in-game playtest is still needed before publishing 0.4.0.
