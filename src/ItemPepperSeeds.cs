using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PepperMod
{
    public class ItemPepperSeeds : Item
    {
        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling)
        {
            if (blockSel == null || byEntity == null)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            if (byEntity.Controls != null && byEntity.Controls.ShiftKey)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            handling = EnumHandHandling.PreventDefault;

            IWorldAccessor world = byEntity.World;
            if (world.Side != EnumAppSide.Server)
            {
                return;
            }

            if (blockSel.Face != BlockFacing.UP)
            {
                return;
            }

            BlockPos plantPos = blockSel.Position.AddCopy(BlockFacing.UP);
            Block targetBlock = world.BlockAccessor.GetBlock(plantPos);

            if (targetBlock.Replaceable < 6000 || !CanPlantOn(world, plantPos.DownCopy()))
            {
                return;
            }

            string pepperType = GetPepperType(slot.Itemstack.Collectible.Code.Path);
            Block plantBlock = world.GetBlock(new AssetLocation("peppermod", "crop-" + pepperType + "-1"));

            if (plantBlock == null || plantBlock.Id == 0)
            {
                world.Logger.Warning("Pepper Mod: Could not find block crop-{0}-1", pepperType);
                return;
            }

            world.BlockAccessor.SetBlock(plantBlock.BlockId, plantPos, slot.Itemstack);
            world.BlockAccessor.TriggerNeighbourBlockUpdate(plantPos);

            EntityPlayer player = byEntity as EntityPlayer;
            bool isCreative = player != null && player.Player != null &&
                player.Player.WorldData.CurrentGameMode == EnumGameMode.Creative;

            if (!isCreative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }
        }

        private static string GetPepperType(string itemPath)
        {
            const string prefix = "seeds-";
            if (itemPath.StartsWith(prefix))
            {
                return itemPath.Substring(prefix.Length);
            }

            return itemPath;
        }

        private static bool CanPlantOn(IWorldAccessor world, BlockPos groundPos)
        {
            Block groundBlock = world.BlockAccessor.GetBlock(groundPos);
            string path = groundBlock.Code == null ? "" : groundBlock.Code.Path;

            return path.Contains("farmland") ||
                   path.Contains("soil") ||
                   path.Contains("compost");
        }
    }
}
