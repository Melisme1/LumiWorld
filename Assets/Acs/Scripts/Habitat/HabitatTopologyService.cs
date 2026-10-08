using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    // Pure topology: no model positions, production clocks, scene mutations or save writes.
    public static class HabitatTopologyService
    {
        public const int MaximumHexCount = 6;

        private sealed class Match
        {
            public int group;
            public HabitatState previous;
            public int overlap;
        }

        public static List<HabitatState> Rebuild(IEnumerable<HabitatHexMember> input,
            IReadOnlyList<HabitatState> previous)
        {
            var members = new List<HabitatHexMember>();
            var placementIds = new HashSet<string>(StringComparer.Ordinal);
            if (input != null)
                foreach (var member in input)
                    if (member != null)
                    {
                        if (string.IsNullOrEmpty(member.placementId) || !placementIds.Add(member.placementId) ||
                            string.IsNullOrEmpty(member.islandId) || string.IsNullOrEmpty(member.natureCardId))
                            throw new ArgumentException("Habitat member thiếu/trùng stable ID hoặc thiếu Nature/island ID.");
                        members.Add(member);
                    }
            members.Sort(CompareMembers);

            var byHex = new Dictionary<string, Dictionary<HexCoordinates, HabitatHexMember>>(StringComparer.Ordinal);
            foreach (var member in members)
            {
                string key = member.islandId ?? string.Empty;
                if (!byHex.TryGetValue(key, out var hexes)) byHex[key] = hexes = new Dictionary<HexCoordinates, HabitatHexMember>();
                if (hexes.ContainsKey(member.hex)) throw new ArgumentException("Hai Nature cards cùng home hex: " + member.hex);
                hexes.Add(member.hex, member);
            }

            var groups = new List<HabitatState>();
            var visited = new HashSet<HabitatHexMember>();
            foreach (var seed in members)
            {
                if (!visited.Add(seed)) continue;
                var group = new HabitatState { islandId = seed.islandId, natureCardId = seed.natureCardId,
                    affinity = seed.affinity, island = seed.island };
                var queue = new Queue<HabitatHexMember>();
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    group.members.Add(current);
                    for (int direction = 0; direction < 6; direction++)
                    {
                        if (group.members.Count + queue.Count >= MaximumHexCount) break;
                        if (!byHex[seed.islandId ?? string.Empty].TryGetValue(current.hex.GetNeighbor(direction), out var neighbor) ||
                            visited.Contains(neighbor) || !CanConnect(seed, neighbor)) continue;
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
                group.members.Sort(CompareMembers);
                groups.Add(group);
            }

            // Largest shared membership retains identity. Ties use old identity and sorted topology.
            // This preserves selections across refresh, growth, recall and the survivor of a merge/split.
            var matches = new List<Match>();
            if (previous != null)
                foreach (var old in previous)
                {
                    if (old == null || old.members == null || string.IsNullOrEmpty(old.habitatId)) continue;
                    var oldIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var member in old.members) if (member != null) oldIds.Add(member.placementId);
                    for (int i = 0; i < groups.Count; i++)
                    {
                        var group = groups[i];
                        if (old.islandId != group.islandId || old.natureCardId != group.natureCardId ||
                            old.affinity != group.affinity || old.island != group.island) continue;
                        int overlap = 0;
                        foreach (var member in group.members) if (oldIds.Contains(member.placementId)) overlap++;
                        if (overlap > 0) matches.Add(new Match { group = i, previous = old, overlap = overlap });
                    }
                }
            matches.Sort((a, b) =>
            {
                int order = b.overlap.CompareTo(a.overlap);
                if (order == 0) order = string.CompareOrdinal(a.previous.habitatId, b.previous.habitatId);
                return order == 0 ? a.group.CompareTo(b.group) : order;
            });
            var assignedGroups = new HashSet<int>();
            var assignedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var match in matches)
            {
                if (assignedGroups.Contains(match.group) || assignedIds.Contains(match.previous.habitatId)) continue;
                assignedGroups.Add(match.group);
                assignedIds.Add(match.previous.habitatId);
                var group = groups[match.group];
                group.habitatId = match.previous.habitatId;
                if (match.previous.slots != null)
                    foreach (var slot in match.previous.slots)
                        if (slot != null) group.slots.Add(new HabitatResourceSlot { tier = slot.tier, index = slot.index,
                            selectedResourceId = slot.selectedResourceId });
            }
            foreach (var group in groups)
            {
                if (string.IsNullOrEmpty(group.habitatId)) group.habitatId = Guid.NewGuid().ToString("N");
                group.EnsureSlots();
            }
            return groups;
        }

        private static bool CanConnect(HabitatHexMember a, HabitatHexMember b)
        {
            return !string.IsNullOrEmpty(a.natureCardId) && a.natureCardId == b.natureCardId &&
                a.islandId == b.islandId && a.affinity == b.affinity && a.island == b.island &&
                ((a.connectionClusterId <= 0 && b.connectionClusterId <= 0) ||
                 (a.connectionClusterId > 0 && a.connectionClusterId == b.connectionClusterId));
        }

        private static int CompareMembers(HabitatHexMember a, HabitatHexMember b)
        {
            int order = string.CompareOrdinal(a.islandId, b.islandId);
            if (order == 0) order = a.hex.Q.CompareTo(b.hex.Q);
            if (order == 0) order = a.hex.R.CompareTo(b.hex.R);
            return order == 0 ? string.CompareOrdinal(a.placementId, b.placementId) : order;
        }
    }
}
