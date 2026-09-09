using Vintagestory.API.Common;

namespace PepperMod
{
    public class PepperModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterBlockClass("BlockPerennialPepperPlant", typeof(BlockPerennialPepperPlant));
            api.RegisterBlockEntityClass("PerennialPepperPlant", typeof(BlockEntityPerennialPepperPlant));
            api.RegisterItemClass("ItemPepperSeeds", typeof(ItemPepperSeeds));
            api.RegisterItemClass("ItemPepperFood", typeof(ItemPepperFood));
            api.RegisterItemClass("ItemPepperBundle", typeof(ItemPepperBundle));
            api.RegisterBlockClass("BlockHangingPepperBundle", typeof(BlockHangingPepperBundle));
            api.RegisterBlockEntityClass("HangingPepperBundle", typeof(BlockEntityHangingPepperBundle));
        }
    }
}
