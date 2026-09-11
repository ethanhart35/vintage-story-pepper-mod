using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

internal static class SeedTextureTests
{
    public static void Run(Action<string, Action> check)
    {
        var definition = PepperBundleTests.Read("itemtypes/seeds.json");
        var types = definition["variantgroups"][0]["states"].Values<string>().ToArray();
        string TexturePath(string type) => Path.Combine(PepperBundleTests.Root, "assets/peppermod/textures",
            ((string)definition["texture"]["base"]).Replace("{type}", type) + ".png");

        check("all eight seeds have distinct finished transparent inventory artwork", () => {
            var hashes = new HashSet<string>();
            foreach (string type in types) {
                using var bitmap = new BitmapExternal(TexturePath(type), null);
                Require(bitmap.Width == 256 && bitmap.Height == 256, type + ": wrong dimensions");
                var pixels = bitmap.Pixels;
                Require(pixels.Count(p => ((uint)p >> 24) == 0) > 30000, type + ": background is not transparent");
                Require(pixels.Count(p => ((uint)p >> 24) == 255) > 5000, type + ": blank or placeholder icon");
                for (int i = 0; i < 256; i++)
                    Require(new[] { pixels[i], pixels[255 * 256 + i], pixels[i * 256], pixels[i * 256 + 255] }
                        .All(p => ((uint)p >> 24) == 0), type + ": artwork touches canvas border");
                var bytes = new byte[pixels.Length * sizeof(int)];
                Buffer.BlockCopy(pixels, 0, bytes, 0, bytes.Length);
                Require(hashes.Add(Convert.ToHexString(SHA256.HashData(bytes))), type + ": duplicate seed artwork");
            }
            Require(types.Length == 8, "Expected eight seed varieties");
        });

        check("seed colors and alpha survive the actual Vintage Story texture atlas", () => {
            foreach (string type in types) {
                using var bitmap = new BitmapExternal(TexturePath(type), null);
                var atlas = new TextureAtlas(512, 512, 0, 0);
                Require(atlas.InsertTexture(0, bitmap, true), type + ": cannot insert into atlas");
                var positions = new Vintagestory.API.Client.TextureAtlasPosition[1];
                atlas.PopulateAtlasPositions(positions, 0);
                var pixels = bitmap.Pixels;
                for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++) {
                    int actual = atlas.GetPixel(positions[0].x1 + (x + .5f) / 512, positions[0].y1 + (y + .5f) / 512);
                    Require(actual == pixels[y * bitmap.Width + x], $"{type}: texture atlas changed pixel {x},{y}");
                }
            }
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
