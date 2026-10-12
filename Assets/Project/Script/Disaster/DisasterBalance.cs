using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    /// <summary>
    /// Quy tắc riêng cho MỘT loại thiên tai. Cho phép hệ thống linh hoạt theo từng môi trường:
    /// mỗi loại có thể chỉ tác động lên một số BiomeAffinity nhất định, bán kính lan khác nhau...
    /// </summary>
    [Serializable]
    public sealed class DisasterRule
    {
        public DisasterKind kind = DisasterKind.Obstacle;
        [Min(0f)] public float weight = 1f;

        [Tooltip("Bật: chỉ được spawn trên ô có affinity nằm trong affectedBiomes. Tắt: mọi môi trường.")]
        public bool restrictToBiomes;

        [Tooltip("Danh sách môi trường bị ảnh hưởng. Rỗng + restrictToBiomes = không bao giờ spawn.")]
        public List<BiomeAffinity> affectedBiomes = new List<BiomeAffinity>();

        [Tooltip("Bán kính lan tính theo số bước hex. 0 = chỉ 1 ô.")]
        [Min(0)] public int spreadRadius = 0;

        [Tooltip("Giây giữa 2 nhịp lan. <= 0 = lan ngay 1 lần rồi đứng yên.")]
        [Min(0f)] public float spreadIntervalSeconds = 0f;

        [Tooltip("Số ô bị phủ tối đa (kể cả ô gốc). Chặn bão/cháy lan vô hạn.")]
        [Min(1)] public int maxTiles = 1;

        [Tooltip("Thời lượng tồn tại (giây) nếu không được chữa.")]
        [Min(1f)] public float durationSeconds = 60f;

        [Tooltip("Số thẻ mưa cần để dập hẳn thiên tai này (theo cường độ yêu cầu ở DisasterCatalog).")]
        [Min(1)] public int rainToCure = 1;

        [Tooltip("Hệ số tốc độ khai thác khi thiên tai đang hoạt động trên ô (0 = dừng hẳn).")]
        [Range(0f, 1f)] public float harvestSpeedMultiplier = 0f;

        public bool AffectsBiome(BiomeAffinity affinity)
        {
            if (!restrictToBiomes) return true;
            if (affectedBiomes == null || affectedBiomes.Count == 0) return false;
            for (int i = 0; i < affectedBiomes.Count; i++)
                if (affectedBiomes[i] == affinity) return true;
            return false;
        }
    }

    /// <summary>
    /// Balance tổng của hệ thống thiên tai. Mọi con số "cảm giác game" nằm ở đây, không hard-code.
    ///
    /// AN TOÀN: script này chỉ chứa số liệu và luật. Nó KHÔNG đụng tới Renderer/Material,
    /// nên không thể làm hỏng asset. Việc tô màu ô nằm ở TileDisasterVisualTint.
    /// </summary>
    [CreateAssetMenu(fileName = "DisasterBalance", menuName = "LumiWorld/Disaster/Disaster Balance")]
    public sealed class DisasterBalance : ScriptableObject
    {
        private const int ExpectedRuleCount = 5; // 5 loại trong DisasterKind

        [Header("Nhịp xuất hiện (phút)")]
        [Tooltip("Khoảng nghỉ ngẫu nhiên tối thiểu giữa 2 lần sinh vật cản/thiên tai.")]
        [Min(0f)] public float minSpawnIntervalMinutes = 5f;

        [Tooltip("Khoảng nghỉ ngẫu nhiên tối đa giữa 2 lần sinh vật cản/thiên tai.")]
        [Min(0f)] public float maxSpawnIntervalMinutes = 10f;

        [Tooltip("Bật/Tắt sinh thiên tai tự động. Tắt để test thủ công từ Inspector hoặc bằng phím tắt.")]
        public bool enableAutoSpawn = true;

        [Tooltip("Số thiên tai tối đa cùng tồn tại trên bản đồ.")]
        [Min(0)] public int maxConcurrentDisasters = 3;

        [Header("Vật cản (Obstacle)")]
        [Tooltip("Tỉ lệ spawn ra Vật cản so với các thiên tai khác khi cùng điều kiện.")]
        [Min(0f)] public float obstacleWeight = 3f;

        [Tooltip("Prefab hiển thị vật cản (đá, gỗ mục...). Để trống thì dùng khối debug để vẫn nhìn thấy.")]
        public GameObject obstaclePrefab;

        [Header("Ràng buộc theo môi trường")]
        [Tooltip("Quy tắc riêng cho từng loại thiên tai. Loại không có mặt ở đây dùng mặc định an toàn.")]
        public List<DisasterRule> rules = new List<DisasterRule>();

        [Header("Bão / Lốc xoáy theo mật độ mưa")]
        [Tooltip("Số thẻ mưa dùng trong cửa sổ thời gian dưới đây sẽ sinh một cơn bão.")]
        [Min(1)] public int cycloneRainThreshold = 4;

        [Tooltip("Độ dài cửa sổ đếm mưa (giây). 4 thẻ trong 10 giây -> bão.")]
        [Min(0.1f)] public float cycloneRainWindowSeconds = 10f;

        [Tooltip("Bão có được sinh tự động khi vượt ngưỡng mưa không.")]
        public bool spawnCycloneOnRainOverdose = true;

        [Header("Mặc định cho loại thiên tai thiếu Rule")]
        [Min(1f)] public float defaultDurationSeconds = 60f;
        [Min(1)] public int defaultMaxTiles = 1;
        [Min(1)] public int defaultRainToCure = 1;

        public DisasterRule GetRule(DisasterKind kind)
        {
            if (rules != null)
            {
                for (int i = 0; i < rules.Count; i++)
                {
                    if (rules[i] != null && rules[i].kind == kind) return rules[i];
                }
            }
            return null;
        }

        public int ResolveMaxTiles(DisasterKind kind)
        {
            var rule = GetRule(kind);
            return rule != null ? Mathf.Max(1, rule.maxTiles) : defaultMaxTiles;
        }

        public float ResolveDuration(DisasterKind kind)
        {
            var rule = GetRule(kind);
            return rule != null ? Mathf.Max(1f, rule.durationSeconds) : defaultDurationSeconds;
        }

        public int ResolveRainToCure(DisasterKind kind)
        {
            var rule = GetRule(kind);
            return rule != null ? Mathf.Max(1, rule.rainToCure) : defaultRainToCure;
        }

        public float ResolveHarvestMultiplier(DisasterKind kind)
        {
            if (!DisasterCatalog.BlocksHarvest(kind)) return 1f;
            var rule = GetRule(kind);
            return rule != null ? Mathf.Clamp01(rule.harvestSpeedMultiplier) : 0f;
        }

        public bool AffectsBiome(DisasterKind kind, BiomeAffinity affinity)
        {
            var rule = GetRule(kind);
            return rule == null || rule.AffectsBiome(affinity);
        }

        public float ResolveSpreadInterval(DisasterKind kind)
        {
            var rule = GetRule(kind);
            return rule != null ? Mathf.Max(0f, rule.spreadIntervalSeconds) : 0f;
        }

        /// <summary>Chọn ngẫu nhiên một loại thiên tai có thể xuất hiện trên môi trường cho trước.</summary>
        public DisasterKind PickRandomKind(BiomeAffinity affinity, System.Random rng)
        {
            if (rng == null) rng = new System.Random();

            var pool = new List<DisasterKind>();
            var weights = new List<float>();
            float total = 0f;

            System.Action<DisasterKind, float> consider = (kind, weight) =>
            {
                if (weight <= 0f) return;
                var rule = GetRule(kind);
                if (rule != null && rule.restrictToBiomes && !rule.AffectsBiome(affinity)) return;
                pool.Add(kind);
                weights.Add(weight);
                total += weight;
            };

            consider(DisasterKind.Obstacle, obstacleWeight);
            foreach (var rule in rules)
            {
                if (rule == null) continue;
                if (rule.kind == DisasterKind.Obstacle) continue;
                if (rule.kind == DisasterKind.Cyclone) continue; // bão kích hoạt bằng mưa, không random
                consider(rule.kind, rule.weight);
            }

            // Không cấu hình gì thì vẫn luôn có vật cản làm mặc định.
            if (pool.Count == 0) return DisasterKind.Obstacle;

            double roll = rng.NextDouble() * total;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0) return pool[i];
            }
            return pool[pool.Count - 1];
        }

        /// <summary>
        /// Kiểm tra asset có đủ dữ liệu để chạy không. Gọi từ OnValidate (Editor) và lúc Start.
        /// Trả về true nếu dùng được.
        /// </summary>
        public bool IsValid(out string error)
        {
            if (rules == null || rules.Count < ExpectedRuleCount)
            {
                error = $"Thiếu rule: có {rules?.Count ?? 0}/{ExpectedRuleCount} loại. " +
                        "Bấm nút 'Fill Missing Rules' bên dưới hoặc chạy Tools > LumiWorld > Disaster > Repair Balance Asset.";
                return false;
            }

            for (int kind = 0; kind < ExpectedRuleCount; kind++)
            {
                var wanted = (DisasterKind)kind;
                if (GetRule(wanted) == null)
                {
                    error = $"Thiếu rule cho {DisasterCatalog.DisplayName(wanted)} (kind {kind}).";
                    return false;
                }
            }

            if (maxSpawnIntervalMinutes < minSpawnIntervalMinutes)
            {
                error = "maxSpawnIntervalMinutes nhỏ hơn minSpawnIntervalMinutes.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Thêm rule còn thiếu (giữ nguyên rule đã có). Dùng khi asset bị dựng thiếu.</summary>
        [ContextMenu("Fill Missing Rules")]
        public void FillMissingRules()
        {
            if (rules == null) rules = new List<DisasterRule>();

            for (int kind = 0; kind < ExpectedRuleCount; kind++)
            {
                var wanted = (DisasterKind)kind;
                if (GetRule(wanted) != null) continue;
                rules.Add(CreateDefaultRule(wanted));
            }
        }

        private static DisasterRule CreateDefaultRule(DisasterKind kind)
        {
            var rule = new DisasterRule { kind = kind };

            switch (kind)
            {
                case DisasterKind.ForestFire:
                    rule.weight = 1f;
                    rule.restrictToBiomes = true;
                    rule.affectedBiomes = new List<BiomeAffinity> { BiomeAffinity.Leafwood, BiomeAffinity.Windheath };
                    rule.spreadRadius = 1;
                    rule.spreadIntervalSeconds = 5f;   // 5s mới lan sang ô kề (mặc định mới)
                    rule.maxTiles = 6;
                    rule.durationSeconds = 45f;
                    rule.rainToCure = 1;
                    rule.harvestSpeedMultiplier = 0f;
                    break;

                case DisasterKind.Cyclone:
                    rule.weight = 0f; // chỉ sinh khi mưa vượt ngưỡng
                    rule.spreadRadius = 2;
                    rule.spreadIntervalSeconds = 5f;
                    rule.maxTiles = 7;
                    rule.durationSeconds = 40f;
                    rule.rainToCure = 2;
                    rule.harvestSpeedMultiplier = 0f;
                    break;

                case DisasterKind.Drought:
                    rule.weight = 1.2f;
                    rule.restrictToBiomes = true;
                    rule.affectedBiomes = new List<BiomeAffinity>
                        { BiomeAffinity.Bloomfield, BiomeAffinity.Leafwood, BiomeAffinity.Windheath };
                    rule.maxTiles = 1;
                    rule.durationSeconds = 60f;
                    rule.rainToCure = 1;
                    rule.harvestSpeedMultiplier = 0f;
                    break;

                case DisasterKind.BarrenLand:
                    rule.weight = 1f;
                    rule.maxTiles = 1;
                    rule.durationSeconds = 90f;
                    rule.rainToCure = 1;
                    rule.harvestSpeedMultiplier = 0.5f;
                    break;

                default: // Obstacle
                    rule.weight = 0f; // weight riêng ở obstacleWeight
                    rule.maxTiles = 1;
                    rule.durationSeconds = 120f;
                    rule.rainToCure = 1;
                    rule.harvestSpeedMultiplier = 0f;
                    break;
            }

            return rule;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (rules == null) return;
            if (!IsValid(out string error))
                Debug.LogWarning($"[LumiWorld Disaster] DisasterBalance '{name}' chưa hợp lệ: {error}", this);
        }
#endif
    }
}
