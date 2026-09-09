using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PepperMod
{
    public class BlockHangingPepperBundle : Block
    {
        public bool HasCeiling(IWorldAccessor world, BlockPos pos)
        {
            var ceilingPos = pos.UpCopy();
            var ceiling = world.BlockAccessor.GetBlock(ceilingPos);
            return ceiling.CanAttachBlockAt(world.BlockAccessor, this, ceilingPos, BlockFacing.DOWN, new Cuboidi(7, 0, 7, 9, 0, 9));
        }

        public bool CanStayAt(IWorldAccessor world, BlockPos pos) => HasCeiling(world, pos)
            && !world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid).IsLiquid();

        public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
        {
            if (blockSel.Face != BlockFacing.DOWN || !HasCeiling(world, blockSel.Position))
            {
                failureCode = "requiresceiling";
                return false;
            }
            if (world.BlockAccessor.GetBlock(blockSel.Position, BlockLayersAccess.Fluid).IsLiquid())
            {
                failureCode = "cannotplaceinwater";
                return false;
            }
            return base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (world.Side == EnumAppSide.Server && world.BlockAccessor.GetBlock(pos).Id == Id && !CanStayAt(world, pos))
                world.BlockAccessor.BreakBlock(pos, null);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak)) return false;
            if (world.Side == EnumAppSide.Client) return true;
            return (world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityHangingPepperBundle)?.TryTake(byPlayer) == true;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            var entity = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityHangingPepperBundle;
            var contents = entity?.GetContents();
            return contents == null ? Array.Empty<ItemStack>() : new[] { contents };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var contents = (world.BlockAccessor.GetBlockEntity(pos) as BlockEntityHangingPepperBundle)?.GetContents();
            if (contents != null) return contents;
            var item = world.GetItem(new AssetLocation(Code.Domain, "pepperbundle-" + Variant["state"] + "-" + Variant["type"]));
            return item == null ? null : new ItemStack(item);
        }
    }
}
