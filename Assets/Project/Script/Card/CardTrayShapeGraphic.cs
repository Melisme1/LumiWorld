using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monolithic Warm Stone vector-mesh graphic for the P-03 Card Deck tray.
/// Renders a single seamless silhouette (wide rounded card shelf + concave S-curve
/// shoulders + raised rounded tab dock + integrated top-center chevron dome) with a
/// feathered organic drop shadow, continuous hairline border, and anti-aliased outer edge.
/// Also supports a compact collapsed pill state and accurate silhouette raycast filtering
/// so clicks over the transparent air above the shoulders pass through to the 3D world.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class CardTrayShapeGraphic : MaskableGraphic, ICanvasRaycastFilter
{
    [Header("Expanded Silhouette")]
    [SerializeField] private float shelfWidth = 592f;
    [SerializeField] private float shelfHeight = 132f;
    [SerializeField] private float dockWidth = 436f;
    [SerializeField] private float dockHeight = 42f;
    [SerializeField] private float shelfCornerRadius = 26f;
    [SerializeField] private float shoulderFilletRadius = 19f;
    [SerializeField] private float dockCornerRadius = 23f;

    [Header("Integrated Top Chevron Dome")]
    [SerializeField] private bool showChevronDome = true;
    [SerializeField] private float domeHalfWidth = 24f;
    [SerializeField] private float domeHeight = 19f;
    [SerializeField] private float domeBaseFilletRadius = 9f;
    [SerializeField] private float domeTopCornerRadius = 10f;

    [Header("Collapsed Pill State")]
    [SerializeField] private bool collapsedMode = false;
    [SerializeField] private float collapsedWidth = 56f;
    [SerializeField] private float collapsedHeight = 26f;

    [Header("Warm Stone Styling")]
    [SerializeField] private Color fillColor = new Color(0.937f, 0.918f, 0.886f, 1f);      // #EFEAE2
    [SerializeField] private Color borderColor = new Color(0.835f, 0.808f, 0.765f, 1f);    // #D5CEC3
    [SerializeField] private float borderThickness = 1.5f;
    [SerializeField] private float antiAliasWidth = 1.25f;

    [Header("Organic Drop Shadow")]
    [SerializeField] private Color shadowColor = new Color(0.145f, 0.106f, 0.071f, 0.17f);
    [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -4.5f);
    [SerializeField] private float shadowSpread = 7.5f;

    [Header("Tessellation")]
    [SerializeField] private int arcSegments = 12;

    private readonly List<Vector2> contour = new List<Vector2>(160);
    private readonly List<Vector2> normals = new List<Vector2>(160);

    public float ShelfWidth
    {
        get => shelfWidth;
        set
        {
            if (Mathf.Approximately(shelfWidth, value)) return;
            shelfWidth = value;
            SetVerticesDirty();
        }
    }

    public float ShelfHeight => shelfHeight;
    public float DockWidth => dockWidth;
    public float DockHeight => dockHeight;
    public float DomeHeight => showChevronDome ? domeHeight : 0f;
    public float TotalExpandedHeight => shelfHeight + dockHeight + (showChevronDome ? domeHeight : 0f);
    public float CollapsedWidth => collapsedWidth;
    public float CollapsedHeight => collapsedHeight;

    public bool CollapsedMode
    {
        get => collapsedMode;
        set
        {
            if (collapsedMode == value) return;
            collapsedMode = value;
            SetVerticesDirty();
        }
    }

    public void ConfigureGeometry(
        float newShelfWidth,
        float newShelfHeight,
        float newDockWidth,
        float newDockHeight,
        float newShelfCornerRadius = 26f,
        float newShoulderFilletRadius = 19f,
        float newDockCornerRadius = 23f,
        bool includeChevronDome = true,
        float newDomeHalfWidth = 24f,
        float newDomeHeight = 19f,
        float newDomeBaseFillet = 9f,
        float newDomeTopRadius = 10f)
    {
        shelfWidth = newShelfWidth;
        shelfHeight = newShelfHeight;
        dockWidth = newDockWidth;
        dockHeight = newDockHeight;
        shelfCornerRadius = newShelfCornerRadius;
        shoulderFilletRadius = newShoulderFilletRadius;
        dockCornerRadius = newDockCornerRadius;
        showChevronDome = includeChevronDome;
        domeHalfWidth = newDomeHalfWidth;
        domeHeight = newDomeHeight;
        domeBaseFilletRadius = newDomeBaseFillet;
        domeTopCornerRadius = newDomeTopRadius;
        SetVerticesDirty();
    }

    public void ConfigureStyle(Color fill, Color border, Color shadow)
    {
        fillColor = fill;
        borderColor = border;
        shadowColor = shadow;
        SetVerticesDirty();
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!isActiveAndEnabled || !raycastTarget) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 local))
        {
            return false;
        }

        if (contour.Count < 3)
        {
            RebuildContour(out _);
        }

        // Convert to silhouette coordinate space (origin at bottom-center of RectTransform)
        Vector2 p = LocalToSilhouettePoint(local);
        return IsPointInPolygon(p, contour);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        RebuildContour(out Vector2 fanCenter);
        if (contour.Count < 3) return;

        ComputeOutwardNormals();

        Vector2 baseOffset = SilhouetteToLocalOffset();
        float alphaMul = color.a;

        // 1. Soft multi-ring feathered drop shadow hugging the exact silhouette
        if (shadowColor.a > 0.001f && alphaMul > 0.001f)
        {
            AddFeatheredShadow(vh, fanCenter, baseOffset, alphaMul);
        }

        // 2. Main Warm Stone fill body (slightly inset to meet the hairline border cleanly)
        float inset = Mathf.Max(0f, borderThickness);
        Color32 fill32 = MultiplyColor(fillColor, color);
        AddFillFan(vh, fanCenter, baseOffset, -inset, fill32);

        // 3. Continuous crisp hairline perimeter border (convex corners + concave fillets + dome)
        Color32 border32 = MultiplyColor(borderColor, color);
        if (inset > 0.05f)
        {
            AddPerimeterStrip(vh, baseOffset, -inset, 0f, border32, border32);
        }

        // 4. Anti-aliased outer alpha fringe for razor-smooth curves at every resolution
        if (antiAliasWidth > 0.05f)
        {
            Color32 clearBorder32 = border32;
            clearBorder32.a = 0;
            AddPerimeterStrip(vh, baseOffset, 0f, antiAliasWidth, border32, clearBorder32);
        }
    }

    private Vector2 SilhouetteToLocalOffset()
    {
        Rect r = rectTransform.rect;
        // Origin of silhouette is horizontal center, bottom edge of rect
        return new Vector2(r.center.x, r.yMin);
    }

    private Vector2 LocalToSilhouettePoint(Vector2 local)
    {
        return local - SilhouetteToLocalOffset();
    }

    private void RebuildContour(out Vector2 fanCenter)
    {
        contour.Clear();
        int seg = Mathf.Clamp(arcSegments, 4, 24);

        if (collapsedMode)
        {
            BuildCollapsedPillContour(seg, out fanCenter);
            return;
        }

        BuildExpandedMonolithContour(seg, out fanCenter);
    }

    private void BuildCollapsedPillContour(int seg, out Vector2 fanCenter)
    {
        float w = Mathf.Max(20f, collapsedWidth);
        float h = Mathf.Max(16f, collapsedHeight);
        float halfW = w * 0.5f;
        float r = Mathf.Min(h * 0.5f, halfW - 0.5f);

        // CCW starting at bottom-right straight end
        AddArc(new Vector2(halfW - r, r), r, -90f, 0f, seg);
        AddArc(new Vector2(halfW - r, h - r), r, 0f, 90f, seg);
        AddArc(new Vector2(-halfW + r, h - r), r, 90f, 180f, seg);
        AddArc(new Vector2(-halfW + r, r), r, 180f, 270f, seg);

        fanCenter = new Vector2(0f, h * 0.5f);
    }

    private void BuildExpandedMonolithContour(int seg, out Vector2 fanCenter)
    {
        float sw = Mathf.Max(180f, shelfWidth);
        float sh = Mathf.Max(48f, shelfHeight);
        float halfShelf = sw * 0.5f;

        float rShelf = Mathf.Clamp(shelfCornerRadius, 4f, Mathf.Min(sh * 0.45f, halfShelf * 0.4f));

        // Clamp dock width so shoulders always have clean room for outer corner + concave fillet
        float maxHalfDock = Mathf.Max(40f, halfShelf - rShelf - 12f);
        float halfDock = Mathf.Clamp(dockWidth * 0.5f, 40f, maxHalfDock);
        float dh = Mathf.Max(16f, dockHeight);
        float dockTop = sh + dh;

        float availShoulder = Mathf.Max(4f, halfShelf - rShelf - halfDock);
        float rShoulder = Mathf.Clamp(shoulderFilletRadius, 2f, Mathf.Min(availShoulder, dh * 0.65f));
        float rDockTop = Mathf.Clamp(dockCornerRadius, 4f, Mathf.Min(dh - rShoulder, halfDock * 0.4f));
        if (rDockTop < 4f)
        {
            rDockTop = Mathf.Min(dh * 0.5f, halfDock * 0.4f);
            rShoulder = Mathf.Min(rShoulder, Mathf.Max(2f, dh - rDockTop));
        }

        // Check whether integrated top-center chevron dome fits cleanly on dock top
        float dockTopFlatHalf = halfDock - rDockTop;
        bool buildDome = showChevronDome && domeHeight > 2f && dockTopFlatHalf > domeHalfWidth + domeBaseFilletRadius + 6f;

        // Trace CCW perimeter starting from bottom-right corner
        // 1. Bottom-right shelf convex corner (270 -> 360)
        AddArc(new Vector2(halfShelf - rShelf, rShelf), rShelf, -90f, 0f, seg);

        // 2. Top-right shelf convex corner (0 -> 90)
        AddArc(new Vector2(halfShelf - rShelf, sh - rShelf), rShelf, 0f, 90f, seg);

        // 3. Right concave shoulder fillet (270 -> 180, curving up-left from shelf top to dock right wall)
        AddArc(new Vector2(halfDock + rShoulder, sh + rShoulder), rShoulder, 270f, 180f, seg);

        // 4. Top-right dock convex corner (0 -> 90)
        AddArc(new Vector2(halfDock - rDockTop, dockTop - rDockTop), rDockTop, 0f, 90f, seg);

        // 5. Integrated top-center chevron dome (right-to-left in CCW order)
        if (buildDome)
        {
            float dhDome = domeHeight;
            float rBase = Mathf.Clamp(domeBaseFilletRadius, 2f, dhDome * 0.5f);
            float rTop = Mathf.Clamp(domeTopCornerRadius, 2f, Mathf.Min(dhDome - rBase, domeHalfWidth - 1f));
            float domeTopY = dockTop + dhDome;

            // Right concave dome base fillet (270 -> 180, curving up-left into right dome wall)
            AddArc(new Vector2(domeHalfWidth + rBase, dockTop + rBase), rBase, 270f, 180f, seg);

            // Right convex dome top corner (0 -> 90)
            AddArc(new Vector2(domeHalfWidth - rTop, domeTopY - rTop), rTop, 0f, 90f, seg);

            // Left convex dome top corner (90 -> 180)
            AddArc(new Vector2(-domeHalfWidth + rTop, domeTopY - rTop), rTop, 90f, 180f, seg);

            // Left concave dome base fillet (0 -> -90, curving down-left back onto dock top)
            AddArc(new Vector2(-domeHalfWidth - rBase, dockTop + rBase), rBase, 0f, -90f, seg);
        }

        // 6. Top-left dock convex corner (90 -> 180)
        AddArc(new Vector2(-halfDock + rDockTop, dockTop - rDockTop), rDockTop, 90f, 180f, seg);

        // 7. Left concave shoulder fillet (0 -> -90, curving down-left from dock left wall to shelf top)
        AddArc(new Vector2(-halfDock - rShoulder, sh + rShoulder), rShoulder, 0f, -90f, seg);

        // 8. Top-left shelf convex corner (90 -> 180)
        AddArc(new Vector2(-halfShelf + rShelf, sh - rShelf), rShelf, 90f, 180f, seg);

        // 9. Bottom-left shelf convex corner (180 -> 270)
        AddArc(new Vector2(-halfShelf + rShelf, rShelf), rShelf, 180f, 270f, seg);

        fanCenter = new Vector2(0f, sh * 0.5f);
    }

    private void AddArc(Vector2 center, float radius, float startDeg, float endDeg, int segments)
    {
        if (radius <= 0.01f)
        {
            AddPointIfDistinct(center);
            return;
        }

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float rad = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
            Vector2 pt = new Vector2(center.x + Mathf.Cos(rad) * radius, center.y + Mathf.Sin(rad) * radius);
            AddPointIfDistinct(pt);
        }
    }

    private void AddPointIfDistinct(Vector2 pt)
    {
        if (contour.Count > 0 && (contour[contour.Count - 1] - pt).sqrMagnitude < 0.01f)
        {
            return;
        }
        if (contour.Count > 2 && (contour[0] - pt).sqrMagnitude < 0.01f)
        {
            return;
        }
        contour.Add(pt);
    }

    private void ComputeOutwardNormals()
    {
        normals.Clear();
        int n = contour.Count;
        if (n < 3) return;

        for (int i = 0; i < n; i++)
        {
            Vector2 prev = contour[(i - 1 + n) % n];
            Vector2 curr = contour[i];
            Vector2 next = contour[(i + 1) % n];

            Vector2 e0 = (curr - prev).normalized;
            Vector2 e1 = (next - curr).normalized;

            // For a CCW contour, outward normal of edge (dx, dy) is (dy, -dx)
            Vector2 n0 = new Vector2(e0.y, -e0.x);
            Vector2 n1 = new Vector2(e1.y, -e1.x);

            Vector2 miter = n0 + n1;
            float sqrLen = miter.sqrMagnitude;
            if (sqrLen < 0.0001f)
            {
                normals.Add(n0);
            }
            else
            {
                miter /= Mathf.Sqrt(sqrLen);
                float denom = Mathf.Max(0.65f, Vector2.Dot(miter, n0));
                float scale = Mathf.Min(1.35f, 1f / denom);
                normals.Add(miter * scale);
            }
        }
    }

    private void AddFeatheredShadow(VertexHelper vh, Vector2 fanCenter, Vector2 baseOffset, float alphaMul)
    {
        Vector2 shBase = baseOffset + shadowOffset;
        float spread = Mathf.Max(2f, shadowSpread);

        Color s0 = shadowColor;
        s0.a *= alphaMul;
        Color s1 = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * 0.52f * alphaMul);
        Color s2 = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * 0.16f * alphaMul);
        Color s3 = new Color(shadowColor.r, shadowColor.g, shadowColor.b, 0f);

        Color32 c0 = s0;
        Color32 c1 = s1;
        Color32 c2 = s2;
        Color32 c3 = s3;

        AddFillFan(vh, fanCenter, shBase, 0f, c0);
        AddPerimeterStrip(vh, shBase, 0f, spread * 0.30f, c0, c1);
        AddPerimeterStrip(vh, shBase, spread * 0.30f, spread * 0.65f, c1, c2);
        AddPerimeterStrip(vh, shBase, spread * 0.65f, spread, c2, c3);
    }

    private void AddFillFan(VertexHelper vh, Vector2 fanCenter, Vector2 baseOffset, float normalOffset, Color32 vertColor)
    {
        int n = contour.Count;
        int centerIdx = vh.currentVertCount;

        UIVertex v = UIVertex.simpleVert;
        v.color = vertColor;
        v.uv0 = Vector2.zero;
        v.position = baseOffset + fanCenter;
        vh.AddVert(v);

        for (int i = 0; i < n; i++)
        {
            v.position = baseOffset + contour[i] + normals[i] * normalOffset;
            vh.AddVert(v);
        }

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            vh.AddTriangle(centerIdx, centerIdx + 1 + i, centerIdx + 1 + next);
        }
    }

    private void AddPerimeterStrip(
        VertexHelper vh,
        Vector2 baseOffset,
        float innerOffset,
        float outerOffset,
        Color32 innerColor,
        Color32 outerColor)
    {
        int n = contour.Count;
        int startIdx = vh.currentVertCount;

        UIVertex v = UIVertex.simpleVert;
        v.uv0 = Vector2.zero;

        for (int i = 0; i < n; i++)
        {
            v.color = innerColor;
            v.position = baseOffset + contour[i] + normals[i] * innerOffset;
            vh.AddVert(v);

            v.color = outerColor;
            v.position = baseOffset + contour[i] + normals[i] * outerOffset;
            vh.AddVert(v);
        }

        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            int i0 = startIdx + i * 2;
            int o0 = i0 + 1;
            int i1 = startIdx + next * 2;
            int o1 = i1 + 1;

            vh.AddTriangle(i0, o0, o1);
            vh.AddTriangle(i0, o1, i1);
        }
    }

    private static Color32 MultiplyColor(Color a, Color b)
    {
        return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
    }

    private static bool IsPointInPolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        int n = poly.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 pi = poly[i];
            Vector2 pj = poly[j];
            bool intersect = ((pi.y > p.y) != (pj.y > p.y)) &&
                             (p.x < (pj.x - pi.x) * (p.y - pi.y) / ((pj.y - pi.y) + 1e-6f) + pi.x);
            if (intersect) inside = !inside;
        }
        return inside;
    }
}
