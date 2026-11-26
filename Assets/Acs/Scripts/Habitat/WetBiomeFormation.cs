using System;
using System.Collections.Generic;
using System.Linq;

namespace LumiWorld.Acs
{
    public sealed class BiomeFormationCandidate
    {
        public string ExistingHabitatId { get; }
        public IReadOnlyList<string> MemberIds { get; }
        public string Key { get; }
        internal BiomeFormationCandidate(string existing, IEnumerable<string> ids, HabitatHexMember origin)
        {
            ExistingHabitatId = existing;
            var sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            MemberIds = Array.AsReadOnly(sorted);
            Key = origin.islandId + ":" + origin.natureCardId + ":" + origin.affinity + ":" +
                origin.elevationLevel + ":" + (existing ?? string.Empty) + ":" + string.Join("|", sorted);
        }
    }

    /// <summary>Candidate detection never forms a habitat. Approval must revalidate against live ownership.</summary>
    public static class WetBiomeFormation
    {
        public static bool AllowsElevation(BiomeAffinity affinity, int elevation)
        {
            switch (affinity)
            {
                case BiomeAffinity.Lilywater:
                case BiomeAffinity.Reedmarsh: return elevation == 0;
                case BiomeAffinity.Rainleaf: return elevation == 1 || elevation == 2;
                case BiomeAffinity.Streamstone: return elevation == 2;
                default: return false;
            }
        }

        public static List<BiomeFormationCandidate> FindCandidates(IEnumerable<HabitatHexMember> source,
            IReadOnlyList<HabitatState> existing, bool validateFootprintOnly = false)
        {
            var all = source.Where(m => m != null && m.island == HabitatIsland.Wet).ToList();
            var owners = new Dictionary<string, HabitatState>(StringComparer.Ordinal);
            foreach (var habitat in existing)
                if (habitat != null && habitat.confirmedFormation && habitat.island == HabitatIsland.Wet)
                    foreach (var member in habitat.members) owners[member.placementId] = habitat;
            var result = new List<BiomeFormationCandidate>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var island in all.GroupBy(m => m.islandId))
            {
                var byHex = island.ToDictionary(m => m.hex);
                foreach (var origin in island.OrderBy(m => m.hex.Q).ThenBy(m => m.hex.R))
                {
                    if (!AllowsElevation(origin.affinity, origin.elevationLevel)) continue;
                    for (int rows = 2; rows <= 4; rows++)
                        for (int rotation = 0; rotation < 6; rotation++)
                        {
                            var members = new List<HabitatHexMember>();
                            bool valid = true;
                            for (int q = 0; q < rows && valid; q++)
                                for (int r = 0; r < rows - q; r++)
                                {
                                    var hex = origin.hex + new HexCoordinates(q, r).Rotate60Clockwise(rotation);
                                    if (!byHex.TryGetValue(hex, out var m) || m.natureCardId != origin.natureCardId ||
                                        m.affinity != origin.affinity || m.elevationLevel != origin.elevationLevel)
                                    { valid = false; break; }
                                    members.Add(m);
                                }
                            if (!valid) continue;
                            HabitatState parent = null;
                            foreach (var m in members)
                                if (owners.TryGetValue(m.placementId, out var owner))
                                {
                                    if (parent != null && parent != owner) { valid = false; break; }
                                    parent = owner;
                                }
                            if (!valid) continue;
                            var ids = new HashSet<string>(members.Select(m => m.placementId), StringComparer.Ordinal);
                            if (parent != null && (parent.HexCount >= members.Count ||
                                parent.members.Any(m => !ids.Contains(m.placementId)))) continue;
                            if (!validateFootprintOnly && members.Count != (parent == null ? 3 : parent.HexCount == 3 ? 6 : 10)) continue;
                            var candidate = new BiomeFormationCandidate(parent?.habitatId, ids, origin);
                            if (seen.Add(candidate.Key)) result.Add(candidate);
                        }
                }
            }
            return result.OrderByDescending(c => c.ExistingHabitatId != null)
                .ThenByDescending(c => c.MemberIds.Count).ThenBy(c => c.Key, StringComparer.Ordinal).ToList();
        }

        public static bool IsValidFootprint(IReadOnlyList<HabitatHexMember> members)
        {
            if (members.Count != 3 && members.Count != 6 && members.Count != 10) return false;
            return FindCandidates(members, Array.Empty<HabitatState>(), true).Any(c => c.MemberIds.Count == members.Count);
        }

        public static bool TryConfirm(string candidateKey, IReadOnlyList<HabitatState> current,
            out List<HabitatState> confirmed)
        {
            confirmed = null;
            if (current == null || string.IsNullOrEmpty(candidateKey)) return false;
            var members = current.SelectMany(h => h.members).ToList();
            var candidate = FindCandidates(members, current).Find(c => c.Key == candidateKey);
            if (candidate == null) return false;
            var ids = new HashSet<string>(candidate.MemberIds, StringComparer.Ordinal);
            var selected = members.Where(m => ids.Contains(m.placementId)).ToList();
            var first = selected[0];
            var parent = current.FirstOrDefault(h => h.habitatId == candidate.ExistingHabitatId);
            var formation = new HabitatState { habitatId = parent?.habitatId ?? Guid.NewGuid().ToString("N"),
                islandId = first.islandId, natureCardId = first.natureCardId, affinity = first.affinity,
                island = first.island, members = selected, confirmedFormation = true,
                slots = parent?.slots ?? new List<HabitatResourceSlot>() };
            var previous = current.Where(h => !h.members.Any(m => ids.Contains(m.placementId))).ToList();
            previous.Add(formation);
            confirmed = HabitatTopologyService.Rebuild(members, previous);
            foreach (var habitat in confirmed)
                habitat.residents.AddRange(current.SelectMany(h => h.residents).Where(r => habitat.ContainsHex(r.homeHex)));
            return true;
        }

        internal static void RestoreConfirmed(List<HabitatHexMember> members, IReadOnlyList<HabitatState> previous,
            List<HabitatState> groups, HashSet<HabitatHexMember> visited)
        {
            if (previous == null) return;
            var byId = members.ToDictionary(m => m.placementId, StringComparer.Ordinal);
            foreach (var old in previous.OrderBy(h => h?.habitatId, StringComparer.Ordinal))
            {
                if (old == null || old.island != HabitatIsland.Wet || !old.confirmedFormation) continue;
                var restored = new List<HabitatHexMember>();
                foreach (var member in old.members)
                    if (byId.TryGetValue(member.placementId, out var live) && !visited.Contains(live) &&
                        live.islandId == old.islandId && live.natureCardId == old.natureCardId)
                        restored.Add(live);
                if (restored.Count != old.HexCount || !IsValidFootprint(restored)) continue;
                groups.Add(new HabitatState { habitatId = old.habitatId, islandId = old.islandId,
                    natureCardId = old.natureCardId, affinity = old.affinity, island = old.island,
                    confirmedFormation = true, members = restored });
                foreach (var member in restored) visited.Add(member);
            }
        }
    }
}
