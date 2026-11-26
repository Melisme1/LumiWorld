using System;

namespace LumiWorld.Acs
{
    // Approved GDD rules for known resources. Additional resources remain data-driven.
    // Extend this reference when more GDD rules are approved; it never creates assets or starts production.
    internal static class GddResourceDesignReference
    {
        internal sealed class SpeciesSpec
        {
            internal readonly string Key, Id;
            internal readonly BiomeAffinity Primary, Secondary;
            internal SpeciesSpec(string key, string id, BiomeAffinity primary, BiomeAffinity secondary)
            { Key = key; Id = id; Primary = primary; Secondary = secondary; }
        }

        internal sealed class ResourceSpec
        {
            internal readonly string Id;
            internal readonly BiomeAffinity Affinity;
            internal readonly ResourceTier Tier;
            // Primary/secondary lists are AND; all minimum counts are 1.
            internal readonly string[] Primary, Secondary;
            internal ResourceSpec(string biome, string item, BiomeAffinity affinity, ResourceTier tier, string primary, string secondary = "")
            {
                Id = "resource." + biome + "." + item; Affinity = affinity; Tier = tier;
                Primary = primary.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                Secondary = secondary.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        internal static readonly SpeciesSpec[] Species =
        {
            new SpeciesSpec("spriglide", "flyingsquirrel", BiomeAffinity.Leafwood, BiomeAffinity.Windheath),
            new SpeciesSpec("bramblet", "deer", BiomeAffinity.Leafwood, BiomeAffinity.Windheath),
            new SpeciesSpec("frondle", "grasshopper", BiomeAffinity.Leafwood, BiomeAffinity.Bloomfield),
            new SpeciesSpec("petaluff", "caterpillar", BiomeAffinity.Bloomfield, BiomeAffinity.Leafwood),
            new SpeciesSpec("dandrift", "flowerdog", BiomeAffinity.Bloomfield, BiomeAffinity.Windheath),
            new SpeciesSpec("galume", "peacock", BiomeAffinity.Windheath, BiomeAffinity.Bloomfield),
            new SpeciesSpec("heathorn", "highlandcow", BiomeAffinity.Windheath, BiomeAffinity.Bloomfield),
            new SpeciesSpec("reedle", "mudskipper", BiomeAffinity.Reedmarsh, BiomeAffinity.Streamstone),
            new SpeciesSpec("craggle", "mossycrab", BiomeAffinity.Reedmarsh, BiomeAffinity.Streamstone),
            new SpeciesSpec("lotelle", "lotusswan", BiomeAffinity.Lilywater, BiomeAffinity.Reedmarsh),
            new SpeciesSpec("iriwing", "dragonfly", BiomeAffinity.Lilywater, BiomeAffinity.Streamstone),
            new SpeciesSpec("rivlet", "otter", BiomeAffinity.Streamstone, BiomeAffinity.Lilywater),
            new SpeciesSpec("pebbara", "capybara", BiomeAffinity.Streamstone, BiomeAffinity.Reedmarsh),
            new SpeciesSpec("fernkin", "grassfox", BiomeAffinity.Rainleaf, BiomeAffinity.Streamstone),
            new SpeciesSpec("puddleaf", "blossomfrog", BiomeAffinity.Rainleaf, BiomeAffinity.Reedmarsh),
            new SpeciesSpec("heliko", "parrot", BiomeAffinity.Rainleaf, BiomeAffinity.Streamstone)
        };

        internal static readonly ResourceSpec[] Resources =
        {
            new ResourceSpec("leafwood", "oak_wood", BiomeAffinity.Leafwood, ResourceTier.Tier1, "spriglide"),
            new ResourceSpec("leafwood", "sticks", BiomeAffinity.Leafwood, ResourceTier.Tier1, "bramblet"),
            new ResourceSpec("leafwood", "tender_leaves", BiomeAffinity.Leafwood, ResourceTier.Tier1, "frondle"),
            new ResourceSpec("leafwood", "raspberries", BiomeAffinity.Leafwood, ResourceTier.Tier2, "bramblet", "petaluff"),
            new ResourceSpec("leafwood", "acorns", BiomeAffinity.Leafwood, ResourceTier.Tier2, "spriglide,frondle", "petaluff"),
            new ResourceSpec("bloomfield", "wild_daisies", BiomeAffinity.Bloomfield, ResourceTier.Tier1, "petaluff"),
            new ResourceSpec("bloomfield", "dandelion_flowers", BiomeAffinity.Bloomfield, ResourceTier.Tier1, "dandrift"),
            new ResourceSpec("bloomfield", "nectar", BiomeAffinity.Bloomfield, ResourceTier.Tier2, "petaluff", "galume"),
            new ResourceSpec("bloomfield", "wheat", BiomeAffinity.Bloomfield, ResourceTier.Tier2, "dandrift", "heathorn"),
            new ResourceSpec("bloomfield", "lavender_sprigs", BiomeAffinity.Bloomfield, ResourceTier.Tier2, "dandrift", "frondle,heathorn"),
            new ResourceSpec("windheath", "foxtail_plumes", BiomeAffinity.Windheath, ResourceTier.Tier1, "galume"),
            new ResourceSpec("windheath", "lichen", BiomeAffinity.Windheath, ResourceTier.Tier1, "heathorn"),
            new ResourceSpec("windheath", "rosemary", BiomeAffinity.Windheath, ResourceTier.Tier2, "galume", "dandrift"),
            new ResourceSpec("windheath", "juniper_berries", BiomeAffinity.Windheath, ResourceTier.Tier2, "heathorn", "spriglide,bramblet"),
            new ResourceSpec("windheath", "flax_stalks", BiomeAffinity.Windheath, ResourceTier.Tier2, "galume,heathorn", "dandrift"),
            new ResourceSpec("windheath", "mountain_blueberries", BiomeAffinity.Windheath, ResourceTier.Tier2, "heathorn", "dandrift,spriglide"),
            new ResourceSpec("reedmarsh", "reed_stalks", BiomeAffinity.Reedmarsh, ResourceTier.Tier1, "reedle", ""),
            new ResourceSpec("reedmarsh", "wet_clay", BiomeAffinity.Reedmarsh, ResourceTier.Tier1, "craggle", ""),
            new ResourceSpec("lilywater", "lotus_leaves", BiomeAffinity.Lilywater, ResourceTier.Tier1, "lotelle", ""),
            new ResourceSpec("lilywater", "water_mint", BiomeAffinity.Lilywater, ResourceTier.Tier1, "iriwing", ""),
            new ResourceSpec("streamstone", "stream_waterweed", BiomeAffinity.Streamstone, ResourceTier.Tier1, "rivlet", ""),
            new ResourceSpec("streamstone", "river_stones", BiomeAffinity.Streamstone, ResourceTier.Tier1, "pebbara", ""),
            new ResourceSpec("rainleaf", "fern_fronds", BiomeAffinity.Rainleaf, ResourceTier.Tier1, "fernkin", ""),
            new ResourceSpec("rainleaf", "pitcher_dew", BiomeAffinity.Rainleaf, ResourceTier.Tier1, "puddleaf", ""),
            new ResourceSpec("rainleaf", "hibiscus_blooms", BiomeAffinity.Rainleaf, ResourceTier.Tier1, "heliko", ""),
            new ResourceSpec("rainleaf", "tropical_fruit", BiomeAffinity.Rainleaf, ResourceTier.Tier2, "fernkin,heliko", ""),
            new ResourceSpec("rainleaf", "resin", BiomeAffinity.Rainleaf, ResourceTier.Tier2, "fernkin,puddleaf", ""),
            new ResourceSpec("rainleaf", "cacao_pods", BiomeAffinity.Rainleaf, ResourceTier.Tier2, "fernkin,puddleaf,heliko", ""),
            new ResourceSpec("lilywater", "water_lettuce", BiomeAffinity.Lilywater, ResourceTier.Tier2, "lotelle", "rivlet"),
            new ResourceSpec("lilywater", "water_caltrops", BiomeAffinity.Lilywater, ResourceTier.Tier2, "lotelle,iriwing", "rivlet"),
            new ResourceSpec("lilywater", "cattail", BiomeAffinity.Lilywater, ResourceTier.Tier2, "iriwing", "rivlet"),
            new ResourceSpec("streamstone", "sugarcane_stalks", BiomeAffinity.Streamstone, ResourceTier.Tier2, "rivlet", "iriwing,heliko"),
            new ResourceSpec("streamstone", "rattan_canes", BiomeAffinity.Streamstone, ResourceTier.Tier2, "rivlet", "fernkin,heliko"),
            new ResourceSpec("streamstone", "limestone", BiomeAffinity.Streamstone, ResourceTier.Tier2, "pebbara", "craggle"),
            new ResourceSpec("streamstone", "driftwood", BiomeAffinity.Streamstone, ResourceTier.Tier2, "pebbara", "reedle,fernkin"),
            new ResourceSpec("reedmarsh", "reed_plumes", BiomeAffinity.Reedmarsh, ResourceTier.Tier2, "reedle", "pebbara"),
            new ResourceSpec("reedmarsh", "water_chestnuts", BiomeAffinity.Reedmarsh, ResourceTier.Tier2, "craggle", "lotelle,puddleaf"),
            new ResourceSpec("streamstone", "spring_water", BiomeAffinity.Streamstone, ResourceTier.Tier2, "rivlet,pebbara", "")
        };
    }
}
