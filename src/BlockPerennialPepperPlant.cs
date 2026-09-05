using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PepperMod
{
    public class BlockPerennialPepperPlant : Block
    {
        private const float DefaultHarvestHoldSeconds = 1.5f;
        private static readonly AssetLocation PickingSound = new AssetLocation("game", "sounds/block/leafy-picking");
        private readonly Dictionary<string, BlockPos> harvestStarts = new Dictionary<string, BlockPos>();

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (byPlayer?.PlayerUID == null)
            {
                return false;
            }

            harvestStarts.Remove(byPlayer.PlayerUID);
            if (!IsRipeAt(world, blockSel) || !world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                return false;
            }

            harvestStarts[byPlayer.PlayerUID] = blockSel.Position.Copy();
            world.PlaySoundAt(PickingSound, blockSel.Position, 0, byPlayer);
            return true;
        }

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (!IsHarvesting(world, byPlayer, blockSel))
            {
                ClearHarvest(byPlayer);
                return false;
            }

            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemAttack);

            // The server finishes the hold; the client waits for that result.
            return world.Side == EnumAppSide.Client || secondsUsed < HarvestHoldSeconds();
        }

        public override void OnBlockInteractStop(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            bool wasHarvesting = IsHarvesting(world, byPlayer, blockSel);
            ClearHarvest(byPlayer);

            if (world.Side != EnumAppSide.Server || !wasHarvesting || !(secondsUsed >= HarvestHoldSeconds()))
            {
                return;
            }

            if (world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            {
                GetOrCreateBlockEntity(world, blockSel)?.TryHarvest(byPlayer);
            }
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer,
            BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            ClearHarvest(byPlayer);
            return true;
        }

        private bool IsHarvesting(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            return byPlayer?.PlayerUID != null && blockSel?.Position != null &&
                harvestStarts.TryGetValue(byPlayer.PlayerUID, out BlockPos start) &&
                start.Equals(blockSel.Position) && IsRipeAt(world, blockSel);
        }

        private bool IsRipeAt(IWorldAccessor world, BlockSelection blockSel)
        {
            return blockSel?.Position != null && IsRipe() &&
                world.BlockAccessor.GetBlock(blockSel.Position).BlockId == BlockId;
        }

        private void ClearHarvest(IPlayer byPlayer)
        {
            if (byPlayer?.PlayerUID != null)
            {
                harvestStarts.Remove(byPlayer.PlayerUID);
            }
        }

        private float HarvestHoldSeconds()
        {
            float seconds = Attributes?["perennialPepperPlant"]["harvestHoldSeconds"].AsFloat(DefaultHarvestHoldSeconds)
                ?? DefaultHarvestHoldSeconds;
            return seconds > 0 && float.IsFinite(seconds) ? seconds : DefaultHarvestHoldSeconds;
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

            if (!CanPlantOn(world.BlockAccessor, pos.DownCopy()))
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

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos,
            BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            if (!CanPlantOn(blockAccessor, pos.DownCopy()) ||
                blockAccessor.GetBlock(pos, BlockLayersAccess.Fluid).IsLiquid()) return false;

            return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand, attributes);
        }

        private static bool CanPlantOn(IBlockAccessor blockAccessor, BlockPos groundPos)
        {
            Block groundBlock = blockAccessor.GetBlock(groundPos);
            string path = groundBlock.Code == null ? "" : groundBlock.Code.Path;

            return path.Contains("farmland") ||
                   path.Contains("soil") ||
                   path.Contains("compost");
        }
    }
}
