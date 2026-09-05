using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using PepperMod;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Server;

int passed = 0;
int failed = 0;
void Check(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine("PASS " + name);
        passed++;
    }
    catch (Exception error)
    {
        Console.Error.WriteLine("FAIL " + name + ": " + error);
        failed++;
    }
}
void Assert(bool condition, string message = "Assertion failed")
{
    if (!condition) throw new Exception(message);
}

Check("starting a hold produces no drops", () => {
    var f = new Fixture();
    Assert(f.Block.OnBlockInteractStart(f.World, f.Player, f.Selection));
    Assert(f.Drops.Count == 0 && f.CurrentBlock.BlockId == 8);
});
Check("short click and early stop do not harvest", () => {
    foreach (float time in new[] { 0f, .1f, 1f, 1.49f, float.NaN }) {
        var f = new Fixture(); f.Start(); f.Stop(time);
        Assert(f.Drops.Count == 0 && f.CurrentBlock.BlockId == 8);
    }
});
Check("server hold completes at 1.5 seconds", () => {
    var f = new Fixture(); f.Start();
    Assert(f.Step(1.49f)); Assert(!f.Step(1.5f));
    Assert(f.Drops.Count == 0); f.Stop(1.5f);
    Assert(f.Drops.Sum(d => d.Stack.StackSize) >= 16 && f.Drops.Sum(d => d.Stack.StackSize) <= 24);
    Assert(f.CurrentBlock.BlockId == 10 && f.Plant.DirtyCount == 1);
});
Check("holding longer still harvests only once", () => {
    var f = new Fixture(); f.Start(); f.Stop(2f);
    int count = f.Drops.Count;
    f.Stop(3f); Assert(!f.Step(3f)); Assert(f.Drops.Count == count && count > 0);
});
Check("stop without start cannot harvest", () => {
    var f = new Fixture(); f.Stop(2f); Assert(f.Drops.Count == 0);
});
Check("cancel clears the hold even if a late stop arrives", () => {
    foreach (float time in new[] { .3f, 1.49f, 2f }) {
        var f = new Fixture(); f.Start();
        Assert(f.Block.OnBlockInteractCancel(time, f.World, f.Player, f.Selection, (EnumItemUseCancelReason)0));
        f.Stop(2f); Assert(f.Drops.Count == 0);
    }
});
Check("new hold after cancellation must finish its own timer", () => {
    var f = new Fixture(); f.Start();
    f.Block.OnBlockInteractCancel(1.4f, f.World, f.Player, f.Selection, (EnumItemUseCancelReason)0);
    f.Start(); f.Stop(.2f); Assert(f.Drops.Count == 0);
    f.Start(); f.Stop(1.5f); Assert(f.Drops.Count > 0);
});
Check("moving to a different plant cancels", () => {
    var f = new Fixture(); f.Start();
    var other = new BlockSelection { Position = f.Selection.Position.AddCopy(1, 0, 0) };
    Assert(!f.Block.OnBlockInteractStep(1f, f.World, f.Player, other));
    f.Stop(2f); Assert(f.Drops.Count == 0);
});
Check("losing the selection cancels", () => {
    var f = new Fixture(); f.Start();
    Assert(!f.Block.OnBlockInteractStep(1f, f.World, f.Player, null));
    f.Stop(2f); Assert(f.Drops.Count == 0);
});
Check("replacing a plant during the hold prevents harvesting", () => {
    var f = new Fixture(); f.Start(); f.CurrentBlock = f.Harvested;
    Assert(!f.Step(2f)); f.Stop(2f); Assert(f.Drops.Count == 0);
});
Check("two players cannot duplicate one plant's yield", () => {
    var f = new Fixture(); var other = Fixture.PlayerWithId("second");
    f.Start(); Assert(f.Block.OnBlockInteractStart(f.World, other, f.Selection));
    f.Stop(1.5f); int count = f.Drops.Count;
    f.Block.OnBlockInteractStop(1.5f, f.World, other, f.Selection);
    Assert(count > 0 && f.Drops.Count == count);
});
Check("canceling one player's hold does not cancel another's", () => {
    var f = new Fixture(); var other = Fixture.PlayerWithId("second");
    f.Start(); f.Block.OnBlockInteractStart(f.World, other, f.Selection);
    f.Block.OnBlockInteractCancel(.5f, f.World, f.Player, f.Selection, (EnumItemUseCancelReason)0);
    f.Block.OnBlockInteractStop(1.5f, f.World, other, f.Selection);
    Assert(f.Drops.Count > 0);
});
Check("client waits for server without spawning items", () => {
    var f = new Fixture(EnumAppSide.Client); f.Start();
    Assert(f.Step(2f)); f.Stop(2f);
    Assert(f.Drops.Count == 0 && f.CurrentBlock.BlockId == 8);
});
Check("unripe and missing targets cannot start", () => {
    var f = new Fixture(); f.CurrentBlock = f.Harvested;
    Assert(!f.Block.OnBlockInteractStart(f.World, f.Player, f.Selection));
    Assert(!f.Block.OnBlockInteractStart(f.World, f.Player, null));
    Assert(!f.Block.OnBlockInteractStart(f.World, null, f.Selection));
});
Check("claims checked at start and completion", () => {
    var f = new Fixture(); f.AccessAllowed = false;
    Assert(!f.Block.OnBlockInteractStart(f.World, f.Player, f.Selection));
    f.AccessAllowed = true; f.Start(); f.AccessAllowed = false; f.Stop(2f);
    Assert(f.Drops.Count == 0);
});
Check("seasonal harvest restriction is preserved", () => {
    var f = new Fixture(); f.Temperature = -5; f.Start(); f.Stop(2f);
    Assert(f.Drops.Count == 0 && f.CurrentBlock.BlockId == 8);
});
Check("custom hold duration and fallback duration", () => {
    var f = new Fixture(); f.Config["harvestHoldSeconds"] = 2;
    f.Start(); Assert(f.Step(1.5f)); f.Stop(1.5f); Assert(f.Drops.Count == 0);
    f.Start(); Assert(!f.Step(2f)); f.Stop(2f); Assert(f.Drops.Count > 0);
    foreach (float value in new[] { 0f, -2f }) {
        var invalid = new Fixture(); invalid.Config["harvestHoldSeconds"] = value;
        invalid.Start(); Assert(invalid.Step(1.49f)); Assert(!invalid.Step(1.5f));
    }
    var fallback = new Fixture(); fallback.Config.Remove("harvestHoldSeconds");
    fallback.Start(); Assert(fallback.Step(1.49f)); Assert(!fallback.Step(1.5f));
});
Check("drop stacks fall vertically within the plant footprint", () => {
    for (int seed = 0; seed < 100; seed++) {
        var f = new Fixture(seed: seed); f.Config["seedChanceOnHarvest"] = 1;
        f.Start(); f.Stop(1.5f);
        int peppers = f.Drops.Where(d => d.Stack.Item.Code.Path == "vegetable-jalapeno").Sum(d => d.Stack.StackSize);
        int seeds = f.Drops.Where(d => d.Stack.Item.Code.Path == "seeds-jalapeno").Sum(d => d.Stack.StackSize);
        Assert(peppers >= 16 && peppers <= 24 && seeds == 1);
        foreach (var drop in f.Drops) {
            Assert(drop.Stack.StackSize >= 1 && drop.Stack.StackSize <= 4);
            Assert(drop.Velocity.X == 0 && drop.Velocity.Z == 0 && drop.Velocity.Y == -.005);
            double dx = drop.Position.X - f.Selection.Position.X - .5;
            double dz = drop.Position.Z - f.Selection.Position.Z - .5;
            Assert(Math.Sqrt(dx * dx + dz * dz) <= .300001);
            double y = drop.Position.Y - f.Selection.Position.Y;
            Assert(y >= .65 && y <= .80);
        }
    }
});
HeldItemTransformTests.Run(Check);
SpiceTests.Run(Check);
WildPepperTests.Run(Check);
Console.WriteLine($"Passed {passed} regression tests.");
if (failed > 0) Console.Error.WriteLine($"Failed {failed} regression tests.");
Environment.ExitCode = failed == 0 ? 0 : 1;

public class Proxy : DispatchProxy
{
    public System.Func<MethodInfo, object[], object> Handler;
    protected override object Invoke(MethodInfo method, object[] args) => Handler(method, args);
    public static T Make<T>(System.Func<MethodInfo, object[], object> handler) where T : class
    {
        T value = Create<T, Proxy>();
        ((Proxy)(object)value).Handler = handler;
        return value;
    }
}

public class TestPlant : BlockEntityPerennialPepperPlant
{
    public int DirtyCount;
    public override void MarkDirty(bool redrawOnClient = false, IPlayer skipPlayer = null) => DirtyCount++;
}

public class Fixture
{
    public BlockPerennialPepperPlant Block = new BlockPerennialPepperPlant { BlockId = 8, Code = new AssetLocation("peppermod", "crop-jalapeno-8") };
    public Block Harvested = new Block { BlockId = 10, Code = new AssetLocation("peppermod", "crop-jalapeno-10") };
    public Block CurrentBlock;
    public BlockSelection Selection = new BlockSelection { Position = new BlockPos(10, 20, 30) };
    public IWorldAccessor World;
    public IPlayer Player;
    public TestPlant Plant = new TestPlant();
    public bool AccessAllowed = true;
    public float Temperature = 20;
    public JObject Config;
    public List<(ItemStack Stack, Vec3d Position, Vec3d Velocity)> Drops = new();
    private static readonly PropertyInfo EntityBlock = typeof(BlockEntity).GetProperty("Block");

    public static IPlayer PlayerWithId(string id)
    {
        // IPlayer has an internal abstract member, so use the game's implementation
        // with only identity data initialized, without starting a server.
        var player = (ServerPlayer)RuntimeHelpers.GetUninitializedObject(typeof(ServerPlayer));
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var worldData = new ServerWorldPlayerData();
        typeof(ServerWorldPlayerData).GetField("PlayerUID", flags).SetValue(worldData, id);
        typeof(ServerPlayer).GetField("worlddata", flags).SetValue(player, worldData);
        typeof(ServerPlayer).GetField("serverdata", flags).SetValue(player, new ServerPlayerData { PlayerUID = id });
        return player;
    }

    public Fixture(EnumAppSide side = EnumAppSide.Server, int seed = 1)
    {
        Config = JObject.Parse("{ 'harvestHoldSeconds': 1.5, 'harvestStage': 8, 'postHarvestStage': 10, 'harvestPepperMin': 16, 'harvestPepperMax': 24, 'seedChanceOnHarvest': 0, 'harvestDropStackSize': 4 }");
        Block.Attributes = new JsonObject(new JObject { ["perennialPepperPlant"] = Config });
        CurrentBlock = Block;
        Player = PlayerWithId("first");
        var claims = Proxy.Make<ILandClaimAPI>((m, _) => m.Name == "TryAccess" ? AccessAllowed : throw new NotSupportedException(m.Name));
        var calendar = Proxy.Make<IGameCalendar>((m, _) => m.Name == "get_TotalHours" ? 100.0 : throw new NotSupportedException(m.Name));
        var blocks = Proxy.Make<IBlockAccessor>((m, args) => {
            switch (m.Name) {
                case "GetBlock": return CurrentBlock;
                case "GetBlockEntity": return Plant;
                case "GetClimateAt": return new ClimateCondition { Temperature = Temperature };
                case "ExchangeBlock": CurrentBlock = Harvested; EntityBlock.SetValue(Plant, Harvested); return null;
                default: throw new NotSupportedException(m.Name);
            }
        });
        Random random = new Random(seed);
        World = Proxy.Make<IWorldAccessor>((m, args) => {
            switch (m.Name) {
                case "get_Side": return side;
                case "get_Claims": return claims;
                case "get_BlockAccessor": return blocks;
                case "get_Calendar": return calendar;
                case "get_Rand": return random;
                case "PlaySoundAt": return null;
                case "GetBlock": return Harvested;
                case "GetItem": return new Item { ItemId = 1, Code = (AssetLocation)args[0], MaxStackSize = 64 };
                case "SpawnItemEntity": Drops.Add(((ItemStack)args[0], ((Vec3d)args[1]).Clone(), ((Vec3d)args[2]).Clone())); return null;
                default: throw new NotSupportedException(m.Name);
            }
        });
        var api = Proxy.Make<ICoreAPI>((m, _) => m.Name switch {
            "get_Side" => side,
            "get_World" => World,
            _ => throw new NotSupportedException(m.Name)
        });
        Plant.Api = api; Plant.Pos = Selection.Position.Copy(); EntityBlock.SetValue(Plant, Block);
    }

    public void Start() => Block.OnBlockInteractStart(World, Player, Selection);
    public bool Step(float seconds) => Block.OnBlockInteractStep(seconds, World, Player, Selection);
    public void Stop(float seconds) => Block.OnBlockInteractStop(seconds, World, Player, Selection);
}
