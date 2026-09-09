using System.Globalization;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using PepperMod;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.Client.NoObf;

internal static class PreparedPepperTests
{
    private static string Root {
        get {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "modinfo.json"))) dir = dir.Parent;
            return dir?.FullName ?? throw new Exception("Project root not found");
        }
    }
    private static JObject Read(string path) => JObject.Parse(File.ReadAllText(Path.Combine(Root, "assets/peppermod", path)));
    private static JToken Pick(JObject definition, string field, string code) =>
        (definition[field + "ByType"] as JObject)?.Properties().FirstOrDefault(p => WildcardUtil.Match(p.Name, code))?.Value ?? definition[field];
    private static JToken Expand(JToken token, string type) {
        if (token == null) return null;
        var result = token.DeepClone();
        foreach (var value in (result as JContainer)?.Descendants().OfType<JValue>() ?? Enumerable.Empty<JValue>())
            if (value.Type == JTokenType.String) value.Value = ((string)value).Replace("{type}", type);
        return result;
    }
    private static void Require(bool condition, string message = "Prepared pepper regression failed") {
        if (!condition) throw new Exception(message);
    }
    internal static PreparedTestFood Load(string type, string state, int id = 100) {
        var json = Read("itemtypes/food/" + (state == "raw" ? "vegetable" : "preparedpepper") + ".json");
        string code = state == "raw" ? "vegetable-" + type : $"preparedpepper-{state}-{type}";
        return new PreparedTestFood {
            Code = new AssetLocation("peppermod", code), ItemId = id, MaxStackSize = (int)json["maxstacksize"],
            Attributes = new JsonObject(Expand(Pick(json, "attributes", code), type)),
            CombustibleProps = Expand(Pick(json, "combustibleProps", code), type)?.ToObject<CombustibleProperties>(),
            TransitionableProps = Pick(json, "transitionableProps", code).ToObject<TransitionableProperties[]>(),
            NutritionProps = Pick(json, "nutritionProps", code).ToObject<FoodNutritionProperties>()
        };
    }
    public static void Run(Action<string, Action> check) {
        var types = Read("itemtypes/food/vegetable.json")["variantgroups"][0]["states"].Values<string>().ToArray();
        check("prepared pepper skin stays opaque and uncorrupted in the actual game texture atlas", () => {
            string path = Path.Combine(Root, "assets/peppermod/textures/item/food/prepared/pepper-skins.png");
            using var bitmap = new BitmapExternal(path, null);
            var pixels = bitmap.Pixels;
            Require(pixels.All(p => ((uint)p >> 24) == 255), "Pepper skin must be fully opaque");
            var atlas = new TextureAtlas(4096, 2048, 0, 0);
            Require(atlas.InsertTexture(0, bitmap, true));
            var positions = new Vintagestory.API.Client.TextureAtlasPosition[1];
            atlas.PopulateAtlasPositions(positions, 0);
            int mismatches = 0, transparent = 0;
            for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++) {
                int packed = atlas.GetPixel(positions[0].x1 + (x + .5f) / 4096, positions[0].y1 + (y + .5f) / 2048);
                if (packed != pixels[y * bitmap.Width + x]) mismatches++;
                if (((uint)packed >> 24) != 255) transparent++;
            }
            Require(mismatches == 0, $"Game atlas corrupts {mismatches} skin pixels, including {transparent} transparent pixels, at {bitmap.Width}x{bitmap.Height}");
        });
        check("all pepper baking chains resolve and halve Scoville and spice at each step", () => {
            foreach (string type in types) {
                var items = new[] { Load(type, "raw"), Load(type, "baked"), Load(type, "dried") };
                for (int i = 0; i < items.Length; i++) {
                    var item = items[i];
                    Require(item.Attributes["peppermodScoville"].AsInt() >= 0);
                    var baking = BakingProperties.ReadFrom(new ItemStack(item));
                    Require(baking != null && baking.LevelFrom < baking.LevelTo && baking.StartScaleY == 1 && baking.EndScaleY == 1);
                    if (i < 2) {
                        Require(baking.ResultCode == items[i + 1].Code.ToString());
                        Require(item.CombustibleProps.SmeltedStack.Code.ToString() == baking.ResultCode);
                        Require(item.CombustibleProps.SmeltingType == EnumSmeltType.Bake && item.CombustibleProps.SmeltedRatio == 1);
                        Require(items[i + 1].Attributes["peppermodScoville"].AsInt() * 2 == item.Attributes["peppermodScoville"].AsInt());
                        Require(items[i + 1].Attributes["peppermodSpice"].AsFloat() * 2 == item.Attributes["peppermodSpice"].AsFloat());
                    } else Require(baking.ResultCode == null && item.CombustibleProps == null);
                    if (i > 0) Require(baking.InitialCode == items[i - 1].Code.ToString());
                    Require(new OvenItemData(new ItemStack(item)).TimeToBake > 0);
                }
            }
        });
        check("raw jalapenos and habaneros keep seven days and other shelf lives stay unchanged", () => {
            foreach (string type in types) foreach (string state in new[] { "raw", "baked", "dried" }) {
                var props = Load(type, state).TransitionableProps.Single();
                Require(props.Type == EnumTransitionType.Perish && props.TransitionedStack.Code.ToString() == "game:rot");
                Require(props.TransitionRatio == .25f && props.TransitionHours.avg > 0);
                bool rawFinishedPepper = state == "raw" && (type == "jalapeno" || type == "habanero");
                Require(props.FreshHours.avg == (rawFinishedPepper ? 168 : state == "raw" ? 336 : state == "baked" ? 168 : 2880));
                Require(props.FreshHours.var == (rawFinishedPepper ? 0 : state == "raw" ? 48 : state == "baked" ? 24 : 0));
            }
        });
        check("baked and dried bites apply reduced spice through the real food interaction", () => {
            foreach (string type in types) foreach (string state in new[] { "baked", "dried" }) {
                var item = Load(type, state);
                var f = new FoodFixture(count: 1, heat: item.Attributes["peppermodSpice"].AsFloat());
                f.Food.Code = item.Code; f.Food.NutritionProps = item.NutritionProps;
                f.Food.Eat(.2f, f.Slot, f.Player);
                Require(PepperSpiceSystem.ReadState(f.Player).Heat == 0 && f.Slot.Itemstack.StackSize == 1);
                f.Food.Eat(1, f.Slot, f.Player);
                Require(f.Slot.Empty && PepperSpiceSystem.ReadState(f.Player).Heat == item.Attributes["peppermodSpice"].AsFloat());
                Require(f.Player.Saturation == item.NutritionProps.Satiety);
            }
        });
        check("Scoville tooltip is localized and preserves vanilla nutrition and perish text", () => {
            var lang = Read("lang/en.json");
            string locale = Lang.DefaultLocale;
            string oldLocale = Lang.CurrentLocale;
            var localeProperty = typeof(Lang).GetProperty("CurrentLocale");
            localeProperty.SetValue(null, locale);
            Lang.AvailableLanguages.TryGetValue(locale, out var previous);
            var translation = Proxy.Make<ITranslationService>((m, a) => {
                string key = (string)a[0];
                string text = (string)lang[key.Replace("peppermod:", "")] ?? key;
                return m.Name switch {
                    "HasTranslation" => true,
                    "Get" or "GetMatching" => string.Format(CultureInfo.InvariantCulture, text, (object[])a[1]),
                    "GetIfExists" => text == key ? null : text,
                    _ => throw new NotSupportedException(m.Name)
                };
            });
            Lang.AvailableLanguages[locale] = translation;
            try {
                foreach (string type in types) foreach (string state in new[] { "raw", "baked", "dried" }) {
                    var f = new OvenFixture(type); var item = f.Items[Array.IndexOf(new[] { "raw", "baked", "dried" }, state)];
                    var tooltip = new StringBuilder();
                    item.GetHeldItemInfo(new DummySlot(new ItemStack(item)), tooltip, f.World, false);
                    Require(tooltip.ToString().Contains($"Scoville heat: {item.Attributes["peppermodScoville"].AsInt().ToString("N0", CultureInfo.InvariantCulture)} SHU"), tooltip.ToString());
                    Require(tooltip.ToString().Contains("When eaten:") && tooltip.ToString().Contains("perish-test-marker"), tooltip.ToString());
                }
                var edge = new OvenFixture("jalapeno");
                edge.Items[0].Attributes = new JsonObject(new JObject { ["peppermodScoville"] = -1 });
                var line = new StringBuilder(); edge.Items[0].GetHeldItemInfo(edge.Oven.Inventory[0], line, edge.World, false);
                Require(line.ToString().Contains("0 SHU"));
                edge.Items[0].Attributes = null; line.Clear();
                edge.Items[0].GetHeldItemInfo(edge.Oven.Inventory[0], line, edge.World, false);
                Require(!line.ToString().Contains("SHU"));
            } finally {
                localeProperty.SetValue(null, oldLocale);
                if (previous == null) Lang.AvailableLanguages.Remove(locale);
                else Lang.AvailableLanguages[locale] = previous;
            }
        });
        check("vanilla oven converts raw to baked to dried and safely stops at dried", () => {
            foreach (string type in types) {
                var f = new OvenFixture(type);
                f.Oven.Advance(1000, 100); Require(f.Oven.Inventory[0].Itemstack.Item == f.Items[0]);
                f.Oven.Advance(1000, 200); Require(f.Oven.Inventory[0].Itemstack.Item == f.Items[1]);
                Require(f.Oven.Inventory[0].Itemstack.StackSize == 1);
                f.Oven.Advance(1000, 200); Require(f.Oven.Inventory[0].Itemstack.Item == f.Items[2]);
                var dried = f.Oven.Inventory[0].Itemstack;
                f.Oven.Advance(10000, 300); Require(ReferenceEquals(dried, f.Oven.Inventory[0].Itemstack));
                Require(f.Oven.Height == 1 && f.Oven.DirtyCount == 2);
            }
        });
        check("baked peppers can start a second oven batch and retain existing spoilage", () => {
            foreach (string type in types) {
                var f = new OvenFixture(type);
                f.Items[0].AgeFraction = .5f;
                f.Oven.Advance(1000, 200);
                var baked = f.Oven.Inventory[0].Itemstack;
                var tree = (TreeAttribute)baked.Attributes.GetTreeAttribute("transitionstate");
                Require(tree != null && ((FloatArrayAttribute)tree["transitionedHours"]).value[0] > 0);
                var fresh = ((FloatArrayAttribute)tree["freshHours"]).value[0];
                var transitioned = ((FloatArrayAttribute)tree["transitionedHours"]).value[0];
                f.Items[1].AgeFraction = transitioned / (fresh + 24);
                f.Oven.Load(baked);
                f.Oven.Advance(1000, 200);
                tree = (TreeAttribute)f.Oven.Inventory[0].Itemstack.Attributes.GetTreeAttribute("transitionstate");
                Require(f.Oven.Inventory[0].Itemstack.Item == f.Items[2]);
                Require(((FloatArrayAttribute)tree["freshHours"]).value[0] == 2880 && ((FloatArrayAttribute)tree["transitionedHours"]).value[0] > 0);
            }
        });
        check("prepared items preserve fruit geometry hand placement and valid texture UVs", () => {
            var raw = Read("itemtypes/food/vegetable.json"); var prepared = Read("itemtypes/food/preparedpepper.json");
            foreach (string field in new[] { "guiTransformByType", "groundTransformByType", "tpHandTransformByType", "fpHandTransformByType" })
                Require(JToken.DeepEquals(raw[field], prepared[field]));
            var lang = Read("lang/en.json");
            foreach (string type in types) foreach (string state in new[] { "baked", "dried" }) {
                string code = $"preparedpepper-{state}-{type}";
                Require(!string.IsNullOrEmpty((string)lang["item-" + code]));
                var original = Read($"shapes/item/food/vegetable/{type}.json");
                var shape = Read($"shapes/item/food/prepared/{state}/{type}.json");
                Require(original["elements"].Count() == shape["elements"].Count());
                var parsed = shape.ToObject<Shape>();
                Require(parsed.Elements.All(e => e.GetLocalTransformMatrix(0).All(float.IsFinite)));
                foreach (var texture in ((JObject)shape["textures"]).Properties())
                    Require(File.Exists(Path.Combine(Root, "assets/peppermod/textures", (string)texture.Value + ".png")));
                for (int i = 0; i < shape["elements"].Count(); i++) {
                    var e = shape["elements"][i];
                    foreach (string field in new[] { "name", "from", "to", "rotationOrigin", "rotationX", "rotationY", "rotationZ", "stepParentName" })
                        Require(JToken.DeepEquals(e[field], original["elements"][i][field]));
                    foreach (var face in ((JObject)e["faces"]).Properties()) {
                        Require(face.Value["uv"].Values<double>().All(n => double.IsFinite(n) && n >= 0 && n <= 16));
                        if ((string)face.Value["texture"] != "#base") continue;
                        var uv = face.Value["uv"].Values<double>().ToArray();
                        Require(Math.Abs(uv[0] - uv[2]) > .00001 && Math.Abs(uv[1] - uv[3]) > .00001, type + " " + e["name"] + " degenerate UV");
                    }
                }
            }
        });
    }
}

internal class PreparedTestFood : ItemPepperFood
{
    public float AgeFraction;
    public void SetApi(ICoreAPI core) => api = core;
    public override TransitionState UpdateAndGetTransitionState(IWorldAccessor world, ItemSlot slot, EnumTransitionType type) {
        var props = TransitionableProps.Single();
        return new TransitionState { Props = props, FreshHours = props.FreshHours.avg, TransitionHours = props.TransitionHours.avg,
            TransitionedHours = AgeFraction * (props.FreshHours.avg + props.TransitionHours.avg) };
    }
    public override float AppendPerishableInfoText(ItemSlot slot, StringBuilder text, IWorldAccessor world) {
        text.AppendLine("perish-test-marker"); return 0;
    }
}

internal class PreparedTestOven : BlockEntityOven
{
    public int DirtyCount;
    private static readonly FieldInfo DataField = typeof(BlockEntityOven).GetField("bakingData", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private OvenItemData[] Data => (OvenItemData[])DataField.GetValue(this);
    public float Height => Data[0].CurHeightMul;
    public void Load(ItemStack stack, int index = 0) { Inventory[index].Itemstack = stack; Data[index] = new OvenItemData(stack); }
    public void Advance(float dt, float temperature, int index = 0) {
        Data[index].temp = temperature;
        typeof(BlockEntityOven).GetMethod("IncrementallyBake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, new object[] { dt, index });
    }
    public override void MarkDirty(bool redrawOnClient = false, IPlayer skipPlayer = null) => DirtyCount++;
}

internal class OvenFixture
{
    public PreparedTestOven Oven = new();
    public PreparedTestFood[] Items;
    public IWorldAccessor World;
    public OvenFixture(string type) {
        Items = new[] { PreparedPepperTests.Load(type, "raw", 101), PreparedPepperTests.Load(type, "baked", 102), PreparedPepperTests.Load(type, "dried", 103) };
        var calendar = Proxy.Make<IGameCalendar>((m, _) => m.Name == "get_TotalHours" ? 100.0 : throw new NotSupportedException(m.Name));
        var mods = Proxy.Make<IModLoader>((m, _) => m.Name == "GetMod" ? null : throw new NotSupportedException(m.Name));
        var rand = new Random(12);
        ICoreAPI api = null;
        World = Proxy.Make<IWorldAccessor>((m, a) => m.Name switch {
            "get_Side" => EnumAppSide.Server, "get_Api" => api, "get_Calendar" => calendar, "get_Rand" => rand,
            "get_BlockAccessor" => null,
            "GetItem" => Items.Single(i => i.Code.Equals((AssetLocation)a[0])),
            _ => throw new NotSupportedException(m.Name)
        });
        api = Proxy.Make<ICoreAPI>((m, _) => m.Name switch {
            "get_World" => World, "get_Side" => EnumAppSide.Server, "get_ModLoader" => mods,
            _ => throw new NotSupportedException(m.Name)
        });
        foreach (var item in Items) item.SetApi(api);
        Oven.Api = api; Oven.Load(new ItemStack(Items[0]));
    }
}
