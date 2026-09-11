using System.Reflection;
using Newtonsoft.Json.Linq;
using PepperMod;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using Vintagestory.Server;

internal static class PepperBundleTests
{
    internal static string Root {
        get {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "modinfo.json"))) dir = dir.Parent;
            return dir?.FullName ?? throw new Exception("No project root");
        }
    }
    internal static JObject Read(string path) => JObject.Parse(File.ReadAllText(Path.Combine(Root, "assets/peppermod", path)));
    internal static readonly string[] Types = { "jalapeno", "habanero", "serrano" };
    internal static readonly string[] States = { "raw", "baked", "dried" };
    internal static string SingleCode(string type, string state) => state == "raw" ? $"vegetable-{type}" : $"preparedpepper-{state}-{type}";
    internal static void Require(bool value, string message = "Bundle regression failed") { if (!value) throw new Exception(message); }
    public static void Run(Action<string, Action> check) {
        check("bundle recipes use eight matching peppers around one flax fiber", () => {
            var f = new BundleFixture();
            Require(f.Recipes.Count == Types.Length * States.Length * 2);
            foreach (string type in Types) foreach (string state in States) {
                var recipe = f.Recipe("tie", type, state);
                Require(recipe.Width == 3 && recipe.Height == 3 && !recipe.Shapeless);
                Require(recipe.IngredientPattern == "PPPPFPPPP");
                var slots = f.TieInputs(type, state, 2);
                Require(recipe.Matches(f.Player, f.World, slots, 3));
                Require(recipe.Output.ResolvedItemStack.StackSize == 1);
                Require(recipe.Output.ResolvedItemStack.Collectible == f.Bundle(type, state));
                Require(recipe.ConsumeInput(f.Player, slots, 3));
                Require(slots.All(s => s.StackSize == 1), "Craft must consume exactly one item from all nine slots");
                Require(recipe.ConsumeInput(f.Player, slots, 3));
                Require(slots.All(s => s.Empty));
            }
        });
        check("bundle recipes reject missing fiber misplaced fiber mixed types and mixed stages", () => {
            var f = new BundleFixture();
            foreach (string type in Types) foreach (string state in States) {
                var recipe = f.Recipe("tie", type, state);
                for (int missing = 0; missing < 9; missing++) {
                    var slots = f.TieInputs(type, state); slots[missing].Itemstack = null;
                    Require(!recipe.Matches(f.Player, f.World, slots, 3));
                }
                var wrong = f.TieInputs(type, state); (wrong[0], wrong[4]) = (wrong[4], wrong[0]);
                Require(!recipe.Matches(f.Player, f.World, wrong, 3));
                wrong = f.TieInputs(type, state); wrong[0].Itemstack = new ItemStack(f.Single(type == "jalapeno" ? "habanero" : "jalapeno", state));
                Require(!recipe.Matches(f.Player, f.World, wrong, 3));
                wrong = f.TieInputs(type, state); wrong[0].Itemstack = new ItemStack(f.Single(type, state == "raw" ? "baked" : "raw"));
                Require(!recipe.Matches(f.Player, f.World, wrong, 3));
            }
        });
        check("unpacking in any grid slot returns exactly eight peppers in the same state", () => {
            var f = new BundleFixture();
            foreach (string type in Types) foreach (string state in States) for (int position = 0; position < 9; position++) {
                var recipe = f.Recipe("untie", type, state);
                var slots = Enumerable.Range(0, 9).Select(_ => (ItemSlot)new DummySlot()).ToArray();
                slots[position].Itemstack = new ItemStack(f.Bundle(type, state), 2);
                Require(recipe.Matches(f.Player, f.World, slots, 3));
                Require(recipe.Output.ResolvedItemStack.StackSize == 8 && recipe.Output.ResolvedItemStack.Item == f.Single(type, state));
                Require(recipe.ConsumeInput(f.Player, slots, 3) && slots[position].StackSize == 1);
                Require(recipe.Ingredients.Values.All(i => i.ReturnedStack == null), "Unpacking must not duplicate fiber");
            }
        });
        check("tying and untying preserve the oldest timer without repeated crafting refresh", () => {
            var f = new BundleFixture();
            foreach (string type in Types) foreach (string state in States) foreach (float age in new[] { .5f, 1.3f }) {
                var slots = f.TieInputs(type, state);
                f.SetAge(slots[0].Itemstack, age);
                var expected = slots[0].Itemstack.Attributes.GetTreeAttribute("transitionstate").ToJsonToken();
                f.Single(type, state).SetTemperature(f.World, slots[0].Itemstack, 110, false);
                var bundle = new DummySlot(new ItemStack(f.Bundle(type, state)));
                bundle.Itemstack.Collectible.OnCreatedByCrafting(slots, bundle, f.Recipe("tie", type, state));
                Require(bundle.Itemstack.Attributes.GetTreeAttribute("transitionstate").ToJsonToken() == expected);
                Require(bundle.Itemstack.Collectible.GetTemperature(f.World, bundle.Itemstack) == 110);
                for (int repeat = 0; repeat < 8; repeat++) {
                    var output = new DummySlot(new ItemStack(f.Single(type, state), 8));
                    output.Itemstack.Collectible.OnCreatedByCrafting(new ItemSlot[] { bundle }, output, f.Recipe("untie", type, state));
                    Require(output.Itemstack.Attributes.GetTreeAttribute("transitionstate").ToJsonToken() == expected);
                    Require(!ReferenceEquals(bundle.Itemstack.Attributes.GetTreeAttribute("transitionstate"), output.Itemstack.Attributes.GetTreeAttribute("transitionstate")));
                    slots = f.TieInputs(type, state);
                    foreach (var slot in slots.Where((_, i) => i != 4)) slot.Itemstack.Attributes["transitionstate"] = output.Itemstack.Attributes.GetTreeAttribute("transitionstate").Clone();
                    bundle = new DummySlot(new ItemStack(f.Bundle(type, state)));
                    bundle.Itemstack.Collectible.OnCreatedByCrafting(slots, bundle, f.Recipe("tie", type, state));
                    Require(bundle.Itemstack.Attributes.GetTreeAttribute("transitionstate").ToJsonToken() == expected);
                }
            }
        });
        check("bundles stay inedible and match individual freshness and per-pepper Scoville", () => {
            var f = new BundleFixture();
            foreach (string type in Types) foreach (string state in States) {
                var item = f.Bundle(type, state); var single = f.Single(type, state);
                Require(item.NutritionProps == null && item.Attributes["peppermodBundleCount"].AsInt() == 8 && item.MaxStackSize == 8);
                Require(item.Attributes["peppermodScoville"].AsInt() == single.Attributes["peppermodScoville"].AsInt());
                var perish = item.TransitionableProps.Single(); var loose = single.TransitionableProps.Single();
                Require(perish.FreshHours.avg == loose.FreshHours.avg && perish.FreshHours.var == loose.FreshHours.var);
                Require(perish.TransitionHours.avg == loose.TransitionHours.avg && perish.TransitionRatio == 8 * loose.TransitionRatio);
                var slot = new DummySlot(new ItemStack(item));
                var handling = EnumHandHandling.NotHandled;
                item.OnHeldInteractStart(slot, f.Player.Entity, null, null, true, ref handling);
                item.Eat(3, slot, f.Player.Entity);
                Require(slot.StackSize == 1 && PepperSpiceSystem.ReadState(f.Player.Entity).Heat == 0);
            }
        });
        check("four vanilla oven slots bake and dry 32 peppers without changing capacity", () => {
            foreach (string type in Types) {
                var f = new BundleFixture(); var oven = new PreparedTestOven { Api = f.Api };
                int capacity = (int)typeof(BlockEntityOven).GetProperty("bakeableCapacity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(oven);
                Require(capacity == 4);
                for (int slot = 0; slot < capacity; slot++) oven.Load(new ItemStack(f.Bundle(type, "raw")), slot);
                for (int slot = 0; slot < capacity; slot++) {
                    Require(!BakingProperties.ReadFrom(oven.Inventory[slot].Itemstack).LargeItem);
                    oven.Advance(1000, 200, slot); Require(oven.Inventory[slot].Itemstack.Item == f.Bundle(type, "baked"));
                    oven.Advance(1000, 200, slot); Require(oven.Inventory[slot].Itemstack.Item == f.Bundle(type, "dried"));
                    oven.Advance(10000, 300, slot); Require(oven.Inventory[slot].StackSize == 1 && oven.Inventory[slot].Itemstack.Item == f.Bundle(type, "dried"));
                }
                Require(Enumerable.Range(0, capacity).Sum(i => oven.Inventory[i].StackSize * 8) == 32);
                Require(f.Recipe("untie", type, "dried").Output.ResolvedItemStack.StackSize * capacity == 32);
            }
        });
        check("bundle models attach eight staggered peppers by their stems around the cord and fit the oven", () => {
            var definition = Read("itemtypes/food/pepperbundle.json");
            foreach (string type in Types) foreach (string state in States) {
                var json = Read($"shapes/item/food/bundle/{state}/{type}.json");
                var shape = json.ToObject<Shape>(); string code = $"pepperbundle-{state}-{type}";
                Require(shape.Elements.Select(e => e.Name).Distinct().Count() == shape.Elements.Length);
                Require(shape.Elements.Count(e => e.Name.StartsWith("Cord-loop-")) == 6);
                Require(!shape.Elements.Any(e => e.Name.StartsWith("Cord-tie-") || e.Name.StartsWith("Cord-knot-")), "No strings should extend out to the fruit");
                var rawElements = Read($"shapes/item/food/bundle/raw/{type}.json")["elements"];
                Require(json["elements"].Count() == rawElements.Count());
                for (int i = 0; i < shape.Elements.Length; i++) foreach (string key in new[] { "name", "from", "to", "rotationOrigin", "rotationX", "rotationY", "rotationZ", "scaleX", "scaleY", "scaleZ", "stepParentName" })
                    Require(JToken.DeepEquals(json["elements"][i][key], rawElements[i][key]), "Baking must not rearrange the bundle");
                foreach (var texture in ((JObject)json["textures"]).Properties()) {
                    string p = (string)texture.Value;
                    string full = p.StartsWith("game:") ? Path.Combine(Environment.GetEnvironmentVariable("VINTAGE_STORY"), "assets/survival/textures", p[5..] + ".png") : Path.Combine(Root, "assets/peppermod/textures", p + ".png");
                    Require(File.Exists(full), full);
                }
                var spine = shape.Elements.Where(e => e.Name.StartsWith("Cord-spine-")).ToArray();
                var octants = new HashSet<int>();
                var heights = new List<double>();
                for (int i = 1; i <= 8; i++) {
                    Require(shape.Elements.Any(e => e.Name.StartsWith($"Pepper-{i}-")));
                    var stem = shape.Elements.Single(e => e.Name == $"Pepper-{i}-" + (type == "jalapeno" ? "Stem-cut-end" : "Stem-tip"));
                    var stemTip = Point(stem, .5f, 1, .5f);
                    Require(spine.Min(e => DistanceToSegment(stemTip, Point(e, .5f, 0, .5f), Point(e, .5f, 1, .5f))) < .004, "Stem must touch the central cord: " + stem.Name);
                    heights.Add(stemTip.Y);
                    var fruit = shape.Elements.Where(e => e.Name.StartsWith($"Pepper-{i}-" + (type == "jalapeno" ? "Fruit-" : "Pepper-fruit-")))
                        .Select(e => Point(e, .5f, .5f, .5f)).ToArray();
                    double dx = fruit.Average(p => p.X) - stemTip.X, dz = fruit.Average(p => p.Z) - stemTip.Z;
                    Require(Math.Sqrt(dx * dx + dz * dz) > .05, "Fruit must hang outside the central cord");
                    double angle = (Math.Atan2(dz, dx) + Math.Tau) % Math.Tau;
                    octants.Add((int)(angle / (Math.PI / 4)));
                }
                Require(octants.Count >= 6 && octants.Select(o => o / 2).Distinct().Count() == 4, "Peppers must cover the full circumference, not two opposite sides");
                heights.Sort();
                Require(heights.Zip(heights.Skip(1), (a, b) => b - a).All(gap => gap > .04), "Stem attachments must be staggered, not paired rows");
                var display = definition["attributesByType"][code]["onDisplayTransform"].ToObject<ModelTransform>().AsMatrix;
                var ground = definition["groundTransformByType"][code].ToObject<ModelTransform>().AsMatrix;
                var points = shape.Elements.Where(e => e.FacesResolved?.Any(f => f?.Enabled == true) == true)
                    .SelectMany(e => from x in new[] { 0f, 1f } from y in new[] { 0f, 1f } from z in new[] { 0f, 1f } select Point(e, x, y, z)).ToArray();
                var ovenPoints = points.Select(p => Transform(display, p)).ToArray();
                Require(ovenPoints.All(p => double.IsFinite(p.X + p.Y + p.Z)));
                Require(ovenPoints.Min(p => p.Y) >= .009 && ovenPoints.Max(p => p.Y) < .2);
                Require(ovenPoints.Max(p => p.X) - ovenPoints.Min(p => p.X) < .31 && ovenPoints.Max(p => p.Z) - ovenPoints.Min(p => p.Z) < .31);
                Require(Math.Abs(points.Select(p => Transform(ground, p)).Min(p => p.Y) - .01) < .001, "Dropped bundle floats or clips through ground");
            }
        });
    }
    private static Vec3d Point(ShapeElement e, float x, float y, float z) => Transform(e.GetLocalTransformMatrix(0),
        new Vec3d(x * (e.To[0] - e.From[0]) / 16, y * (e.To[1] - e.From[1]) / 16, z * (e.To[2] - e.From[2]) / 16));
    private static Vec3d Transform(float[] m, Vec3d p) => new(m[0] * p.X + m[4] * p.Y + m[8] * p.Z + m[12], m[1] * p.X + m[5] * p.Y + m[9] * p.Z + m[13], m[2] * p.X + m[6] * p.Y + m[10] * p.Z + m[14]);
    private static double DistanceToSegment(Vec3d p, Vec3d a, Vec3d b) {
        double dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy + (p.Z - a.Z) * dz) / (dx * dx + dy * dy + dz * dz), 0, 1);
        return p.DistanceTo(new Vec3d(a.X + t * dx, a.Y + t * dy, a.Z + t * dz));
    }
}

internal class BundleTestFood : ItemPepperFood
{
    public void SetApi(ICoreAPI value) => api = value;
    public void Eat(float seconds, ItemSlot slot, EntityAgent entity) => tryEatStop(seconds, slot, entity);
    public override TransitionState UpdateAndGetTransitionState(IWorldAccessor world, ItemSlot slot, EnumTransitionType type) {
        if (slot.Empty) return null;
        var props = TransitionableProps.Single();
        var tree = slot.Itemstack.Attributes.GetTreeAttribute("transitionstate");
        return new TransitionState { Props = props,
            FreshHours = tree == null ? props.FreshHours.avg : ((FloatArrayAttribute)tree["freshHours"]).value[0],
            TransitionHours = tree == null ? props.TransitionHours.avg : ((FloatArrayAttribute)tree["transitionHours"]).value[0],
            TransitionedHours = tree == null ? 0 : ((FloatArrayAttribute)tree["transitionedHours"]).value[0] };
    }
}

internal class BundleFixture
{
    public readonly Dictionary<string, Item> Items = new();
    public List<GridRecipe> Recipes;
    public IWorldAccessor World;
    public ICoreAPI Api;
    public IPlayer Player;
    public BundleFixture() {
        var definition = PepperBundleTests.Read("itemtypes/food/pepperbundle.json");
        int id = 1000;
        foreach (string type in PepperBundleTests.Types) foreach (string state in PepperBundleTests.States) {
            var original = PreparedPepperTests.Load(type, state);
            var single = new BundleTestFood { ItemId = id++, Code = original.Code, MaxStackSize = 64, Attributes = original.Attributes,
                NutritionProps = original.NutritionProps, TransitionableProps = original.TransitionableProps, CombustibleProps = original.CombustibleProps };
            Items.Add(single.Code.ToString(), single);
            string code = $"pepperbundle-{state}-{type}";
            Items.Add("peppermod:" + code, new BundleTestFood { ItemId = id++, Code = new AssetLocation("peppermod", code), MaxStackSize = (int)definition["maxstacksize"],
                Attributes = new JsonObject(definition["attributesByType"][code].DeepClone()), TransitionableProps = definition["transitionablePropsByType"][code].ToObject<TransitionableProperties[]>(),
                CombustibleProps = definition["combustiblePropsByType"][code]?.ToObject<CombustibleProperties>() });
        }
        Items.Add("game:flaxfibers", new Item { ItemId = id++, Code = new AssetLocation("game:flaxfibers"), MaxStackSize = 64 });
        Items.Add("game:rot", new Item { ItemId = id++, Code = new AssetLocation("game:rot"), MaxStackSize = 64 });
        var calendar = Proxy.Make<IGameCalendar>((m, _) => m.Name == "get_TotalHours" ? 100.0 : throw new NotSupportedException(m.Name));
        var events = Proxy.Make<IEventAPI>((m, _) => m.Name == "TriggerMatchesRecipe" ? true : throw new NotSupportedException(m.Name));
        var logger = Proxy.Make<ILogger>((m, a) => m.Name.StartsWith("Error") ? throw new Exception(string.Join(" ", a)) : null);
        var random = new Random(31);
        var recipeIndex = new System.Collections.Generic.OrderedDictionary<IRecipeIngredientBase, List<IRecipeBase>>();
        World = Proxy.Make<IWorldAccessor>((m, a) => m.Name switch {
            "get_Side" => EnumAppSide.Server, "get_Api" => Api, "get_Calendar" => calendar, "get_Rand" => random, "get_Logger" => logger,
            "get_FastSearchRecipesByIngredient" => recipeIndex,
            "GetItem" => a[0] is AssetLocation location ? Items.GetValueOrDefault(location.ToString()) : Items.Values.Single(i => i.ItemId == (int)a[0]),
            _ => throw new NotSupportedException(m.Name)
        });
        Api = Proxy.Make<ICoreAPI>((m, _) => m.Name switch {
            "get_World" => World, "get_Side" => EnumAppSide.Server, "get_Event" => events, "get_Logger" => logger,
            _ => throw new NotSupportedException(m.Name)
        });
        var apiField = typeof(CollectibleObject).GetField("api", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var item in Items.Values) apiField.SetValue(item, Api);
        Player = Fixture.PlayerWithId("bundle-test");
        typeof(ServerWorldPlayerData).GetField("Entityplayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(Player.WorldData, new EntityPlayer { Api = Api, World = World });
        Recipes = JArray.Parse(File.ReadAllText(Path.Combine(PepperBundleTests.Root, "assets/peppermod/recipes/grid/pepperbundles.json"))).ToObject<List<GridRecipe>>();
        foreach (var recipe in Recipes) PepperBundleTests.Require(recipe.Resolve(World, "pepper bundle regression"), recipe.Name.ToString());
    }
    public BundleTestFood Single(string type, string state) => (BundleTestFood)Items["peppermod:" + PepperBundleTests.SingleCode(type, state)];
    public BundleTestFood Bundle(string type, string state) => (BundleTestFood)Items[$"peppermod:pepperbundle-{state}-{type}"];
    public GridRecipe Recipe(string operation, string type, string state) => Recipes.Single(r => r.Name.Path == $"bundle-{operation}-{state}-{type}");
    public ItemSlot[] TieInputs(string type, string state, int quantity = 1) => Enumerable.Range(0, 9).Select(i => {
        var stack = new ItemStack(i == 4 ? Items["game:flaxfibers"] : Single(type, state), quantity);
        if (i != 4) SetAge(stack, .1f);
        return (ItemSlot)new DummySlot(stack);
    }).ToArray();
    public void SetAge(ItemStack stack, float age) {
        var props = stack.Collectible.TransitionableProps.Single(); var tree = new TreeAttribute();
        tree.SetDouble("createdTotalHours", 20); tree.SetDouble("lastUpdatedTotalHours", 100);
        tree["freshHours"] = new FloatArrayAttribute(new[] { props.FreshHours.avg });
        tree["transitionHours"] = new FloatArrayAttribute(new[] { props.TransitionHours.avg });
        tree["transitionedHours"] = new FloatArrayAttribute(new[] { age <= 1 ? age * props.FreshHours.avg : props.FreshHours.avg + (age - 1) * props.TransitionHours.avg });
        stack.Attributes["transitionstate"] = tree;
    }
}
