using System.Reflection;
using Newtonsoft.Json.Linq;
using PepperMod;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Common;
using Vintagestory.Server;

internal static class HangingBundleTests
{
    private static void Require(bool value, string message = "Hanging bundle regression failed") => PepperBundleTests.Require(value, message);
    public static void Run(Action<string, Action> check)
    {
        check("all loose raw baked and dried peppers lie flat and centered in each oven slot", () => {
            var types = PepperBundleTests.Read("itemtypes/food/vegetable.json")["variantgroups"][0]["states"].Values<string>();
            foreach (string type in types) foreach (string state in PepperBundleTests.States) {
                var item = PreparedPepperTests.Load(type, state);
                var matrix = item.Attributes["onDisplayTransform"].AsObject<ModelTransform>().AsMatrix;
                var shape = PepperBundleTests.Read($"shapes/item/food/{(state == "raw" ? "vegetable" : "prepared/" + state)}/{type}.json").ToObject<Shape>();
                var points = Corners(shape).Select(p => Transform(matrix, p)).ToArray();
                Require(Math.Abs(points.Min(p => p.Y) - .01) < .001, $"{state} {type}: clips or floats in the oven");
                Require(points.Max(p => p.Y) < .2, $"{state} {type}: must lie flat");
                Require(points.Max(p => p.X) - points.Min(p => p.X) < .3 && points.Max(p => p.Z) - points.Min(p => p.Z) < .3);
                Require(Math.Abs((points.Max(p => p.X) + points.Min(p => p.X)) / 2 - .5) < .001);
                Require(Math.Abs((points.Max(p => p.Z) + points.Min(p => p.Z)) / 2 - .5) < .001);
            }
        });
        check("hanging variants reuse all six bundle models and suspend their loops from a ceiling hook", () => {
            var definition = PepperBundleTests.Read("blocktypes/food/hangingpepperbundle.json");
            Require((string)definition["entityClass"] == "HangingPepperBundle" && !definition["collisionBoxes"].Any());
            Require((string)PepperBundleTests.Read("itemtypes/food/pepperbundle.json")["class"] == "ItemPepperBundle");
            foreach (string type in PepperBundleTests.Types) foreach (string state in PepperBundleTests.States) {
                string code = $"hangingpepperbundle-{state}-{type}";
                var shapeRef = definition["shapeByType"][code];
                Require((string)shapeRef["base"] == $"item/food/bundle/{state}/{type}");
                var shape = PepperBundleTests.Read($"shapes/item/food/bundle/{state}/{type}.json").ToObject<Shape>();
                var points = Corners(shape).ToArray();
                double offset = (double)shapeRef["offsetY"];
                Require(Math.Abs(points.Max(p => p.Y) + offset - .925) < .002);
                Require(points.Min(p => p.Y) + offset >= 0);
                Require(definition["texturesByType"][code]["cord"]["base"].Value<string>() == "game:block/linen");
                Require(shapeRef["overlays"].Count() == 1 && (string)shapeRef["overlays"][0]["base"] == "block/food/bundle-hook");
                Require((string)definition["texturesByType"][code]["hook"]["base"] == "game:block/metal/ingot/iron");
            }
        });
        check("ceiling hook reaches its mount and the rope apex without changing portable bundle models", () => {
            var hook = PepperBundleTests.Read("shapes/block/food/bundle-hook.json").ToObject<Shape>();
            var vertices = Corners(hook).ToArray();
            Require(vertices.Max(p => p.Y) >= 1 && vertices.Max(p => p.Y) < 1.002);
            Require(hook.Elements.Length == 6);
            foreach (var element in hook.Elements) foreach (var face in element.FacesResolved)
                Require(face?.Enabled == true && face.Texture == "hook", $"{element.Name}: unexpected texture {face?.Texture}");
            var seat = new Shape { Elements = hook.Elements.Where(e => e.Name.StartsWith("Hook-seat-")).ToArray() };
            var seatPoints = Corners(seat).ToArray();
            Require(seatPoints.Min(p => p.Z) < .5 && seatPoints.Max(p => p.Z) > .5, "Hook must pass through the loop plane");
            Require(seatPoints.Min(p => p.X) < .5 && seatPoints.Max(p => p.X) > .5);
            Require(seatPoints.Min(p => p.Y) < .916 && seatPoints.Max(p => p.Y) > .914, "Rope apex must rest in the hook seat");
            foreach (string type in PepperBundleTests.Types) foreach (string state in PepperBundleTests.States) {
                var portable = PepperBundleTests.Read($"shapes/item/food/bundle/{state}/{type}.json");
                Require(portable["textures"]["hook"] == null && portable["elements"].All(e => !e["name"].Value<string>().StartsWith("Hook-")));
            }
        });
        check("bundles hang under different ceiling materials and consume exactly one item", () => {
            foreach (var material in new[] { EnumBlockMaterial.Stone, EnumBlockMaterial.Wood, EnumBlockMaterial.Glass, EnumBlockMaterial.Metal }) {
                var f = new HangingFixture(); f.Ceiling.BlockMaterial = material;
                var input = f.Input("jalapeno", "raw", 3); f.Place(input);
                Require(f.Current.Id > 0 && input.StackSize == 2);
                var placed = f.Entity.GetContents();
                Require(placed.StackSize == 1 && placed.Item == f.Item("jalapeno", "raw"));
                Require(!ReferenceEquals(placed.Attributes, input.Itemstack.Attributes));
            }
        });
        check("hanging placement rejects walls unsupported ceilings water occupied blocks and claims", () => {
            foreach (string reason in new[] { "wall", "ceiling", "water", "occupied", "claim", "repeat", "client" }) {
                var f = new HangingFixture(reason == "client" ? EnumAppSide.Client : EnumAppSide.Server);
                var input = f.Input("habanero", "raw", 2);
                if (reason == "ceiling") f.Ceiling.Supported = false;
                if (reason == "water") f.Water = true;
                if (reason == "occupied") f.Current = f.Ceiling;
                if (reason == "claim") f.Access = false;
                f.Place(input, reason == "wall" ? BlockFacing.NORTH : BlockFacing.DOWN, reason != "repeat");
                Require(f.Entity == null && input.StackSize == 2, reason);
            }
        });
        check("raw and baked bundles air dry in 72 hours without changing variety or pepper count", () => {
            foreach (string type in PepperBundleTests.Types) foreach (string state in new[] { "raw", "baked" }) {
                var f = new HangingFixture(); f.Place(f.Input(type, state));
                f.Now += 71; var before = f.Entity.GetContents(); Require(before.Item == f.Item(type, state));
                f.Now += 1; var dried = f.Entity.GetContents();
                Require(dried.Item == f.Item(type, "dried") && dried.StackSize == 1);
                Require(dried.Collectible.Attributes["peppermodBundleCount"].AsInt() == 8);
                Require(f.Current.Code.Path == $"hangingpepperbundle-dried-{type}");
                Require(f.Age(dried) > 0 && dried.Attributes.GetTreeAttribute("transitionstate") != null, "Drying must carry over freshness");
                Require(!dried.Attributes.HasAttribute(ItemPepperBundle.ProgressKey));
            }
        });
        check("hanging save reload and large time jumps match incremental drying", () => {
            var incremental = new HangingFixture(); incremental.Place(incremental.Input("habanero", "raw"));
            for (int i = 0; i < 100; i++) { incremental.Now += 1; incremental.Entity.GetContents(); }
            var f = new HangingFixture(); f.Place(f.Input("habanero", "raw"));
            f.Now += 24; f.Entity.GetContents(); var saved = new TreeAttribute(); f.Entity.ToTreeAttributes(saved);
            f.Now += 76; f.Restore(saved); var result = f.Entity.GetContents();
            Require(result.Item == f.Item("habanero", "dried"));
            Require(Math.Abs(f.Age(result) - incremental.Age(incremental.Entity.GetContents())) < .01);
        });
        check("pickup and rehanging retain freshness and pause drying while carried", () => {
            var f = new HangingFixture(); f.Place(f.Input("jalapeno", "raw"));
            f.Now += 24; var entity = f.Entity;
            Require(entity.TryTake(f.Player)); Require(!entity.TryTake(f.Player));
            var taken = f.Inventory.Received.Single();
            Require(taken.Attributes.GetDouble(ItemPepperBundle.ProgressKey) == 24 && f.Current.Id == 0);
            f.Now += 24;
            var slot = new DummySlot(taken); f.Place(slot);
            Require(f.Entity.GetContents().Attributes.GetDouble(ItemPepperBundle.ProgressKey) == 24);
            f.Now += 47; Require(f.Entity.GetContents().Item == f.Item("jalapeno", "raw"));
            f.Now += 1; Require(f.Entity.GetContents().Item == f.Item("jalapeno", "dried"));
        });
        check("breaking the support drops one bundle with its drying progress", () => {
            var f = new HangingFixture(); f.Place(f.Input("jalapeno", "raw")); f.Now += 12;
            var hanging = (BlockHangingPepperBundle)f.Current;
            f.Ceiling.Supported = false;
            hanging.OnNeighbourBlockChange(f.World, f.Pos, f.Pos.UpCopy());
            hanging.OnNeighbourBlockChange(f.World, f.Pos, f.Pos.UpCopy());
            Require(f.Drops.Count == 1 && f.Current.Id == 0);
            Require(f.Drops[0].StackSize == 1 && f.Drops[0].Attributes.GetDouble(ItemPepperBundle.ProgressKey) == 12);
        });
        check("full inventory drops the picked bundle and denied pickup leaves it hanging", () => {
            var f = new HangingFixture(); f.Place(f.Input("habanero", "dried")); f.Inventory.Full = true;
            f.Access = false; Require(!f.Entity.TryTake(f.Player)); Require(f.Current.Id > 0 && f.Drops.Count == 0);
            f.Access = true; Require(f.Entity.TryTake(f.Player)); Require(f.Drops.Count == 1 && f.Current.Id == 0);
        });
        check("flooded bundles drop once and saved unsupported bundles are removed on tick", () => {
            var f = new HangingFixture(); f.Place(f.Input("jalapeno", "raw")); f.Water = true;
            var hanging = (BlockHangingPepperBundle)f.Current;
            hanging.OnNeighbourBlockChange(f.World, f.Pos, f.Pos.UpCopy());
            hanging.OnNeighbourBlockChange(f.World, f.Pos, f.Pos.UpCopy());
            Require(f.Current.Id == 0 && f.Drops.Count == 1 && f.Drops[0].StackSize == 1);
            var g = new HangingFixture(); g.Place(g.Input("habanero", "raw"));
            var saved = new TreeAttribute(); g.Entity.ToTreeAttributes(saved); g.Restore(saved);
            g.Ceiling.Supported = false; g.Tick();
            Require(g.Current.Id == 0 && g.Drops.Count == 1);
        });
        check("fully spoiled hanging bundles drop rot and remove the block on tick", () => {
            var f = new HangingFixture(); var slot = f.Input("jalapeno", "raw");
            f.Item("jalapeno", "raw").SetTransitionState(slot.Itemstack, EnumTransitionType.Perish, 160);
            f.Place(slot); f.Now += 72; f.Tick();
            Require(f.Current.Id == 0 && f.Drops.Count == 1);
            Require(f.Drops[0].CodeName() == "game:rot" && f.Drops[0].StackSize == 2);
        });
        check("spoiling bundles do not become dried and dried bundles still eventually rot", () => {
            var f = new HangingFixture(); var input = f.Input("jalapeno", "raw");
            f.Item("jalapeno", "raw").SetTransitionState(input.Itemstack, EnumTransitionType.Perish, 160);
            f.Place(input); f.Now += 72;
            var rotten = f.Entity.GetContents(); Require(rotten.CodeName() == "game:rot" && rotten.StackSize == 2);
            var g = new HangingFixture(); g.Place(g.Input("habanero", "raw")); g.Now += 20000;
            Require(g.Entity.GetContents().CodeName() == "game:rot");
        });
        check("creative hanging does not consume held bundles and client time cannot dry them", () => {
            var f = new HangingFixture(); ((ServerWorldPlayerData)f.Player.WorldData).GameMode = EnumGameMode.Creative;
            var input = f.Input("jalapeno", "raw", 2); f.Place(input); Require(input.StackSize == 2 && f.Current.Id > 0);
            var client = new HangingFixture(EnumAppSide.Client); var stack = client.Input("jalapeno", "raw").Itemstack;
            Require(ReferenceEquals(stack, ItemPepperBundle.AdvanceHanging(client.Api, stack, 1000)));
            Require(!stack.Attributes.HasAttribute(ItemPepperBundle.ProgressKey));
        });
    }
    private static string CodeName(this ItemStack stack) => stack.Collectible.Code.ToString();
    internal static IEnumerable<Vec3d> Corners(Shape shape) => shape.Elements.Where(e => e.FacesResolved?.Any(f => f?.Enabled == true) == true)
        .SelectMany(e => from x in new[] { 0f, 1f } from y in new[] { 0f, 1f } from z in new[] { 0f, 1f }
            select Transform(e.GetLocalTransformMatrix(0), new Vec3d(x * (e.To[0] - e.From[0]) / 16, y * (e.To[1] - e.From[1]) / 16, z * (e.To[2] - e.From[2]) / 16)));
    private static Vec3d Transform(float[] m, Vec3d p) => new(m[0]*p.X+m[4]*p.Y+m[8]*p.Z+m[12],m[1]*p.X+m[5]*p.Y+m[9]*p.Z+m[13],m[2]*p.X+m[6]*p.Y+m[10]*p.Z+m[14]);
}

internal class HangingTestEntity : BlockEntityHangingPepperBundle
{
    public override void MarkDirty(bool redrawOnClient = false, IPlayer skipPlayer = null) { }
}
internal class HangingTestCeiling : Block
{
    public bool Supported = true;
    public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing facing, Cuboidi area = null)
        => Supported && facing == BlockFacing.DOWN && area != null;
}
internal class HangingInventory : PlayerInventoryManager, IPlayerInventoryManager
{
    public bool Full;
    public List<ItemStack> Received = new();
    public HangingInventory(IPlayer player) : base(new Vintagestory.API.Datastructures.OrderedDictionary<string, InventoryBase>(), player) { }
    public override ItemSlot CurrentHoveredSlot { get; set; }
    public override void BroadcastHotbarSlot() { }
    public override bool DropItem(ItemSlot slot, bool fullStack) => false;
    public override void NotifySlot(IPlayer player, ItemSlot slot) { }
    bool IPlayerInventoryManager.TryGiveItemstack(ItemStack stack, bool slotNotifyEffect) {
        if (Full) return false;
        Received.Add(stack.Clone()); stack.StackSize = 0; return true;
    }
}
internal class HangingFixture
{
    public double Now = 100;
    public bool Access = true, Water;
    public BlockPos Pos = new(10,20,30);
    public Block Air = new() { BlockId = 0, Code = new AssetLocation("game:air"), Replaceable = 10000 };
    public HangingTestCeiling Ceiling = new() { BlockId = 9, Code = new AssetLocation("game:stone"), Replaceable = 0 };
    public Block Current;
    public HangingTestEntity Entity;
    public Dictionary<string, Item> Items = new();
    public Dictionary<string, BlockHangingPepperBundle> Blocks = new();
    public List<ItemStack> Drops = new();
    public ICoreAPI Api;
    public IWorldAccessor World;
    public IPlayer Player;
    public EntityPlayer PlayerEntity;
    public HangingInventory Inventory;
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public HangingFixture(EnumAppSide side = EnumAppSide.Server) {
        Current = Air;
        var definition = PepperBundleTests.Read("itemtypes/food/pepperbundle.json");
        int id = 100;
        foreach (string type in PepperBundleTests.Types) foreach (string state in PepperBundleTests.States) {
            string code = $"pepperbundle-{state}-{type}";
            var item = new ItemPepperBundle { ItemId = id++, Code = new AssetLocation("peppermod", code), MaxStackSize = 8,
                Attributes = new JsonObject(definition["attributesByType"][code].DeepClone()), TransitionableProps = definition["transitionablePropsByType"][code].ToObject<TransitionableProperties[]>() };
            item.Variant = new Vintagestory.API.Util.RelaxedReadOnlyDictionary<string,string>(new Dictionary<string,string> { ["type"] = type, ["state"] = state }); Items.Add(item.Code.ToString(), item);
            var block = new BlockHangingPepperBundle { BlockId = id++, Code = new AssetLocation("peppermod", "hanging"+code), CollisionBoxes = Array.Empty<Cuboidf>(), Replaceable = 0 };
            block.Variant = item.Variant; Blocks.Add(block.Code.ToString(),block);
        }
        Items.Add("game:rot", new Item { ItemId = id++, Code = new AssetLocation("game:rot") });
        var calendar = Proxy.Make<IGameCalendar>((m,_) => m.Name == "get_TotalHours" ? Now : throw new NotSupportedException(m.Name));
        var claims = Proxy.Make<ILandClaimAPI>((m,_) => m.Name == "TryAccess" ? Access : throw new NotSupportedException(m.Name));
        var logger = Proxy.Make<ILogger>((m,a) => m.Name.Contains("Error") ? throw new Exception(string.Join(" ",a)) : null);
        var random = new Random(11);
        var accessor = Proxy.Make<IBlockAccessor>((m,a) => {
            switch(m.Name) {
                case "GetBlock":
                    if (a.Length > 1 && (int)a[1] == BlockLayersAccess.Fluid) return Water ? new Block { LiquidCode = "water", MatterState = EnumMatterState.Liquid } : Air;
                    return ((BlockPos)a[0]).Equals(Pos.UpCopy()) ? Ceiling : Current;
                case "GetBlockEntity": return Entity;
                case "SetBlock":
                    Current = (int)a[0] == 0 ? Air : Blocks.Values.Single(b => b.Id == (int)a[0]);
                    if (Current.Id == 0) { Entity = null; return null; }
                    NewEntity(); Entity.OnBlockPlaced(a.Length > 2 ? (ItemStack)a[2] : null); return null;
                case "ExchangeBlock": Current = Blocks.Values.Single(b => b.Id == (int)a[0]); typeof(BlockEntity).GetProperty("Block").SetValue(Entity,Current); return null;
                case "BreakBlock": Drops.AddRange(Current.GetDrops(World,Pos,null)); Current = Air; Entity = null; return null;
                default: throw new NotSupportedException(m.Name);
            }
        });
        World = Proxy.Make<IWorldAccessor>((m,a) => m.Name switch {
            "get_Side" => side, "get_Api" => Api, "get_Calendar" => calendar, "get_Rand" => random, "get_Logger" => logger,
            "get_Claims" => claims, "get_BlockAccessor" => accessor, "PlayerByUid" => Player,
            "GetItem" => a[0] is AssetLocation loc ? Items.GetValueOrDefault(loc.ToString()) : Items.Values.Single(i => i.Id == (int)a[0]),
            "GetBlock" => a[0] is AssetLocation loc ? Blocks.GetValueOrDefault(loc.ToString()) : Blocks.Values.Single(i => i.Id == (int)a[0]),
            "SpawnItemEntity" => Drop((ItemStack)a[0]),
            _ => throw new NotSupportedException(m.Name)
        });
        Api = Proxy.Make<ICoreAPI>((m,_) => m.Name switch { "get_World" => World, "get_Side" => side, "get_Logger" => logger, _ => throw new NotSupportedException(m.Name) });
        foreach (var item in Items.Values) typeof(CollectibleObject).GetField("api",Flags).SetValue(item,Api);
        foreach (var item in Items.Values.Where(i => i.TransitionableProps != null)) foreach (var prop in item.TransitionableProps) prop.TransitionedStack.Resolve(World,"hanging test");
        Player = Fixture.PlayerWithId("hanging"); Inventory = new HangingInventory(Player);
        typeof(ServerWorldPlayerData).GetField("connected",Flags).SetValue(Player.WorldData,true);
        typeof(ServerPlayer).GetField("inventoryMgr",Flags).SetValue(Player,Inventory);
        PlayerEntity = new EntityPlayer { Api = Api, World = World };
        typeof(ServerWorldPlayerData).GetField("Entityplayer",Flags).SetValue(Player.WorldData,PlayerEntity);
    }
    private object Drop(ItemStack stack) { Drops.Add(stack.Clone()); return null; }
    private void NewEntity() { Entity = new HangingTestEntity { Api = Api, Pos = Pos.Copy() }; typeof(BlockEntity).GetProperty("Block").SetValue(Entity,Current); }
    public void Restore(ITreeAttribute tree) { NewEntity(); Entity.FromTreeAttributes(tree,World); }
    public void Tick() => typeof(BlockEntityHangingPepperBundle).GetMethod("OnTick", Flags).Invoke(Entity, new object[] { 2f });
    public ItemPepperBundle Item(string type,string state) => (ItemPepperBundle)Items[$"peppermod:pepperbundle-{state}-{type}"];
    public DummySlot Input(string type,string state,int count=1) {
        var slot = new DummySlot(new ItemStack(Item(type,state),count));
        slot.Itemstack.Collectible.UpdateAndGetTransitionState(World,slot,EnumTransitionType.Perish); return slot;
    }
    public void Place(ItemSlot input,BlockFacing face=null,bool first=true) {
        var handling=EnumHandHandling.NotHandled;
        input.Itemstack.Collectible.OnHeldInteractStart(input,PlayerEntity,new BlockSelection { Position=Pos.UpCopy(),Face=face??BlockFacing.DOWN },null,first,ref handling);
    }
    public float Age(ItemStack stack) => ((FloatArrayAttribute)stack.Attributes.GetTreeAttribute("transitionstate")["transitionedHours"]).value[0];
}
