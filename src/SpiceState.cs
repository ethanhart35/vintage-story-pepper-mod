using System;

namespace PepperMod
{
    public enum SpiceLevel { None, Mild, Hot, Extreme }

    public readonly struct SpiceState
    {
        public const float MaximumHeat = 100;
        public const float HotThreshold = 34;
        public const float ExtremeThreshold = 67;
        public const float CoolingDelaySeconds = 5;
        public float Heat { get; }
        public float CoolingDelay { get; }
        public SpiceLevel Level => Heat <= 0 ? SpiceLevel.None : Heat < HotThreshold ? SpiceLevel.Mild
            : Heat < ExtremeThreshold ? SpiceLevel.Hot : SpiceLevel.Extreme;

        public SpiceState(float heat, float coolingDelay = 0)
        {
            Heat = float.IsFinite(heat) ? Math.Clamp(heat, 0, MaximumHeat) : 0;
            CoolingDelay = Heat > 0 && float.IsFinite(coolingDelay) ? Math.Clamp(coolingDelay, 0, CoolingDelaySeconds) : 0;
        }

        public SpiceState Add(float amount) => float.IsFinite(amount) && amount > 0
            ? new SpiceState(Heat + Math.Min(amount, MaximumHeat), CoolingDelaySeconds) : this;

        public SpiceState Cool(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds <= 0) return this;
            float coolingTime = Math.Max(0, seconds - CoolingDelay);
            return new SpiceState(Heat - coolingTime, Math.Max(0, CoolingDelay - seconds));
        }

        public float WarmBody(float temperature, float normalTemperature, float seconds)
        {
            if (Level < SpiceLevel.Hot || !float.IsFinite(seconds) || seconds <= 0
                || !float.IsFinite(temperature) || !float.IsFinite(normalTemperature)) return temperature;
            float limit = normalTemperature + 2;
            return temperature >= limit ? temperature : Math.Min(limit, temperature + .12f * seconds);
        }

        public float SegmentFill(int index)
        {
            float start = index == 0 ? 0 : index == 1 ? HotThreshold : ExtremeThreshold;
            float end = index == 0 ? HotThreshold : index == 1 ? ExtremeThreshold : MaximumHeat;
            return Math.Clamp((Heat - start) / (end - start), 0, 1);
        }

        public float RedIntensity => Level == SpiceLevel.Extreme ? Math.Clamp((Heat - ExtremeThreshold) / 16, .25f, 1) : 0;
    }
}
