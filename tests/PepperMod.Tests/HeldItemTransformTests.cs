using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

internal static class HeldItemTransformTests
{
    public static void Run(Action<string, Action> check)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "modinfo.json"))) root = root.Parent;
        if (root == null) throw new Exception("Cannot locate the mod assets for held-item tests.");
        string game = Environment.GetEnvironmentVariable("VINTAGE_STORY")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vintagestory");
        var item = JObject.Parse(File.ReadAllText(Path.Combine(root.FullName, "assets/peppermod/itemtypes/food/vegetable.json")));
        var vanilla = JObject.Parse(File.ReadAllText(Path.Combine(game, "assets/survival/itemtypes/food/vegetable.json")));
        foreach (string variety in PepperBundleTests.Types)
        {
            var transform = item["tpHandTransformByType"]["*-" + variety].ToObject<ModelTransform>().EnsureDefaultValues();
            var reference = vanilla["tpHandTransformByType"]["*-bellpepper"].ToObject<ModelTransform>().EnsureDefaultValues();
            var player = LoadShape(Path.Combine(game, "assets/game/shapes/entity/humanoid/seraph.json"));
            var hands = Elements(player.Elements).SelectMany(e => e.AttachmentPoints ?? [])
                .Where(a => a.Code == "RightHand" || a.Code == "LeftHand").ToArray();
            if (hands.Length != 2) throw new Exception("Expected both player hand attachment points.");
            var pepper = LoadShape(Path.Combine(root.FullName, $"assets/peppermod/shapes/item/food/vegetable/{variety}.json"));

            foreach (var hand in hands)
            {
                check($"{variety} uses the vanilla food grip in {hand.Code}", () => {
                    var actual = HandMatrix(transform, hand).TransformVector(new Vec4f(.5f, .5f, .5f, 1));
                    var expected = HandMatrix(reference, hand).TransformVector(new Vec4f(.5f, 3f / 16, .5f, 1));
                    float error = MathF.Max(MathF.Abs(actual.X - expected.X),
                        MathF.Max(MathF.Abs(actual.Y - expected.Y), MathF.Abs(actual.Z - expected.Z)));
                    Require(error < .002f, $"Fruit center is displaced from the vanilla grip by {error} blocks.");
                    Require(MathF.Abs(transform.Rotation.Z - reference.Rotation.Z) < 1,
                        "The fruit must project out of the grip at the vanilla food angle.");
                });
            }

            check($"held {variety} geometry has a visible food-sized silhouette", () => {
                var points = new List<Vec4f>();
                var hand = HandMatrix(transform, hands.Single(a => a.Code == "RightHand"));
                foreach (var element in pepper.Elements.Where(e => e.FacesResolved?.Any(f => f?.Enabled == true) == true))
                {
                    Require(element.Children == null || element.Children.Length == 0,
                        "Update the bounds test if the item changes from flattened to nested geometry.");
                    var local = new Matrixf(element.GetLocalTransformMatrix(0));
                    for (int i = 0; i < 8; i++)
                    {
                        var p = new Vec4f(
                            (float)((element.To[0] - element.From[0]) / 16 * ((i & 1) == 0 ? 0 : 1)),
                            (float)((element.To[1] - element.From[1]) / 16 * ((i & 2) == 0 ? 0 : 1)),
                            (float)((element.To[2] - element.From[2]) / 16 * ((i & 4) == 0 ? 0 : 1)), 1);
                        points.Add(hand.TransformVector(local.TransformVector(p)));
                    }
                }
                Require(points.Count > 0 && points.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z)),
                    "The held model must contain finite visible geometry.");
                float length = points.Max(p => p.X) - points.Min(p => p.X);
                float thickness = points.Max(p => p.Z) - points.Min(p => p.Z);
                Require(length > (variety == "jalapeno" ? .35f : .25f) && length < .6f, $"Unexpected held length: {length} blocks.");
                Require(thickness > (variety == "serrano" ? .06f : .09f) && thickness < (variety == "habanero" ? .28f : .2f), $"Unexpected held thickness: {thickness} blocks.");
            });
        }
    }

    // EntityShapeRenderer.RenderItem scales before translating and combines the
    // hand's rotation with the item's rotation. ModelTransform.AsMatrix differs.
    private static Matrixf HandMatrix(ModelTransform t, AttachmentPoint a) => new Matrixf().Identity()
        .Translate(t.Origin.X, t.Origin.Y, t.Origin.Z)
        .Scale(t.ScaleXYZ.X, t.ScaleXYZ.Y, t.ScaleXYZ.Z)
        .Translate(a.PosX / 16 + t.Translation.X, a.PosY / 16 + t.Translation.Y, a.PosZ / 16 + t.Translation.Z)
        .Rotate((float)(a.RotationX + t.Rotation.X) * GameMath.DEG2RAD,
            (float)(a.RotationY + t.Rotation.Y) * GameMath.DEG2RAD,
            (float)(a.RotationZ + t.Rotation.Z) * GameMath.DEG2RAD)
        .Translate(-t.Origin.X, -t.Origin.Y, -t.Origin.Z);

    private static Shape LoadShape(string path) => JsonConvert.DeserializeObject<Shape>(File.ReadAllText(path));
    private static IEnumerable<ShapeElement> Elements(ShapeElement[] elements)
    {
        foreach (var e in elements ?? [])
        {
            yield return e;
            foreach (var child in Elements(e.Children)) yield return child;
        }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
