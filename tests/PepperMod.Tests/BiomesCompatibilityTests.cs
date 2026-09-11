using System.Collections;
using System.IO.Compression;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.ServerMods.NoObf;

internal static class BiomesCompatibilityTests
{
    private const string AssetPath = "config/biomes/blockconfig/peppermod.json";
    private static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<string, string[]> Regions = new() {
        ["jalapeno"] = new[] { "pacific nearctic", "pacific neotropic", "atlantic neotropic" },
        ["habanero"] = new[] { "atlantic neotropic" },
        ["serrano"] = new[] { "pacific nearctic", "pacific neotropic", "atlantic neotropic" }
    };
    private static void Require(bool condition, string message) {
        if (!condition) throw new Exception(message);
    }
    private static string ReadLocal(string path) => File.ReadAllText(Path.Combine(PepperBundleTests.Root, "assets/peppermod", path));

    public static void Run(Action<string, Action> check)
    {
        check("optional Biomes rules cover only finished peppers in their existing Biomes regions", () => {
            var rules = JObject.Parse(ReadLocal(AssetPath));
            Require(rules.Properties().Select(p => p.Name).SequenceEqual(new[] { "BlockPatches" }), "Compatibility must only affect wild plant patches");
            var patches = (JObject)rules["BlockPatches"];
            Require(patches.Count == Regions.Count, "Only finished peppers have compatibility entries");
            foreach (var (type, regions) in Regions) {
                var rule = patches[$"crop-{type}-*"];
                Require(rule != null && rule["biorealm"].Values<string>().SequenceEqual(regions), "Unexpected regions for " + type);
                Require((string)rule["bioriver"] == "both", "Rivers must not be required or excluded");
                for (int stage = 1; stage <= 11; stage++)
                    Require(WildcardUtil.Match($"crop-{type}-*", $"crop-{type}-{stage}"), "Missing plant stage");
            }
            foreach (string unrelated in new[] { "crop-cayenne-8", "crop-bell-pepper-8", "crop-flax-4", "shrubberrybush-habanero-ripe" })
                Require(!patches.Properties().Any(p => WildcardUtil.Match(p.Name, unrelated)), "Compatibility changes another crop: " + unrelated);
            var modInfo = JObject.Parse(File.ReadAllText(Path.Combine(PepperBundleTests.Root, "modinfo.json")));
            Require(modInfo["dependencies"]["biomes"] == null, "Biomes must remain optional");
        });

        string package = Environment.GetEnvironmentVariable("BIOMES_TEST_ZIP");
        if (string.IsNullOrWhiteSpace(package)) {
            Console.WriteLine("SKIP native Biomes integration (set BIOMES_TEST_ZIP to a downloaded Biomes 2.2.0 ZIP)");
            return;
        }
        check("Biomes 2.2.0 loads our config and filters all finished peppers in all realms and river states", () => CheckNative(package));
    }

    // Use the released mod's loader and filter without installing it or running worldgen.
    private static void CheckNative(string package)
    {
        using var zip = ZipFile.OpenRead(package);
        string ReadEntry(string name) {
            using var reader = new StreamReader(zip.GetEntry(name)?.Open() ?? throw new Exception("Missing Biomes file: " + name));
            return reader.ReadToEnd();
        }
        Require((string)JObject.Parse(ReadEntry("modinfo.json"))["version"] == "2.2.0", "Native fixture expects Biomes 2.2.0");
        using var dllStream = zip.GetEntry("Biomes.dll").Open();
        using var bytes = new MemoryStream(); dllStream.CopyTo(bytes);
        var assembly = Assembly.Load(bytes.ToArray());
        var configType = assembly.GetType("Biomes.BiomesConfig", true);
        var modType = assembly.GetType("Biomes.BiomesModSystem", true);
        var cacheType = assembly.GetType("Biomes.Caches.VegetationCache", true);
        var dataType = assembly.GetType("Biomes.Api.BiomeData", true);
        var realms = JArray.Parse(ReadEntry("assets/biomes/config/realms.json")).Values<string>().ToList();
        var standardAssets = zip.Entries.Where(e => e.FullName.StartsWith("assets/biomes/config/biomes/blockconfig/") && e.FullName.EndsWith(".json"))
            .Select(e => Asset("biomes:" + e.FullName["assets/biomes/".Length..], ReadEntry(e.FullName))).ToList();
        var ourAsset = Asset("peppermod:" + AssetPath, ReadLocal(AssetPath));

        object CreateCache(bool includePepper, bool first) {
            var assets = standardAssets.ToList();
            if (includePepper) assets.Insert(first ? 0 : assets.Count, ourAsset);
            var manager = Proxy.Make<IAssetManager>((m, args) => {
                Require(m.Name == "GetMany" && (string)args[0] == "config/biomes/blockconfig" && args[1] == null,
                    "Biomes must discover compatibility across asset domains");
                return assets;
            });
            var api = Proxy.Make<ICoreAPI>((m, _) => m.Name == "get_Assets" ? manager : throw new NotSupportedException(m.Name));
            var config = Activator.CreateInstance(configType, true);
            configType.GetField("ValidRealms", Instance).SetValue(config, realms);
            var indexes = (IDictionary)configType.GetField("ValidRealmIndexes", Instance).GetValue(config);
            for (int i = 0; i < realms.Count; i++) indexes.Add(realms[i], i);
            var mod = Activator.CreateInstance(modType);
            modType.GetField("Config", Instance).SetValue(mod, config);
            configType.GetMethod("LoadBlockConfigs", Instance).Invoke(config, new[] { mod, api });
            return Activator.CreateInstance(cacheType, Instance, null, new[] { mod }, null);
        }

        var baseline = CreateCache(false, false);
        var compatibilityFirst = CreateCache(true, true);
        var compatibilityLast = CreateCache(true, false);
        var wild = JsonConvert.DeserializeObject<BlockPatch[]>(ReadLocal("worldgen/blockpatches/wild-pepper-plants.json"));
        var input = wild.SelectMany(p => p.blockCodes).Select(code => new BlockPatch { blockCodes = new[] { code } }).ToList();
        input.Add(new BlockPatch { blockCodes = new[] { new AssetLocation("game", "crop-flax-4") } });
        input.Add(new BlockPatch { blockCodes = new[] { new AssetLocation("test", "unrelated-crop-4") } });
        HashSet<string> Filter(object cache, object biome) {
            cacheType.GetMethod("GenBlockPatchCacheEntry", Instance).Invoke(cache, new object[] { biome, input.ToArray() });
            var stored = (IDictionary)cacheType.GetField("_patchCache", Instance).GetValue(cache);
            return ((BlockPatch[])stored[biome]).SelectMany(p => p.blockCodes).Select(c => c.ToString()).ToHashSet();
        }
        for (int index = 0; index < realms.Count; index++) foreach (bool river in new[] { false, true }) {
            var biome = Activator.CreateInstance(dataType, new object[] { 0 });
            dataType.GetMethod("SetRealm").Invoke(biome, new object[] { index, true });
            dataType.GetMethod(river ? "SetRiver" : "SetNoRiver").Invoke(biome, new object[] { true });
            var original = Filter(baseline, biome);
            var first = Filter(compatibilityFirst, biome);
            var last = Filter(compatibilityLast, biome);
            Require(first.SetEquals(last), "Asset loading order changes compatibility");
            foreach (var patch in input) {
                var code = patch.blockCodes[0];
                if (code.Domain != "peppermod") {
                    Require(first.Contains(code.ToString()) == original.Contains(code.ToString()), "Changed unrelated crop " + code);
                    continue;
                }
                string type = Regions.Keys.Single(type => code.Path.StartsWith($"crop-{type}-"));
                bool expected = Regions[type].Contains(realms[index]);
                Require(first.Contains(code.ToString()) == expected, $"{code}: incorrect spawn eligibility in {realms[index]}, river={river}");
                if (type == "habanero") Require(!original.Contains(code.ToString()), "Baseline no longer reproduces the missing habanero support");
            }
        }
    }

    private static IAsset Asset(string location, string text) => Proxy.Make<IAsset>((m, _) => m.Name switch {
        "ToText" => text,
        "get_Location" => new AssetLocation(location),
        _ => throw new NotSupportedException(m.Name)
    });
}
