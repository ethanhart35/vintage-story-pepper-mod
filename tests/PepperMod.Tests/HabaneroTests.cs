using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperMod;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vector3 = System.Numerics.Vector3;

internal static class HabaneroTests
{
    public static void Run(Action<string, Action> check)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "modinfo.json"))) root = root.Parent;
        string assets = Path.Combine(root.FullName, "assets/peppermod");
        var stages = Enumerable.Range(1, 11).ToDictionary(id => id,
            id => JObject.Parse(File.ReadAllText(Path.Combine(assets, $"shapes/block/plant/crop/habanero/stage{id}.json"))));
        var item = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes/item/food/vegetable/habanero.json")));
        var plantDefinition = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes/plant/crop/jalapeno.json")));
        var food = JObject.Parse(File.ReadAllText(Path.Combine(assets, "itemtypes/food/vegetable.json")));

        check("all habanero shapes load with valid rooted geometry and runtime textures", () => {
            foreach (var json in stages.Values.Append(item))
            {
                var shape = json.ToObject<Shape>();
                var names = new HashSet<string>();
                string rootName = (string)json["elements"][0]["name"];
                Require(shape.Elements.Length > 4, "Placeholder shape remains");
                foreach (var element in shape.Elements)
                {
                    Require(names.Add(element.Name), "Duplicate name " + element.Name);
                    Require(element.GetLocalTransformMatrix(0).All(float.IsFinite), "Invalid transform " + element.Name);
                    Require(element.Children == null || element.Children.Length == 0, "Expected flat geometry");
                    Require(Enumerable.Range(0, 3).All(i => element.To[i] >= element.From[i]), "Inverted bounds");
                }
                foreach (var element in json["elements"].Skip(1))
                {
                    Require((string)element["stepParentName"] == rootName, "Missing VSMC2 parent");
                    foreach (var face in ((JObject)element["faces"]).Properties().Select(p => p.Value))
                    {
                        string key = ((string)face["texture"])[1..];
                        string modelPath = (string)json["textures"][key];
                        var textureMap = ReferenceEquals(json, item) ? food["texturesByType"]["*-habanero"] : plantDefinition["textures"];
                        string runtimePath = ((string)textureMap[key]?["base"])?.Replace("{type}", "habanero");
                        Require(modelPath != null && runtimePath == modelPath, "Model/runtime texture mismatch: " + key);
                        Require(File.Exists(Path.Combine(assets, "textures", modelPath + ".png")), "Missing texture " + modelPath);
                        Require(face["uv"].Count() == 4 && face["uv"].Values<double>().All(n => double.IsFinite(n) && n >= 0 && n <= 16), "Invalid UV");
                    }
                }
            }
        });
        check("habanero maturity harvest regrowth and dormancy preserve the bush skeleton", () => {
            foreach (int stage in new[] { 6, 7, 10, 11 })
                Require(JToken.DeepEquals(Canopy(stages[8]), Canopy(stages[stage])), "Canopy changed at stage " + stage);
            Require(JToken.DeepEquals(Canopy(stages[8], true), Canopy(stages[9], true)), "Dormant skeleton changed");
            Require(stages[9]["elements"].Count() == Canopy(stages[9], true).Count + 1, "Dormant foliage remains");
            Require(!stages[10]["elements"].Any(e => ((string)e["name"]).Contains("-fruit-")), "Harvested fruit remains");
        });
        check("habanero fruit ripens from green to orange without moving its attachment", () => {
            Require(FruitTextures(stages[6]).SetEquals(new[] { "#peppergreen" }), "First fruit must be green");
            Require(FruitTextures(stages[7]).SetEquals(new[] { "#peppergreen", "#pepper" }), "Ripening fruit should be mixed");
            Require(FruitTextures(stages[8]).SetEquals(new[] { "#pepper" }), "Ripe fruit must be orange");
            Require(FruitTextures(stages[11]).SetEquals(new[] { "#peppergreen" }), "Regrowing fruit must be green");
            foreach (int id in new[] { 6, 7, 8, 11 })
            {
                var current = stages[id].ToObject<Shape>();
                var mature = stages[8].ToObject<Shape>();
                foreach (var calyx in current.Elements.Where(e => e.Name.EndsWith("-calyx-collar")))
                {
                    var ripe = mature.Elements.Single(e => e.Name == calyx.Name);
                    Require(calyx.RotationOrigin.SequenceEqual(ripe.RotationOrigin), "Fruit pivot moved");
                    string stalkName = calyx.Name.Replace("-calyx-collar", "-stalk-B");
                    var stalk = current.Elements.Single(e => e.Name == stalkName);
                    Require(Vector3.Distance(End(stalk), new Vector3((float)calyx.RotationOrigin[0], (float)calyx.RotationOrigin[1], (float)calyx.RotationOrigin[2])) < .02f, "Floating fruit: " + calyx.Name);
                }
            }
        });
        check("habanero leaves and fruit stalks stay attached to their supporting branches", () => {
            foreach (var json in stages.Values)
            {
                var elements = json.ToObject<Shape>().Elements;
                var branches = elements.Where(e => e.Name.StartsWith("Branch-")).ToArray();
                foreach (var leaf in elements.Where(e => e.Name.StartsWith("Leaf-")))
                {
                    var petiole = elements.Single(e => e.Name == leaf.Name.Replace("Leaf-", "Petiole-"));
                    var pivot = new Vector3((float)leaf.RotationOrigin[0], (float)leaf.RotationOrigin[1], (float)leaf.RotationOrigin[2]);
                    Require(Vector3.Distance(End(petiole), pivot) < .04f, "Detached leaf " + leaf.Name);
                }
                foreach (var stalk in elements.Where(e => e.Name.StartsWith("Pepper-") && e.Name.EndsWith("-stalk-A")))
                    Require(branches.Min(b => DistanceToSegment(Start(stalk), Start(b), End(b))) < .06f, "Detached stalk " + stalk.Name);
            }
        });
        check("habanero wind pins leaf bases and keeps all stems flowers and fruit rigid", () => {
            foreach (var stage in stages.Values)
            foreach (var element in stage["elements"].Skip(1))
            foreach (var face in ((JObject)element["faces"]).Properties().Select(p => p.Value))
            {
                int[] flags = face["windMode"].Values<int>().ToArray();
                int[] expected = ((string)element["name"]).StartsWith("Leaf-") ? [0, 7, 7, 0] : [0, 0, 0, 0];
                Require(flags.SequenceEqual(expected), "Incorrect wind mode on " + element["name"]);
            }
        });
        check("ripe habanero harvest drops habaneros and retains the mature plant", () => {
            var fixture = new Fixture();
            fixture.Block.Code = new AssetLocation("peppermod", "crop-habanero-8");
            fixture.Harvested.Code = new AssetLocation("peppermod", "crop-habanero-10");
            fixture.Start(); fixture.Step(1.5f); fixture.Stop(1.5f);
            Require(fixture.CurrentBlock.Code.Path == "crop-habanero-10", "Habanero did not enter harvested state");
            int quantity = fixture.Drops.Sum(drop => drop.Stack.StackSize);
            Require(quantity >= 16 && quantity <= 24, "Incorrect habanero harvest size");
            Require(fixture.Drops.All(drop => drop.Stack.Collectible.Code.Path == "vegetable-habanero"
                && drop.Velocity.X == 0 && drop.Velocity.Z == 0 && drop.Velocity.Y < 0), "Incorrect habanero drops");
        });
        check("two habanero bites fill the spice meter from empty", () => {
            Require(food["nutritionPropsByType"]["*-habanero"]["satiety"].Value<int>() == 20, "Unexpected satiety");
            float heat = food["attributesByType"]["*-habanero"]["peppermodSpice"].Value<float>();
            Require(heat == 50, "Unexpected habanero heat");
            var fixture = new FoodFixture(heat: heat);
            fixture.Food.Code = new AssetLocation("peppermod", "vegetable-habanero");
            fixture.Food.Eat(1, fixture.Slot, fixture.Player);
            Require(fixture.Player.Saturation == 20 && PepperSpiceSystem.ReadState(fixture.Player).Heat == 50
                && PepperSpiceSystem.ReadState(fixture.Player).Level == SpiceLevel.Hot, "First habanero should reach Hot at 50 spice");
            PepperSpiceSystem.TickPlayer(fixture.Player, 2);
            fixture.Food.Eat(1, fixture.Slot, fixture.Player);
            Require(fixture.Player.Saturation == 40 && PepperSpiceSystem.ReadState(fixture.Player).Heat == 100
                && PepperSpiceSystem.ReadState(fixture.Player).Level == SpiceLevel.Extreme, "Second habanero should fill the meter");
            fixture.Food.Eat(1, fixture.Slot, fixture.Player);
            Require(PepperSpiceSystem.ReadState(fixture.Player).Heat == 100, "Further habaneros should respect the heat cap");
        });
    }

    private static JArray Canopy(JObject shape, bool branchesOnly = false) => new(shape["elements"].Where(e => {
        string name = (string)e["name"];
        return name.StartsWith("Branch-") || !branchesOnly && (name.StartsWith("Leaf-") || name.StartsWith("Petiole-"));
    }));
    private static HashSet<string> FruitTextures(JObject shape) => shape["elements"]
        .Where(e => ((string)e["name"]).Contains("-fruit-"))
        .SelectMany(e => ((JObject)e["faces"]).Properties().Select(p => (string)p.Value["texture"])).ToHashSet();
    private static Vector3 Point(ShapeElement e, double y) {
        var p = new Matrixf(e.GetLocalTransformMatrix(0)).TransformVector(new Vec4f(
            (float)(e.To[0] - e.From[0]) / 32, (float)y / 16, (float)(e.To[2] - e.From[2]) / 32, 1));
        return new Vector3(p.X * 16, p.Y * 16, p.Z * 16);
    }
    private static Vector3 Start(ShapeElement e) => Point(e, .025);
    private static Vector3 End(ShapeElement e) => Point(e, e.To[1] - e.From[1] - .025);
    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b) {
        var d = b - a;
        float t = Math.Clamp(Vector3.Dot(p - a, d) / d.LengthSquared(), 0, 1);
        return Vector3.Distance(p, a + t * d);
    }
    private static void Require(bool condition, string message) {
        if (!condition) throw new Exception(message);
    }
}
