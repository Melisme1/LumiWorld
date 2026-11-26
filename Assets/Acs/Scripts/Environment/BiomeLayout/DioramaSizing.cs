using System;

namespace LumiWorld.Acs
{
    public enum DioramaSizeAxis { Height, Footprint }

    /// <summary>Metre-based limits. Uniform scaling preserves each model's proportions.</summary>
    [Serializable]
    public sealed class DioramaSizeLimits
    {
        public DioramaSizeAxis primaryAxis;
        public float targetMin = .35f;
        public float targetMax = .5f;
        public float minHeight = .04f;
        public float maxHeight = 1f;
        public float minFootprint = .15f;
        public float maxFootprint = 1f;
    }

    public static class DioramaSizing
    {
        public static bool TryGetScaleRange(float height, float width, DioramaSizeLimits limits,
            out float minimum, out float maximum)
        {
            minimum = maximum = 0;
            if (limits == null || !Positive(height) || !Positive(width) ||
                !Positive(limits.targetMin) || !Positive(limits.targetMax) ||
                !Positive(limits.maxHeight) || !Positive(limits.maxFootprint) ||
                limits.minHeight < 0 || limits.minFootprint < 0)
                return false;

            float primary = limits.primaryAxis == DioramaSizeAxis.Height ? height : width;
            minimum = Math.Max(limits.targetMin / primary,
                Math.Max(limits.minHeight / height, limits.minFootprint / width));
            maximum = Math.Min(limits.targetMax / primary,
                Math.Min(limits.maxHeight / height, limits.maxFootprint / width));
            return Positive(minimum) && Positive(maximum) && minimum <= maximum;
        }

        private static bool Positive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
