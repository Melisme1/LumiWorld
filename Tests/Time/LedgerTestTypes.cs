// Minimal habitat inputs for compiling the REAL ProductionLedger in the standalone .NET harness.
// These types are outside Assets and never included in a Unity player build.
using System.Collections.Generic;
namespace LumiWorld.Acs
{
    public enum ResourceTier { Tier1, Tier2 }
    public sealed class HabitatHexMember { public string placementId; }
    public sealed class HabitatState
    {
        public string habitatId, islandId, natureCardId;
        public List<HabitatHexMember> members = new List<HabitatHexMember>();
    }
}
