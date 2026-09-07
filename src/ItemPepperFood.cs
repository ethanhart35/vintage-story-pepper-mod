using System;
using System.Globalization;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace PepperMod
{
    public class ItemPepperFood : Item
    {
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            int bundleCount = Attributes?["peppermodBundleCount"].AsInt(0) ?? 0;
            if (bundleCount > 0) dsc.AppendLine(Lang.Get("peppermod:bundle-count", bundleCount));
            var scoville = Attributes?["peppermodScoville"];
            if (scoville?.Exists == true)
            {
                int units = Math.Max(0, scoville.AsInt(0));
                string label = bundleCount > 0 ? "peppermod:scoville-units-per-pepper" : "peppermod:scoville-units";
                dsc.AppendLine(Lang.Get(label, units.ToString("N0", CultureInfo.InvariantCulture)));
            }
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputSlots, ItemSlot outputSlot, IRecipeBase byRecipe)
        {
            base.OnCreatedByCrafting(allInputSlots, outputSlot, byRecipe);
            if (byRecipe.Name?.Domain != "peppermod" || !byRecipe.Name.Path.StartsWith("bundle-", StringComparison.Ordinal)
                || outputSlot?.Itemstack == null) return;

            // Tying is not cooking: retain the oldest pepper's exact perish timer,
            // without vanilla crafting's freshness bonus or a new random lifetime.
            ITreeAttribute oldestTimer = null;
            float oldestAge = -1;
            float temperature = 20;
            foreach (var input in allInputSlots)
            {
                if (input?.Itemstack?.Collectible is not ItemPepperFood pepper) continue;
                var state = pepper.UpdateAndGetTransitionState(api.World, input, EnumTransitionType.Perish);
                var timer = input.Itemstack?.Attributes.GetTreeAttribute("transitionstate");
                if (state == null || timer == null) continue;
                float age = state.TransitionedHours <= state.FreshHours
                    ? state.TransitionedHours / Math.Max(1, state.FreshHours)
                    : 1 + (state.TransitionedHours - state.FreshHours) / Math.Max(1, state.TransitionHours);
                if (float.IsFinite(age) && age > oldestAge) { oldestAge = age; oldestTimer = timer; }
                temperature = Math.Max(temperature, pepper.GetTemperature(api.World, input.Itemstack));
            }
            if (oldestTimer != null)
            {
                outputSlot.Itemstack.Attributes["transitionstate"] = oldestTimer.Clone();
                outputSlot.Itemstack.Collectible.SetTemperature(api.World, outputSlot.Itemstack, temperature, false);
            }
        }

        protected override void tryEatStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity)
        {
            if (slot?.Itemstack == null || byEntity == null) return;
            ItemStack before = slot.Itemstack;
            int count = before.StackSize;
            float heat = before.Collectible.Attributes?["peppermodSpice"].AsFloat(0) ?? 0;

            // Let vanilla decide whether eating completed and apply normal food/spoilage effects.
            base.tryEatStop(secondsUsed, slot, byEntity);

            int remaining = slot.Itemstack?.Collectible == before.Collectible ? slot.Itemstack.StackSize : 0;
            if (byEntity.World.Side == EnumAppSide.Server && remaining < count && byEntity is EntityPlayer player)
            {
                PepperSpiceSystem.AddSpice(player, heat);
            }
        }
    }
}
