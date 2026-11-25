# QP's Chisel Tools Shadow Fix

A tiny Server-side fix for the Shadow bug in QP's Chisel Tools (probably shorter than this description).

Have you ever noticed weird lighting/shadow bugs when pasting with your pantograph? Are you tired of having to "flip" blocks with the chisel to get it to "update the light of the block"? Even though it kinda looks like QP does update the block's shadow and lighting, especially after 1.17.3?
Well, it is called "Absorption" by Vintage Story, and QP's Chisel Tools doesn't always update the value. However, with this mod it does!

The fix also works in single player (since a single player game just runs a server locally).

![Before and after absorption is fixed](images/before-n-after.gif)

## The bug explained

QP's Chisel Tools isn't Open Source (as far is I can see?) so some of the following assumptions can be wrong!

The fundemental issue is that QP's Chisel Tools often edits voxels directly in a block without asking the game to update the cuboid list. This means that the game never gets a chance to recalculate a new absorption value, which means adjecent blocks never gets their light "recalculated". QP's Chisel Tools did try and fix this in version 1.17.3 but since it still doesn't recalculate the cuboid list it still operates on a stale absorption value. Furthermore, as far as I can see it only tries to "relight" on PasteMaterials which is not the only mode where the shape (and therefore absorption) of a block is changed.

See [ChiselToolsShadowFixSystem.cs](https://github.com/hutli/chiseltools-shadow-fix/blob/main/src/ChiselToolsShadowFixSystem.cs) -> `FixAbsorption` function to see how I force absorption recalculation.
