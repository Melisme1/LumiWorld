namespace LumiWorld.Acs
{
    public enum BiomeAffinity
    {
        None = 0,
        Leafwood = 1,
        Bloomfield = 2,
        Windheath = 3,
        Reedmarsh = 4,
        Lilywater = 5,
        Streamstone = 6,
        Rainleaf = 7
    }

    public enum HabitatIsland { None = 0, Starter = 1, Wet = 2 }
    public enum AffinityRole { None = 0, Primary = 1, Secondary = 2 }
    public enum SpeciesIntegrationStatus { Active = 0, PendingData = 1, PendingModel = 2 }
    public enum ResourceTier { Tier1 = 1, Tier2 = 2 }

    public static class HabitatDataRules
    {
        public static HabitatIsland GetIsland(BiomeAffinity affinity)
        {
            switch (affinity)
            {
                case BiomeAffinity.Leafwood:
                case BiomeAffinity.Bloomfield:
                case BiomeAffinity.Windheath:
                    return HabitatIsland.Starter;
                case BiomeAffinity.Reedmarsh:
                case BiomeAffinity.Lilywater:
                case BiomeAffinity.Streamstone:
                case BiomeAffinity.Rainleaf:
                    return HabitatIsland.Wet;
                default:
                    return HabitatIsland.None;
            }
        }
    }
}
