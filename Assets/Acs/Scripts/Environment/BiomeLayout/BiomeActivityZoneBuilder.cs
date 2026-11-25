using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    public sealed class BiomeZoneData
    {
        public readonly List<BiomeActivitySample> land = new List<BiomeActivitySample>();
        public readonly List<BiomeActivitySample> water = new List<BiomeActivitySample>();
        public readonly List<BiomeObstacleCircle> obstacles = new List<BiomeObstacleCircle>();
    }

    public static class BiomeActivityZoneBuilder
    {
        public static BiomeZoneData Build(BiomeLayoutProfile profile, DioramaPlan.Surface surface, List<DioramaPlan.Prop> props)
        {
            var result = new BiomeZoneData();
            foreach (DioramaPlan.Prop prop in props)
                if (prop.radius > 0)
                    result.obstacles.Add(new BiomeObstacleCircle
                    {
                        localXZ = new Vector2(prop.x, prop.z), topY = prop.y + prop.height,
                        radius = prop.radius, label = prop.role
                    });
            foreach (DioramaSurface.Spot spot in DioramaSurface.Spots(surface, props, profile.creatureAgentRadius,
                profile.walkableSampleSpacing, profile.swimmableSampleSpacing, profile.minSwimWaterDepthMeters, profile.dryCorridorWidth))
                (spot.wet ? result.water : result.land).Add(new BiomeActivitySample
                {
                    localPosition = new Vector3(spot.x, spot.y, spot.z), surfaceY = spot.y,
                    waterDepth = spot.depth, clearanceRadius = spot.clearance
                });
            return result;
        }

        public static void Apply(BiomeActivityZones zones, int tier, BiomeLayoutProfile profile, BiomeZoneData data)
        {
            zones.SetZoneData(tier, profile.surfaceSettings.tileHeight - profile.surfaceSettings.waterSurfaceDrop,
                data.land, data.water, data.obstacles);
        }
    }
}
