# Jalapeno candidate models

A reference set of 11 models based on the approved mature bush. These stages
have now been copied into the active `jalapeno` folder used by the game. Earlier
handmade models are preserved in the local, Git-ignored `backups` folder. This
candidate folder is excluded from the installed mod package.

| File | State |
| --- | --- |
| stage1.json | Sprout with two seed leaves |
| stage2.json | Seedling with six leaves |
| stage3.json | Young branching plant |
| stage4.json | Leafy developing bush |
| stage5.json | Flowering bush |
| stage6.json | Full-size bush with the first small peppers |
| stage7.json | Growing, nearly ripe peppers |
| stage8.json | Ripe bush; identical to the approved mature.json |
| stage9.json | Dormant; mature branches without leaves or fruit |
| stage10.json | Harvested; mature canopy without peppers |
| stage11.json | Regrowing peppers on the same mature canopy |

Stages 6, 7, 8, 10, and 11 share identical branches, leaves, and leaf stems.
Stage 9 retains those same structural branches. Developing fruit stays attached
at the mature fruit positions. Stems and peppers are rigid; leaf wind settings
pin the leaf bases.

Open [the interactive preview](../../../../../../../art/previews/jalapeno-stages.html)
to select stages and views. The preview fits each plant individually by default;
enable **Same scale** to compare their actual sizes. Each JSON can be opened in
VSMC2 and uses the existing jalapeno textures.

Validation: all 11 shapes deserialize through Vintage Story's API. The offline
preview uses the API's element transforms. The stages are now active; a native
in-game visual and gameplay test is still needed.
