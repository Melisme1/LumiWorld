using System;
using System.Collections.Generic;
using LumiWorld.Acs.TimeSystem;
using UnityEngine;

namespace LumiWorld.Economy.Crafting
{
    /// <summary>
    /// Hệ thống quản lý hàng đợi chế biến tài nguyên (Crafting Queue System).
    /// Tích hợp lõi GameTimeRuntime & TimerService của dự án LumiWorld:
    /// - Quản lý các mẻ chế biến đang đếm ngược thời gian.
    /// - Hỗ trợ tạm dừng/tiếp tục cùng nhịp đồng hồ gameplay chung.
    /// - Tự động hoàn thành khi hết giờ và giữ thành phẩm chờ người chơi bấm nhận (Claim).
    /// </summary>
    public class CraftingQueueSystem : MonoBehaviour
    {
        private static CraftingQueueSystem _instance;
        public static CraftingQueueSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<CraftingQueueSystem>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("CraftingQueueSystem");
                        _instance = go.AddComponent<CraftingQueueSystem>();
                    }
                }
                return _instance;
            }
        }

        [Header("Giới hạn hàng đợi")]
        [Tooltip("Số mẻ chế biến tối đa có thể chạy đồng thời")]
        [SerializeField] private int maxConcurrentJobs = 6;

        private readonly List<ActiveCraftingJob> activeJobs = new List<ActiveCraftingJob>();
        private TimerService timerService;

        public IReadOnlyList<ActiveCraftingJob> ActiveJobs => activeJobs;
        public int MaxConcurrentJobs => maxConcurrentJobs;

        public event Action<ActiveCraftingJob> OnJobStarted;
        public event Action<ActiveCraftingJob> OnJobCompleted;
        public event Action<ActiveCraftingJob> OnJobClaimed;
        public event Action OnQueueChanged;

        private EconomySaveSystem saveSystem;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            InitializeTimers();

            // Hàng chờ nằm trong bản lưu kinh tế của tài khoản: dựng lại mỗi lần vào đảo,
            // nên đổi scene (World map, thăm đảo) hay mở lại game đều không mất mẻ đang chạy
            if (!WorldLaunch.IsVisit) LoadFromSave();
        }

        private void OnDestroy()
        {
            if (saveSystem != null) saveSystem.PreparingSave -= CaptureToSave;
            if (_instance == this) _instance = null;
        }

        /// <summary>Hủy hàng chờ đang chạy (New Game, đổi tài khoản); lần vào đảo kế tiếp nạp lại từ bản lưu mới.</summary>
        public static void DestroyIfExists()
        {
            if (_instance != null) Destroy(_instance.gameObject);
        }

        // =========================================================
        // LƯU / NẠP (economy_<id>.json)
        // =========================================================

        private static bool CanChange => !WorldLaunch.IsVisit && !SaveSync.SavesSuspended;

        private void LoadFromSave()
        {
            saveSystem = EconomySaveSystem.Instance;
            if (saveSystem == null) return;
            saveSystem.PreparingSave += CaptureToSave;

            EconomySaveData saved = saveSystem.Progress;
            if (!DateTime.TryParse(saved.savedAtUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out DateTime savedAtUtc))
                savedAtUtc = DateTime.UtcNow;
            DateTime now = DateTime.UtcNow;

            activeJobs.Clear();
            foreach (CraftingJobRecord record in saved.craftingJobs)
            {
                if (record == null || string.IsNullOrEmpty(record.jobId)) continue;

                var job = new ActiveCraftingJob
                {
                    jobId = record.jobId,
                    recipeId = record.recipeId,
                    quantity = record.quantity,
                    timerId = "craft_" + record.jobId,
                    durationSeconds = record.durationSeconds,
                    isCompleted = record.isCompleted,
                    isClaimed = false
                };

                if (job.Recipe == null)
                    Debug.LogWarning($"[LumiWorld Chế Biến] Không tìm thấy công thức '{record.recipeId}' của mẻ {record.jobId}; giữ mẻ trong bản lưu.", this);

                if (!job.isCompleted)
                {
                    // Chế biến chạy theo giờ thật cả khi tắt game; đồng hồ máy bị lùi thì giữ số giây đã lưu
                    double remaining = Math.Min(record.durationSeconds, Math.Max(0, record.remainingSeconds));
                    if (now >= savedAtUtc &&
                        DateTime.TryParse(record.completeAtUtc, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                            out DateTime completeAt))
                    {
                        remaining = Math.Min(record.durationSeconds, (completeAt - now).TotalSeconds);
                    }

                    if (remaining <= 0) job.isCompleted = true;
                    else timerService.StartCountdown(job.timerId, remaining, OfflineTimerPolicy.Advance);
                }

                activeJobs.Add(job);
            }

            if (activeJobs.Count > 0) OnQueueChanged?.Invoke();
        }

        // Ghi lại hàng chờ ngay trước mỗi lần EconomySaveSystem ghi file
        private void CaptureToSave()
        {
            if (!CanChange || saveSystem == null) return;

            DateTime now = DateTime.UtcNow;
            List<CraftingJobRecord> records = saveSystem.Progress.craftingJobs;
            records.Clear();
            foreach (ActiveCraftingJob job in activeJobs)
            {
                if (job == null || job.isClaimed) continue;
                double remaining = job.isCompleted ? 0 : GetRemainingSeconds(job);
                records.Add(new CraftingJobRecord
                {
                    jobId = job.jobId,
                    recipeId = job.recipeId,
                    quantity = job.quantity,
                    durationSeconds = job.durationSeconds,
                    completeAtUtc = now.AddSeconds(remaining).ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    remainingSeconds = remaining,
                    isCompleted = job.isCompleted
                });
            }
        }

        // Ghi file ngay: thay đổi kho (trừ nguyên liệu / cộng thành phẩm) và hàng chờ vào cùng một lần ghi
        private void SaveNow()
        {
            if (saveSystem == null) return;
            saveSystem.MarkDirty();
            if (!saveSystem.SaveNow())
                Debug.LogWarning("[LumiWorld Chế Biến] Chưa ghi được hàng chờ chế biến; sẽ thử lại ở lần lưu kế tiếp.", this);
        }

        private void InitializeTimers()
        {
            if (timerService != null) return;
            // Sử dụng clock dùng chung từ GameTimeRuntime
            var clock = GameTimeRuntime.Instance.Clock;
            timerService = new TimerService(clock);
        }

        private void Update()
        {
            if (timerService == null) InitializeTimers();

            bool changed = false;
            // Duyệt kiểm tra các job đang chạy
            for (int i = 0; i < activeJobs.Count; i++)
            {
                var job = activeJobs[i];
                if (job == null || job.isCompleted) continue;

                if (timerService.Contains(job.timerId))
                {
                    // Đọc thời gian còn lại
                    var record = timerService.Read(job.timerId);
                    if (record.remainingSeconds <= 0 || timerService.TryConsumeSignals(job.timerId, out _))
                    {
                        job.isCompleted = true;
                        timerService.Cancel(job.timerId);
                        changed = true;
                        OnJobCompleted?.Invoke(job);
                    }
                }
                else
                {
                    // Timer không tồn tại hoặc đã xong
                    job.isCompleted = true;
                    changed = true;
                    OnJobCompleted?.Invoke(job);
                }
            }

            if (changed)
            {
                if (saveSystem != null && CanChange) saveSystem.MarkDirty();
                OnQueueChanged?.Invoke();
            }
        }

        /// <summary>
        /// Bắt đầu chế biến một công thức với số lượng mẻ chỉ định.
        /// Trừ nguyên liệu kho ngay lập tức và khởi động timer đếm ngược.
        /// </summary>
        public bool TryStartCrafting(CraftingRecipeData recipe, int quantity, out string failureReason)
        {
            failureReason = string.Empty;
            if (!CanChange)
            {
                failureReason = "Không thể chế biến lúc này.";
                return false;
            }
            if (recipe == null)
            {
                failureReason = "Công thức không hợp lệ.";
                return false;
            }

            if (quantity <= 0)
            {
                failureReason = "Số lượng chế biến phải lớn hơn 0.";
                return false;
            }

            if (activeJobs.Count >= maxConcurrentJobs)
            {
                failureReason = $"Hàng đợi chế biến đã đầy ({maxConcurrentJobs}/{maxConcurrentJobs}). Hãy nhận bớt thành phẩm!";
                return false;
            }

            var inventory = ResourceInventory.Instance;
            if (inventory == null)
            {
                failureReason = "Kho tài nguyên chưa sẵn sàng.";
                return false;
            }

            // 1. Kiểm tra nguyên liệu đầu vào
            var scaledInputs = CraftingCalculator.GetScaledInputs(recipe, quantity);
            if (!inventory.HasAll(scaledInputs))
            {
                failureReason = "Không đủ nguyên liệu trong kho.";
                return false;
            }

            // 2. Kiểm tra sức chứa thành phẩm
            if (recipe.outputResource != null)
            {
                int freeSpace = inventory.GetFreeSpace(recipe.outputResource);
                int neededSpace = recipe.outputAmount * quantity;
                if (freeSpace < neededSpace)
                {
                    failureReason = $"Kho không đủ chỗ chứa cho {neededSpace} {recipe.outputResource.resourceName} (chỉ còn trống {freeSpace}).";
                    return false;
                }
            }

            // 3. Trừ nguyên liệu từ kho một cách an toàn
            if (!inventory.TryRemoveAll(scaledInputs))
            {
                failureReason = "Lỗi khi trừ nguyên liệu kho.";
                return false;
            }

            // 4. Tạo job mới
            string jobId = Guid.NewGuid().ToString("N");
            string timerId = "craft_" + jobId;
            double totalDuration = recipe.craftDurationSeconds * quantity;

            var job = new ActiveCraftingJob
            {
                jobId = jobId,
                recipeId = recipe.recipeID,
                quantity = quantity,
                timerId = timerId,
                durationSeconds = totalDuration,
                isCompleted = totalDuration <= 0f,
                isClaimed = false
            };

            activeJobs.Add(job);

            // 5. Khởi động timer nếu có thời gian chờ
            if (totalDuration > 0f)
            {
                InitializeTimers();
                timerService.StartCountdown(timerId, totalDuration, OfflineTimerPolicy.Advance);
            }

            // Nguyên liệu đã trừ khỏi kho: ghi kho và mẻ mới trong cùng một lần
            SaveNow();

            OnJobStarted?.Invoke(job);
            OnQueueChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Lấy thời gian còn lại của một mẻ chế biến tính bằng giây.
        /// </summary>
        public float GetRemainingSeconds(ActiveCraftingJob job)
        {
            if (job == null || job.isCompleted) return 0f;
            if (timerService == null) return 0f;

            if (timerService.Contains(job.timerId))
            {
                return (float)timerService.Read(job.timerId).remainingSeconds;
            }
            return 0f;
        }

        /// <summary>
        /// Lấy tỉ lệ tiến độ chế biến (0.0 đến 1.0).
        /// </summary>
        public float GetProgress(ActiveCraftingJob job)
        {
            if (job == null || job.isCompleted) return 1f;
            if (job.durationSeconds <= 0.0) return 1f;

            float remaining = GetRemainingSeconds(job);
            float progress = 1f - (remaining / (float)job.durationSeconds);
            return Mathf.Clamp01(progress);
        }

        /// <summary>
        /// Nhận thành phẩm của một job đã hoàn thành vào kho.
        /// </summary>
        public bool TryClaimJob(ActiveCraftingJob job, out string failureReason)
        {
            failureReason = string.Empty;
            if (!CanChange)
            {
                failureReason = "Không thể nhận thành phẩm lúc này.";
                return false;
            }
            if (job == null)
            {
                failureReason = "Mục chế biến không tồn tại.";
                return false;
            }

            if (!job.isCompleted)
            {
                failureReason = "Mẻ chế biến vẫn đang trong thời gian chờ.";
                return false;
            }

            if (job.isClaimed)
            {
                failureReason = "Thành phẩm đã được nhận trước đó.";
                return false;
            }

            var recipe = job.Recipe;
            if (recipe == null || recipe.outputResource == null)
            {
                failureReason = "Dữ liệu công thức thành phẩm không hợp lệ.";
                return false;
            }

            var inventory = ResourceInventory.Instance;
            if (inventory == null)
            {
                failureReason = "Kho tài nguyên chưa sẵn sàng.";
                return false;
            }
            int totalAmount = job.TotalOutputAmount;
            if (totalAmount <= 0 || !activeJobs.Contains(job))
            {
                failureReason = "Mẻ chế biến hoặc số lượng thành phẩm không hợp lệ.";
                return false;
            }
            if (!inventory.TryAddAll(new[] { new ResourceInventory.Deposit(recipe.outputResource, totalAmount) }, () =>
            {
                job.isClaimed = true;
                activeJobs.Remove(job);
            }))
            {
                failureReason = "Kho không đủ chỗ để nhận toàn bộ thành phẩm. Hãy dọn kho!";
                return false;
            }

            // Thành phẩm đã vào kho và mẻ đã bỏ khỏi hàng chờ: ghi cùng một lần, không nhận lại được
            SaveNow();

            OnJobClaimed?.Invoke(job);
            OnQueueChanged?.Invoke();
            Debug.Log($"[LumiWorld Chế Biến] Đã nhận {recipe.outputResource.resourceID} x{totalAmount}; kho hiện có {inventory.GetAmount(recipe.outputResource.resourceID)}.", this);
            return true;
        }

        /// <summary>
        /// Nhận toàn bộ các mẻ chế biến đã hoàn thành vào kho cùng lúc.
        /// </summary>
        public int ClaimAllCompleted()
        {
            int claimedCount = 0;
            for (int i = activeJobs.Count - 1; i >= 0; i--)
            {
                var job = activeJobs[i];
                if (job.isCompleted && !job.isClaimed)
                {
                    if (TryClaimJob(job, out _))
                    {
                        claimedCount++;
                    }
                }
            }
            return claimedCount;
        }

        /// <summary>
        /// Đếm số mẻ đã hoàn thành đang chờ nhận.
        /// </summary>
        public int GetCompletedCount()
        {
            int count = 0;
            foreach (var job in activeJobs)
            {
                if (job.isCompleted && !job.isClaimed) count++;
            }
            return count;
        }
    }
}
