using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LumiWorld.Acs
{
    /// <summary>
    /// Resolution-independent vector-mesh capsule / rounded-rect UI graphic (rectangular body + true circular-arc end-caps).
    /// Eliminates 9-slice sprite stretching/scratching at any aspect ratio or scale.
    /// Optionally includes a sculpted bottom-center chevron dome for top-docked collapsible HUD trays
    /// (symmetric counterpart to P-03 CardTrayShapeGraphic), with exact silhouette raycast filtering.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedCapsuleGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        [Header("Body Geometry")]
        [SerializeField] private float cornerRadius = 18f;
        [SerializeField] private bool fullCapsuleEnds = true;

        [Header("Optional Bottom Chevron Dome (Top-Docked Collapsible HUD)")]
        [SerializeField] private bool includeBottomDome = false;
        [SerializeField] private float bodyHeight = 46f;
        [SerializeField] private float domeHalfWidth = 24f;
        [SerializeField] private float domeHeight = 18f;
        [SerializeField] private float domeBaseFilletRadius = 8f;
        [SerializeField] private float domeBottomCornerRadius = 9f;

        [Header("Warm Stone Styling")]
        [SerializeField] private Color fillColor = new Color(0.937f, 0.918f, 0.886f, 1f);
        [SerializeField] private Color borderColor = new Color(0.835f, 0.808f, 0.765f, 1f);
        [SerializeField] private float borderThickness = 1.4f;
        [SerializeField] private float antiAliasWidth = 1.2f;

        [Header("Soft Drop Shadow")]
        [SerializeField] private Color shadowColor = new Color(0.145f, 0.106f, 0.071f, 0.16f);
        [SerializeField] private Vector2 shadowOffset = new Vector2(0f, -3.5f);
        [SerializeField] private float shadowSpread = 6f;

        [Header("Tessellation")]
        [SerializeField] private int arcSegments = 12;

        private readonly List<Vector2> contour = new List<Vector2>(128);
        private readonly List<Vector2> normals = new List<Vector2>(128);

        public Color FillColor
        {
            get => fillColor;
            set
            {
                if (fillColor == value) return;
                fillColor = value;
                SetVerticesDirty();
            }
        }

        public Color BorderColor
        {
            get => borderColor;
            set
            {
                if (borderColor == value) return;
                borderColor = value;
                SetVerticesDirty();
            }
        }

        public void ConfigurePill(
            Color fill,
            Color border,
            float radius = 18f,
            bool fullCapsule = true,
            float borderWidth = 1.4f,
            float shadowAlpha = 0.14f,
            float shadowDepth = 3.5f,
            float shadowBlur = 6f)
        {
            fillColor = fill;
            borderColor = border;
            cornerRadius = radius;
            fullCapsuleEnds = fullCapsule;
            borderThickness = borderWidth;
            includeBottomDome = false;
            shadowColor = new Color(0.145f, 0.106f, 0.071f, shadowAlpha);
            shadowOffset = new Vector2(0f, -shadowDepth);
            shadowSpread = shadowBlur;
            SetVerticesDirty();
        }

        public void ConfigureTopCollapsibleTray(
            bool expandedWithDome,
            float trayBodyHeight,
            float radius = 22f,
            float newDomeHalfWidth = 24f,
            float newDomeHeight = 18f,
            float newDomeBaseFillet = 8f,
            float newDomeBottomRadius = 9f)
        {
            includeBottomDome = expandedWithDome;
            bodyHeight = trayBodyHeight;
            cornerRadius = radius;
            fullCapsuleEnds = !expandedWithDome;
            domeHalfWidth = newDomeHalfWidth;
            domeHeight = newDomeHeight;
            domeBaseFilletRadius = newDomeBaseFillet;
            domeBottomCornerRadius = newDomeBottomRadius;
            SetVerticesDirty();
        }

        public void ConfigureStyle(Color fill, Color border, Color shadow, float borderWidth = 1.4f)
        {
            fillColor = fill;
            borderColor = border;
            shadowColor = shadow;
            borderThickness = borderWidth;
            SetVerticesDirty();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!isActiveAndEnabled || !raycastTarget) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 local))
                return false;

            if (contour.Count < 3) RebuildContour(out _);
            return IsPointInPolygon(local, contour);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            RebuildContour(out Vector2 fanCenter);
            if (contour.Count < 3) return;

            ComputeOutwardNormals();

            float alphaMul = color.a;
            if (shadowColor.a > 0.001f && alphaMul > 0.001f)
            {
                AddFeatheredShadow(vh, fanCenter, alphaMul);
            }

            float inset = Mathf.Max(0f, borderThickness);
            Color32 fill32 = MultiplyColor(fillColor, color);
            AddFillFan(vh, fanCenter, Vector2.zero, -inset, fill32);

            Color32 border32 = MultiplyColor(borderColor, color);
            if (inset > 0.05f)
            {
                AddPerimeterStrip(vh, Vector2.zero, -inset, 0f, border32, border32);
            }

            if (antiAliasWidth > 0.05f)
            {
                Color32 clearBorder = border32;
                clearBorder.a = 0;
                AddPerimeterStrip(vh, Vector2.zero, 0f, antiAliasWidth, border32, clearBorder);
            }
        }

        private void RebuildContour(out Vector2 fanCenter)
        {
            contour.Clear();
            Rect r = rectTransform.rect;
            float w = Mathf.Max(8f, r.width);
            float h = Mathf.Max(8f, r.height);
            int seg = Mathf.Clamp(arcSegments, 6, 24);

            float cx = r.center.x;
            float halfW = w * 0.5f;

            if (!includeBottomDome)
            {
                float rad = fullCapsuleEnds
                    ? Mathf.Min(h * 0.5f, halfW - 0.5f)
                    : Mathf.Clamp(cornerRadius, 2f, Mathf.Min(h * 0.5f, halfW - 0.5f));

                float left = r.xMin + rad;
                float right = r.xMax - rad;
                float bottom = r.yMin + rad;
                float top = r.yMax - rad;

                // True vector capsule/rounded rect: rectangular center + 2 semicircular end-caps (CCW)
                AddArc(new Vector2(right, bottom), rad, -90f, 0f, seg);
                AddArc(new Vector2(right, top), rad, 0f, 90f, seg);
                AddArc(new Vector2(left, top), rad, 90f, 180f, seg);
                AddArc(new Vector2(left, bottom), rad, 180f, 270f, seg);

                fanCenter = r.center;
                return;
            }

            // Top-docked collapsible monolith: rounded main bar on top + sculpted bottom-center chevron dome
            float topY = r.yMax;
            float mainH = Mathf.Clamp(bodyHeight, 24f, h - 4f);
            float barBottomY = topY - mainH;
            float radBar = Mathf.Clamp(cornerRadius, 4f, Mathf.Min(mainH * 0.5f, halfW * 0.45f));

            float availDomeH = Mathf.Max(0f, barBottomY - r.yMin);
            float flatBottomHalf = halfW - radBar;
            bool buildDome = availDomeH > 2f && flatBottomHalf > domeHalfWidth + domeBaseFilletRadius + 6f;

            // 1. Top-right convex corner (0 -> 90)
            AddArc(new Vector2(cx + halfW - radBar, topY - radBar), radBar, 0f, 90f, seg);
            // 2. Top-left convex corner (90 -> 180)
            AddArc(new Vector2(cx - halfW + radBar, topY - radBar), radBar, 90f, 180f, seg);
            // 3. Bottom-left bar convex corner (180 -> 270)
            AddArc(new Vector2(cx - halfW + radBar, barBottomY + radBar), radBar, 180f, 270f, seg);

            // 4. Sculpted bottom-center chevron dome (left-to-right in CCW order)
            if (buildDome)
            {
                float dH = Mathf.Min(domeHeight, availDomeH);
                float rBase = Mathf.Clamp(domeBaseFilletRadius, 2f, dH * 0.5f);
                float rBot = Mathf.Clamp(domeBottomCornerRadius, 2f, Mathf.Min(dH - rBase, domeHalfWidth - 1f));
                float domeBotY = barBottomY - dH;

                // Left concave base fillet (90 -> 0, curving down-right into left dome wall)
                AddArc(new Vector2(cx - domeHalfWidth - rBase, barBottomY - rBase), rBase, 90f, 0f, seg);
                // Left convex dome bottom corner (180 -> 270)
                AddArc(new Vector2(cx - domeHalfWidth + rBot, domeBotY + rBot), rBot, 180f, 270f, seg);
                // Right convex dome bottom corner (270 -> 360)
                AddArc(new Vector2(cx + domeHalfWidth - rBot, domeBotY + rBot), rBot, -90f, 0f, seg);
                // Right concave base fillet (180 -> 90, curving up-right back onto bar bottom)
                AddArc(new Vector2(cx + domeHalfWidth + rBase, barBottomY - rBase), rBase, 180f, 90f, seg);
            }

            // 5. Bottom-right bar convex corner (270 -> 360)
            AddArc(new Vector2(cx + halfW - radBar, barBottomY + radBar), radBar, -90f, 0f, seg);

            fanCenter = new Vector2(cx, topY - mainH * 0.5f);
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
                AddPointIfDistinct(new Vector2(center.x + Mathf.Cos(rad) * radius, center.y + Mathf.Sin(rad) * radius));
            }
        }

        private void AddPointIfDistinct(Vector2 pt)
        {
            if (contour.Count > 0 && (contour[contour.Count - 1] - pt).sqrMagnitude < 0.01f) return;
            if (contour.Count > 2 && (contour[0] - pt).sqrMagnitude < 0.01f) return;
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
                    normals.Add(miter * Mathf.Min(1.35f, 1f / denom));
                }
            }
        }

        private void AddFeatheredShadow(VertexHelper vh, Vector2 fanCenter, float alphaMul)
        {
            float spread = Mathf.Max(2f, shadowSpread);
            Color32 c0 = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * alphaMul);
            Color32 c1 = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * 0.50f * alphaMul);
            Color32 c2 = new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a * 0.15f * alphaMul);
            Color32 c3 = new Color(shadowColor.r, shadowColor.g, shadowColor.b, 0f);

            AddFillFan(vh, fanCenter, shadowOffset, 0f, c0);
            AddPerimeterStrip(vh, shadowOffset, 0f, spread * 0.30f, c0, c1);
            AddPerimeterStrip(vh, shadowOffset, spread * 0.30f, spread * 0.65f, c1, c2);
            AddPerimeterStrip(vh, shadowOffset, spread * 0.65f, spread, c2, c3);
        }

        private void AddFillFan(VertexHelper vh, Vector2 fanCenter, Vector2 offset, float normalOffset, Color32 vertColor)
        {
            int n = contour.Count;
            int centerIdx = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = vertColor;
            v.uv0 = Vector2.zero;
            v.position = offset + fanCenter;
            vh.AddVert(v);

            for (int i = 0; i < n; i++)
            {
                v.position = offset + contour[i] + normals[i] * normalOffset;
                vh.AddVert(v);
            }

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                vh.AddTriangle(centerIdx, centerIdx + 1 + i, centerIdx + 1 + next);
            }
        }

        private void AddPerimeterStrip(VertexHelper vh, Vector2 offset, float innerOffset, float outerOffset, Color32 innerColor, Color32 outerColor)
        {
            int n = contour.Count;
            int startIdx = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.uv0 = Vector2.zero;

            for (int i = 0; i < n; i++)
            {
                v.color = innerColor;
                v.position = offset + contour[i] + normals[i] * innerOffset;
                vh.AddVert(v);

                v.color = outerColor;
                v.position = offset + contour[i] + normals[i] * outerOffset;
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

        private static Color32 MultiplyColor(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

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
}
