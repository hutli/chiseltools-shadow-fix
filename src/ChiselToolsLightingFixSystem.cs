using System;
using System.Reflection;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace ChiselToolsLightingFix
{
    public class ChiselToolsLightingFixSystem : ModSystem
    {
        public const string ModId = "chiseltools-lighting-fix";

        private static readonly string[] RelightModes = { "SHAPEPASTE", "PASTEALL", "ADDPASTE", "ADDPASTEALL", "MIRRORPASTE" };
        private static int RelightDelayMs = 2000;
        private const int ClientRelightDelayMs = 250;

        private static bool Enabled = true;


        private static MethodInfo getToolModeFromSlot;
        private static MethodInfo rebuildCuboidList;

        private Harmony harmony;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.ChatCommands.Create("relight")
                .WithDescription("Toggle relight (enabled by default)")
                .HandleWith(EnableRelight);

            api.ChatCommands.Create("relight-delay")
                .WithDescription("Set relight delay in milliseconds")
                .WithArgs(api.ChatCommands.Parsers.Int("Delay in milliseconds"))
                .HandleWith(SetRelightDelay);

            try
            {
                Type pantograph = AccessTools.TypeByName("chisel.src.ItemPantograph")
                    ?? throw new MissingMemberException("type chisel.src.ItemPantograph");
                getToolModeFromSlot = AccessTools.Method(pantograph, "GetToolModeFromSlot", new[] { typeof(ItemSlot) })
                    ?? throw new MissingMemberException("ItemPantograph.GetToolModeFromSlot");
                MethodInfo interactStart = AccessTools.Method(pantograph, "OnHeldInteractStart")
                    ?? throw new MissingMemberException("ItemPantograph.OnHeldInteractStart");
                rebuildCuboidList = AccessTools.Method(
                    AccessTools.TypeByName("Vintagestory.GameContent.BlockEntityMicroBlock"), "RebuildCuboidList", Type.EmptyTypes);
                if (rebuildCuboidList == null)
                    Mod.Logger.Warning("BlockEntityMicroBlock.RebuildCuboidList not found; only the server relight will be used.");

                harmony = new Harmony(ModId);
                harmony.Patch(interactStart, postfix: new HarmonyMethod(typeof(ChiselToolsLightingFixSystem), nameof(OnHeldInteractStartPostfix)));
            }
            catch (Exception e)
            {
                Mod.Logger.Error("Failed to patch QP's Chisel Tools pantograph/multigraph (has chiseltools been updated?): {0}", e);
                harmony?.UnpatchAll(ModId);
                harmony = null;
            }
        }
        private TextCommandResult EnableRelight(TextCommandCallingArgs args)
        {
            Enabled = !Enabled;
            return TextCommandResult.Success(Enabled
                ? $"Relight ON with {RelightDelayMs}ms delay"
                : "Relight OFF");
        }
        private TextCommandResult SetRelightDelay(TextCommandCallingArgs args)
        {
            int delay = (int)args[0];
            if (delay < 0) return TextCommandResult.Error("Delay can't be negative.");
            RelightDelayMs = delay;
            return TextCommandResult.Success($"Relight delay set to {RelightDelayMs}ms");
        }
        private static void OnHeldInteractStartPostfix(Item __instance, ItemSlot slot, EntityAgent byEntity,
            BlockSelection blockSel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!Enabled) return;
            if (!firstEvent || blockSel == null || handling == EnumHandHandling.NotHandled) return;
            if (!(byEntity?.World?.Api is ICoreClientAPI capi)) return;
            if (slot?.Itemstack == null) return;

            string mode = (string)getToolModeFromSlot.Invoke(__instance, new object[] { slot });
            if (Array.IndexOf(RelightModes, mode) < 0) return;

            BlockPos pos = blockSel.Position.Copy();
            capi.Event.RegisterCallback(_ => RequestRelight(capi, pos), RelightDelayMs);
        }

        private static void RequestRelight(ICoreClientAPI capi, BlockPos pos)
        {
            capi.Network.GetChannel("pastenet")?.SendPacket(pos);
            capi.Event.RegisterCallback(_ => FixAbsorption(capi, pos), ClientRelightDelayMs);
        }
        private static void FixAbsorption(ICoreClientAPI capi, BlockPos pos)
        {
            IBlockAccessor ba = capi.World.BlockAccessor;
            BlockEntity be = ba.GetBlockEntity(pos);
            if (be == null || rebuildCuboidList == null || !rebuildCuboidList.DeclaringType.IsInstanceOfType(be)) return;

            rebuildCuboidList.Invoke(be, null);

            int now = ba.GetBlock(pos).GetLightAbsorption(ba, pos);
            ba.MarkAbsorptionChanged(now == 0 ? 1 : 0, now, pos);
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(ModId);
            harmony = null;
            base.Dispose();
        }
    }
}
