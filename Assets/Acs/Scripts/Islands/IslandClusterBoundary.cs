using System;
using System.Collections.Generic;

namespace LumiWorld.Acs.Islands
{
    /// <summary>Finite expansion lattice. Radius counts cluster steps, excluding the central cluster.</summary>
    public sealed class IslandClusterBoundary
    {
        private readonly HashSet<HexCoordinates> centers = new HashSet<HexCoordinates>();
        private readonly HashSet<HexCoordinates> land = new HashSet<HexCoordinates>();
        private readonly HashSet<HexCoordinates> margin = new HashSet<HexCoordinates>();

        public IslandClusterBoundary(HexCoordinates origin, int expansionRadius, int clusterHexRadius,
            IReadOnlyList<HexCoordinates> clusterNeighborOffsets, int emptyHexMargin = 1)
        {
            if (expansionRadius < 0) throw new ArgumentOutOfRangeException(nameof(expansionRadius));
            if (clusterHexRadius < 0) throw new ArgumentOutOfRangeException(nameof(clusterHexRadius));
            if (emptyHexMargin < 1) throw new ArgumentOutOfRangeException(nameof(emptyHexMargin));
            if (clusterNeighborOffsets == null || clusterNeighborOffsets.Count != 6)
                throw new ArgumentException("Supply the six offsets from the existing expansion lattice.");

            var frontier = new List<HexCoordinates> { origin };
            centers.Add(origin);
            for (int ring = 0; ring < expansionRadius; ring++)
            {
                var next = new List<HexCoordinates>();
                foreach (var center in frontier)
                    foreach (var offset in clusterNeighborOffsets)
                        if (centers.Add(center + offset)) next.Add(center + offset);
                frontier = next;
            }
            foreach (var center in centers)
                for (int q = -clusterHexRadius; q <= clusterHexRadius; q++)
                    for (int r = Math.Max(-clusterHexRadius, -q - clusterHexRadius);
                         r <= Math.Min(clusterHexRadius, -q + clusterHexRadius); r++)
                        land.Add(center + new HexCoordinates(q, r));

            var expanded = new HashSet<HexCoordinates>(land);
            frontier = new List<HexCoordinates>(land);
            for (int ring = 0; ring < emptyHexMargin; ring++)
            {
                var next = new List<HexCoordinates>();
                foreach (var hex in frontier)
                    for (int direction = 0; direction < 6; direction++)
                    {
                        var neighbor = hex.GetNeighbor(direction);
                        if (!expanded.Add(neighbor)) continue;
                        margin.Add(neighbor);
                        next.Add(neighbor);
                    }
                frontier = next;
            }
        }

        public int ClusterCapacity => centers.Count;
        public IEnumerable<HexCoordinates> LandHexes => land;
        public bool AllowsClusterCenter(HexCoordinates hex) => centers.Contains(hex);
        public bool AllowsLandHex(HexCoordinates hex) => land.Contains(hex);
        public bool IsEmptyMargin(HexCoordinates hex) => margin.Contains(hex);

        public bool Overlaps(IslandClusterBoundary other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
            // Both islands keep their own empty margin; even their margins cannot overlap.
            foreach (var hex in land)
                if (other.land.Contains(hex) || other.margin.Contains(hex)) return true;
            foreach (var hex in margin)
                if (other.land.Contains(hex) || other.margin.Contains(hex)) return true;
            return false;
        }
    }
}
