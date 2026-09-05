using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace PepperMod
{
    public class SpiceHud : HudElement
    {
        private SpiceState state;
        private LoadedTexture vignetteTexture;
        private float redOpacity;
        private float pulseTime;
        private readonly Vec4f tint = new Vec4f(1, 1, 1, 0);
        public override bool Focusable => false;
        public override bool ShouldReceiveMouseEvents() => false;
        public override bool ShouldReceiveKeyboardEvents() => false;

        public SpiceHud(ICoreClientAPI api) : base(api) { }

        public void Update(SpiceState value)
        {
            state = value;
            if (state.Level == SpiceLevel.None)
            {
                redOpacity = 0;
                pulseTime = 0;
                if (IsOpened()) TryClose();
                return;
            }
            if (SingleComposer == null) ComposeMeter();
            for (int i = 0; i < 3; i++)
            {
                var bar = SingleComposer.GetStatbar("spice-" + i);
                float fill = state.SegmentFill(i);
                if (Math.Abs(bar.GetValue() - fill) > .0001f) bar.SetValue(fill);
            }
            if (!IsOpened()) TryOpen();
        }

        private void ComposeMeter()
        {
            var bounds = ElementBounds.Fixed(0, 0, 252, 42)
                .WithAlignment(EnumDialogArea.RightBottom).WithFixedAlignmentOffset(-24, -145);
            SingleComposer = capi.Gui.CreateCompo("peppermod-spice", bounds);
            string[] labels = { "mild", "hot", "extreme" };
            double[][] colors = { new double[] { .43, .65, .27, 1 }, new double[] { .95, .55, .16, 1 }, new double[] { .83, .18, .14, 1 } };
            for (int i = 0; i < 3; i++)
            {
                SingleComposer.AddStaticText(Lang.Get("peppermod:spice-" + labels[i]),
                    CairoFont.WhiteSmallText().WithFontSize(14), EnumTextOrientation.Center,
                    ElementBounds.Fixed(i * 86, 0, 80, 22));
                SingleComposer.AddStatbar(ElementBounds.Fixed(i * 86, 25, 80, 10), colors[i], false, "spice-" + i);
            }
            SingleComposer.Compose();
            for (int i = 0; i < 3; i++)
            {
                var bar = SingleComposer.GetStatbar("spice-" + i);
                bar.SetValues(0, 0, 1);
                bar.SetLineInterval(1);
            }
        }

        public override void OnRenderGUI(float dt)
        {
            if (state.Level == SpiceLevel.None || capi.World.Player?.Entity?.Alive != true
                || capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Spectator) return;

            float frameTime = capi.IsGamePaused || !float.IsFinite(dt) ? 0 : Math.Clamp(dt, 0, .1f);
            pulseTime = (pulseTime + frameTime) % 4;
            float target = state.RedIntensity * (.28f + .04f * MathF.Sin(pulseTime * MathF.PI / 2));
            redOpacity += (target - redOpacity) * Math.Min(1, frameTime * 3);
            if (redOpacity > .001f)
            {
                if (vignetteTexture == null) CreateVignette();
                tint.A = redOpacity;
                RenderVignette();
            }
            base.OnRenderGUI(dt);
        }

        private void CreateVignette()
        {
            const int size = 256;
            int[] rgba = new int[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) / size * 2 - 1;
                float dy = (y + .5f) / size * 2 - 1;
                float edge = Math.Clamp((MathF.Sqrt(dx * dx + dy * dy) - .5f) / .65f, 0, 1);
                int alpha = (int)(255 * edge * edge * (3 - 2 * edge));
                rgba[y * size + x] = (alpha << 24) | (12 << 16) | (16 << 8) | 190;
            }
            vignetteTexture = new LoadedTexture(capi, 0, size, size);
            capi.Render.LoadOrUpdateTextureFromRgba(rgba, true, 0, ref vignetteTexture);
        }

        private void RenderVignette()
        {
            // Transparent overlay pixels must not occlude the HUD drawn afterward.
            capi.Render.GLDepthMask(false);
            try
            {
                capi.Render.Render2DTexture(vignetteTexture.TextureId, 0, 0, capi.Render.FrameWidth, capi.Render.FrameHeight, 50, tint);
            }
            finally
            {
                capi.Render.GLDepthMask(true);
            }
        }

        public override void Dispose()
        {
            if (IsOpened()) TryClose();
            vignetteTexture?.Dispose();
            vignetteTexture = null;
            base.Dispose();
        }
    }
}
