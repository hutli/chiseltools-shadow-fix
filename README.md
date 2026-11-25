# QP's Chisel Tools Shadow Fix

A tiny Server-side fix for the Shadow bug in QP's Chisel Tools (the mod itself is probably shorter than this description).

Have you ever noticed weird lighting/shadow bugs when pasting with your pantograph? Are you tired of having to "flip" blocks with the chisel to get it to "update the light of the block"?
Well, it is called "Absorption" by Vintage Story, and QP's Chisel Tools doesn't always update it. However, with this mod it does!

The fix also works in single-player (since a single-player game just runs a server locally).

![Before and after absorption is fixed](images/before-n-after.gif)

## The bug explained

QP's Chisel Tools isn't Open Source (as far as I can tell), so some of the following assumptions may be wrong!

The fundemental issue is that QP's Chisel Tools often edits voxels directly in a block without asking the game to update the cuboid list. This means the game never gets a chance to recalculate a new absorption value, so adjacent blocks never get their light "recalculated". QP's Chisel Tools did try to fix this in version 1.1,7.3 but since it still doesn't recalculate the cuboid list, it still operates on a stale absorption value. Furthermore, as far as I can see, it only tries to relight on "PasteMaterials", which isn't the only mode where a block's shape (and therefore absorption) changes.

See [ChiselToolsShadowFixSystem.cs](https://github.com/hutli/chiseltools-shadow-fix/blob/main/src/ChiselToolsShadowFixSystem.cs) -> `FixAbsorption` function to see how I force absorption recalculation.
