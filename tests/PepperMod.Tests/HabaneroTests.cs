using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PepperMod;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
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
                        var textureMap = ReferenceEquals(json, item) ? food["texturesByType"]["*-habanero"]
                            : plantDefinition["texturesByType"]?["crop-habanero-*"] ?? plantDefinition["textures"];
                        string runtimePath = ((string)textureMap[key]?["base"])?.Replace("{type}", "habanero");
                        Require(modelPath != null && runtimePath == modelPath, "Model/runtime texture mismatch: " + key);
                        Require(File.Exists(Path.Combine(assets, "textures", modelPath + ".png")), "Missing texture " + modelPath);
                        Require(face["uv"].Count() == 4 && face["uv"].Values<double>().All(n => double.IsFinite(n) && n >= 0 && n <= 16), "Invalid UV");
                    }
                }
            }
        });
        check("habanero skin remains opaque and pixel-perfect inside the game atlas", () => {
            using var bitmap = new BitmapExternal(Path.Combine(assets, "textures/block/plant/habanero/pepper.png"), null);
            Require(bitmap.Width == 1024 && bitmap.Height == 1024, "Expected a game-safe 1024-square atlas");
            var pixels = bitmap.Pixels;
            Require(pixels.All(p => ((uint)p >> 24) == 255), "Habanero skin contains transparent pixels");
            var atlas = new TextureAtlas(2048, 2048, 0, 0);
            Require(atlas.InsertTexture(0, bitmap, true), "Could not insert Habanero skin");
            var positions = new TextureAtlasPosition[1];
            atlas.PopulateAtlasPositions(positions, 0);
            for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
                Require(atlas.GetPixel(positions[0].x1 + (x + .5f) / 2048, positions[0].y1 + (y + .5f) / 2048)
                    == pixels[y * bitmap.Width + x], $"Skin corruption at {x}, {y}");
        });
        check("habanero fruit has a full lower body and blunt uneven end", () => {
            var fruit = item.ToObject<Shape>().Elements.Where(e => e.Name.Contains("-fruit-")).ToArray();
            var points = fruit.SelectMany(Corners).ToArray();
            float minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y), height = maxY - minY;
            float width = points.Max(p => p.X) - points.Min(p => p.X);
            Require(height / width > 1.15 && height / width < 1.45, "Fruit is too elongated or too round");
            var low = points.Where(p => p.Y < minY + height * .10).ToArray();
            var high = points.Where(p => p.Y > minY + height * .65).ToArray();
            float lowerWidth = low.Max(p => p.X) - low.Min(p => p.X);
            Require(lowerWidth > width * .48 && lowerWidth < width * .80, "Bottom must be broad and blunt, not pointed or cylindrical");
            Require(high.Max(p => p.X) - high.Min(p => p.X) > width * .85, "Broad shoulders are missing");
            Require(fruit.Where(e => e.Name.Contains("-fold-")).Any(e => Math.Abs(e.RotationX) > 2 || Math.Abs(e.RotationZ) > 2), "Longitudinal folds are missing");
        });
        check("charred habanero skin has substantial dark blistering while raw skin stays bright", () => {
            using var bitmap = new BitmapExternal(Path.Combine(assets, "textures/block/plant/habanero/pepper.png"), null);
            var pixels = bitmap.Pixels;
            double DarkFraction(int left, int top) {
                int dark = 0;
                for (int y = top; y < top + 512; y++) for (int x = left; x < left + 512; x++) {
                    uint pixel = (uint)pixels[y * bitmap.Width + x];
                    double brightness = ((pixel >> 16 & 255) + (pixel >> 8 & 255) + (pixel & 255)) / 3.0;
                    if (brightness < 80) dark++;
                }
                return dark / (512.0 * 512);
            }
            double charred = DarkFraction(0, 512);
            Require(charred > .20 && charred < .70, "Charred skin needs dark blisters plus exposed orange skin");
            Require(DarkFraction(512, 0) < .02, "Fresh fruit acquired charred marks");
        });
        check("only cooked habaneros are named charred without changing saved item IDs", () => {
            var lang = JObject.Parse(File.ReadAllText(Path.Combine(assets, "lang/en.json")));
            Require((string)lang["item-preparedpepper-baked-habanero"] == "Charred Habanero", "Loose charred name missing");
            Require((string)lang["item-pepperbundle-baked-habanero"] == "Charred Habanero Bundle", "Bundle charred name missing");
            Require((string)lang["block-hangingpepperbundle-baked-habanero"] == "Charred Habanero Bundle", "Hanging charred name missing");
            foreach (string type in new[] { "jalapeno", "serrano", "cayenne", "poblano", "bell-pepper", "banana-pepper", "ghost-pepper" })
                Require(((string)lang[$"item-preparedpepper-baked-{type}"]).StartsWith("Baked "), "Another variety was renamed");
            var raw = PreparedPepperTests.Load("habanero", "raw");
            var charred = PreparedPepperTests.Load("habanero", "baked");
            var dried = PreparedPepperTests.Load("habanero", "dried");
            Require(raw.Attributes["bakingProperties"]["resultCode"].AsString() == charred.Code.ToString(), "Raw-to-charred identity changed");
            Require(charred.Code.Path == "preparedpepper-baked-habanero", "Saved cooked items would stop resolving");
            Require(charred.Attributes["bakingProperties"]["resultCode"].AsString() == dried.Code.ToString(), "Charred-to-dried chain changed");
        });
        check("habanero plant item and bundle skins resolve to the correct atlas quarter", () => {
            void CheckSkin(JObject shape, string state, JToken runtime) {
                foreach (var element in shape["elements"].Where(e => ((string)e["name"]).Contains("-fruit-")))
                foreach (var face in ((JObject)element["faces"]).Properties().Select(p => p.Value)) {
                    string key = ((string)face["texture"])[1..];
                    Require((string)runtime[key]["base"] == (string)shape["textures"][key], "Runtime skin differs from preview");
                    string color = key == "peppergreen" ? "green" : state;
                    double u = color is "raw" or "dried" ? 8 : 0, v = color is "baked" or "dried" ? 8 : 0;
                    var uv = face["uv"].Values<double>().ToArray();
                    Require(uv[0] >= u + .25 && uv[2] <= u + 7.75 && uv[1] >= v + .25 && uv[3] <= v + 7.75, "UV crosses a skin boundary");
                }
            }
            foreach (int id in new[] { 6, 7, 8, 11 }) CheckSkin(stages[id], "raw", plantDefinition["texturesByType"]["crop-habanero-*"]);
            var prepared = JObject.Parse(File.ReadAllText(Path.Combine(assets, "itemtypes/food/preparedpepper.json")));
            var bundles = JObject.Parse(File.ReadAllText(Path.Combine(assets, "itemtypes/food/pepperbundle.json")));
            var hanging = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes/food/hangingpepperbundle.json")));
            foreach (string state in new[] { "raw", "baked", "dried" }) {
                var loose = state == "raw" ? item : JObject.Parse(File.ReadAllText(Path.Combine(assets, $"shapes/item/food/prepared/{state}/habanero.json")));
                CheckSkin(loose, state, (state == "raw" ? food : prepared)["texturesByType"]["*-habanero"]);
                var bundle = JObject.Parse(File.ReadAllText(Path.Combine(assets, $"shapes/item/food/bundle/{state}/habanero.json")));
                CheckSkin(bundle, state, bundles["texturesByType"][$"pepperbundle-{state}-habanero"]);
                CheckSkin(bundle, state, hanging["texturesByType"][$"hangingpepperbundle-{state}-habanero"]);
            }
            Require(JToken.DeepEquals(plantDefinition["textures"], plantDefinition["texturesByType"]["*"]), "Other crops' textures changed");
            Require(JToken.DeepEquals(prepared["textures"], prepared["texturesByType"]["*"]), "Other prepared peppers' textures changed");
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
    private static IEnumerable<Vector3> Corners(ShapeElement e) {
        var matrix = new Matrixf(e.GetLocalTransformMatrix(0));
        for (int i = 0; i < 8; i++) {
            var p = matrix.TransformVector(new Vec4f((float)(e.To[0] - e.From[0]) / 16 * (i & 1),
                (float)(e.To[1] - e.From[1]) / 16 * ((i >> 1) & 1),
                (float)(e.To[2] - e.From[2]) / 16 * ((i >> 2) & 1), 1));
            yield return new Vector3(p.X, p.Y, p.Z);
        }
    }
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
