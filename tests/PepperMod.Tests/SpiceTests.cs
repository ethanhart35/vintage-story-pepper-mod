using System.Reflection;
using Newtonsoft.Json.Linq;
using PepperMod;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

internal static class SpiceTests
{
    public static void Run(Action<string, Action> check)
    {
        check("spice tier boundaries and repeated jalapenos", () => {
            Require(default(SpiceState).Level == SpiceLevel.None);
            var state = new SpiceState().Add(25); Require(state.Level == SpiceLevel.Mild);
            state = state.Cool(2).Add(25); Require(state.Level == SpiceLevel.Hot);
            state = state.Cool(2).Add(25); Require(state.Level == SpiceLevel.Extreme);
            Require(new SpiceState(33.99f).Level == SpiceLevel.Mild);
            Require(new SpiceState(34).Level == SpiceLevel.Hot);
            Require(new SpiceState(66.99f).Level == SpiceLevel.Hot);
            Require(new SpiceState(67).Level == SpiceLevel.Extreme);
        });
        check("spice is capped and invalid doses are ignored", () => {
            var state = new SpiceState(80).Add(1000); Require(state.Heat == 100);
            foreach (float bad in new[] { 0f, -10f, float.NaN, float.PositiveInfinity })
                Require(state.Add(bad).Heat == 100 && state.Add(bad).CoolingDelay == 5);
            Require(new SpiceState(float.NaN).Heat == 0);
            Require(new SpiceState(-1).Heat == 0);
        });
        check("cooling grace and decay do not depend on tick size", () => {
            var state = new SpiceState().Add(25);
            Require(state.Cool(5).Heat == 25);
            Require(state.Cool(6).Heat == 24);
            Require(state.Cool(30).Level == SpiceLevel.None);
            var stepped = state;
            for (int i = 0; i < 120; i++) stepped = stepped.Cool(.25f);
            Require(stepped.Heat == state.Cool(30).Heat);
            foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) Require(state.Cool(bad).Heat == 25);
        });
        check("spice cools through all levels and HUD segments drain", () => {
            var extreme = new SpiceState(100);
            Require(extreme.SegmentFill(0) == 1 && extreme.SegmentFill(1) == 1 && extreme.SegmentFill(2) == 1);
            var hot = extreme.Cool(34); Require(hot.Level == SpiceLevel.Hot && hot.SegmentFill(2) == 0);
            var mild = hot.Cool(33); Require(mild.Level == SpiceLevel.Mild && mild.SegmentFill(1) == 0);
            Require(mild.Cool(33).Level == SpiceLevel.None);
            Require(mild.RedIntensity == 0 && hot.RedIntensity == 0 && extreme.RedIntensity > 0);
        });
        check("mild never warms and hot warming is capped", () => {
            Require(new SpiceState(25).WarmBody(35, 37, 60) == 35);
            Require(new SpiceState(50).WarmBody(35, 37, 1) > 35);
            Require(new SpiceState(50).WarmBody(35, 37, 10000) == 39);
            Require(new SpiceState(100).WarmBody(41, 37, 10) == 41);
            Require(new SpiceState(100).WarmBody(35, 37, float.NaN) == 35);
        });
        check("short and canceled bites do not grant food or spice", () => {
            foreach (float time in new[] { 0f, .2f, .94f, float.NaN }) {
                var f = new FoodFixture(); f.Food.Eat(time, f.Slot, f.Player);
                Require(f.Player.Saturation == 0 && f.Slot.Itemstack.StackSize == 3);
                Require(PepperSpiceSystem.ReadState(f.Player).Heat == 0);
            }
            var canceled = new FoodFixture();
            canceled.Food.OnHeldInteractCancel(2, canceled.Slot, canceled.Player, null, null, (EnumItemUseCancelReason)0);
            Require(PepperSpiceSystem.ReadState(canceled.Player).Heat == 0);
        });
        check("completed vanilla bite grants 20 satiety and spice exactly once", () => {
            var f = new FoodFixture(); f.Food.Eat(1, f.Slot, f.Player);
            Require(f.Player.Saturation == 20 && f.Slot.Itemstack.StackSize == 2);
            Require(PepperSpiceSystem.ReadState(f.Player).Heat == 25 && f.Player.DamageCalls == 0);
        });
        check("last pepper and empty-slot callbacks are safe", () => {
            var f = new FoodFixture(count: 1); f.Food.Eat(1, f.Slot, f.Player);
            Require(f.Slot.Empty && PepperSpiceSystem.ReadState(f.Player).Heat == 25);
            f.Food.Eat(2, f.Slot, f.Player);
            Require(f.Player.Saturation == 20 && PepperSpiceSystem.ReadState(f.Player).Heat == 25);
        });
        check("client cannot consume or author spice state", () => {
            var f = new FoodFixture(server: false); f.Food.Eat(1, f.Slot, f.Player);
            PepperSpiceSystem.AddSpice(f.Player, 100); PepperSpiceSystem.TickPlayer(f.Player, 100);
            Require(f.Player.Saturation == 0 && f.Slot.Itemstack.StackSize == 3);
            Require(PepperSpiceSystem.ReadState(f.Player).Heat == 0);
        });
        check("nonspicy peppers never open a spice meter", () => {
            var f = new FoodFixture(heat: 0); f.Food.Eat(1, f.Slot, f.Player);
            Require(f.Player.Saturation == 20 && !f.Player.WatchedAttributes.HasAttribute(PepperSpiceSystem.AttributeKey));
        });
        check("spice state is independent for each player", () => {
            var a = new FoodFixture(); var b = new FoodFixture();
            PepperSpiceSystem.AddSpice(a.Player, 100); PepperSpiceSystem.AddSpice(b.Player, 25);
            PepperSpiceSystem.TickPlayer(a.Player, 10);
            Require(PepperSpiceSystem.ReadState(a.Player).Heat == 95 && PepperSpiceSystem.ReadState(b.Player).Heat == 25);
        });
        check("server warming updates actual body temperature without damage", () => {
            var f = new FoodFixture(heat: 100); f.Food.Eat(1, f.Slot, f.Player);
            float before = f.Player.Temperature.CurBodyTemperature;
            PepperSpiceSystem.TickPlayer(f.Player, 1);
            Require(f.Player.Temperature.CurBodyTemperature > before && f.Player.DamageCalls == 0);
            PepperSpiceSystem.TickPlayer(f.Player, 1000);
            Require(f.Player.Temperature.CurBodyTemperature <= 39 && f.Player.DamageCalls == 0);
            Require(!f.Player.WatchedAttributes.HasAttribute(PepperSpiceSystem.AttributeKey));
            float cooled = f.Player.Temperature.CurBodyTemperature;
            PepperSpiceSystem.TickPlayer(f.Player, 1); Require(f.Player.Temperature.CurBodyTemperature == cooled);
        });
        check("death clears spice and dead players cannot add heat", () => {
            var f = new FoodFixture(); PepperSpiceSystem.AddSpice(f.Player, 100);
            f.Player.Alive = false; PepperSpiceSystem.TickPlayer(f.Player, .25f); PepperSpiceSystem.AddSpice(f.Player, 100);
            Require(PepperSpiceSystem.ReadState(f.Player).Heat == 0);
        });
        check("spice survives watched-attribute save and restore", () => {
            var f = new FoodFixture(); PepperSpiceSystem.AddSpice(f.Player, 75);
            PepperSpiceSystem.TickPlayer(f.Player, 2);
            var restored = new FoodFixture();
            byte[] bytes = ((TreeAttribute)f.Player.WatchedAttributes.GetTreeAttribute(PepperSpiceSystem.AttributeKey)).ToBytes();
            var tree = TreeAttribute.CreateFromBytes(bytes);
            restored.Player.WatchedAttributes.SetAttribute(PepperSpiceSystem.AttributeKey, tree);
            Require(PepperSpiceSystem.ReadState(restored.Player).Heat == 75);
            Require(PepperSpiceSystem.ReadState(restored.Player).CoolingDelay == 3);
            PepperSpiceSystem.TickPlayer(restored.Player, 4);
            Require(PepperSpiceSystem.ReadState(restored.Player).Heat == 74);
        });
        check("spice item assets and localized HUD labels are wired", () => {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "modinfo.json"))) root = root.Parent;
            var item = JObject.Parse(File.ReadAllText(Path.Combine(root.FullName, "assets/peppermod/itemtypes/food/vegetable.json")));
            Require((string)item["class"] == "ItemPepperFood");
            Require((float)item["nutritionPropsByType"]["*-jalapeno"]["satiety"] == 20);
            Require((float)item["nutritionPropsByType"]["*"]["satiety"] == 80);
            Require((float)item["attributesByType"]["*-jalapeno"]["peppermodSpice"] == 25);
            Require((float)item["attributesByType"]["*"]["peppermodSpice"] == 0);
            var lang = JObject.Parse(File.ReadAllText(Path.Combine(root.FullName, "assets/peppermod/lang/en.json")));
            foreach (string label in new[] { "mild", "hot", "extreme" }) Require(!string.IsNullOrEmpty((string)lang["spice-" + label]));
        });
        check("red vignette has transparent center and disposes its texture", () => {
            int[] pixels = null;
            int deleted = 0;
            var drawCalls = new List<string>();
            bool failDraw = false;
            var loadedGuis = new List<GuiDialog>();
            var gui = Proxy.Make<IGuiAPI>((m, args) => {
                if (m.Name == "get_LoadedGuis") return loadedGuis;
                if (m.Name == "DeleteTexture") { deleted++; return null; }
                throw new NotSupportedException(m.Name);
            });
            var render = Proxy.Make<IRenderAPI>((m, args) => {
                if (m.Name == "GLDepthMask") { drawCalls.Add("depth:" + args[0]); return null; }
                if (m.Name == "get_FrameWidth") return 1920;
                if (m.Name == "get_FrameHeight") return 1080;
                if (m.Name == "Render2DTexture") {
                    drawCalls.Add("draw");
                    if (failDraw) throw new InvalidOperationException("Test render failure");
                    return null;
                }
                if (m.Name != "LoadOrUpdateTextureFromRgba") throw new NotSupportedException(m.Name);
                pixels = (int[])args[0];
                var texture = (LoadedTexture)args[3];
                Require(texture.Width == 256 && texture.Height == 256);
                texture.TextureId = 42;
                return null;
            });
            var api = Proxy.Make<ICoreClientAPI>((m, _) => m.Name switch {
                "get_Gui" => gui, "get_Render" => render,
                _ => throw new NotSupportedException(m.Name)
            });
            var hud = new SpiceHud(api);
            Require(!hud.Focusable && !hud.ShouldReceiveMouseEvents() && !hud.ShouldReceiveKeyboardEvents());
            hud.Update(default);
            Require(!hud.IsOpened());
            typeof(SpiceHud).GetMethod("CreateVignette", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
            Require(pixels.Length == 256 * 256);
            Require(((uint)pixels[128 * 256 + 128] >> 24) == 0);
            Require(((uint)pixels[0] >> 24) > 200);
            Require((pixels[0] & 255) > ((pixels[0] >> 16) & 255));
            var draw = typeof(SpiceHud).GetMethod("RenderVignette", BindingFlags.Instance | BindingFlags.NonPublic);
            draw.Invoke(hud, null);
            Require(drawCalls.SequenceEqual(new[] { "depth:False", "draw", "depth:True" }));
            drawCalls.Clear(); failDraw = true;
            try { draw.Invoke(hud, null); throw new Exception("Expected render failure"); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { }
            Require(drawCalls.SequenceEqual(new[] { "depth:False", "draw", "depth:True" }));
            hud.Dispose();
            Require(deleted == 1);
        });
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new Exception("Spice regression failed.");
    }
}

internal class TestPepperFood : ItemPepperFood
{
    public void Eat(float seconds, ItemSlot slot, EntityAgent entity) => tryEatStop(seconds, slot, entity);
    public override TransitionState UpdateAndGetTransitionState(IWorldAccessor world, ItemSlot slot, EnumTransitionType type) => null;
    public void SetApi(ICoreAPI core) => api = core;
}

internal class SpicePlayer : EntityPlayer
{
    public float Saturation;
    public int DamageCalls;
    public EntityBehaviorBodyTemperature Temperature;
    public override bool Alive { get; set; } = true;
    public override void ReceiveSaturation(float amount, EnumFoodCategory category, float delay, float nutritionMultiplier) => Saturation += amount;
    public override bool ReceiveDamage(DamageSource source, float damage) { DamageCalls++; return false; }
    public override T GetBehavior<T>() => Temperature as T;
}

internal class FoodFixture
{
    public SpicePlayer Player = new();
    public TestPepperFood Food = new() { ItemId = 123, Code = new AssetLocation("peppermod", "vegetable-jalapeno"), NutritionProps = new FoodNutritionProperties { Satiety = 20, FoodCategory = EnumFoodCategory.Vegetable } };
    public DummySlot Slot;

    public FoodFixture(bool server = true, int count = 3, float heat = 25)
    {
        var player = Fixture.PlayerWithId("spice-test");
        var inventory = new SpiceInventoryManager(player);
        var field = player.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single(f => typeof(IPlayerInventoryManager).IsAssignableFrom(f.FieldType));
        field.SetValue(player, inventory);
        object Handle(MethodInfo m, object[] args) => m.Name switch {
            "get_Side" => server ? EnumAppSide.Server : EnumAppSide.Client,
            "PlayerByUid" => player,
            _ => throw new NotSupportedException(m.Name)
        };
        IWorldAccessor world = server ? Proxy.Make<IServerWorldAccessor>(Handle) : Proxy.Make<IWorldAccessor>(Handle);
        Player.World = world;
        Player.Temperature = new EntityBehaviorBodyTemperature(Player) { NormalBodyTemperature = 37 };
        var bodyTemp = new TreeAttribute(); bodyTemp.SetFloat("bodytemp", 35);
        Player.WatchedAttributes.SetAttribute("bodyTemp", bodyTemp);
        typeof(EntityBehaviorBodyTemperature).GetField("tempTree", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Player.Temperature, bodyTemp);
        Food.Attributes = new JsonObject(new JObject { ["peppermodSpice"] = heat });
        Food.SetApi(Proxy.Make<ICoreAPI>((m, _) => m.Name == "get_World" ? world : throw new NotSupportedException(m.Name)));
        Slot = new DummySlot(new ItemStack(Food, count));
    }
}

internal class SpiceInventoryManager : Vintagestory.Common.PlayerInventoryManager
{
    public SpiceInventoryManager(IPlayer player) : base(new Vintagestory.API.Datastructures.OrderedDictionary<string, InventoryBase>(), player) { }
    public override ItemSlot CurrentHoveredSlot { get; set; }
    public override void BroadcastHotbarSlot() { }
    public override void NotifySlot(IPlayer player, ItemSlot slot) { }
    public override bool DropItem(ItemSlot slot, bool fullStack) => false;
}
