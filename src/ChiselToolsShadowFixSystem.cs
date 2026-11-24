using System;
using System.Reflection;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace ChiselToolsShadowFix
{
    public class ChiselToolsShadowFixSystem : ModSystem
    {
        public const string ModId = "chiseltoolsshadowfix";

        private static readonly string[] RelightModes = { "SHAPEPASTE", "PASTEALL", "ADDPASTE", "ADDPASTEALL", "MIRRORPASTE" };
        private static int RelightDelayMs = 500;
        private const int ClientRelightDelayMs = 250;

        private static bool Enabled = true;

        private static MethodInfo getToolModeFromSlot;
        private static MethodInfo rebuildCuboidList;

        private static IClientNetworkChannel clientChannel;

        private Harmony harmony;

        public override void Start(ICoreAPI api)
        {
            rebuildCuboidList = AccessTools.Method(
                AccessTools.TypeByName("Vintagestory.GameContent.BlockEntityMicroBlock"), "RebuildCuboidList", Type.EmptyTypes);
            if (rebuildCuboidList == null)
                Mod.Logger.Warning("BlockEntityMicroBlock.RebuildCuboidList not found; light absorption can't be fixed.");

            api.Network.RegisterChannel(ModId).RegisterMessageType<BlockPos>();
        }

        #region Server

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Network.GetChannel(ModId).SetMessageHandler<BlockPos>(OnRelightRequest);
        }

        private static void OnRelightRequest(IServerPlayer player, BlockPos pos)
        {
            IWorldAccessor world = player.Entity.World;
            if (!world.Claims.TryAccess(player, pos, EnumBlockAccessFlags.Use)) return;

            BlockEntity be = FixAbsorption(world.BlockAccessor, pos);
            be?.MarkDirty(true);
        }

        #endregion

        #region Client

        public override void StartClientSide(ICoreClientAPI api)
        {
            clientChannel = api.Network.GetChannel(ModId);

            api.ChatCommands.Create("relight")
                .WithDescription("Toggle relight (enabled by default)")
                .HandleWith(EnableRelight);

            api.ChatCommands.Create("relight-delay")
                .WithDescription("Set relight delay in milliseconds")
                .WithArgs(api.ChatCommands.Parsers.Int("Delay in milliseconds"))
                .HandleWith(SetRelightDelay);

            try
            {
                // ItemPantograph is internal to chiseltools, so it's looked up by name.
                Type pantograph = AccessTools.TypeByName("chisel.src.ItemPantograph")
                    ?? throw new MissingMemberException("type chisel.src.ItemPantograph");
                getToolModeFromSlot = AccessTools.Method(pantograph, "GetToolModeFromSlot", new[] { typeof(ItemSlot) })
                    ?? throw new MissingMemberException("ItemPantograph.GetToolModeFromSlot");
                MethodInfo interactStart = AccessTools.Method(pantograph, "OnHeldInteractStart")
                    ?? throw new MissingMemberException("ItemPantograph.OnHeldInteractStart");

                harmony = new Harmony(ModId);
                harmony.Patch(interactStart, postfix: new HarmonyMethod(typeof(ChiselToolsShadowFixSystem), nameof(OnHeldInteractStartPostfix)));
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
                ? $"Relight ON with {RelightDelayMs}ms delay ({(clientChannel.Connected ? "server" : "client-only")} fix)"
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
            if (clientChannel.Connected)
            {
                clientChannel.SendPacket(pos);
                return;
            }

            capi.Network.GetChannel("pastenet")?.SendPacket(pos);
            capi.Event.RegisterCallback(_ => FixAbsorption(capi.World.BlockAccessor, pos), ClientRelightDelayMs);
        }

        #endregion

        private static BlockEntity FixAbsorption(IBlockAccessor ba, BlockPos pos)
        {
            BlockEntity be = ba.GetBlockEntity(pos);
            if (be == null || rebuildCuboidList == null || !rebuildCuboidList.DeclaringType.IsInstanceOfType(be)) return null;

            rebuildCuboidList.Invoke(be, null);

            int now = ba.GetBlock(pos).GetLightAbsorption(ba, pos);
            ba.MarkAbsorptionChanged(now == 0 ? 1 : 0, now, pos);
            return be;
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(ModId);
            harmony = null;
            clientChannel = null;
            base.Dispose();
        }
    }
}