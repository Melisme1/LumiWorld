using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Miter-joined flat-top hexagonal progress rim starting at 12 o'clock clockwise around HarmonyCore.
/// Renders a recessed slate groove + subtle warm-stone outer hairline + glacial-teal-to-champagne progress fill + glowing leading tip.
/// </summary>
public sealed class HarmonyCrystalFx : MaskableGraphic
{
    // Flat-top regular hexagon perimeter starting at 12 o'clock (0, 1) going clockwise
    private static readonly Vector2[] HexPath =
    {
        new Vector2(0f, 1f),
        new Vector2(0.5f, 1f),
        new Vector2(1f, 0f),
        new Vector2(0.5f, -1f),
        new Vector2(-0.5f, -1f),
        new Vector2(-1f, 0f),
        new Vector2(-0.5f, 1f),
        new Vector2(0f, 1f)
    };

    private static readonly Color GrooveSlate = new Color(0.10f, 0.17f, 0.22f, 0.74f);
    private static readonly Color OuterWarmHairline = new Color(0.98f, 0.97f, 0.95f, 0.44f);
    private static readonly Color ProgressStartTeal = new Color(0.34f, 0.80f, 0.75f, 0.98f);
    private static readonly Color ProgressEndChampagne = new Color(0.91f, 0.73f, 0.36f, 1f);
    private static readonly Color RippleColor = new Color(0.76f, 0.96f, 0.95f, 1f);

    public float progress;
    public bool hovered;

    private float flashTime = -10f;
    private float flashDuration = 0.55f;
    private float hoverBlend;
    private float prevProgress = -1f;
    private float prevHover = -1f;
    private float prevFlash = -1f;

    public void Flash(bool milestone = false)
    {
        flashDuration = milestone ? 0.75f : 0.50f;
        flashTime = Time.unscaledTime;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        hoverBlend = Mathf.MoveTowards(hoverBlend, hovered ? 1f : 0f, dt * 6f);
        float flash = Mathf.Clamp01(1f - (Time.unscaledTime - flashTime) / Mathf.Max(0.01f, flashDuration));

        bool tipAnimating = progress > 0.005f && progress < 0.999f;
        if (!Mathf.Approximately(progress, prevProgress) ||
            !Mathf.Approximately(hoverBlend, prevHover) ||
            !Mathf.Approximately(flash, prevFlash) ||
            flash > 0f || tipAnimating)
        {
            SetVerticesDirty();
            prevProgress = progress;
            prevHover = hoverBlend;
            prevFlash = flash;
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Vector2 size = rectTransform.rect.size;
        if (size.x < 4f || size.y < 4f) return;

        // Enforce exact regular flat-top hexagon aspect (width : height = 1 : 0.8660254)
        float halfH = Mathf.Min(size.y * 0.5f, size.x * 0.5f * 0.8660254f);
        float halfW = halfH / 0.8660254f;
        Vector2 baseHalf = new Vector2(halfW, halfH);

        float clamped = Mathf.Clamp01(progress);
        float flash = Mathf.Clamp01(1f - (Time.unscaledTime - flashTime) / Mathf.Max(0.01f, flashDuration));

        // 1. Delicate warm-ivory outer hairline framing the track
        AddMiteredHexArc(vh, baseHalf, 1f, 3.6f, 4.9f, OuterWarmHairline, OuterWarmHairline);

        // 2. Deep-slate recessed groove for unmistakable contrast on bright ocean
        AddMiteredHexArc(vh, baseHalf, 1f, -3.5f, 3.6f, GrooveSlate, GrooveSlate);

        // 3. Filled clockwise progress arc (Glacial Teal -> Refined Champagne Gold)
        if (clamped > 0.002f)
        {
            Color startCol = Color.Lerp(ProgressStartTeal, Color.white, hoverBlend * 0.14f + flash * 0.28f);
            Color endCol = Color.Lerp(ProgressEndChampagne, Color.white, hoverBlend * 0.16f + flash * 0.32f);
            Vector2 tipPos = AddMiteredHexArc(vh, baseHalf, clamped, -2.35f, 2.35f, startCol, endCol);

            // Soft luminous bead + crisp 4-point glint at the leading edge
            if (clamped < 0.998f)
            {
                float breath = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 4.2f);
                AddSoftDisc(vh, tipPos, 9.5f * breath, new Color(endCol.r, endCol.g, endCol.b, 0.36f), 14);
                AddSoftDisc(vh, tipPos, 4.2f * breath, new Color(1f, 0.98f, 0.90f, 0.96f), 12);
            }
        }

        // 4. Expanding kinetic hex ripple on Harmony gain or milestone
        if (flash > 0.001f)
        {
            float expand = (1f - flash) * 14f;
            Color ripple = RippleColor;
            ripple.a = flash * flash * 0.72f;
            AddMiteredHexArc(vh, baseHalf, 1f, 2.5f + expand, 4.8f + expand, ripple, ripple);
        }
    }

    private static Vector2 AddMiteredHexArc(
        VertexHelper vh,
        Vector2 baseHalf,
        float fraction,
        float innerOffsetPx,
        float outerOffsetPx,
        Color startColor,
        Color endColor)
    {
        float halfH = Mathf.Max(1f, baseHalf.y);
        float inScale = Mathf.Max(0.1f, (halfH + innerOffsetPx) / halfH);
        float outScale = Mathf.Max(inScale + 0.01f, (halfH + outerOffsetPx) / halfH);

        float totalLen = 0f;
        for (int i = 1; i < HexPath.Length; i++)
            totalLen += Vector2.Distance(Vector2.Scale(HexPath[i - 1], baseHalf), Vector2.Scale(HexPath[i], baseHalf));

        float targetLen = Mathf.Clamp01(fraction) * totalLen;
        float walked = 0f;
        Vector2 lastTip = Vector2.Scale(HexPath[0], baseHalf);

        for (int i = 1; i < HexPath.Length && walked < targetLen - 0.001f; i++)
        {
            Vector2 pA = Vector2.Scale(HexPath[i - 1], baseHalf);
            Vector2 pB = Vector2.Scale(HexPath[i], baseHalf);
            float segLen = Vector2.Distance(pA, pB);
            if (segLen < 0.0001f) continue;

            float take = Mathf.Min(segLen, targetLen - walked);
            float t = take / segLen;

            Vector2 inA = pA * inScale;
            Vector2 outA = pA * outScale;
            Vector2 inB = Vector2.Lerp(pA * inScale, pB * inScale, t);
            Vector2 outB = Vector2.Lerp(pA * outScale, pB * outScale, t);

            float uA = totalLen > 0.001f ? walked / targetLen : 0f;
            float uB = totalLen > 0.001f ? (walked + take) / targetLen : 1f;
            Color cA = Color.Lerp(startColor, endColor, uA);
            Color cB = Color.Lerp(startColor, endColor, uB);

            int v0 = vh.currentVertCount;
            vh.AddVert(inA, cA, Vector2.zero);
            vh.AddVert(outA, cA, Vector2.zero);
            vh.AddVert(outB, cB, Vector2.zero);
            vh.AddVert(inB, cB, Vector2.zero);

            vh.AddTriangle(v0, v0 + 1, v0 + 2);
            vh.AddTriangle(v0, v0 + 2, v0 + 3);

            walked += take;
            lastTip = Vector2.Lerp(pA, pB, t);
        }

        return lastTip;
    }

    private static void AddSoftDisc(VertexHelper vh, Vector2 center, float radius, Color centerColor, int segments)
    {
        int centerIdx = vh.currentVertCount;
        vh.AddVert(center, centerColor, Vector2.zero);

        Color edgeColor = new Color(centerColor.r, centerColor.g, centerColor.b, 0f);
        for (int i = 0; i < segments; i++)
        {
            float a = i * (Mathf.PI * 2f / segments);
            Vector2 pos = center + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            vh.AddVert(pos, edgeColor, Vector2.zero);
        }

        for (int i = 0; i < segments; i++)
        {
            int cur = centerIdx + 1 + i;
            int next = centerIdx + 1 + ((i + 1) % segments);
            vh.AddTriangle(centerIdx, cur, next);
        }
    }
}

/// <summary>
/// Refined Additive sparkle & soft ambient crystal glow overlay.
/// Renders staggered delicate facet twinkles (always gently alive, never noisy) plus subtle upward light motes on Harmony gain.
/// </summary>
public sealed class HarmonySparkleOverlay : MaskableGraphic
{
    private struct SparkleSpec
    {
        public Vector2 Anchor;
        public float Period;
        public float Phase;
        public float MaxRadius;
        public Color Tint;
    }

    private struct BurstMote
    {
        public Vector2 Origin;
        public Vector2 Velocity;
        public float Age;
        public float Life;
        public float Size;
        public Color Tint;
    }

    private static readonly SparkleSpec[] CoreSparkles =
    {
        new SparkleSpec { Anchor = new Vector2(-0.32f,  0.40f), Period = 2.4f, Phase = 0.00f, MaxRadius = 15f, Tint = new Color(0.95f, 1.00f, 1.00f, 1f) },
        new SparkleSpec { Anchor = new Vector2( 0.36f,  0.26f), Period = 2.9f, Phase = 0.85f, MaxRadius = 13f, Tint = new Color(0.70f, 0.99f, 0.94f, 1f) },
        new SparkleSpec { Anchor = new Vector2(-0.42f, -0.16f), Period = 3.3f, Phase = 1.65f, MaxRadius = 11f, Tint = new Color(1.00f, 0.93f, 0.78f, 1f) },
        new SparkleSpec { Anchor = new Vector2( 0.24f, -0.34f), Period = 2.7f, Phase = 2.25f, MaxRadius = 12f, Tint = new Color(0.84f, 0.96f, 1.00f, 1f) },
        new SparkleSpec { Anchor = new Vector2( 0.04f,  0.48f), Period = 3.1f, Phase = 1.25f, MaxRadius = 10f, Tint = new Color(0.98f, 0.95f, 0.98f, 1f) }
    };

    private static Material sharedAdditiveMat;

    public bool drawAmbientHalo = true;
    public bool hovered;

    private readonly BurstMote[] motes = new BurstMote[12];
    private int moteCursor;
    private float hoverBlend;

    protected override void Awake()
    {
        base.Awake();
        ApplyAdditiveMaterial();
    }

    public void ApplyAdditiveMaterial()
    {
        if (sharedAdditiveMat == null)
        {
            Shader s = Resources.Load<Shader>("UI/Harmony/LumiWorld_UI_Additive")
                ?? Shader.Find("LumiWorld/UI/AdditiveGlow");
            if (s != null)
            {
                sharedAdditiveMat = new Material(s) { name = "HarmonyAdditiveGlow_Mat" };
            }
        }
        if (sharedAdditiveMat != null)
            material = sharedAdditiveMat;
    }

    public void TriggerBurst(int count = 7)
    {
        Vector2 half = rectTransform.rect.size * 0.5f;
        int n = Mathf.Clamp(count, 1, motes.Length);
        for (int i = 0; i < n; i++)
        {
            float angle = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.28f, 0.28f);
            float startR = Random.Range(0.28f, 0.62f);
            Vector2 origin = new Vector2(Mathf.Cos(angle) * half.x * startR, Mathf.Sin(angle) * half.y * startR);
            Vector2 vel = new Vector2(Mathf.Cos(angle) * Random.Range(14f, 34f), Mathf.Sin(angle) * Random.Range(12f, 26f) + Random.Range(16f, 34f));

            motes[moteCursor] = new BurstMote
            {
                Origin = origin,
                Velocity = vel,
                Age = 0f,
                Life = Random.Range(0.55f, 0.85f),
                Size = Random.Range(6f, 11f),
                Tint = (i % 2 == 0) ? new Color(0.74f, 0.99f, 0.94f, 1f) : new Color(1f, 0.91f, 0.68f, 1f)
            };
            moteCursor = (moteCursor + 1) % motes.Length;
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        hoverBlend = Mathf.MoveTowards(hoverBlend, hovered ? 1f : 0f, dt * 6f);

        for (int i = 0; i < motes.Length; i++)
        {
            if (motes[i].Life > 0f)
            {
                motes[i].Age += dt;
                if (motes[i].Age >= motes[i].Life)
                    motes[i].Life = 0f;
            }
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Vector2 half = rectTransform.rect.size * 0.5f;
        if (half.x < 4f || half.y < 4f) return;

        float t = Time.unscaledTime;

        // 1. Subtle ambient crystal halo (kept soft so facets stay crisp)
        if (drawAmbientHalo)
        {
            float breath = 0.85f + 0.15f * Mathf.Sin(t * 1.6f);
            float haloA = (0.09f + hoverBlend * 0.08f) * breath;
            AddSoftRadial(vh, Vector2.zero, Mathf.Min(half.x, half.y) * 1.05f, new Color(0.45f, 0.86f, 0.92f, haloA), 20);
        }

        // 2. Five staggered crystalline 4-point facet glints
        for (int i = 0; i < CoreSparkles.Length; i++)
        {
            SparkleSpec sp = CoreSparkles[i];
            float phase = Mathf.Repeat((t + sp.Phase) / sp.Period, 1f);
            float env = phase < 0.54f ? Mathf.Sin((phase / 0.54f) * Mathf.PI) : 0f;
            env = env * env;
            float intensity = Mathf.Clamp01(env * (0.90f + hoverBlend * 0.25f));
            if (intensity < 0.02f) continue;

            Vector2 pos = new Vector2(sp.Anchor.x * half.x, sp.Anchor.y * half.y);
            float rot = Mathf.Sin(t * 0.9f + sp.Phase) * 0.34f;
            float r = sp.MaxRadius * (0.62f + 0.58f * intensity);

            Color glowCol = sp.Tint;
            glowCol.a = intensity * 0.38f;
            AddSoftRadial(vh, pos, r * 0.95f, glowCol, 12);

            Color starCol = sp.Tint;
            starCol.a = intensity;
            AddSoftStar(vh, pos, r, rot, starCol);
        }

        // 3. Transient rising burst motes on score gain / milestone
        for (int i = 0; i < motes.Length; i++)
        {
            if (motes[i].Life <= 0f) continue;
            float u = Mathf.Clamp01(motes[i].Age / motes[i].Life);
            float fade = Mathf.Sin(u * Mathf.PI) * (1f - u * 0.45f);
            Vector2 pos = motes[i].Origin + motes[i].Velocity * u;
            Color c = motes[i].Tint;
            c.a = fade * 0.90f;
            AddSoftRadial(vh, pos, motes[i].Size * (1.2f - 0.35f * u), new Color(c.r, c.g, c.b, c.a * 0.45f), 10);
            AddSoftStar(vh, pos, motes[i].Size * (1.05f - 0.3f * u), u * 1.1f, c);
        }
    }

    private static void AddSoftRadial(VertexHelper vh, Vector2 center, float radius, Color centerColor, int segments)
    {
        int cIdx = vh.currentVertCount;
        vh.AddVert(center, centerColor, Vector2.zero);
        Color zero = new Color(centerColor.r, centerColor.g, centerColor.b, 0f);
        for (int i = 0; i < segments; i++)
        {
            float a = i * (Mathf.PI * 2f / segments);
            vh.AddVert(center + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius), zero, Vector2.zero);
        }
        for (int i = 0; i < segments; i++)
            vh.AddTriangle(cIdx, cIdx + 1 + i, cIdx + 1 + ((i + 1) % segments));
    }

    private static void AddSoftStar(VertexHelper vh, Vector2 center, float radius, float angleOffset, Color tint)
    {
        int cIdx = vh.currentVertCount;
        Color core = new Color(1f, 1f, 1f, tint.a);
        Color tip = new Color(tint.r, tint.g, tint.b, 0f);
        Color inner = new Color(tint.r, tint.g, tint.b, tint.a * 0.45f);

        vh.AddVert(center, core, Vector2.zero);
        for (int i = 0; i < 8; i++)
        {
            float a = angleOffset + i * (Mathf.PI * 0.25f);
            bool isLong = (i % 2 == 0);
            float dist = isLong ? radius : radius * 0.20f;
            Vector2 p = center + new Vector2(Mathf.Cos(a) * dist, Mathf.Sin(a) * dist);
            vh.AddVert(p, isLong ? tip : inner, Vector2.zero);
        }
        for (int i = 0; i < 8; i++)
            vh.AddTriangle(cIdx, cIdx + 1 + i, cIdx + 1 + ((i + 1) % 8));
    }
}

/// <summary>
/// Subtle additive emerald glow behind each active TerrainCharge crown jewel.
/// </summary>
public sealed class HarmonyChargeGlowFx : MaskableGraphic
{
    private static Material sharedChargeMat;

    public bool activeCharge;
    public bool placementMode;
    public bool hovered;
    public float phaseOffset;

    private float hoverBlend;

    protected override void Awake()
    {
        base.Awake();
        if (sharedChargeMat == null)
        {
            Shader s = Resources.Load<Shader>("UI/Harmony/LumiWorld_UI_Additive")
                ?? Shader.Find("LumiWorld/UI/AdditiveGlow");
            if (s != null)
                sharedChargeMat = new Material(s) { name = "HarmonyChargeGlow_Mat" };
        }
        if (sharedChargeMat != null)
            material = sharedChargeMat;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        hoverBlend = Mathf.MoveTowards(hoverBlend, hovered ? 1f : 0f, dt * 8f);

        if (rectTransform.parent != null)
        {
            float targetScale = !activeCharge ? 1f : (hovered ? 1.16f : (placementMode ? 1.07f : 1f));
            float next = Mathf.MoveTowards(rectTransform.parent.localScale.x, targetScale, dt * 2.6f);
            rectTransform.parent.localScale = Vector3.one * next;
        }

        if (activeCharge)
            SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!activeCharge) return;

        Vector2 half = rectTransform.rect.size * 0.5f;
        float t = Time.unscaledTime + phaseOffset;
        float pulse = placementMode
            ? (0.55f + 0.45f * Mathf.Sin(t * 5.0f))
            : (0.65f + 0.35f * Mathf.Sin(t * 2.1f));

        Color c = placementMode
            ? new Color(0.95f, 0.86f, 0.48f, (0.34f + hoverBlend * 0.22f) * pulse)
            : new Color(0.42f, 0.90f, 0.62f, (0.24f + hoverBlend * 0.24f) * pulse);

        int cIdx = vh.currentVertCount;
        vh.AddVert(Vector2.zero, c, Vector2.zero);
        Color edge = new Color(c.r, c.g, c.b, 0f);
        const int segs = 14;
        for (int i = 0; i < segs; i++)
        {
            float a = i * (Mathf.PI * 2f / segs);
            vh.AddVert(new Vector2(Mathf.Cos(a) * half.x * 1.15f, Mathf.Sin(a) * half.y * 1.05f), edge, Vector2.zero);
        }
        for (int i = 0; i < segs; i++)
            vh.AddTriangle(cIdx, cIdx + 1 + i, cIdx + 1 + ((i + 1) % segs));
    }
}
