using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    public enum HabitatGround { Land, Bank, Water, Either }
    public enum DioramaFacing { Random, Waterward, Outward, Authored }
    [Serializable] public struct CompositionPoint { public float u, v; }

    [Serializable]
    public class DioramaRule
    {
        public string roleId;
        public int count1, count3, count6, count10;
        public HabitatGround ground;
        public CompositionPoint[] guides;
        public string[] clusterAroundParentRole;
        public float radiusMin, radiusMax, spread, spacing, obstacleRadius, yOffset;
        public float scaleMin = 1, scaleMax = 1, tiltMin, tiltMax, yaw;
        public bool waterHeight;
        public DioramaFacing facing;
        public float crossingLength, crossingPivotToCenter;
        public float allowedVisualOverlap = .35f;
        public float solidFootprintFraction = .7f;
        public bool preserveAuthoredPivot;
        public float parentHeightFraction;
        public bool hangFromParent;

        public DioramaRule CopyForCount(int count)
        {
            var copy = (DioramaRule)MemberwiseClone();
            copy.count1 = copy.count3 = copy.count6 = copy.count10 = count;
            return copy;
        }
    }

    /// <summary>Pure placement logic. Coordinates and dimensions are habitat-local metres.</summary>
    public static class DioramaPlan
    {
        public struct Cell
        {
            public float x, z, groundY, waterY, shore, edge, waterAngle;
            public bool wet;
        }
        public struct Variant
        {
            public int sourceIndex;
            public float height, footprint, scaleMin, scaleMax;
            public bool measured;
        }
        public class Prop
        {
            public string role;
            public int slot, variant;
            public float x, y, z, yaw, tilt, scale, radius, visualRadius, height;
        }
        public struct Surface
        {
            public List<Cell> cells;
            public float minX, minZ, width, length;
        }

        // Compatibility for callers that deliberately use authored sizes.
        public static List<Prop> Place(Surface surface, DioramaRule[] rules, int[] poolSizes,
            int tier, int seed, List<Prop> locked)
        {
            var variants = new Variant[rules.Length][];
            for (int i = 0; i < rules.Length; i++)
            {
                variants[i] = new Variant[poolSizes[i]];
                for (int j = 0; j < poolSizes[i]; j++)
                    variants[i][j] = new Variant { sourceIndex = j, scaleMin = rules[i].scaleMin, scaleMax = rules[i].scaleMax };
            }
            return Place(surface, rules, variants, tier, seed, locked);
        }

        public static List<Prop> Place(Surface surface, DioramaRule[] rules, Variant[][] variants,
            int tier, int seed, List<Prop> locked)
        {
            if (tier != 1 && tier != 3 && tier != 6 && tier != 10) throw new ArgumentOutOfRangeException(nameof(tier));
            var result = new List<Prop>(locked);
            var roles = new HashSet<string>();
            for (int layerIndex = 0; layerIndex < rules.Length; layerIndex++)
            {
                DioramaRule rule = rules[layerIndex];
                if (string.IsNullOrEmpty(rule.roleId) || !roles.Add(rule.roleId))
                    throw new InvalidOperationException("Each layer needs a unique role ID.");
                int count = tier == 1 ? rule.count1 : tier == 3 ? rule.count3 : tier == 6 ? rule.count6 : rule.count10;
                if (count == 0) continue;
                if (variants[layerIndex].Length == 0) throw new InvalidOperationException("No suitable asset for " + rule.roleId);
                List<Prop> parents = result.FindAll(p => Array.IndexOf(rule.clusterAroundParentRole ?? Array.Empty<string>(), p.role) >= 0);
                bool satellites = rule.clusterAroundParentRole != null && rule.clusterAroundParentRole.Length > 0;
                if (satellites && parents.Count == 0)
                    throw new InvalidOperationException("Enable a parent layer or disable its satellites: " + rule.roleId);
                for (int slot = 0; slot < count; slot++)
                {
                    if (result.Exists(p => p.role == rule.roleId && p.slot == slot)) continue;
                    var random = new Random(StableSeed(seed, rule.roleId, slot));
                    Variant variant = variants[layerIndex][random.Next(variants[layerIndex].Length)];
                    float scale = Between(random, variant.scaleMin, variant.scaleMax);
                    Prop parent = satellites ? parents[slot % parents.Count] : null;
                    Target(surface, rule, slot, count, seed, random, parents.Count, parent, out double targetX, out double targetZ);
                    float visualRadius = variant.footprint * scale * .5f;
                    float radius = variant.measured && rule.obstacleRadius > 0
                        ? visualRadius * rule.solidFootprintFraction : rule.obstacleRadius;
                    Cell cell = FindCell(surface, rule, parent, result, targetX, targetZ, radius, visualRadius);
                    var prop = new Prop
                    {
                        role = rule.roleId, slot = slot, variant = variant.sourceIndex,
                        x = cell.x, z = cell.z, y = (rule.waterHeight ? cell.waterY : cell.groundY) + rule.yOffset,
                        yaw = Facing(surface, rule, cell, random), tilt = Between(random, rule.tiltMin, rule.tiltMax),
                        scale = scale, radius = radius, visualRadius = visualRadius, height = variant.height * scale
                    };
                    if (parent != null && rule.parentHeightFraction > 0)
                        prop.y = parent.y + parent.height * rule.parentHeightFraction + rule.yOffset - (rule.hangFromParent ? prop.height : 0);
                    if (rule.crossingLength > 0)
                    {
                        SpanBanks(surface, cell, rule, prop);
                        if (variant.measured && (prop.scale < variant.scaleMin || prop.scale > variant.scaleMax))
                            throw new InvalidOperationException($"Bridge span {prop.scale * rule.crossingLength:F2} m exceeds size limits ({variant.scaleMin * rule.crossingLength:F2}–{variant.scaleMax * rule.crossingLength:F2} m): " + rule.roleId);
                        prop.visualRadius = variant.footprint * prop.scale * .5f;
                        prop.height = variant.height * prop.scale;
                        prop.radius = 0;
                    }
                    result.Add(prop);
                }
            }
            return result;
        }

        private static void Target(Surface surface, DioramaRule rule, int slot, int count, int seed,
            Random random, int parentCount, Prop parent, out double x, out double z)
        {
            if (parent != null)
            {
                double angle = (slot % parentCount) * 2.39996 + Math.Sin(seed * .35 + slot / parentCount * 1.91) * 1.34;
                double distance = Between(random, rule.radiusMin, rule.radiusMax);
                x = parent.x + Math.Cos(angle) * distance;
                z = parent.z + Math.Sin(angle) * distance;
                return;
            }
            if (rule.guides == null || rule.guides.Length == 0) throw new InvalidOperationException("Missing guides: " + rule.roleId);
            CompositionPoint guide = rule.guides[(slot * rule.guides.Length / count) % rule.guides.Length];
            x = surface.minX + guide.u * surface.width + Between(random, -rule.spread, rule.spread);
            z = surface.minZ + guide.v * surface.length + Between(random, -rule.spread, rule.spread);
        }

        private static Cell FindCell(Surface surface, DioramaRule rule, Prop parent, List<Prop> placed,
            double targetX, double targetZ, float radius, float visualRadius)
        {
            int best = -1;
            double bestCost = double.MaxValue;
            for (int index = 0; index < surface.cells.Count; index++)
            {
                Cell cell = surface.cells[index];
                if (cell.edge < Math.Max(.11f, radius * .7f) ||
                    (rule.ground == HabitatGround.Water && (!cell.wet || (rule.crossingLength <= 0 && cell.shore < visualRadius * .65f))) ||
                    ((rule.ground == HabitatGround.Land || rule.ground == HabitatGround.Bank) && cell.wet) ||
                    (rule.ground == HabitatGround.Bank && cell.shore > .27f)) continue;
                if (parent != null)
                {
                    double distance = Dist(cell.x, cell.z, parent.x, parent.z);
                    if (distance + .00001 < rule.radiusMin || distance > rule.radiusMax + .00001) continue;
                }
                double cost = (targetX - cell.x) * (targetX - cell.x) + (targetZ - cell.z) * (targetZ - cell.z);
                bool blocked = false;
                foreach (Prop other in placed)
                {
                    double distance = Dist(cell.x, cell.z, other.x, other.z);
                    double clearance = other.role == rule.roleId ? rule.spacing : .095;
                    if (other != parent && radius > 0 && other.radius > 0)
                        clearance = Math.Max(clearance, radius + other.radius);
                    if (distance < clearance) { blocked = true; break; }
                    if (other != parent && other.role == rule.roleId)
                    {
                        double overlap = (visualRadius + other.visualRadius) * (1 - rule.allowedVisualOverlap) - distance;
                        if (overlap > 0) cost += overlap * overlap * 12;
                    }
                }
                if (!blocked && cost < bestCost) { best = index; bestCost = cost; }
            }
            if (best < 0) throw new InvalidOperationException("No space for " + rule.roleId + ". Reduce count/size or adjust guides.");
            return surface.cells[best];
        }

        private static float Facing(Surface surface, DioramaRule rule, Cell cell, Random random)
        {
            switch (rule.facing)
            {
                case DioramaFacing.Waterward: return cell.waterAngle;
                case DioramaFacing.Outward:
                    return (float)(Math.Atan2(-(cell.z - surface.minZ - surface.length * .5f),
                        cell.x - surface.minX - surface.width * .5f) * 180 / Math.PI);
                case DioramaFacing.Authored: return rule.yaw;
                default: return Between(random, 0, 360);
            }
        }

        private static void SpanBanks(Surface surface, Cell center, DioramaRule rule, Prop prop)
        {
            Cell? left = null, right = null;
            foreach (Cell cell in surface.cells)
            {
                if (cell.wet || cell.shore < .12f || Math.Abs(cell.z - center.z) > .01f) continue;
                if (cell.x < center.x && (!left.HasValue || cell.x > left.Value.x)) left = cell;
                if (cell.x > center.x && (!right.HasValue || cell.x < right.Value.x)) right = cell;
            }
            if (!left.HasValue || !right.HasValue) throw new InvalidOperationException("Bridge needs two dry banks: " + rule.roleId);
            prop.yaw = 0;
            prop.scale = (right.Value.x - left.Value.x) / rule.crossingLength;
            prop.x = (left.Value.x + right.Value.x) * .5f;
            prop.y = Math.Max(left.Value.groundY, right.Value.groundY) + rule.yOffset;
        }

        private static int StableSeed(int seed, string role, int slot)
        {
            unchecked
            {
                int hash = seed * 397 ^ slot;
                foreach (char character in role) hash = hash * 31 + character;
                return hash;
            }
        }
        private static float Between(Random random, float min, float max) => (float)(min + (max - min) * random.NextDouble());
        internal static double Dist(float x, float z, float a, float b) => Math.Sqrt((x - a) * (x - a) + (z - b) * (z - b));
    }
}
