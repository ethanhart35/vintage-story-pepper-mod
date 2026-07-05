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
        }
    }
}
