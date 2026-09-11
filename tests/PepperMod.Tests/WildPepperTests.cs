using Newtonsoft.Json;
using PepperMod;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

internal static class WildPepperTests
{
    public static void Run(Action<string, Action> check)
    {
        check("wild pepper patches accept all climates and elevations", () => {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "modinfo.json"))) root = root.Parent;
            var patches = JsonConvert.DeserializeObject<BlockPatch[]>(File.ReadAllText(Path.Combine(root.FullName,
                "assets/peppermod/worldgen/blockpatches/wild-pepper-plants.json")));
            var config = new BlockPatchConfig();
            var soil = new Block { Code = new AssetLocation("game", "soil-low-normal"), Fertility = 5 };
            int previousSeaLevel = TerraGenConfig.seaLevel;
            try {
                foreach (int height in new[] { 256, 512, 1024 }) {
                    TerraGenConfig.seaLevel = (int)(height * .43);
                    foreach (var patch in patches)
                    foreach (int temp in new[] { 0, 128, 255 })
                    foreach (int rain in new[] { 0, 128, 255 })
                    foreach (float forest in new[] { 0f, .5f, 1f })
                    foreach (int y in new[] { 1, TerraGenConfig.seaLevel, height - 1 }) {
                        if (!config.IsPatchSuitableAt(patch, soil, height, (temp << 16) | (rain << 8), y, forest, forest, 0))
                            throw new Exception($"Patch rejected climate {temp}/{rain}, forest {forest}, elevation {y}/{height}.");
                    }
                }
                if (patches.Length != PepperBundleTests.Types.Length) throw new Exception("Expected only finished pepper patch groups.");
                var expectedCodes = PepperBundleTests.Types
                    .SelectMany(type => Enumerable.Range(4, 5).Select(stage => $"peppermod:crop-{type}-{stage}"));
                var patchJson = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(Path.Combine(root.FullName,
                    "assets/peppermod/worldgen/blockpatches/wild-pepper-plants.json")));
                if (!patchJson.SelectMany(patch => patch["blockCodes"]).Select(code => code.ToString()).SequenceEqual(expectedCodes))
                    throw new Exception("Only finished pepper stages 4-8 should spawn wild.");
            }
            finally { TerraGenConfig.seaLevel = previousSeaLevel; }
        });

        check("wild peppers place on even poor soil but not unsupported ground", () => {
            foreach (string ground in new[] { "soil-verylow-normal", "soil-low-normal", "soil-high-normal", "farmland-dry-low", "compost", "rock-granite", "sand-granite", "glacierice", "air" }) {
                bool expected = ground.StartsWith("soil") || ground.StartsWith("farmland") || ground == "compost";
                var result = Place(ground, false, false);
                if (result.Placed != expected || result.Blocks != (expected ? 1 : 0) || result.Entities != (expected ? 1 : 0))
                    throw new Exception("Incorrect placement on " + ground);
            }
        });
        check("wild peppers do not replace solid blocks or grow underwater", () => {
            foreach (var result in new[] { Place("soil-low-normal", true, false), Place("soil-low-normal", false, true) })
                if (result.Placed || result.Blocks != 0 || result.Entities != 0) throw new Exception("Unsafe worldgen placement.");
        });
    }

    private static (bool Placed, int Blocks, int Entities) Place(string groundCode, bool water, bool occupied)
    {
        int blocks = 0, entities = 0;
        var position = new BlockPos(10, 100, 10);
        var ground = new Block { Code = new AssetLocation("game", groundCode), Replaceable = 0 };
        var air = new Block { Code = new AssetLocation("game", "air"), Replaceable = 10000 };
        var liquid = new Block { Code = new AssetLocation("game", "water-still-7"), LiquidCode = "water", MatterState = EnumMatterState.Liquid };
        var accessor = Proxy.Make<IBlockAccessor>((m, args) => {
            switch (m.Name) {
                case "GetBlock":
                    if (args.Length > 1 && (int)args[1] == BlockLayersAccess.Fluid) return water ? liquid : air;
                    return ((BlockPos)args[0]).Y < position.Y || occupied ? ground : air;
                case "SetBlock": blocks++; return null;
                case "SpawnBlockEntity": entities++; return null;
                default: throw new NotSupportedException(m.Name);
            }
        });
        var plant = new BlockPerennialPepperPlant { BlockId = 8, Code = new AssetLocation("peppermod", "crop-jalapeno-8"), EntityClass = "PerennialPepperPlant" };
        return (plant.TryPlaceBlockForWorldGen(accessor, position, BlockFacing.UP, null), blocks, entities);
    }
}
