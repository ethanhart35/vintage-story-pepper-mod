using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PepperMod
{
    public class ItemPepperBundle : ItemPepperFood
    {
        public const string ProgressKey = "peppermodAirDryHours";
        public double DryingHours => Attributes?["airDryHours"].AsDouble(72) ?? 72;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
            EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (blockSel?.Face != BlockFacing.DOWN || slot?.Empty != false || byEntity is not EntityPlayer player)
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            handling = EnumHandHandling.PreventDefault;
            if (!firstEvent || byEntity.World.Side != EnumAppSide.Server) return;
            var world = byEntity.World;
            var selection = blockSel.Clone();
            selection.Position = blockSel.Position.DownCopy();
            selection.DidOffset = true;
            if (!world.Claims.TryAccess(player.Player, selection.Position, EnumBlockAccessFlags.BuildOrBreak)) return;

            UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
            if (slot.Itemstack?.Item != this) return;
            var block = world.GetBlock(new AssetLocation(Code.Domain, "hanging" + Code.Path)) as BlockHangingPepperBundle;
            string failure = null;
            if (block == null || !block.TryPlaceBlock(world, player.Player, slot.Itemstack, selection, ref failure)) return;

            if (player.Player.WorldData.CurrentGameMode != EnumGameMode.Creative) slot.TakeOut(1);
            slot.MarkDirty();
        }

        // The owner supplies only time spent hanging. Split a catch-up interval at
        // the drying boundary so an unloaded chunk ages raw, then dried, food in order.
        public static ItemStack AdvanceHanging(ICoreAPI api, ItemStack stack, double hours)
        {
            if (stack?.Item is not ItemPepperBundle item || api.Side != EnumAppSide.Server) return stack;
            if (!double.IsFinite(hours) || hours < 0) hours = 0;
            var slot = new DummySlot(stack);
            var state = AdvancePerish(api, slot, 0);
            if (slot.Itemstack?.Item != item) return slot.Itemstack;

            double required = item.DryingHours;
            if (!double.IsFinite(required) || required <= 0) required = 72;
            double progress = stack.Attributes.GetDouble(ProgressKey);
            if (!double.IsFinite(progress)) progress = 0;
            progress = Math.Clamp(progress, 0, required);

            if (item.Variant["state"] != "dried" && state?.TransitionLevel == 0)
            {
                double step = Math.Min(hours, required - progress);
                state = AdvancePerish(api, slot, step);
                hours -= step;
                if (slot.Itemstack?.Item != item) return slot.Itemstack;
                progress += step;
                stack.Attributes.SetDouble(ProgressKey, progress);

                if (progress >= required && state?.TransitionLevel == 0)
                {
                    var dried = api.World.GetItem(new AssetLocation(item.Code.Domain, "pepperbundle-dried-" + item.Variant["type"]));
                    var perish = dried?.TransitionableProps?.FirstOrDefault(p => p.Type == EnumTransitionType.Perish);
                    if (dried != null && perish != null)
                    {
                        var result = new ItemStack(dried, stack.StackSize) { Attributes = stack.Attributes.Clone() };
                        result.Attributes.RemoveAttribute("transitionstate");
                        result.Attributes.RemoveAttribute(ProgressKey);
                        CollectibleObject.CarryOverFreshness(api, slot, result, perish);
                        dried.SetTemperature(api.World, result, item.GetTemperature(api.World, stack), false);
                        slot.Itemstack = result;
                    }
                }
            }

            if (hours > 0) AdvancePerish(api, slot, hours);
            return slot.Itemstack;
        }

        private static TransitionState AdvancePerish(ICoreAPI api, ItemSlot slot, double hours)
        {
            if (slot.Itemstack?.Item is not ItemPepperBundle item) return null;
            // The block entity owns elapsed time. Suppress the normal lazy catch-up
            // here, then advance with vanilla rates and transition handling exactly once.
            slot.Itemstack.Attributes.GetTreeAttribute("transitionstate")?.SetDouble("lastUpdatedTotalHours", api.World.Calendar.TotalHours);
            var state = item.UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish);
            if (slot.Itemstack?.Item != item || state == null || hours <= 0) return state;
            float rate = item.GetTransitionRateMul(api.World, slot, EnumTransitionType.Perish);
            item.SetTransitionState(slot.Itemstack, EnumTransitionType.Perish, state.TransitionedHours + (float)(hours * Math.Max(0, rate)));
            return item.UpdateAndGetTransitionState(api.World, slot, EnumTransitionType.Perish);
        }
    }
}
