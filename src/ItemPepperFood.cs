using Vintagestory.API.Common;

namespace PepperMod
{
    public class ItemPepperFood : Item
    {
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
