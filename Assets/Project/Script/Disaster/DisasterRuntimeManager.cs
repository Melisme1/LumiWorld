using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    /// <summary>
    /// Bộ điều phối trung tâm của hệ thống thiên tai/vật cản.
    ///
    /// Trách nhiệm:
    ///  - Sinh thiên tai/vật cản ngẫu nhiên mỗi 5–10 phút (theo DisasterBalance + IDisasterClock).
    ///  - Quản lý vòng đời: lan theo môi trường (cháy) hoặc bán kính (bão), đếm giờ, tự tắt.
    ///  - Đếm mật độ dùng mưa: 4 thẻ mưa trong 10 giây -> sinh bão.
    ///  - Cung cấp API cho thẻ mưa (áp mưa để dập), cho khai thác (hệ số), cho UI (danh sách highlight).
    ///
    /// KHÔNG tự ý sửa các hệ thống khác: mọi tương tác đều qua TileDisasterState + API công khai.
    /// Việc tô màu ô uỷ quyền hoàn toàn cho TileDisasterVisualTint (an toàn với asset material).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class DisasterRuntimeManager : MonoBehaviour
    {
        private static DisasterRuntimeManager instance;

        public static DisasterRuntimeManager Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<DisasterRuntimeManager>();
                return instance;
            }
        }

        public static bool TryGetActive(out DisasterRuntimeManager manager)
        {
            manager = Instance;
            return manager != null && manager.isActiveAndEnabled;
        }

        [Header("Tham chiếu")]
        [SerializeField] private HexWorldGenerator worldGenerator;
        [Tooltip("Balance của hệ thống. Bắt buộc để chạy.")]
        [SerializeField] private DisasterBalance balance;
        [Tooltip("Đồng hồ dùng cho vòng đời. Trống = wall-clock thực.")]
        [SerializeField] private MonoBehaviour clockSource;

        [Header("Test Tools (chỉ dùng khi phát triển)")]
        [Tooltip("Bật để dùng phím tắt sinh thiên tai trong Play Mode: O=Vật cản, F=Cháy, C=Bão, D=Hạn, B=Khô cằn, R=Xóa hết.")]
        [SerializeField] private bool enableTestHotkeys = true;
        [Tooltip("Màu khối debug đánh dấu vật cản khi chưa gán obstaclePrefab.")]
        [SerializeField] private Color debugObstacleColor = new Color(0.35f, 0.36f, 0.40f, 1f);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Tooltip("In log xác nhận chuỗi spawn -> state -> renderer/shader khi sinh thiên tai. Chỉ có trong Editor/Development Build.")]
        [SerializeField] private bool debugSpawnDiagnostics = true;
#endif

        private IDisasterClock clock;
        private readonly Dictionary<string, DisasterInstance> disasters = new Dictionary<string, DisasterInstance>();
        private readonly List<DisasterInstance> activeList = new List<DisasterInstance>();
        private readonly Dictionary<string, double> nextSpreadAt = new Dictionary<string, double>();
        private readonly Dictionary<string, List<HexCoordinates>> spreadFrontier = new Dictionary<string, List<HexCoordinates>>();
        private readonly Dictionary<string, double> lastTick = new Dictionary<string, double>();
        private readonly Dictionary<string, GameObject> spawnedVisuals = new Dictionary<string, GameObject>();

        // Cửa sổ đếm mưa để kích hoạt bão.
        private readonly List<double> rainTimestamps = new List<double>();

        private System.Random rng;
        private double nextSpawnAt;
        private int instanceCounter;

        public event Action<DisasterInstance> DisasterStarted;
        public event Action<DisasterInstance> DisasterEnded;
        /// <summary>Phát khi tập ô bị ảnh hưởng thay đổi (lan/tắt) để UI vẽ lại highlight.</summary>
        public event Action<DisasterInstance> DisasterChanged;

        public IReadOnlyList<DisasterInstance> ActiveDisasters => activeList;
        public DisasterBalance Balance => balance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void Awake()
        {
            instance = this;
            clock = ResolveClock();
            rng = new System.Random(Environment.TickCount);
        }

        private void OnEnable()
        {
            // CỐ Ý KHÔNG ScheduleNextSpawn() ở đây: OnEnable chạy TRƯỚC Start, lúc đó balance
            // còn null nên lịch sẽ không được đặt và nextSpawnAt giữ nguyên 0 -> thiên tai spawn
            // ngay frame đầu. Lịch được đặt ở cuối Start() sau khi balance đã sẵn sàng.
        }

        private void Start()
        {
            if (worldGenerator == null) worldGenerator = FindAnyObjectByType<HexWorldGenerator>();
            if (balance == null) balance = Resources.Load<DisasterBalance>("DisasterBalance");
            if (balance != null && !balance.IsValid(out _)) balance.FillMissingRules();

            if (balance == null)
            {
                Debug.LogError("[LumiWorld Disaster] Chưa gán DisasterBalance nên hệ thống thiên tai không chạy. " +
                    "Tạo asset (Create > LumiWorld > Disaster > Disaster Balance) rồi gán vào component này, " +
                    "hoặc đặt tại Resources/DisasterBalance.", this);
                enabled = false;
                return;
            }

            EnsureTileDisasterStates();
            DisasterWarningUI.AttachWhenReady(this);

            // Đặt lịch spawn ĐẦU TIÊN sau khi balance đã chắc chắn sẵn sàng.
            // Nhờ vậy lần sinh thiên tai đầu tiên cách thời điểm vào game đúng nhịp
            // minSpawnIntervalMinutes..maxSpawnIntervalMinutes, không còn nổ ngay frame 1.
            ScheduleNextSpawn();
        }

        /// <summary>Gắn TileDisasterState lên mọi ô hiện có để hệ thống khác truy vấn được ngay.</summary>
        private void EnsureTileDisasterStates()
        {
            if (worldGenerator == null || worldGenerator.MapTiles == null) return;
            foreach (var pair in worldGenerator.MapTiles)
            {
                if (pair.Value == null) continue;
                var state = TileDisasterState.Ensure(pair.Value);
                if (state != null && state.affinity == BiomeAffinity.None)
                    state.affinity = ResolveAffinity(pair.Value);
            }
        }

        private IDisasterClock ResolveClock()
        {
            if (clockSource is IDisasterClock provider) return provider;
            if (clockSource != null)
            {
                var property = clockSource.GetType().GetProperty("NowSeconds");
                if (property != null) return new ReflectionClock(clockSource);
            }
            return new SystemDisasterClock();
        }

        // ------------------------------------------------------------------
        // Sinh thiên tai
        // ------------------------------------------------------------------

        private void ScheduleNextSpawn()
        {
            if (balance == null)
            {
                // Không có balance thì không thể biết nhịp. Đẩy lịch ra vô cực để TickSpawn
                // KHÔNG hiểu nhầm nextSpawnAt = 0 là "đã tới hạn" mà spawn ngay frame đầu.
                nextSpawnAt = double.MaxValue;
                return;
            }
            float minMinutes = Mathf.Max(0f, balance.minSpawnIntervalMinutes);
            float maxMinutes = Mathf.Max(minMinutes, balance.maxSpawnIntervalMinutes);
            double seconds = Mathf.Lerp(minMinutes, maxMinutes, (float)rng.NextDouble()) * 60.0;
            nextSpawnAt = clock.NowSeconds + seconds;
        }

        private void TickSpawn(double now)
        {
            if (!balance.enableAutoSpawn) return;
            if (now < nextSpawnAt) return;

            DisasterKind kind = balance.PickRandomKind(BiomeAffinity.None, rng);

            ScheduleNextSpawn();

            if (disasters.Count >= Mathf.Max(0, balance.maxConcurrentDisasters)) return;

            // CHÁY RỪNG bắt buộc phải sinh trên ô Lush (đất tươi tốt) — giống hệt TestSpawn.
            // Trước đây auto-spawn bỏ qua bộ lọc này nên đám cháy có thể nổ trên ô không có gì
            // để cháy rồi đứng yên (không lan được) => "đám cháy câm".
            bool requireLush = kind == DisasterKind.ForestFire;

            if (TryPickSpawnTile(out HexCoordinates origin, out BiomeAffinity affinity, requireLushForFire: requireLush))
            {
                // Chọn loại theo môi trường của ô ĐÃ CHỌN THẬT, nếu không thì
                // rule restrictToBiomes (hạn hán/cháy) có thể bị áp lên môi trường không hợp lệ.
                if (requireLush || !BiomeRestrictsKind(kind, affinity))
                {
                    SpawnDisaster(kind, origin);
                }
                else
                {
                    SpawnDisaster(balance.PickRandomKind(affinity, rng), origin);
                }
            }
        }

        /// <summary>Loại thiên tai này có bị rule giới hạn môi trường và ô đích có nằm ngoài vùng cho phép không?</summary>
        private bool BiomeRestrictsKind(DisasterKind kind, BiomeAffinity affinity)
        {
            return balance != null && !balance.AffectsBiome(kind, affinity);
        }

        /// <summary>Chọn một ô đất liền đang trống làm điểm phát sinh.</summary>
        private bool TryPickSpawnTile(out HexCoordinates origin, out BiomeAffinity affinity)
        {
            return TryPickSpawnTile(out origin, out affinity, requireLushForFire: false);
        }

        /// <summary>
        /// Chọn điểm phát sinh. Với CHÁY RỪNG, chỉ chọn ô LUSH (đất tươi tốt có môi trường)
        /// vì đất thô Arid không có gì để cháy.
        /// </summary>
        private bool TryPickSpawnTile(out HexCoordinates origin, out BiomeAffinity affinity, bool requireLushForFire)
        {
            origin = default;
            affinity = BiomeAffinity.None;

            if (worldGenerator == null || worldGenerator.MapTiles == null || worldGenerator.MapTiles.Count == 0)
                return false;

            // Gom ô hợp lệ rồi bốc ngẫu nhiên (phân bố đều, không thiên vị ô đầu).
            var candidates = new List<KeyValuePair<HexCoordinates, GameObject>>();
            foreach (var pair in worldGenerator.MapTiles)
            {
                if (pair.Value == null) continue;
                var state = pair.Value.GetComponent<TileDisasterState>();
                // Đã có thiên tai hoặc đã là vật cản thì bỏ qua để tránh chồng đè.
                if (state != null && (state.hasObstacle || state.HasAnyDisaster)) continue;

                // CHÁY RỪNG: chỉ nhận ô CHẮC CHẮN Lush.
                //
                // Bản cũ viết `requireLushForFire && state != null && !state.IsLushTile` — điều kiện
                // `state != null` khiến ô CHƯA có TileDisasterState (chưa được quét) LỌT QUA bộ lọc,
                // rơi vào nhánh Mark-only của ApplyToTile rồi đứng yên vì không lan được => "cháy câm".
                // Bộ lọc phải là điều kiện KHẲNG ĐỊNH: không chứng minh được Lush thì loại.
                if (requireLushForFire && (state == null || !state.IsLushTile)) continue;

                candidates.Add(pair);
            }

            if (candidates.Count == 0) return false;

            var pick = candidates[rng.Next(candidates.Count)];
            origin = pick.Key;
            affinity = ResolveAffinity(pick.Value);
            return true;
        }

        /// <summary>Suy ra môi trường của ô từ state, tên tile hoặc terrainTypeIndex.</summary>
        public static BiomeAffinity ResolveAffinity(GameObject tileObj)
        {
            if (tileObj == null) return BiomeAffinity.None;

            var state = tileObj.GetComponent<TileDisasterState>();
            if (state != null && state.affinity != BiomeAffinity.None) return state.affinity;

            string name = tileObj.name;
            if (name.IndexOf("Bloomfield", StringComparison.OrdinalIgnoreCase) >= 0) return BiomeAffinity.Bloomfield;
            if (name.IndexOf("Leafwood", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Forest", StringComparison.OrdinalIgnoreCase) >= 0) return BiomeAffinity.Leafwood;
            if (name.IndexOf("Windheath", StringComparison.OrdinalIgnoreCase) >= 0) return BiomeAffinity.Windheath;

            var info = tileObj.GetComponent<HexTileInfo>();
            if (info != null)
            {
                switch (info.terrainTypeIndex)
                {
                    case 0: return BiomeAffinity.Bloomfield;
                    case 1: return BiomeAffinity.Leafwood;
                    case 2: return BiomeAffinity.Windheath;
                }
            }
            return BiomeAffinity.None;
        }

        /// <summary>Sinh thiên tai. Gọi từ auto spawn hoặc từ test hotkey / ContextMenu.</summary>
        public DisasterInstance SpawnDisaster(DisasterKind kind, HexCoordinates origin)
        {
            if (balance == null) return null;
            if (worldGenerator == null ||
                !worldGenerator.MapTiles.TryGetValue(origin, out var originTile) || originTile == null)
            {
                return null;
            }

            var instance = new DisasterInstance
            {
                instanceId = kind + "_" + (++instanceCounter).ToString("D4"),
                kind = kind,
                origin = origin,
                totalSeconds = balance.ResolveDuration(kind),
                startedAtUnscaledTime = Time.unscaledTime
            };
            instance.remainingSeconds = instance.totalSeconds;

            disasters[instance.instanceId] = instance;
            activeList.Add(instance);
            nextSpreadAt[instance.instanceId] = clock.NowSeconds + balance.ResolveSpreadInterval(kind);
            spreadFrontier[instance.instanceId] = new List<HexCoordinates> { origin };

            ApplyToTile(instance, originTile);

            // Tô lại ngay lúc sinh để ô gốc đổi màu tức thì.
            TileDisasterState.TryGet(originTile, out var originState);
            originState?.RefreshVisual();

            // CHẨN ĐOÁN: xác nhận chuỗi spawn -> state -> tô màu có chạy hết không.
            // Nếu dòng này in ra mà ô không đổi màu thì lỗi nằm ở khâu RENDERER/SHADER
            // (xem cảnh báo của TileDisasterVisualTint), không phải ở logic thiên tai.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugSpawnDiagnostics && kind == DisasterKind.ForestFire)
            {
                var st = originState;
                var rends = originTile.GetComponentsInChildren<Renderer>(true);
                string shaders = string.Empty;
                int shown = 0;
                foreach (var rr in rends)
                {
                    if (rr == null || rr.sharedMaterial == null) continue;
                    if (shown++ >= 3) break;
                    var sh = rr.sharedMaterial.shader;
                    bool hasBase = rr.sharedMaterial.HasProperty(Shader.PropertyToID("_BaseColor"));
                    bool hasColor = rr.sharedMaterial.HasProperty(Shader.PropertyToID("_Color"));
                    shaders += $"\n      - '{rr.name}' shader={(sh != null ? sh.name : "?")} _BaseColor={hasBase} _Color={hasColor}";
                }

                Debug.Log(
                    $"[LumiWorld Disaster DIAG] Spawn cháy tại {origin} trên ô '{originTile.name}'\n" +
                    $"  • state: isBurned={(st != null && st.isBurned)} " +
                    $"Has(ForestFire)={(st != null && st.Has(DisasterKind.ForestFire))} " +
                    $"aridity={(st != null ? st.aridity : -1f)} IsLush={(st != null && st.IsLushTile)}\n" +
                    $"  • TileDisasterState trên: {(st != null ? st.gameObject.name : "(KHÔNG CÓ)")}\n" +
                    $"  • Renderer tìm được trong cây con: {rends.Length}{shaders}", originTile);
            }
#endif

            DisasterStarted?.Invoke(instance);
            DisasterChanged?.Invoke(instance);
            return instance;
        }

        private void ApplyToTile(DisasterInstance instance, GameObject tileObj)
        {
            if (tileObj == null) return;

            var state = TileDisasterState.Ensure(tileObj);
            if (state == null) return;

            if (instance.kind == DisasterKind.Obstacle) state.hasObstacle = true;
            else state.Mark(instance.kind, instance.instanceId);

            if (instance.kind == DisasterKind.BarrenLand) state.aridity = 1f;

            if (instance.kind == DisasterKind.Obstacle) SpawnObstacleVisual(instance, tileObj);

            // Tô màu ô để nhìn thấy hiệu quả bằng mắt (an toàn với asset material).
            state.RefreshVisual();
        }

        private void SpawnObstacleVisual(DisasterInstance instance, GameObject tileObj)
        {
            if (tileObj == null) return;

            GameObject visual;
            if (balance != null && balance.obstaclePrefab != null)
            {
                visual = Instantiate(balance.obstaclePrefab, tileObj.transform);
                // Neo vào ĐÚNG tâm ô: nếu prefab có gốc lệch, vật cản sẽ trôi ra ngoài mép hex.
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Khối tạm để vẫn nhìn thấy vật cản khi chưa gán prefab.
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "ObstacleDebug";
                visual.transform.SetParent(tileObj.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                visual.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

                var renderer = visual.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var material = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"))
                    {
                        color = debugObstacleColor
                    };
                    renderer.material = material;
                }
            }

            // Không để visual chặn raycast đặt thẻ.
            var collider = visual.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            spawnedVisuals[instance.instanceId] = visual;
        }

        private void ClearVisuals(DisasterInstance instance)
        {
            if (instance == null) return;
            if (spawnedVisuals.TryGetValue(instance.instanceId, out var visual) && visual != null)
                Destroy(visual);
            spawnedVisuals.Remove(instance.instanceId);
        }

        // ------------------------------------------------------------------
        // Vòng đời: lan + đếm giờ + tắt
        // ------------------------------------------------------------------

        private void TickLifecycle(double now)
        {
            for (int i = activeList.Count - 1; i >= 0; i--)
            {
                var instance = activeList[i];
                float delta = (float)Math.Max(0, now - GetLastTick(instance));
                instance.remainingSeconds -= delta;
                SetLastTick(instance, now);

                float spreadInterval = balance.ResolveSpreadInterval(instance.kind);
                double nextSpread = nextSpreadAt.TryGetValue(instance.instanceId, out var t) ? t : double.MaxValue;
                if (spreadInterval > 0f && now >= nextSpread)
                {
                    nextSpreadAt[instance.instanceId] = now + spreadInterval;
                    SpreadDisaster(instance);
                }

                if (instance.IsExpired) EndDisaster(instance);
            }
        }

        private double GetLastTick(DisasterInstance instance)
        {
            return lastTick.TryGetValue(instance.instanceId, out var t) ? t : clock.NowSeconds;
        }

        private void SetLastTick(DisasterInstance instance, double now)
        {
            lastTick[instance.instanceId] = now;
        }

        private void SpreadDisaster(DisasterInstance instance)
        {
            int maxTiles = balance.ResolveMaxTiles(instance.kind);
            if (CountTiles(instance.instanceId) >= maxTiles) return;
            if (worldGenerator == null) return;
            if (!spreadFrontier.TryGetValue(instance.instanceId, out var frontier)) return;

            var nextFrontier = new List<HexCoordinates>();

            foreach (var hex in frontier)
            {
                if (CountTiles(instance.instanceId) >= maxTiles) break;

                for (int dir = 0; dir < 6; dir++)
                {
                    if (CountTiles(instance.instanceId) >= maxTiles) break;

                    HexCoordinates neighbor = hex.GetNeighbor(dir);
                    if (!worldGenerator.MapTiles.TryGetValue(neighbor, out var neighborTile) || neighborTile == null) continue;

                    var neighborState = neighborTile.GetComponent<TileDisasterState>();
                    if (neighborState != null && neighborState.Has(instance.kind)) continue;
                    // Không lan vào ô đã có vật cản để tránh chồng state.
                    if (neighborState != null && neighborState.hasObstacle) continue;

                    if (instance.kind == DisasterKind.ForestFire)
                    {
                        // 1. CHỈ ô LUSH (đất tươi tốt có môi trường) mới bắt lửa.
                        //    Đất thô Arid không có cây cỏ để cháy -> chặn ngay, không tốn gì thêm.
                        //    Đây là điều kiện quan trọng hơn cả so khớp môi trường bên dưới.
                        var neighborStateForFire = neighborState;
                        if (neighborStateForFire == null)
                        {
                            neighborStateForFire = neighborTile.GetComponent<TileDisasterState>();
                            if (neighborStateForFire == null) continue;
                        }
                        if (!neighborStateForFire.IsLushTile) continue;

                        // 2. Chỉ lan trong cùng môi trường (Leafwood chỉ cháy sang Leafwood...).
                        BiomeAffinity originAffinity =
                            worldGenerator.MapTiles.TryGetValue(instance.origin, out var originTile) && originTile != null
                                ? ResolveAffinity(originTile)
                                : BiomeAffinity.None;
                        if (ResolveAffinity(neighborTile) != originAffinity) continue;
                    }
                    else if (!balance.AffectsBiome(instance.kind, ResolveAffinity(neighborTile)))
                    {
                        continue;
                    }

                    // ApplyToTile đã tự RefreshVisual() ở cuối; DestroyTile cũng vậy.
                    if (instance.kind == DisasterKind.ForestFire) DestroyTile(instance, neighborTile);
                    else ApplyToTile(instance, neighborTile);

                    nextFrontier.Add(neighbor);
                }
            }

            frontier.Clear();
            frontier.AddRange(nextFrontier);
            DisasterChanged?.Invoke(instance);
        }

        private int CountTiles(string instanceId)
        {
            int count = 0;
            foreach (var state in TileDisasterState.All)
            {
                if (state == null) continue;
                foreach (var pair in state.ActiveDisasters)
                {
                    if (pair.Value == instanceId) { count++; break; }
                }
            }
            return count;
        }

        /// <summary>Cháy rừng phá hủy ô: mất môi trường + hoàn lại thẻ Thú cho người chơi.</summary>
        private void DestroyTile(DisasterInstance instance, GameObject tileObj)
        {
            if (tileObj == null) return;
            instance.destroyedCount++;

            ReturnCreaturesOnTile(tileObj);

            var burned = TileDisasterState.Ensure(tileObj);
            if (burned != null)
            {
                // Ô đã cháy là BẤT KHẢ HỒI PHỤC cho tới khi người chơi dùng mưa hạ nhiệt:
                // vẫn ghi Mark để UI/tô màu biết ô nằm trong sự kiện cháy.
                burned.isBurned = true; // ô cháy không khai thác/môi trường nữa cho tới khi hồi phục
                burned.Mark(DisasterKind.ForestFire, instance.instanceId);
                burned.aridity = 1f;

                // QUAN TRỌNG: tô màu SAU khi đã set xong isBurned/Mark/aridity.
                // Bản cũ gọi RefreshVisual() bên trong ApplyToTile ngay trước khi DestroyTile,
                // nên ô vừa bắt lửa vẫn giữ màu "chưa cháy" tới tận nhịp lan kế tiếp
                // (spreadIntervalSeconds = 4s) => nhìn như cháy rừng không đổi màu ô.
                burned.RefreshVisual();
            }

            // Ẩn môi trường (props hệ sinh thái) của ô bị cháy.
            SuppressHabitatOnTile(tileObj);
        }

        private void ReturnCreaturesOnTile(GameObject tileObj)
        {
            if (tileObj == null) return;
            var cards = tileObj.GetComponentsInChildren<PlacedCard>(true);
            foreach (var card in cards)
            {
                if (card == null || card.cardData == null) continue;
                if (card.CardType != CardType.Creature) continue;

                // Hoàn thẻ Thú về tay người chơi.
                if (CardDeskController.Instance != null)
                    CardDeskController.Instance.AddRewardCard(card.cardData);
                Destroy(card.gameObject);
            }
        }

        private void SuppressHabitatOnTile(GameObject tileObj)
        {
            if (tileObj == null) return;
            // Tắt các container habitat/props đã sinh trên ô (không xóa đất lục giác gốc).
            foreach (Transform child in tileObj.transform)
            {
                if (child == null) continue;
                string name = child.name;
                if (name.StartsWith("Habitat_", StringComparison.Ordinal) ||
                    name.StartsWith("ConnectorProp_", StringComparison.Ordinal))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void EndDisaster(DisasterInstance instance)
        {
            // Dọn state trên ô thuộc thiên tai này. Duyệt qua TẤT CẢ ô đang có component
            // (kể cả ô không nằm trong danh sách active) để không bỏ sót ô đã bị ẩn.
            foreach (var state in TileDisasterState.All)
            {
                if (state == null) continue;

                // CHỈ dọn ô nào ĐANG giữ ĐÚNG instance này. Trước đây gỡ theo kind nên khi có 2
                // đám cháy song song, đám nào kết thúc trước cũng reset isBurned của tất cả ô
                // trên bản đồ — kể cả ô vẫn đang cháy do instance còn lại.
                if (!state.Clear(instance.kind, instance.instanceId))
                {
                    // Ô không liên quan tới instance này -> bỏ qua, KHÔNG chạm cờ của nó.
                    continue;
                }

                if (instance.kind == DisasterKind.Obstacle) state.hasObstacle = false;

                // Ô cháy hồi phục khi đám cháy kết thúc (môi trường mọc lại).
                // isBurned chỉ được hạ khi instance cháy này thực sự hết hạn/mưa đủ.
                if (instance.kind == DisasterKind.ForestFire)
                {
                    state.isBurned = false;
                    // Ô vừa hồi phục không còn khô cằn. Trước đây aridity = 1 do cháy để lại
                    // khiến IsArid vẫn true -> mưa nhỏ bị từ chối và vẫn bị tô màu khô trên một
                    // ô đã sạch thiên tai (state thừa hưởng từ lúc bị cháy).
                    state.aridity = 0f;
                }

                // BarrenLand: dọn aridity KHÔNG phụ thuộc rainApplied. Trước đây nếu thiên tai
                // tự hết hạn mà chưa hứng mưa nào thì aridity kẹt ở 1 -> ô khô vĩnh viễn.
                // Việc cứu bằng mưa đã được xử lý ngay trong ApplyRain (EndDisaster ngay khi đủ liều).
                if (instance.kind == DisasterKind.BarrenLand) state.aridity = 0f;

                state.RefreshVisual();
            }

            ClearVisuals(instance);

            disasters.Remove(instance.instanceId);
            activeList.Remove(instance);
            nextSpreadAt.Remove(instance.instanceId);
            spreadFrontier.Remove(instance.instanceId);
            lastTick.Remove(instance.instanceId);

            DisasterEnded?.Invoke(instance);
        }

        // ------------------------------------------------------------------
        // Mưa: dập thiên tai + đếm mật độ -> bão
        // ------------------------------------------------------------------

        /// <summary>
        /// Người chơi dùng thẻ mưa lên thiên tai đang phủ ô.
        /// Trả về true nếu mưa được chấp nhận (đủ cường độ), false nếu bị từ chối.
        /// </summary>
        public bool ApplyRain(GameObject tileObj, DisasterRainStrength strength)
        {
            if (tileObj == null) return false;

            var state = tileObj.GetComponent<TileDisasterState>();
            if (state == null) return false;

            bool anyAccepted = false;

            // 1. Khô cằn: BẮT BUỘC mưa lớn mới thấm.
            if (state.IsArid)
            {
                if (strength == DisasterRainStrength.Heavy)
                {
                    state.aridity = 0f;
                    state.hasReceivedWater = true;
                    anyAccepted = true;

                    // Cải tạo xong thì phải KẾT THÚC luôn instance BarrenLand, không chỉ gỡ mark
                    // trên ô. Trước đây chỉ Clear(BarrenLand) nên instance vẫn nằm trong
                    // disasters/activeList tới hết duration: vẫn chiếm slot maxConcurrentDisasters,
                    // vẫn tick lan, và khi hết hạn lại tô màu khô trở lại trên ô đã cải tạo.
                    if (state.ActiveDisasters.TryGetValue(DisasterKind.BarrenLand, out var barrenId) &&
                        disasters.TryGetValue(barrenId, out var barrenInstance))
                    {
                        barrenInstance.rainApplied++;
                        EndDisaster(barrenInstance);
                    }
                    else
                    {
                        // Ô khô không còn instance (thiên tai đã tự tắt trước đó) -> chỉ cần gỡ mark.
                        state.Clear(DisasterKind.BarrenLand);
                    }
                }
                else
                {
                    // Mưa nhỏ không thấm -> từ chối rõ ràng để UI báo "cần mưa lớn".
                    return false;
                }
            }

            if (strength == DisasterRainStrength.Light) anyAccepted = true;
            state.hasReceivedWater = true;

            // 2. Các thiên tai khác đang phủ: mưa đủ cường độ thì giảm / dập.
            var kinds = new List<DisasterKind>(state.ActiveDisasters.Keys);
            foreach (var kind in kinds)
            {
                if (!state.ActiveDisasters.TryGetValue(kind, out var instanceId)) continue;
                if (!disasters.TryGetValue(instanceId, out var instance)) continue;
                if (!KindAcceptsRain(kind, strength)) continue;

                instance.rainApplied++;
                anyAccepted = true;

                if (instance.rainApplied >= balance.ResolveRainToCure(kind))
                    EndDisaster(instance);
                else
                {
                    state.RefreshVisual();
                    DisasterChanged?.Invoke(instance);
                }
            }

            state.RefreshVisual();
            if (anyAccepted) RecordRainForCyclone();
            return anyAccepted;
        }

        private static bool KindAcceptsRain(DisasterKind kind, DisasterRainStrength strength)
        {
            return (int)strength >= DisasterCatalog.RequiredRainStrength(kind);
        }

        /// <summary>Áp mưa lên TOÀN BỘ ô trong bán kính (dùng cho mưa lớn nhiều ô để chữa bão/hạn).</summary>
        public int ApplyRainArea(HexCoordinates center, int radius, DisasterRainStrength strength)
        {
            int cured = 0;
            for (int q = -radius; q <= radius; q++)
            {
                int rMin = Mathf.Max(-radius, -q - radius);
                int rMax = Mathf.Min(radius, -q + radius);
                for (int r = rMin; r <= rMax; r++)
                {
                    HexCoordinates hex = new HexCoordinates(center.Q + q, center.R + r);
                    if (worldGenerator == null) continue;
                    if (!worldGenerator.MapTiles.TryGetValue(hex, out var tile) || tile == null) continue;
                    if (ApplyRain(tile, strength)) cured++;
                }
            }
            return cured;
        }

        private void RecordRainForCyclone()
        {
            rainTimestamps.Add(clock.NowSeconds);
        }

        private void TickRainWindow(double now)
        {
            if (balance == null) return;
            double window = balance.cycloneRainWindowSeconds;
            rainTimestamps.RemoveAll(t => now - t > window);

            if (!balance.spawnCycloneOnRainOverdose) return;
            if (rainTimestamps.Count < balance.cycloneRainThreshold) return;
            if (disasters.Count >= Mathf.Max(0, balance.maxConcurrentDisasters)) return;

            // Vượt ngưỡng: dùng hết mốc rồi sinh một cơn bão ở ô đất liền ngẫu nhiên.
            rainTimestamps.Clear();
            if (TryPickSpawnTile(out HexCoordinates origin, out _))
            {
                Debug.Log("<color=#5AB8FF>[LumiWorld Disaster] Mật độ mưa vượt ngưỡng — sinh Lốc xoáy!</color>");
                SpawnDisaster(DisasterKind.Cyclone, origin);
            }
        }

        // ------------------------------------------------------------------
        // API cho hệ thống khác
        // ------------------------------------------------------------------

        /// <summary>Hệ số tốc độ khai thác của một ô (0 = dừng). Trả 1 nếu không có thiên tai.</summary>
        public float GetHarvestMultiplier(GameObject tileObj)
        {
            if (balance == null || tileObj == null) return 1f;
            var state = tileObj.GetComponent<TileDisasterState>();
            return state != null ? state.HarvestSpeedMultiplier(balance) : 1f;
        }

        /// <summary>Ô có bị chặn khai thác/đặt thẻ không.</summary>
        public bool IsTileBlocked(GameObject tileObj)
        {
            if (balance == null || tileObj == null) return false;
            var state = tileObj.GetComponent<TileDisasterState>();
            return state != null && state.BlocksHarvest(balance);
        }

        public bool TryGetInstance(string instanceId, out DisasterInstance instance)
        {
            return disasters.TryGetValue(instanceId, out instance);
        }

        /// <summary>Danh sách ô đang bị một thiên tai phủ (cho UI highlight).</summary>
        public List<GameObject> GetAffectedTiles(DisasterInstance instance)
        {
            var result = new List<GameObject>();
            if (instance == null || worldGenerator == null) return result;

            foreach (var pair in worldGenerator.MapTiles)
            {
                if (pair.Value == null) continue;
                var state = pair.Value.GetComponent<TileDisasterState>();
                if (state == null) continue;
                if (state.hasObstacle && instance.kind == DisasterKind.Obstacle) { result.Add(pair.Value); continue; }
                if (state.Has(instance.kind)) result.Add(pair.Value);
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Công cụ TEST (không ảnh hưởng gameplay thường)
        // ------------------------------------------------------------------

        [ContextMenu("Test: Spawn Obstacle Now")]
        public void TestSpawnObstacle() => TestSpawn(DisasterKind.Obstacle);

        [ContextMenu("Test: Spawn Forest Fire Now")]
        public void TestSpawnForestFire() => TestSpawn(DisasterKind.ForestFire);

        [ContextMenu("Test: Spawn Cyclone Now")]
        public void TestSpawnCyclone() => TestSpawn(DisasterKind.Cyclone);

        [ContextMenu("Test: Spawn Drought Now")]
        public void TestSpawnDrought() => TestSpawn(DisasterKind.Drought);

        [ContextMenu("Test: Spawn Barren Land Now")]
        public void TestSpawnBarrenLand() => TestSpawn(DisasterKind.BarrenLand);

        [ContextMenu("Test: Clear All Disasters")]
        public void TestClearAll()
        {
            var copy = new List<DisasterInstance>(activeList);
            foreach (var instance in copy) EndDisaster(instance);

            // Quét thêm mọi ô còn sót dấu vết (thiên tai đã tự tắt trước đó, hoặc ô đã bị ẩn)
            // để bấm R luôn trả bản đồ về trạng thái sạch.
            foreach (var state in FindObjectsByType<TileDisasterState>(FindObjectsInactive.Include))
            {
                if (state == null) continue;
                state.ClearAll();
                state.hasObstacle = false;
                state.isBurned = false;
                state.aridity = 0f;
                state.RefreshVisual();
            }

            Debug.Log("[LumiWorld Disaster] Đã xóa toàn bộ thiên tai/vật cản và khôi phục màu ô.");
        }

        /// <summary>Sinh thiên tai ở một ô đất liền ngẫu nhiên — bỏ qua nhịp chờ 5–10 phút.</summary>
        public void TestSpawn(DisasterKind kind)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[LumiWorld Disaster] Test spawn chỉ chạy trong Play Mode.");
                return;
            }

            if (balance == null) balance = Resources.Load<DisasterBalance>("DisasterBalance");
            if (balance == null)
            {
                Debug.LogError("[LumiWorld Disaster] Chưa có DisasterBalance nên không spawn được.");
                return;
            }

            if (worldGenerator == null) worldGenerator = FindAnyObjectByType<HexWorldGenerator>();

            // Cháy rừng chỉ cháy được ô Lush, nên phải lọc ô phát sinh theo yêu cầu đó.
            bool requireLush = kind == DisasterKind.ForestFire;

            // Thu thập lý do để thông báo chính xác (trước đây báo "không tìm được ô trống"
            // kể cả khi bản đồ đầy đất Arid — thông báo sai hướng khiến khó chẩn đoán).
            if (!TryPickSpawnTile(out HexCoordinates origin, out _, requireLushForFire: requireLush))
            {
                if (!requireLush)
                {
                    Debug.LogWarning("[LumiWorld Disaster] Không tìm được ô trống để đặt thiên tai " +
                        "(mọi ô đều đã có thiên tai/vật cản).");
                    return;
                }

                Debug.LogWarning(BuildNoLushTilesMessage());
                return;
            }

            var instance = SpawnDisaster(kind, origin);
            if (instance != null)
                Debug.Log($"[LumiWorld Disaster] TEST spawn {DisasterCatalog.DisplayName(kind)} tại {origin}.");
        }

        /// <summary>
        /// Giải thích vì sao không có ô nào để cháy, kèm số liệu THỰC ĐO được trên bản đồ.
        /// Đây là thông báo hay gặp nhất: bản đồ còn toàn đất thô Arid (Tile_..._Arid) nên
        /// cháy rừng không thể bắt đầu ở đâu cả.
        /// </summary>
        private string BuildNoLushTilesMessage()
        {
            int total = 0;
            int lush = 0;
            int occupied = 0;
            int missingState = 0;
            string sampleName = "(trống)";

            if (worldGenerator != null && worldGenerator.MapTiles != null)
            {
                foreach (var pair in worldGenerator.MapTiles)
                {
                    if (pair.Value == null) continue;
                    total++;
                    if (sampleName == "(trống)") sampleName = pair.Value.name;

                    var st = pair.Value.GetComponent<TileDisasterState>();

                    // QUAN TRỌNG: KHÔNG được coi "không có state" là "không Lush".
                    // Các ô đã được biến đổi (tên có hậu tố _Transformed) từng bị mất
                    // TileDisasterState, và cách đếm cũ đã báo nhầm "Số ô Lush: 0" dù map
                    // có nhiều ô Lush — khiến việc chẩn đoán đi sai hướng.
                    // Nếu chưa có state thì suy từ TÊN ô (giống ResolveAffinity).
                    bool lushTile = st != null
                        ? st.IsLushTile
                        : pair.Value.name.IndexOf("arid", StringComparison.OrdinalIgnoreCase) < 0;

                    if (lushTile) lush++;
                    if (st == null) missingState++;
                    if (st != null && (st.hasObstacle || st.HasAnyDisaster)) occupied++;
                }
            }

            return "[LumiWorld Disaster] Cháy rừng không thể bắt đầu: không có ô LUSH nào trống.\n" +
                $"  • Tổng số ô trên bản đồ: {total}\n" +
                $"  • Số ô Lush (theo tên/state): {lush}   (ô đang bận: {occupied})\n" +
                $"  • Số ô THIẾU TileDisasterState: {missingState}" +
                (missingState > 0
                    ? "  <-- BẤT THƯỜNG: các ô này sẽ không được thiên tai nhận ra. " +
                      "Thường do ô sinh ra từ TransformTile trước khi có bản vá tái tạo state: " +
                      "bấm 'R' để dọn, thoát Play Mode và vào lại."
                    : string.Empty) + "\n" +
                $"  • Ví dụ tên ô hiện tại: '{sampleName}'\n" +
                $"  • CÁCH SỬA: đặt thẻ địa hình Lush (ví dụ 'Tile_..._Lush' / thẻ Forest-Habitat) lên " +
                $"một vài ô trước, rồi bấm F lại. Đất thô Arid (hậu tố '_Arid') KHÔNG có gì để cháy theo thiết kế.";
        }

        private void Update()
        {
            if (balance == null) return;
            double now = clock.NowSeconds;

            TickSpawn(now);
            TickLifecycle(now);
            TickRainWindow(now);

            if (enableTestHotkeys) HandleTestHotkeys();
        }

        private void HandleTestHotkeys()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            if (kb.oKey.wasPressedThisFrame) TestSpawn(DisasterKind.Obstacle);
            else if (kb.fKey.wasPressedThisFrame) TestSpawn(DisasterKind.ForestFire);
            else if (kb.cKey.wasPressedThisFrame) TestSpawn(DisasterKind.Cyclone);
            else if (kb.dKey.wasPressedThisFrame) TestSpawn(DisasterKind.Drought);
            else if (kb.bKey.wasPressedThisFrame) TestSpawn(DisasterKind.BarrenLand);
            else if (kb.rKey.wasPressedThisFrame) TestClearAll();
        }

        private sealed class ReflectionClock : IDisasterClock
        {
            private readonly object source;
            private readonly System.Reflection.PropertyInfo property;

            public ReflectionClock(object source)
            {
                this.source = source;
                property = source.GetType().GetProperty("NowSeconds");
            }

            public double NowSeconds
            {
                get
                {
                    if (property == null) return 0;
                    object value = property.GetValue(source);
                    return value is double d ? d : 0;
                }
            }
        }
    }
}
