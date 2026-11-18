using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs.Editor
{
    // 64-unit design grid, bold rounded strokes, exported as pure-white RGBA masks so UI.Image.color can tint to any color.
    internal static class LumiWorldGlyphDrawing
    {
        private struct Segment { public Vector2 a, b; public Segment(Vector2 a, Vector2 b) { this.a = a; this.b = b; } }

        public static Texture2D Draw(int id)
        {
            var lines = new List<Segment>();
            void P(params float[] p) { for (int i = 2; i < p.Length; i += 2) lines.Add(new Segment(new Vector2(p[i - 2], p[i - 1]), new Vector2(p[i], p[i + 1]))); }
            void Circle(float x, float y, float rx, float ry) { for (int i = 0; i < 32; i++) { float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16; P(x + rx * Mathf.Cos(a), y + ry * Mathf.Sin(a), x + rx * Mathf.Cos(b), y + ry * Mathf.Sin(b)); } }
            void Box(float x, float y, float w, float h) { P(x, y, x + w, y, x + w, y + h, x, y + h, x, y); }
            void Arrow(float x, float y, bool right) { float d = right ? 1 : -1; P(x - 12 * d, y, x + 10 * d, y, x + 3 * d, y - 7); P(x + 10 * d, y, x + 3 * d, y + 7); }
            void Paw() { Circle(32, 39, 10, 8); Circle(19, 28, 3, 5); Circle(27, 20, 3, 5); Circle(38, 20, 3, 5); Circle(46, 28, 3, 5); }

            switch (id)
            {
                case 3: Box(20, 17, 28, 34); P(14, 44, 10, 19, 36, 12); P(27, 28, 41, 28); P(27, 36, 37, 36); break;
                case 5: P(12, 27, 17, 15, 47, 15, 52, 27, 12, 27); P(16, 29, 16, 50, 48, 50, 48, 29); P(23, 50, 23, 37, 34, 37, 34, 50); P(22, 16, 20, 26); P(32, 16, 32, 26); P(42, 16, 44, 26); break;
                case 6: P(13, 36, 17, 49, 47, 49, 51, 36, 13, 36); P(23, 30, 20, 23); P(32, 28, 32, 19); P(41, 30, 44, 23); break;
                case 7: P(25, 13, 39, 13); P(28, 14, 28, 29, 17, 47, 19, 51, 45, 51, 47, 47, 36, 29, 36, 14); P(23, 39, 41, 39); break;
                case 8: P(23, 14, 41, 14, 41, 32, 36, 39, 28, 39, 23, 32, 23, 14); P(22, 19, 14, 19, 14, 29, 24, 34); P(42, 19, 50, 19, 50, 29, 40, 34); P(32, 39, 32, 50); P(23, 51, 41, 51); break;
                case 9: Circle(32, 32, 14, 14); Circle(32, 32, 5, 5); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; P(32 + 15 * Mathf.Cos(a), 32 + 15 * Mathf.Sin(a), 32 + 21 * Mathf.Cos(a), 32 + 21 * Mathf.Sin(a)); } break;
                case 10: Circle(32, 32, 20, 20); Circle(32, 32, 9, 20); P(13, 32, 51, 32); break;
                case 12: Paw(); break;
                case 13: case 30: P(12, 30, 32, 13, 52, 30); P(18, 28, 18, 51, 46, 51, 46, 28); P(27, 50, 27, 37, 37, 37, 37, 50); break;
                case 14: P(32, 12, 37, 27, 52, 32, 37, 37, 32, 52, 27, 37, 12, 32, 27, 27, 32, 12); break;
                case 20: Box(12, 12, 40, 40); Paw(); break;
                case 21: Box(12, 9, 40, 46); for (int i = 0; i < 3; i++) { Box(19, 18 + i * 12, 6, 6); P(31, 21 + i * 12, 45, 21 + i * 12); } P(18, 19, 22, 23, 28, 15); break;
                case 22: P(12, 36, 17, 49, 47, 49, 52, 36, 12, 36); Circle(27, 28, 4, 3); Circle(38, 27, 4, 3); break;
                case 23: Box(20, 20, 24, 30); Box(24, 12, 16, 8); P(26, 35, 38, 35); P(32, 29, 32, 41); break;
                case 24: P(13, 37, 13, 51, 51, 51, 51, 37); P(32, 12, 32, 40, 23, 31); P(32, 40, 41, 31); break;
                case 25: P(12, 16, 17, 16, 23, 42, 46, 42, 51, 24, 21, 24); Circle(27, 50, 2, 2); Circle(44, 50, 2, 2); P(34, 18, 34, 34, 28, 28); P(34, 34, 40, 28); break;
                case 26: P(13, 30, 32, 11, 51, 30, 32, 49, 13, 30); Circle(33, 21, 2, 2); P(22, 34, 36, 34); break;
                case 27: Box(17, 19, 30, 33); P(13, 19, 51, 19); P(25, 19, 25, 12, 39, 12, 39, 19); P(26, 29, 38, 41); P(38, 29, 26, 41); break;
                case 28: P(12, 26, 22, 26, 34, 16, 34, 48, 22, 38, 12, 38, 12, 26); P(42, 23, 47, 28, 47, 36, 42, 41); P(49, 16, 55, 25, 55, 39, 49, 48); break;
                case 29: Circle(32, 22, 9, 9); P(15, 51, 16, 44, 22, 37, 42, 37, 48, 44, 49, 51); break;
                case 31: P(29, 14, 14, 14, 14, 50, 29, 50); Arrow(39, 32, true); break;
                case 32: P(23, 14, 48, 32, 23, 50, 23, 14); break;
                case 33: Box(14, 14, 36, 36); P(22, 32, 42, 32); P(32, 22, 32, 42); break;
                case 34: P(12, 23, 32, 12, 52, 23, 52, 42, 32, 53, 12, 42, 12, 23, 32, 34, 52, 23); P(32, 34, 32, 53); break;
                case 100: P(18, 18, 46, 46); P(46, 18, 18, 46); break;
                case 101: Box(17, 28, 30, 24); P(23, 28, 23, 19, 27, 13, 37, 13, 41, 19, 41, 28); P(32, 37, 32, 44); break;
            }

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / 4f, (y + 0.5f) / 4f);
                    float distance = 100f;
                    foreach (var line in lines)
                    {
                        var v = line.b - line.a;
                        float t = Mathf.Clamp01(Vector2.Dot(p - line.a, v) / Mathf.Max(0.001f, v.sqrMagnitude));
                        distance = Mathf.Min(distance, Vector2.Distance(p, line.a + t * v));
                    }
                    byte alpha = (byte)(255f * Mathf.Clamp01((2.45f - distance) * 4f + 0.5f));
                    pixels[(size - 1 - y) * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }
    }
}
