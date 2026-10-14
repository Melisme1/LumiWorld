using UnityEngine;

namespace LumiWorld.Acs
{
    // Read model for the future avatar grid/detail panel, never authoritative state.
    public sealed class CreatureResidentView
    {
        public string HabitatId { get; internal set; }
        public string IndividualId { get; internal set; }
        public string SpeciesId { get; internal set; }
        public string Name { get; internal set; }
        public Sprite Avatar { get; internal set; }
        public AffinityRole Role { get; internal set; }
        public CreatureCareView Care { get; internal set; }
        public bool IsWorking { get; internal set; }
    }
}
