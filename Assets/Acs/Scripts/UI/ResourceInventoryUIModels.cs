using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    // Presentation values only. Views do not read Inventory, accrue, or mutate habitat selections.
    public sealed class ResourceUIItem
    {
        public string id, name, biome, tier, description;
        public Sprite icon;
        public int stock, capacity;
    }
    public sealed class ResourceUISlot
    {
        public ResourceTier tier;
        public int index;
        public bool unlocked;
        public string name, reason, resourceId;
        public Sprite icon;
        public double rate;
    }
    public sealed class ResourceUIBuffer
    {
        public string habitatId, resourceId, name;
        public Sprite icon;
        public long amount;
        public int cap;
        public bool recovery;
    }
    public sealed class ResourceUIHabitat
    {
        public string id, title, residents, hint, structureKey;
        public int hexCount, residentCount, capacity;
        public List<ResourceUISlot> slots = new List<ResourceUISlot>();
        public List<ResourceUIBuffer> buffers = new List<ResourceUIBuffer>();
    }
    public sealed class ResourceUIOption
    {
        public string id, name, requirements, reason;
        public Sprite icon;
        public bool eligible;
    }

    public static class ResourceInventoryUILayout
    {
        public static float FitScale(float width, float height) => Math.Min(width / 1280f, height / 720f);
        public static float PanelHeight(float height) => Math.Max(200f, height - 110f - 168f);
        public static int UnlockHexCount(ResourceTier tier, int index) => tier == ResourceTier.Tier2 ? 6 : index == 0 ? 3 : 4;
    }
}
