using UnityEngine;

namespace LumiWorld.Acs
{
    [CreateAssetMenu(fileName = "NatureHabitat", menuName = "LumiWorld/Resources/Nature Habitat Definition")]
    public sealed class NatureHabitatDefinition : ScriptableObject
    {
        [Tooltip("CardData Nature hiện có; lấy đúng cardID, không nhóm theo tên hoặc family.")]
        public CardData natureCard;
        public BiomeAffinity affinity;
        public HabitatIsland island;

        public string NatureCardId => natureCard != null ? natureCard.cardID : string.Empty;
    }
}
