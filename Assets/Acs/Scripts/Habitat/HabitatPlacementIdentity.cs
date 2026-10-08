using System;
using UnityEngine;

namespace LumiWorld.Acs
{
    [DisallowMultipleComponent]
    public sealed class HabitatPlacementIdentity : MonoBehaviour
    {
        [SerializeField] private string placementId;
        [SerializeField] private HexCoordinates homeHex;
        [SerializeField] private bool hasHomeHex;
        [Tooltip("Rank GDD dùng cho production mới. Không lấy từ Star Level của AnimalIndividual.")]
        [SerializeField] private CreatureQualityRank qualityRank = CreatureQualityRank.RankI;

        public string PlacementId => placementId;
        public HexCoordinates HomeHex => homeHex;
        public CreatureQualityRank QualityRank => qualityRank;

        public bool TrySetQualityRank(CreatureQualityRank value)
        {
            if ((int)value < (int)CreatureQualityRank.RankI || (int)value > (int)CreatureQualityRank.RankIII) return false;
            HabitatRuntimeManager.PrepareForWorldChange();
            qualityRank = value;
            HabitatRuntimeManager.NotifyWorldChanged();
            if (Application.isPlaying && HabitatRuntimeManager.TryGetActive(out var manager)) manager.RefreshNow();
            return true;
        }

        private void OnValidate()
        {
            if (Application.isPlaying) HabitatRuntimeManager.NotifyWorldChanged();
        }

        public void Bind(PlacedCard placedCard)
        {
            if (placedCard == null) return;
            if (string.IsNullOrEmpty(placementId)) RegenerateId();
            if (!hasHomeHex)
            {
                homeHex = placedCard.placedHex;
                hasHomeHex = true;
            }
        }

        // Called only for duplicated instance identities; it never changes the bound home hex.
        public void RegenerateId() => placementId = Guid.NewGuid().ToString("N");

        public static HabitatPlacementIdentity BindPlacedCard(PlacedCard placedCard)
        {
            if (placedCard == null) return null;
            var identity = placedCard.GetComponent<HabitatPlacementIdentity>();
            if (identity == null) identity = placedCard.gameObject.AddComponent<HabitatPlacementIdentity>();
            identity.Bind(placedCard);
            return identity;
        }

        private void OnDisable() => HabitatRuntimeManager.NotifyWorldChanged();
        private void OnDestroy() => HabitatRuntimeManager.NotifyWorldChanged();
    }
}
