using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PepperMod
{
    public class BlockPerennialPepperPlant : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (world.Side != EnumAppSide.Server)
            {
                return IsRipe();
            }

            BlockEntityPerennialPepperPlant pepperPlant = GetOrCreateBlockEntity(world, blockSel);

            if (pepperPlant != null && pepperPlant.TryHarvest(byPlayer))
            {
                return true;
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);

            if (world.Side != EnumAppSide.Server)
            {
                return;
            }

            if (neibpos.X != pos.X || neibpos.Z != pos.Z || neibpos.Y != pos.Y - 1)
            {
                return;
            }

            if (!CanPlantOn(world, pos.DownCopy()))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }

        private BlockEntityPerennialPepperPlant GetOrCreateBlockEntity(IWorldAccessor world, BlockSelection blockSel)
        {
            BlockEntityPerennialPepperPlant pepperPlant =
                world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPerennialPepperPlant;

            if (pepperPlant != null)
            {
                return pepperPlant;
            }

            world.BlockAccessor.SpawnBlockEntity("PerennialPepperPlant", blockSel.Position);
            return world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityPerennialPepperPlant;
        }

        private bool IsRipe()
        {
            return Code != null && Code.Path.EndsWith("-8");
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
