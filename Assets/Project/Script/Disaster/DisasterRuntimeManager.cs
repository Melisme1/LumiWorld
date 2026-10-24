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

        // Ô đã được MƯA DẬP nhưng thiên tai vẫn đang cháy ở các ô khác.
        //
        // Cần riêng tập này vì: state.Clear(kind) chỉ gỡ Mark trên ô, nhưng ô vẫn có thể
        // là hàng xóm của một ô đang cháy khác -> SpreadDisaster sẽ bén lửa lại (xem nhánh
        // IsLushTile trong SpreadDisaster). Mưa phải mang nghĩa "ô này TẮT LỬA": không cháy,
        // không bén lại, và không truyền lửa đi tiếp cho tới khi thiên tai kết thúc hẳn.
        private readonly Dictionary<string, HashSet<HexCoordinates>> suppressedTiles =
            new Dictionary<string, HashSet<HexCoordinates>>();

        // Theo dõi component particle theo toạ độ ô để bật/tắt khói-lửa đúng trạng thái.
        // Key = toạ độ hex; value = component quản lý hiệu ứng của ô đó.
        private readonly Dictionary<HexCoordinates, TileDisasterParticles> tileParticles =
            new Dictionary<HexCoordinates, TileDisasterParticles>();

        // Ô nào đang được xử lý hiệu ứng khói/lửa thuộc instance nào (để dọn khi thiên tai kết thúc).
        // Key = instanceId; value = tập toạ độ ô đã/đang có hiệu ứng của instance đó.
        private readonly Dictionary<string, HashSet<HexCoordinates>> particleTilesByInstance =
            new Dictionary<string, HashSet<HexCoordinates>>();

        // Ô vừa bén lửa nhưng CHƯA bị phá ngay: chờ hết creationDelaySeconds mới thành ô cháy.
        // Nhờ vậy người chơi kịp thấy "ô này sắp cháy" và có thể dập bằng mưa.
        // Key = instanceId, value = fromTime (giây theo clock) -> danh sách ô.
        private readonly Dictionary<string, Dictionary<HexCoordinates, double>> pendingFire =
            new Dictionary<string, Dictionary<HexCoordinates, double>>();

        // Cửa sổ đếm mưa để kích hoạt bão.
        private readonly List<double> rainTimestamps = new List<double>();
        private bool rainTriggeredCyclonePending;

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
#if UNITY_EDITOR
            if (balance == null)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:DisasterBalance");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    balance = UnityEditor.AssetDatabase.LoadAssetAtPath<DisasterBalance>(path);
                }
            }
#endif
            if (balance != null && !balance.IsValid(out string balanceError))
                Debug.LogWarning($"[LumiWorld Disaster] DisasterBalance chưa hợp lệ: {balanceError}", balance);

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

            // Mọi thay đổi thiên tai đều đánh dấu cần lưu đảo (WorldSaveSystem tự bỏ qua khi đang thăm đảo)
            DisasterStarted += _ => WorldSaveSystem.MarkDirty();
            DisasterChanged += _ => WorldSaveSystem.MarkDirty();
            DisasterEnded += _ => WorldSaveSystem.MarkDirty();

            // Có thiên tai trong bản lưu thì dựng lại; không thì đặt lịch spawn ĐẦU TIÊN sau khi balance
            // đã chắc chắn sẵn sàng. Nhờ vậy lần sinh thiên tai đầu tiên cách thời điểm vào game đúng nhịp
            // minSpawnIntervalMinutes..maxSpawnIntervalMinutes, không còn nổ ngay frame 1.
            if (WorldLaunch.IsVisit) ApplyVisitBurnedTiles();
            else if (!TryRestoreFromSave()) ScheduleNextSpawn();
        }

        /// <summary>
        /// Đang thăm đảo: tô đen và ẩn cây trên các ô cháy theo snapshot của chủ đảo.
        /// Chỉ để xem: không có thiên tai nào chạy, không lan, không ghi gì.
        /// </summary>
        private void ApplyVisitBurnedTiles()
        {
            if (worldGenerator == null) return;
            foreach (HexPointRecord point in VisitSession.BurnedTiles)
            {
                if (point == null) continue;
                var hex = new HexCoordinates(point.q, point.r);
                if (!worldGenerator.MapTiles.TryGetValue(hex, out var tileObj) || tileObj == null) continue;
                var state = TileDisasterState.Ensure(tileObj);
                if (state == null) continue;
                state.coordinates = hex;
                state.isBurned = true;
                SuppressHabitatOnTile(tileObj);
                state.RefreshVisual();
            }
        }

        // ------------------------------------------------------------------
        // Lưu / nạp (world_<id>.json, qua WorldSaveSystem)
        // ------------------------------------------------------------------

        /// <summary>
        /// Ghi dấu thiên tai trên ô và các thiên tai đang chạy. Thời gian ghi dạng "còn bao nhiêu giây"
        /// tại lúc lưu; WorldSaveSystem ghi kèm savedAtUtc.
        /// </summary>
        public void CaptureForSave(DisasterSaveData data)
        {
            data.tiles.Clear();
            data.active.Clear();
            data.saved = true;
            data.instanceCounter = instanceCounter;
            double now = clock.NowSeconds;
            data.nextSpawnInSeconds = nextSpawnAt >= double.MaxValue / 2 ? -1 : Math.Max(0, nextSpawnAt - now);

            foreach (var state in TileDisasterState.All)
            {
                if (state == null) continue;
                bool hasData = state.hasObstacle || state.isBurned || state.aridity > 0f || state.hasReceivedWater || state.HasAnyDisaster;
                if (!hasData) continue;
                var tile = new DisasterTileRecord { q = state.coordinates.Q, r = state.coordinates.R,
                    hasObstacle = state.hasObstacle, isBurned = state.isBurned, aridity = state.aridity,
                    hasReceivedWater = state.hasReceivedWater };
                foreach (var mark in state.ActiveDisasters)
                    tile.marks.Add(new DisasterMarkRecord { kind = mark.Key.ToString(), instanceId = mark.Value });
                data.tiles.Add(tile);
            }

            foreach (var instance in activeList)
            {
                double elapsedSinceTick = Math.Max(0, now - GetLastTick(instance));
                var record = new DisasterRecord
                {
                    instanceId = instance.instanceId, kind = instance.kind.ToString(),
                    q = instance.origin.Q, r = instance.origin.R, originAffinity = (int)instance.originAffinity,
                    remainingSeconds = Math.Max(0, instance.remainingSeconds - elapsedSinceTick),
                    totalSeconds = instance.totalSeconds, rainApplied = instance.rainApplied,
                    destroyedCount = instance.destroyedCount,
                    spreadInSeconds = nextSpreadAt.TryGetValue(instance.instanceId, out var spreadAt) && spreadAt < double.MaxValue / 2
                        ? Math.Max(0, spreadAt - now) : -1
                };
                if (spreadFrontier.TryGetValue(instance.instanceId, out var frontier))
                    foreach (var hex in frontier) record.frontier.Add(new HexPointRecord { q = hex.Q, r = hex.R });
                if (suppressedTiles.TryGetValue(instance.instanceId, out var suppressed))
                    foreach (var hex in suppressed) record.suppressed.Add(new HexPointRecord { q = hex.Q, r = hex.R });
                if (pendingFire.TryGetValue(instance.instanceId, out var pending))
                    foreach (var pair in pending)
                        record.pendingFire.Add(new PendingFireRecord { q = pair.Key.Q, r = pair.Key.R,
                            elapsedSeconds = Math.Max(0, now - pair.Value) });
                data.active.Add(record);
            }
        }

        /// <summary>Dựng lại dấu và đồng hồ disaster từ save. Toàn bộ đồng hồ disaster đóng băng khi rời đảo.</summary>
        private bool TryRestoreFromSave()
        {
            if (WorldLaunch.IsVisit || worldGenerator == null) return false;
            WorldSaveSystem world = WorldSaveSystem.Instance;
            if (world == null || world.SavesBlocked || world.RestoredData == null) return false;
            DisasterSaveData data = world.RestoredData.disasters;
            if (data == null || !data.saved) return false;

            // Disaster time advances only while the owner's island is open. Remaining durations
            // and relative timers were captured when leaving, so menu/map/visit/offline time is excluded.
            const double offline = 0;
            double now = clock.NowSeconds;

            // 1. Dấu trên từng ô
            foreach (var tileRecord in data.tiles)
            {
                if (tileRecord == null) continue;
                var hex = new HexCoordinates(tileRecord.q, tileRecord.r);
                if (!worldGenerator.MapTiles.TryGetValue(hex, out var tileObj) || tileObj == null) continue;
                var state = TileDisasterState.Ensure(tileObj);
                if (state == null) continue;
                state.coordinates = hex;
                state.hasObstacle = tileRecord.hasObstacle;
                state.isBurned = tileRecord.isBurned;
                state.aridity = Mathf.Clamp01(tileRecord.aridity);
                state.hasReceivedWater = tileRecord.hasReceivedWater;
                foreach (var mark in tileRecord.marks)
                    if (mark != null && Enum.TryParse(mark.kind, out DisasterKind markKind))
                        state.Mark(markKind, mark.instanceId);
                if (state.isBurned) SuppressHabitatOnTile(tileObj);
                state.RefreshVisual();
            }

            // 2. Thiên tai đang chạy
            int maxCounter = Math.Max(instanceCounter, data.instanceCounter);
            foreach (var record in data.active)
            {
                if (record == null || string.IsNullOrEmpty(record.instanceId) ||
                    !Enum.TryParse(record.kind, out DisasterKind kind)) continue;

                var instance = new DisasterInstance
                {
                    instanceId = record.instanceId, kind = kind,
                    origin = new HexCoordinates(record.q, record.r),
                    originAffinity = (BiomeAffinity)record.originAffinity,
                    totalSeconds = record.totalSeconds,
                    remainingSeconds = Math.Max(0, record.remainingSeconds - offline),
                    rainApplied = record.rainApplied, destroyedCount = record.destroyedCount,
                    startedAtUnscaledTime = Time.unscaledTime
                };
                disasters[instance.instanceId] = instance;
                activeList.Add(instance);
                lastTick[instance.instanceId] = now;
                nextSpreadAt[instance.instanceId] = record.spreadInSeconds < 0 ? double.MaxValue : now + record.spreadInSeconds;

                var frontier = new List<HexCoordinates>();
                foreach (var point in record.frontier) if (point != null) frontier.Add(new HexCoordinates(point.q, point.r));
                spreadFrontier[instance.instanceId] = frontier;

                if (record.suppressed.Count > 0)
                {
                    EnsureSuppressedSet(instance.instanceId);
                    foreach (var point in record.suppressed)
                        if (point != null) suppressedTiles[instance.instanceId].Add(new HexCoordinates(point.q, point.r));
                }

                if (record.pendingFire.Count > 0)
                {
                    var queue = new Dictionary<HexCoordinates, double>();
                    foreach (var pending in record.pendingFire)
                    {
                        if (pending == null) continue;
                        var hex = new HexCoordinates(pending.q, pending.r);
                        queue[hex] = now - Math.Max(0, pending.elapsedSeconds); // đứng yên khi tắt game
                        ShowSmokeOnTile(instance.instanceId, hex);
                    }
                    pendingFire[instance.instanceId] = queue;
                }

                // Hình ảnh: khối vật cản ở ô gốc, lửa trên ô đang cháy, lốc trên ô bị bão
                if (worldGenerator.MapTiles.TryGetValue(instance.origin, out var originTile) && originTile != null &&
                    kind == DisasterKind.Obstacle)
                    SpawnObstacleVisual(instance, originTile);
                foreach (var state in TileDisasterState.All)
                {
                    if (state == null || !state.HasInstance(kind, instance.instanceId)) continue;
                    if (kind == DisasterKind.ForestFire) ShowFireOnTile(instance.instanceId, state.gameObject);
                    else if (kind == DisasterKind.Cyclone)
                    {
                        var fx = GetTileParticles(state.gameObject, state.coordinates);
                        if (fx != null) { fx.ShowCyclone(); TrackParticleTile(instance.instanceId, state.coordinates); }
                    }
                }

                maxCounter = Math.Max(maxCounter, ParseCounter(instance.instanceId));
                DisasterChanged?.Invoke(instance);
            }
            instanceCounter = maxCounter;

            // 3. Lịch sinh thiên tai mới đứng yên khi tắt game
            if (data.nextSpawnInSeconds >= 0) nextSpawnAt = now + data.nextSpawnInSeconds;
            else ScheduleNextSpawn();

            Debug.Log($"[LumiWorld Disaster] Đã dựng lại {activeList.Count} thiên tai và {data.tiles.Count} ô có dấu; đồng hồ được tiếp tục từ lúc rời đảo.", this);
            return true;
        }

        // "ForestFire_0007" -> 7
        private static int ParseCounter(string instanceId)
        {
            int cut = instanceId != null ? instanceId.LastIndexOf('_') : -1;
            return cut >= 0 && int.TryParse(instanceId.Substring(cut + 1), out int value) ? value : 0;
        }

        /// <summary>Gắn TileDisasterState lên mọi ô hiện có để hệ thống khác truy vấn được ngay.</summary>
        private void EnsureTileDisasterStates()
        {
            if (worldGenerator == null || worldGenerator.MapTiles == null) return;
            foreach (var pair in worldGenerator.MapTiles)
            {
                if (pair.Value == null) continue;
                var state = TileDisasterState.Ensure(pair.Value);
                if (state == null) continue;

                // Ghi toạ độ ô vào state: SpreadDisaster/ApplyRain cần biết ô nằm ở đâu
                // để xử lý theo vị trí (mưa dập lửa tại ô, hàng đợi chờ bén lửa).
                state.coordinates = pair.Key;

                if (state.affinity == BiomeAffinity.None)
                    state.affinity = ResolveAffinity(pair.Value);

                // Chuẩn bị sẵn component particle cho mỗi ô (chưa bật hiệu ứng gì).
                var fx = TileDisasterParticles.Ensure(pair.Value);
                if (fx != null)
                {
                    fx.Configure(balance != null ? balance.smokeParticlePrefab : null,
                                 balance != null ? balance.fireParticlePrefab : null,
                                 balance != null ? balance.cycloneParticlePrefab : null,
                                 balance != null ? balance.cycloneRainParticlePrefab : null);
                    tileParticles[pair.Key] = fx;
                }
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

            if (!balance.TryPickRandomKind(BiomeAffinity.None, rng, out DisasterKind kind))
            {
                ScheduleNextSpawn();
                return;
            }

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
                    if (balance.TryPickRandomKind(affinity, rng, out DisasterKind fallbackKind))
                        SpawnDisaster(fallbackKind, origin);
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
                // Đã có thiên tai, đã là vật cản, hoặc đã bị cháy thì bỏ qua để tránh chồng đè.
                if (state != null && (state.hasObstacle || state.HasAnyDisaster || state.isBurned)) continue;

                // CHÁY RỪNG: chỉ nhận ô CHẮC CHẮN Lush.
                if (requireLushForFire && (state == null || !state.IsLushTile)) continue;

                // ĐIỀU KIỆN SINH THÁI: Chỉ kích hoạt Cháy rừng khi cụm cây KHÔNG tiếp giáp ô Nước
                if (requireLushForFire && ClusterTouchesWater(pair.Key)) continue;

                candidates.Add(pair);
            }

            if (candidates.Count == 0) return false;

            var pick = candidates[rng.Next(candidates.Count)];
            origin = pick.Key;
            affinity = ResolveAffinity(pick.Value);
            return true;
        }

        /// <summary>
        /// Kiểm tra xem cụm cây/ô môi trường tại toạ độ này có tiếp giáp với bất kỳ ô Nước nào không.
        /// Cháy rừng chỉ có thể bùng phát khi cụm cây KHÔNG tiếp giáp ô Nước.
        /// </summary>
        public bool ClusterTouchesWater(HexCoordinates startHex)
        {
            if (worldGenerator == null || worldGenerator.MapTiles == null) return false;

            var visited = new HashSet<HexCoordinates>();
            var queue = new Queue<HexCoordinates>();
            queue.Enqueue(startHex);
            visited.Add(startHex);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                for (int dir = 0; dir < 6; dir++)
                {
                    HexCoordinates neighborCoord = current.GetNeighbor(dir);

                    if (worldGenerator.MapTiles.TryGetValue(neighborCoord, out var neighborObj) && neighborObj != null)
                    {
                        if (IsWaterTile(neighborObj))
                        {
                            return true; // Cụm cây tiếp giáp với ô Nước!
                        }

                        // Nếu là ô cùng cụm môi trường cây xanh (Lush) và chưa duyệt qua thì mở rộng tìm kiếm
                        if (!visited.Contains(neighborCoord))
                        {
                            var neighborState = neighborObj.GetComponent<TileDisasterState>();
                            if (neighborState != null && neighborState.IsLushTile)
                            {
                                visited.Add(neighborCoord);
                                queue.Enqueue(neighborCoord);
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Kiểm tra ô có phải ô Nước (River, Water, Lilywater, Streamstone...) hay không.
        /// </summary>
        public bool IsWaterTile(GameObject tileObj)
        {
            if (tileObj == null) return false;

            string name = tileObj.name;
            if (name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("river", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("ocean", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("lake", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var state = tileObj.GetComponent<TileDisasterState>();
            if (state != null)
            {
                if (state.affinity == BiomeAffinity.Lilywater ||
                    state.affinity == BiomeAffinity.Streamstone ||
                    state.affinity == BiomeAffinity.Reedmarsh)
                {
                    return true;
                }
            }

            var placedCards = tileObj.GetComponentsInChildren<PlacedCard>();
            for (int i = 0; i < placedCards.Length; i++)
            {
                var pc = placedCards[i];
                if (pc != null && pc.cardData != null)
                {
                    string cName = pc.cardData.cardName;
                    if (cName != null && (
                        cName.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cName.IndexOf("river", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cName.IndexOf("lake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cName.IndexOf("stream", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return true;
                    }
                }
            }

            return false;
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
                // Chốt môi trường NGAY LÚC SINH (xem chú thích ở SpreadDisaster).
                // Với cháy rừng, đây là biome duy nhất được phép lan sang.
                originAffinity = ResolveAffinity(originTile),
                totalSeconds = balance.ResolveDuration(kind),
                startedAtUnscaledTime = Time.unscaledTime
            };
            instance.remainingSeconds = instance.totalSeconds;

            disasters[instance.instanceId] = instance;
            activeList.Add(instance);
            nextSpreadAt[instance.instanceId] = clock.NowSeconds + balance.ResolveSpreadInterval(kind);
            spreadFrontier[instance.instanceId] = new List<HexCoordinates> { origin };

            ApplyToTile(instance, originTile);

            // Ô gốc cũng trải qua thời gian khói báo trước như mọi ô lửa lan tới.
            // Chỉ sau FireCreationDelaySeconds mới chuyển thành ô cháy thật trong ResolvePendingFire.
            if (IsForestFire(instance)) EnqueuePendingFire(instance, origin);

            // Tô lại ngay lúc sinh để ô gốc đổi màu tức thì.
            // DestroyTile đã tự RefreshVisual() nên lần này chỉ là bước chốt an toàn.
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

            if (instance.kind == DisasterKind.Cyclone && TryResolveTileHex(tileObj, out var cycloneHex))
            {
                var fx = GetTileParticles(tileObj, cycloneHex);
                if (fx != null)
                {
                    fx.ShowCyclone();
                    TrackParticleTile(instance.instanceId, cycloneHex);
                }
            }

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

                // Nướng các ô đang chờ bén lửa tới hạn (cháy rừng).
                // Đặt SAU SpreadDisaster để ô vào hàng đợi ở nhịp này được đếm giờ ngay từ nhịp này.
                ResolvePendingFire(instance, now);

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
            if (worldGenerator == null) return;
            if (!spreadFrontier.TryGetValue(instance.instanceId, out var frontier)) return;

            var nextFrontier = new List<HexCoordinates>();

            EnsureSuppressedSet(instance.instanceId);

            // ĐẾM MỘT LẦN cho cả nhịp lan rồi tự tăng dần.
            //
            // Bản cũ gọi CountTiles() lại NGAY SAU mỗi lần DestroyTile. Nhưng DestroyTile chỉ
            // Mark thêm instanceId vào ô mới, còn ô gốc thì KHÔNG được Mark (ApplyToTile chỉ
            // Mark cho kind khác ForestFire) -> lần đếm đầu tiên trong nhịp này trả về 0,
            // khiến vòng lặp chạy thêm 1 nhịp nữa để đủ "maxTiles" => tổng số ô bị cháy luôn
            // HỤT ĐÚNG 1 so với maxTiles (đặt 10 thì chỉ thấy 9 ô).
            int count = CountTiles(instance.instanceId);

            // Ô ĐANG CHỜ BÉN LỬA cũng chiếm suất trong maxTiles: nếu không cộng vào,
            // mỗi nhịp lan sẽ nhồi thêm ô mới vượt trần trong khi các ô cũ còn trong hàng đợi.
            if (IsForestFire(instance)) count += CountPendingFire(instance.instanceId);

            // Ô GỐC nay cũng được DestroyTile (xem SpawnDisaster) nên nó ĐÃ nằm trong
            // CountTiles và trong CountPendingFire khi vào hàng đợi -> KHÔNG cộng +1 nữa,
            // nếu không trần maxTiles sẽ bị chặn sớm đúng 1 ô.

            if (count >= maxTiles) return;

            foreach (var hex in frontier)
            {
                // Ô đã được Rain dập có thể vẫn còn trong frontier từ nhịp lan trước,
                // dù hàng đợi bén lửa đã bị xóa. Không cho ô đó tiếp tục làm nguồn lan.
                if (IsSuppressed(instance.instanceId, hex)) continue;
                if (count >= maxTiles) break;

                for (int dir = 0; dir < 6; dir++)
                {
                    if (count >= maxTiles) break;

                    HexCoordinates neighbor = hex.GetNeighbor(dir);
                    if (!worldGenerator.MapTiles.TryGetValue(neighbor, out var neighborTile) || neighborTile == null) continue;

                    var neighborState = neighborTile.GetComponent<TileDisasterState>();
                    if (neighborState != null && neighborState.Has(instance.kind)) continue;
                    // Không lan vào ô đã có vật cản để tránh chồng state.
                    if (neighborState != null && neighborState.hasObstacle) continue;
                    // Ô đã bị MƯA DẬP: tắt lửa vĩnh viễn trong vụ này -> vừa không bén lại,
                    // vừa đóng vai trò "tường chắn" không cho lửa nhảy qua ô này.
                    if (IsSuppressed(instance.instanceId, neighbor)) continue;

                    if (instance.kind == DisasterKind.ForestFire)
                    {
                        // 1. CHỈ ô CÓ CÂY CỎ (môi trường sống thật) mới bắt lửa.
                        //    Ô đất trống không có gì để cháy -> chặn ngay, không tốn gì thêm.
                        //    IsLushTile nay đòi bằng chứng khẳng định (Habitat_* / thẻ Terrain),
                        //    nên cả ô Arid đã hứng mưa mà chưa trồng gì cũng không bắt lửa.
                        var neighborStateForFire = neighborState;
                        if (neighborStateForFire == null)
                        {
                            neighborStateForFire = neighborTile.GetComponent<TileDisasterState>();
                            if (neighborStateForFire == null) continue;
                        }
                        if (!neighborStateForFire.IsLushTile) continue;

                        // 2. Chỉ lan trong CÙNG MỘT môi trường (Leafwood chỉ cháy sang Leafwood...).
                        //
                        // Môi trường gốc được CHỐT MỘT LẦN ở SpawnDisaster và lưu vào
                        // instance.originAffinity, KHÔNG đọc lại từ ô gốc mỗi nhịp lan.
                        // Lý do: ô gốc bị cháy sẽ mất dấu vết môi trường (props bị ẩn, tên có thể
                        // đổi khi TransformTile) nên suy lại sẽ ra None, và điều kiện
                        // "None != affinity" sẽ CHẶN SẠCH mọi ô kề -> lửa chết yểu ngay nhịp đầu.
                        // Chốt sẵn còn đảm bảo vùng cháy giữ nguyên một biome suốt vụ cháy.
                        BiomeAffinity originAffinity = instance.originAffinity;
                        if (originAffinity == BiomeAffinity.None) continue;
                        if (ResolveAffinity(neighborTile) != originAffinity) continue;
                    }
                    else if (!balance.AffectsBiome(instance.kind, ResolveAffinity(neighborTile)))
                    {
                        continue;
                    }

                    // ApplyToTile đã tự RefreshVisual() ở cuối; DestroyTile cũng vậy.
                    if (instance.kind == DisasterKind.ForestFire)
                    {
                        // CHÁY RỪNG: ô KHÔNG bị phá ngay mà vào HÀNG ĐỢI chờ bén lửa.
                        // Sau creationDelaySeconds (mặc định 5s) nó mới thực sự cháy.
                        // Trong khoảng đó người chơi có thể dập bằng mưa.
                        EnqueuePendingFire(instance, neighbor);
                    }
                    else
                    {
                        // Truyền GameObject của ô kề (neighbor chỉ là toạ độ).
                        ApplyToTile(instance, neighborTile);
                    }

                    // Cập nhật NGAY sau khi phủ xong ô, không đếm lại từ đầu.
                    // Với cháy rừng, ô đang chờ cũng tính vào trần maxTiles.
                    count++;

                    nextFrontier.Add(neighbor);
                }
            }

            frontier.Clear();
            frontier.AddRange(nextFrontier);
            DisasterChanged?.Invoke(instance);
        }

        private static bool IsForestFire(DisasterInstance instance)
        {
            return instance != null && instance.kind == DisasterKind.ForestFire;
        }

        // ------------------------------------------------------------------
        // Hiệu ứng Particle (khói / lửa) cho ô cháy rừng
        // ------------------------------------------------------------------

        /// <summary>
        /// Lấy toạ độ hex của một ô một cách chắc chắn:
        ///  1. Từ TileDisasterState.coordinates (đã ghi sẵn ở EnsureTileDisasterStates),
        ///  2. Nếu chưa có thì lấy từ HexTileInfo.coordinates (do HexWorldGenerator gán).
        /// Trả về false nếu không xác định được (khi đó bỏ qua hiệu ứng particle cho ô này).
        /// </summary>
        private static bool TryResolveTileHex(GameObject tileObj, out HexCoordinates hex)
        {
            hex = default;
            if (tileObj == null) return false;

            var state = tileObj.GetComponent<TileDisasterState>();
            if (state != null && (state.coordinates.Q != 0 || state.coordinates.R != 0))
            {
                hex = state.coordinates;
                return true;
            }

            var info = tileObj.GetComponent<HexTileInfo>();
            if (info != null)
            {
                hex = info.coordinates;
                return true;
            }

            if (state != null)
            {
                hex = state.coordinates;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Lấy component particle của ô, tự gắn nếu thiếu (trường hợp ô sinh sau khi
        /// EnsureTileDisasterStates đã chạy). Đồng thời nạp prefab từ balance nếu chưa có.
        /// </summary>
        private TileDisasterParticles GetTileParticles(GameObject tileObj, HexCoordinates hex)
        {
            if (tileObj == null) return null;

            if (tileParticles.TryGetValue(hex, out var fx) && fx != null) return fx;

            fx = TileDisasterParticles.Ensure(tileObj);
            if (fx != null)
            {
                fx.Configure(balance != null ? balance.smokeParticlePrefab : null,
                             balance != null ? balance.fireParticlePrefab : null,
                             balance != null ? balance.cycloneParticlePrefab : null,
                             balance != null ? balance.cycloneRainParticlePrefab : null);
                tileParticles[hex] = fx;
            }
            return fx;
        }

        /// <summary>Ghi nhận ô có hiệu ứng particle thuộc một instance để dọn khi kết thúc.</summary>
        private void TrackParticleTile(string instanceId, HexCoordinates hex)
        {
            if (string.IsNullOrEmpty(instanceId)) return;
            if (!particleTilesByInstance.TryGetValue(instanceId, out var set))
            {
                set = new HashSet<HexCoordinates>();
                particleTilesByInstance[instanceId] = set;
            }
            set.Add(hex);
        }

        /// <summary>
        /// Bật KHÓI cho ô "sắp cháy" (đang chờ bén lửa). Đúng kịch bản: khói xuất hiện
        /// TRƯỚC khi lửa bùng, báo hiệu cho người chơi kịp dùng mưa.
        /// </summary>
        private void ShowSmokeOnTile(string instanceId, HexCoordinates hex)
        {
            if (worldGenerator == null) return;
            if (!worldGenerator.MapTiles.TryGetValue(hex, out var tile) || tile == null) return;

            var fx = GetTileParticles(tile, hex);
            if (fx == null) return;

            fx.ShowSmokeOnly();
            TrackParticleTile(instanceId, hex);
        }

        /// <summary>Bật KHÓI + LỬA cho ô đã bén lửa thật (đã cháy).</summary>
        private void ShowFireOnTile(string instanceId, GameObject tileObj)
        {
            if (tileObj == null) return;
            if (!TryResolveTileHex(tileObj, out var hex)) return;

            var fx = GetTileParticles(tileObj, hex);
            if (fx == null) return;

            fx.ShowSmokeAndFire();
            TrackParticleTile(instanceId, hex);
        }

        /// <summary>Tắt và hủy hiệu ứng particle của một ô (mưa dập hoặc dọn dẹp).</summary>
        private void ClearParticlesOnTile(HexCoordinates hex)
        {
            if (tileParticles.TryGetValue(hex, out var fx) && fx != null)
                fx.ClearAll();
        }

        /// <summary>Tắt và hủy mọi hiệu ứng particle của một đám cháy (khi kết thúc).</summary>
        private void ClearParticlesForInstance(string instanceId)
        {
            if (particleTilesByInstance.TryGetValue(instanceId, out var set))
            {
                foreach (var hex in set) ClearParticlesOnTile(hex);
                particleTilesByInstance.Remove(instanceId);
            }
        }

        // ------------------------------------------------------------------
        // Ô chờ bén lửa + ô đã bị mưa dập (cháy rừng)
        // ------------------------------------------------------------------

        /// <summary>Số giây một ô nằm trong hàng đợi trước khi thực sự thành ô cháy.</summary>
        private const float FireCreationDelaySeconds = 5f;

        private void EnqueuePendingFire(DisasterInstance instance, HexCoordinates hex)
        {
            if (!pendingFire.TryGetValue(instance.instanceId, out var queue))
            {
                queue = new Dictionary<HexCoordinates, double>();
                pendingFire[instance.instanceId] = queue;
            }
            if (queue.ContainsKey(hex)) return;
            queue[hex] = clock.NowSeconds;

            // Ô "sắp cháy": bật KHÓI ngay (báo hiệu trước), chưa có lửa.
            // Đây là kịch bản cốt lõi: khói xuất hiện TRƯỚC khi lửa bùng.
            ShowSmokeOnTile(instance.instanceId, hex);
        }

        private int CountPendingFire(string instanceId)
        {
            return pendingFire.TryGetValue(instanceId, out var queue) ? queue.Count : 0;
        }

        /// <summary>Trả về true nếu ô đang trong giai đoạn khói báo trước của đám cháy.</summary>
        public bool IsTilePendingFire(GameObject tileObj)
        {
            if (!TryResolveTileHex(tileObj, out var hex)) return false;
            foreach (var queue in pendingFire.Values)
            {
                if (queue.ContainsKey(hex)) return true;
            }
            return false;
        }

        /// <summary>
        /// Biến các ô chờ đã hết hạn thành ô cháy thật (DestroyTile).
        /// Chỉ cháy rừng dùng cơ chế này.
        /// </summary>
        private void ResolvePendingFire(DisasterInstance instance, double now)
        {
            if (!IsForestFire(instance)) return;
            if (!pendingFire.TryGetValue(instance.instanceId, out var queue) || queue.Count == 0) return;
            if (worldGenerator == null) return;

            List<HexCoordinates> matured = null;
            foreach (var pair in queue)
            {
                if (now - pair.Value >= FireCreationDelaySeconds)
                {
                    (matured ??= new List<HexCoordinates>()).Add(pair.Key);
                }
            }
            if (matured == null) return;

            foreach (var hex in matured)
            {
                queue.Remove(hex);

                // Ô có thể đã bị mưa dập trong lúc chờ -> không đốt nữa.
                if (IsSuppressed(instance.instanceId, hex)) continue;
                if (!worldGenerator.MapTiles.TryGetValue(hex, out var tile) || tile == null) continue;

                DestroyTile(instance, tile);
            }
        }

        private void EnsureSuppressedSet(string instanceId)
        {
            if (!suppressedTiles.ContainsKey(instanceId))
                suppressedTiles[instanceId] = new HashSet<HexCoordinates>();
        }

        private bool IsSuppressed(string instanceId, HexCoordinates hex)
        {
            return suppressedTiles.TryGetValue(instanceId, out var set) && set.Contains(hex);
        }

        /// <summary>
        /// Mưa dập lửa TẠI MỘT Ô: ô thành "đã tắt" — không cháy, không bén lại,
        /// không nhận chờ bén lửa, và là tường chắn không cho lửa nhảy qua.
        /// </summary>
        private void SuppressFireTile(string instanceId, HexCoordinates hex)
        {
            EnsureSuppressedSet(instanceId);
            suppressedTiles[instanceId].Add(hex);

            // Nếu ô đang trong hàng đợi chờ cháy -> rút ra luôn.
            if (pendingFire.TryGetValue(instanceId, out var queue)) queue.Remove(hex);

            // Ô khói đã có thể được đưa vào frontier ở nhịp lan trước đó. Gỡ nó khỏi
            // frontier để nó không thể bén lửa hoặc tiếp tục truyền lửa ở các nhịp sau.
            if (spreadFrontier.TryGetValue(instanceId, out var frontier))
                frontier.RemoveAll(candidate => candidate.Equals(hex));

            // Mưa dập: tắt và hủy hiệu ứng khói/lửa tại ô này.
            ClearParticlesOnTile(hex);

            // Nếu ô đã cháy -> hồi phục cờ trên ô, như bình thường khi cháy bị dập.
            if (worldGenerator != null && worldGenerator.MapTiles.TryGetValue(hex, out var tile) && tile != null)
            {
                if (TileDisasterState.TryGet(tile, out var st) && st != null)
                {
                    if (st.Clear(DisasterKind.ForestFire, instanceId))
                    {
                        st.isBurned = false;
                        st.aridity = 0f;
                        st.RefreshVisual();
                    }
                }
            }
        }

        /// <summary>Có ô nào của đám cháy này đang chờ bén lửa hoặc đã bị mưa dập không.</summary>
        private bool HasFireTrackedTiles(string instanceId)
        {
            return CountPendingFire(instanceId) > 0 ||
                (suppressedTiles.TryGetValue(instanceId, out var set) && set.Count > 0);
        }

        /// <summary>
        /// Ô cháy rừng này còn "sống" không: đã bị đốt (còn Mark) hoặc đang chờ bén lửa.
        /// Dùng để quyết định đám cháy đã tắt hẳn hay chưa.
        /// </summary>
        private bool FireStillActive(DisasterInstance instance)
        {
            if (!IsForestFire(instance)) return true;
            return CountTiles(instance.instanceId) > 0 || CountPendingFire(instance.instanceId) > 0;
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
            // Ô vừa cháy: dấu trên ô và thẻ thú trả về tay phải vào bản lưu đảo
            WorldSaveSystem.MarkDirty();
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

                // Ô đã bén lửa thật: bật KHÓI + LỬA (khói đã bật từ lúc chờ, lửa mới xuất hiện).
                ShowFireOnTile(instance.instanceId, tileObj);
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

                // Hoàn thẻ Thú về tay người chơi. Bỏ thú khỏi bản lưu đảo ngay trong frame này
                // (Destroy chỉ xóa object sau khi world đã ghi), để không vừa trên đảo vừa trên tay.
                WorldSaveSystem.ForgetPlacement(card);
                if (CardDeskController.Instance != null)
                    CardDeskController.Instance.AddRewardCard(card.cardData);
                Destroy(card.gameObject);
            }
        }

        /// <summary>
        /// Ẩn sạch thực vật/cầu nối trên ô vừa cháy (không xóa đất lục giác gốc).
        ///
        /// VÌ SAO PHẢI QUÉT THEO VỊ TRÍ, KHÔNG PHẢI THEO CÂY CON:
        ///  Cây cỏ của ô nằm trong container "Habitat_*" (con của ô), nhưng props viền nối
        ///  "ConnectorProp_*" lại do HexBiomeClusterConnector sinh ra dưới
        ///  "BiomeConnectionsContainer" (một GameObject riêng của connector, KHÔNG nằm trong ô).
        ///  Bản cũ chỉ duyệt con trực tiếp của ô nên KHÔNG BAO GIỜ thấy được ConnectorProp_*,
        ///  khiến trên ô cháy đen vẫn còn hoa cỏ mọc ở mép.
        ///
        /// Cách đúng: gom các prop Ở GẦN TÂM Ô theo khoảng cách trên mặt phẳng XZ. Bán kính dùng
        /// inner-radius của hex (1.0f) để không vơ sang prop của ô kề (tâm 2 ô cách nhau 2.0f).
        /// </summary>
        private const float HabitatSuppressRadius = 1.0f;

        private void SuppressHabitatOnTile(GameObject tileObj)
        {
            if (tileObj == null) return;

            Vector3 center = tileObj.transform.position;
            float radiusSqr = HabitatSuppressRadius * HabitatSuppressRadius;

            // 1. Container habitat nằm ngay trong ô (cây trồng từ thẻ Terrain).
            var own = tileObj.transform;
            for (int i = 0; i < own.childCount; i++)
            {
                Transform child = own.GetChild(i);
                if (child != null && child.name.StartsWith("Habitat_", StringComparison.Ordinal))
                    child.gameObject.SetActive(false);
            }

            // 2. Props/cầu nối nằm ngoài ô ("BiomeConnectionsContainer") -> lọc theo khoảng cách.
            //    Bắt luôn trường hợp connector chưa kịp tạo container (đang ở dưới transform của nó).
            var connector = HexBiomeClusterConnector.Instance;
            if (connector == null) return;

            TileDisasterState tileState = tileObj.GetComponent<TileDisasterState>();
            if (tileState != null)
                connector.RemoveConnectionsForHex(tileState.coordinates);

            Transform container = connector.ConnectionsContainer;
            if (container == null) return;

            var props = container.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < props.Length; i++)
            {
                Transform prop = props[i];
                if (prop == null || prop == container) continue;

                Vector3 delta = prop.position - center;
                float sqr = delta.x * delta.x + delta.z * delta.z;
                if (sqr > radiusSqr) continue;

                prop.gameObject.SetActive(false);
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

                // SỬA ĐỔI: Khi đám cháy rừng kết thúc:
                // Nếu ô đã bị thiêu rụi (isBurned == true) mà chưa được dập bằng Mưa,
                // ô đó SẼ GIỮ NGUYÊN trạng thái isBurned = true vĩnh viễn (tro tàn),
                // cây cối bị ẩn/xóa sạch và chuyển sang màu đen tro tàn cho tới khi người chơi dùng thẻ Mưa!
                if (instance.kind == DisasterKind.ForestFire)
                {
                    if (state.isBurned)
                    {
                        // Giữ nguyên isBurned = true!
                        state.aridity = 1f;
                        SuppressHabitatOnTile(state.gameObject);
                    }
                }

                // BarrenLand: dọn aridity KHÔNG phụ thuộc rainApplied. Trước đây nếu thiên tai
                // tự hết hạn mà chưa hứng mưa nào thì aridity kẹt ở 1 -> ô khô vĩnh viễn.
                // Việc cứu bằng mưa đã được xử lý ngay trong ApplyRain (EndDisaster ngay khi đủ liều).
                if (instance.kind == DisasterKind.BarrenLand) state.aridity = 0f;

                state.RefreshVisual();
            }

            ClearVisuals(instance);
            ClearParticlesForInstance(instance.instanceId);

            disasters.Remove(instance.instanceId);
            activeList.Remove(instance);
            nextSpreadAt.Remove(instance.instanceId);
            spreadFrontier.Remove(instance.instanceId);
            lastTick.Remove(instance.instanceId);
            pendingFire.Remove(instance.instanceId);
            suppressedTiles.Remove(instance.instanceId);

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

            // Ô đang bốc khói có thể chưa được Mark(ForestFire) cho tới khi hết thời gian chuẩn bị.
            // Vẫn cho phép thẻ mưa nhắm vào ô này để hủy hàng đợi bén lửa.
            if (TryResolveTileHex(tileObj, out var targetHex))
            {
                foreach (var pair in pendingFire)
                {
                    if (!pair.Value.ContainsKey(targetHex) ||
                        !disasters.TryGetValue(pair.Key, out var fireInstance) ||
                        !IsForestFire(fireInstance) ||
                        !balance.CanBeCuredByRain(DisasterKind.ForestFire) ||
                        !KindAcceptsRain(DisasterKind.ForestFire, strength))
                    {
                        continue;
                    }

                    fireInstance.rainApplied++;
                    SuppressFireTile(pair.Key, targetHex);
                    anyAccepted = true;

                    if (!FireStillActive(fireInstance))
                        EndDisaster(fireInstance);
                    else
                        DisasterChanged?.Invoke(fireInstance);
                    break;
                }
            }

            bool activeFireCannotBeCured = false;
            if (state.ActiveDisasters.TryGetValue(DisasterKind.ForestFire, out var activeFireId) &&
                disasters.TryGetValue(activeFireId, out var activeFire) &&
                !balance.CanBeCuredByRain(DisasterKind.ForestFire))
            {
                activeFireCannotBeCured = true;
            }

            // Rain có thể phục hồi tro tàn sau khi đám cháy đã kết thúc, dù rule không cho dập lửa khi đang chạy.
            if (state.isBurned && !activeFireCannotBeCured)
            {
                state.isBurned = false;
                state.aridity = 0f;
                state.hasReceivedWater = true;

                // Tắt hiệu ứng khói/lửa còn sót trên ô tro tàn.
                ClearParticlesOnTile(state.coordinates);

                // Dọn sạch hoàn toàn các container cây cối đã chết/cháy dở trên ô
                var own = tileObj.transform;
                for (int i = own.childCount - 1; i >= 0; i--)
                {
                    Transform child = own.GetChild(i);
                    if (child != null && (child.name.StartsWith("Habitat_", StringComparison.Ordinal) ||
                                          child.name.StartsWith("PlacedCard_", StringComparison.Ordinal)))
                    {
                        // Bỏ thẻ khỏi bản lưu đảo ngay (Destroy chỉ xóa object sau khi world đã ghi frame này),
                        // để mở lại game không hiện thẻ ma trên ô vừa hồi phục
                        foreach (var placed in child.GetComponentsInChildren<PlacedCard>(true))
                            WorldSaveSystem.ForgetPlacement(placed);
                        Destroy(child.gameObject);
                    }
                }

                var tileInfo = tileObj.GetComponent<HexTileInfo>();
                if (tileInfo != null)
                {
                    tileInfo.isOccupied = false;
                }

                // Ô tro tàn đã hồi phục và thẻ trên ô đã bị dọn: ghi lại đảo
                WorldSaveSystem.MarkDirty();
                state.RefreshVisual();
                anyAccepted = true;
            }

            // 1. Khô cằn: BẮT BUỘC mưa lớn mới thấm.
            if (state.IsArid && balance.CanBeCuredByRain(DisasterKind.BarrenLand))
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
                if (!balance.CanBeCuredByRain(kind)) continue;
                if (!KindAcceptsRain(kind, strength)) continue;

                instance.rainApplied++;
                anyAccepted = true;

                // CHÁY RỪNG: đủ 1 thẻ mưa là TẮT LỬA TẠI Ô NÀY ngay lập tức (không chờ gom
                // đủ rainToCure):
                //   - Rút ô khỏi hàng đợi chờ bén lửa (nếu đang chờ).
                //   - Hạ isBurned + gỡ Mark trên ô nếu đã cháy.
                //   - Ghi ô vào danh sách "đã dập" để KHÔNG bén lại và không cho lửa nhảy qua.
                //
                // Các ô ĐANG CHÁY KHÁC của cùng đám cháy vẫn tiếp tục cháy và lan bình thường
                // — mưa chỉ cứu được đúng vùng được tưới. Khi MỌI ô đã tắt thì đám cháy kết thúc.
                if (kind == DisasterKind.ForestFire)
                {
                    SuppressFireTile(instanceId, state.coordinates);

                    // Chỉ kết thúc khi KHÔNG còn ô cháy VÀ không còn ô nào đang chờ bén lửa.
                    // Nếu chỉ đếm ô đã cháy, một đám cháy đang có ô trong hàng đợi sẽ bị
                    // kết thúc oan, và hàng đợi treo lại (ô không bao giờ cháy).
                    if (!FireStillActive(instance))
                        EndDisaster(instance);
                    else
                        DisasterChanged?.Invoke(instance);

                    continue;
                }

                if (instance.rainApplied >= balance.ResolveRainToCure(kind))
                    EndDisaster(instance);
                else
                {
                    state.RefreshVisual();
                    DisasterChanged?.Invoke(instance);
                }
            }

            state.RefreshVisual();
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

        /// <summary>Đếm một thẻ Rain đã được dùng thành công, dù thẻ chữa disaster hay cải tạo ô đất.</summary>
        public void RegisterRainCardUse()
        {
            RecordRainForCyclone();
        }

        private void TickRainWindow(double now)
        {
            if (balance == null) return;
            double window = balance.cycloneRainWindowSeconds;
            rainTimestamps.RemoveAll(t => now - t > window);

            if (!balance.spawnCycloneOnRainOverdose) return;
            var cycloneRule = balance.GetRule(DisasterKind.Cyclone);
            if (cycloneRule == null || !cycloneRule.enabled) return;
            if (!rainTriggeredCyclonePending && rainTimestamps.Count >= balance.cycloneRainThreshold)
            {
                rainTimestamps.Clear();
                rainTriggeredCyclonePending = true;
            }
            if (!rainTriggeredCyclonePending) return;
            if (disasters.Count >= Mathf.Max(0, balance.maxConcurrentDisasters)) return;

            // Giữ yêu cầu đã kích hoạt cho tới khi còn slot và tìm được ô hợp lệ.
            if (TryPickSpawnTile(out HexCoordinates origin, out _))
            {
                DisasterInstance cyclone = SpawnDisaster(DisasterKind.Cyclone, origin);
                if (cyclone != null)
                {
                    rainTriggeredCyclonePending = false;
                    Debug.Log("<color=#5AB8FF>[LumiWorld Disaster] Mật độ mưa vượt ngưỡng — sinh Lốc xoáy!</color>");
                }
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

            // Ô ĐANG CHỜ BÉN LỬA chưa có Mark nên vòng lặp trên không thấy. Vẫn phải đưa vào
            // danh sách highlight: đây chính là cửa sổ để người chơi thấy "sắp cháy" và dập mưa.
            if (IsForestFire(instance) && pendingFire.TryGetValue(instance.instanceId, out var queue))
            {
                foreach (var hex in queue.Keys)
                {
                    if (worldGenerator.MapTiles.TryGetValue(hex, out var tile) && tile != null &&
                        !result.Contains(tile))
                    {
                        result.Add(tile);
                    }
                }
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

            // Dọn thêm các map dùng chung (hàng đợi chờ bén lửa, ô đã dập) đề phòng instance
            // không còn trong activeList nhưng entry vẫn sót.
            pendingFire.Clear();
            suppressedTiles.Clear();

            // Tắt mọi hiệu ứng particle khói/lửa còn sót lại trên bản đồ.
            foreach (var fx in tileParticles.Values)
            {
                if (fx != null) fx.ClearAll();
            }
            particleTilesByInstance.Clear();

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
            int protectedByWater = 0;
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

                    bool lushTile = st != null
                        ? st.IsLushTile
                        : HexBiomeClusterConnector.GetHabitatCardOnTile(pair.Value) != null;

                    if (lushTile)
                    {
                        lush++;
                        if (ClusterTouchesWater(pair.Key)) protectedByWater++;
                    }
                    if (st == null) missingState++;
                    if (st != null && (st.hasObstacle || st.HasAnyDisaster || st.isBurned)) occupied++;
                }
            }

            string waterNote = protectedByWater > 0
                ? $"  • Số ô Lush được BẢO VỆ bởi nguồn Nước tiếp giáp: {protectedByWater} (không thể bén lửa theo quy tắc sinh thái)\n"
                : string.Empty;

            return "[LumiWorld Disaster] Cháy rừng không thể bắt đầu: không có ô LUSH nào hợp lệ để bùng phát.\n" +
                $"  • Tổng số ô trên bản đồ: {total}\n" +
                $"  • Số ô Lush (có cây cỏ / thẻ Terrain): {lush}   (ô đang bận/đã cháy: {occupied})\n" +
                waterNote +
                $"  • Số ô THIẾU TileDisasterState: {missingState}\n" +
                $"  • Ví dụ tên ô hiện tại: '{sampleName}'\n" +
                $"  • QUY TẮC: Cháy rừng chỉ bùng phát trên cụm cây khô ráo KHÔNG tiếp giáp với nguồn Nước.";
        }

        private void Update()
        {
            // Đảo đang xem của người khác: thiên tai đứng yên, không sinh mới
            if (balance == null || WorldLaunch.IsVisit) return;
            double now = clock.NowSeconds;

            TickSpawn(now);
            TickLifecycle(now);
            TickRainWindow(now);

            // Phím tắt thử thiên tai chỉ chạy trong Editor
#if UNITY_EDITOR
            if (enableTestHotkeys) HandleTestHotkeys();
#endif
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
