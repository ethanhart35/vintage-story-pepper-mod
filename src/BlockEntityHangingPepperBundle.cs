using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace PepperMod
{
    public class BlockEntityHangingPepperBundle : BlockEntity
    {
        private ItemStack contents;
        private double lastCheckHour;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server) RegisterGameTickListener(OnTick, 2000);
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            if (byItemStack?.Item is not ItemPepperBundle) return;
            contents = byItemStack.Clone();
            contents.StackSize = 1;
            lastCheckHour = Api.World.Calendar.TotalHours;
            MarkDirty(true);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            contents = tree.GetItemstack("bundle");
            if (contents?.ResolveBlockOrItem(worldAccessForResolve) == false) contents = null;
            lastCheckHour = tree.GetDouble("lastCheckHour", worldAccessForResolve.Calendar.TotalHours);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetItemstack("bundle", contents);
            tree.SetDouble("lastCheckHour", lastCheckHour);
        }

        private void UpdateContents()
        {
            if (Api?.Side != EnumAppSide.Server || contents == null) return;
            double now = Api.World.Calendar.TotalHours;
            double hours = Math.Max(0, now - lastCheckHour);
            contents = ItemPepperBundle.AdvanceHanging(Api, contents, hours);
            lastCheckHour = now;
            bool redraw = false;
            if (contents?.Item is ItemPepperBundle item)
            {
                var block = Api.World.GetBlock(new AssetLocation(item.Code.Domain, "hanging" + item.Code.Path));
                if (block?.Id > 0 && block.Id != Block.Id)
                {
                    Api.World.BlockAccessor.ExchangeBlock(block.Id, Pos);
                    redraw = true;
                }
            }
            MarkDirty(redraw);
        }

        private void OnTick(float dt)
        {
            if (Block is BlockHangingPepperBundle hanging && !hanging.CanStayAt(Api.World, Pos))
            {
                Api.World.BlockAccessor.BreakBlock(Pos, null);
                return;
            }
            UpdateContents();
            if (contents?.Item is not ItemPepperBundle) DropAndRemove();
        }

        public ItemStack GetContents()
        {
            UpdateContents();
            return contents?.Clone();
        }

        public bool TryTake(IPlayer player)
        {
            if (Api.Side != EnumAppSide.Server || !Api.World.Claims.TryAccess(player, Pos, EnumBlockAccessFlags.BuildOrBreak)) return false;
            UpdateContents();
            if (contents == null) return false;
            var taken = contents;
            contents = null;
            Api.World.BlockAccessor.SetBlock(0, Pos);
            if (!player.InventoryManager.TryGiveItemstack(taken)) SpawnDrop(taken);
            return true;
        }

        private void DropAndRemove()
        {
            var dropped = contents;
            contents = null;
            Api.World.BlockAccessor.SetBlock(0, Pos);
            if (dropped != null) SpawnDrop(dropped);
        }

        private void SpawnDrop(ItemStack stack) => Api.World.SpawnItemEntity(stack,
            new Vec3d(Pos.X + .5, Pos.Y + .5, Pos.Z + .5), new Vec3d(0, -.02, 0));

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (contents?.Item is not ItemPepperBundle item) return;
            dsc.AppendLine(contents.GetName());
            if (item.Variant["state"] == "dried") dsc.AppendLine(Lang.Get("peppermod:bundle-dry-ready"));
            else
            {
                var state = item.UpdateAndGetTransitionState(Api.World, new DummySlot(contents.Clone()), EnumTransitionType.Perish);
                if (state?.TransitionLevel > 0)
                {
                    dsc.AppendLine(Lang.Get("peppermod:bundle-spoiling"));
                    return;
                }
                double remaining = Math.Max(0, item.DryingHours - contents.Attributes.GetDouble(ItemPepperBundle.ProgressKey));
                dsc.AppendLine(Lang.Get("peppermod:bundle-drying-hours", Math.Ceiling(remaining)));
            }
        }
    }
}
