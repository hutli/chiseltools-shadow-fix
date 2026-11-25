using System;
using System.Linq;
using System.Reflection;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;


namespace ChiselToolsShadowFix
{
    public class ChiselToolsShadowFixSystem : ModSystem
    {
        public const string ModId = "chiseltoolsshadowfix";

        private static MethodInfo rebuildCuboidList;

        private Harmony harmony;

        public override void StartServerSide(ICoreServerAPI api)
        {
            try
            {
                rebuildCuboidList = AccessTools.Method("Vintagestory.GameContent.BlockEntityMicroBlock:RebuildCuboidList") ?? throw new MissingMemberException("Vintagestory.GameContent.BlockEntityMicroBlock:RebuildCuboidList");

                Assembly chiselTools = api.ModLoader.GetMod("chiseltools")?.Systems.FirstOrDefault()?.GetType().Assembly ?? throw new MissingMemberException("chiseltools mod");

                harmony = new Harmony(ModId);

                foreach (Type type in chiselTools.GetTypes())
                {
                    if (!typeof(Item).IsAssignableFrom(type)) continue;
                    Patch(type, "OnHeldInteractStart", nameof(BlockSelPostfix));
                    Patch(type, "OnHeldAttackStart", nameof(BlockSelPostfix));
                    Patch(type, "Undo", nameof(UndoPostfix));
                }
            }
            catch (Exception e)
            {
                Mod.Logger.Error("Failed to patch QP's Chisel Tools (has chiseltools or VS been updated?): {0}", e);
                harmony?.UnpatchAll(ModId);
                harmony = null;
            }
        }

        private void Patch(Type type, string method, string postfix)
        {
            MethodInfo original = AccessTools.DeclaredMethod(type, method);
            if (original != null) harmony.Patch(original, postfix: new HarmonyMethod(typeof(ChiselToolsShadowFixSystem), postfix));
        }

        private static void BlockSelPostfix(BlockSelection blockSel, EntityAgent byEntity)
        {
            // When a block is clicked (right and left) with any chiseltools tool we run the fix.
            // 
            // We could trigger this more fine grained, i.e. only on modes that can change shape
            // and therefore the light absorption of a block, but since ChiselTools
            // is constantly developing new modes I decided to always do it.
            // This mod shouldn't be very heavy as these calculations are the same
            // as when just using a normal chisel or placing/removing blocks.
            if (blockSel != null) FixAbsorption(byEntity.World, blockSel.Position);
        }

        private static void UndoPostfix(ItemSlot slot, ICoreAPI ___api)
        {
            // When undo is run from any chiseltools tool we run the fix.
            //
            // Since undo isn't an interaction on a block we need to get the position from the tool itself
            BlockPos pos = slot.Itemstack?.Attributes.GetBlockPos("undoposition");
            if (pos != null) FixAbsorption(___api.World, pos);
        }

        private static void FixAbsorption(IWorldAccessor world, BlockPos pos)
        {
            if (world.Side != EnumAppSide.Server) return;

            IBlockAccessor ba = world.BlockAccessor;
            BlockEntity be = ba.GetBlockEntity(pos);
            if (be == null || !rebuildCuboidList.DeclaringType.IsInstanceOfType(be)) return;

            // Rebuild cuboid list from actual block voxels (edited directly by ChiselTools)
            // This results in a recalculation of the absorption flag
            rebuildCuboidList.Invoke(be, null);

            // Read the block's updated light absorption
            int newAbsorption = ba.GetBlock(pos).GetLightAbsorption(ba, pos);

            // Force a "relight" of all surrounding blocks.
            // We use a fake old value that is different from the new corrected one.
            //
            // ChiselTools triggers a similar relight in PasteMaterials.
            // However, this works on the stale absorption flag
            // since it never rebuilds the cuboid list.
            // Also, it never does this on things like
            // rotate, mirror, move, modify, undo etc - which can also change the absorption.
            ba.MarkAbsorptionChanged(newAbsorption == 0 ? 1 : 0, newAbsorption, pos);

            // Send the updated block to clients and mark the chunk for saving.
            be.MarkDirty(true);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(ModId);
            harmony = null;
            base.Dispose();
        }
    }
}