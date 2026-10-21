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

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            InitializeTimers();
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
            if (recipe == null)
            {
                failureReason = "Invalid recipe.";
                return false;
            }

            if (quantity <= 0)
            {
                failureReason = "Crafting quantity must be greater than 0.";
                return false;
            }

            if (activeJobs.Count >= maxConcurrentJobs)
            {
                failureReason = $"Crafting queue is full ({maxConcurrentJobs}/{maxConcurrentJobs}). Please claim completed items!";
                return false;
            }

            var inventory = ResourceInventory.Instance;
            if (inventory == null)
            {
                failureReason = "Resource warehouse is not ready.";
                return false;
            }

            // 1. Kiểm tra nguyên liệu đầu vào
            var scaledInputs = CraftingCalculator.GetScaledInputs(recipe, quantity);
            if (!inventory.HasAll(scaledInputs))
            {
                failureReason = "Not enough materials in warehouse.";
                return false;
            }

            // 2. Kiểm tra sức chứa thành phẩm
            if (recipe.outputResource != null)
            {
                int freeSpace = inventory.GetFreeSpace(recipe.outputResource);
                int neededSpace = recipe.outputAmount * quantity;
                if (freeSpace < neededSpace)
                {
                    failureReason = $"Warehouse does not have enough space for {neededSpace} {recipe.outputResource.resourceName} (only {freeSpace} remaining).";
                    return false;
                }
            }

            // 3. Trừ nguyên liệu từ kho một cách an toàn
            if (!inventory.TryRemoveAll(scaledInputs))
            {
                failureReason = "Error deducting materials from warehouse.";
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
            if (job == null)
            {
                failureReason = "Crafting item does not exist.";
                return false;
            }

            if (!job.isCompleted)
            {
                failureReason = "Crafting batch is still in progress.";
                return false;
            }

            if (job.isClaimed)
            {
                failureReason = "Items already claimed.";
                return false;
            }

            var recipe = job.Recipe;
            if (recipe == null || recipe.outputResource == null)
            {
                failureReason = "Invalid recipe output data.";
                return false;
            }

            var inventory = ResourceInventory.Instance;
            if (inventory == null)
            {
                failureReason = "Resource warehouse is not ready.";
                return false;
            }
            int totalAmount = job.TotalOutputAmount;
            if (totalAmount <= 0 || !activeJobs.Contains(job))
            {
                failureReason = "Invalid crafting job or output quantity.";
                return false;
            }
            if (!inventory.TryAddAll(new[] { new ResourceInventory.Deposit(recipe.outputResource, totalAmount) }, () =>
            {
                job.isClaimed = true;
                activeJobs.Remove(job);
            }))
            {
                failureReason = "Warehouse full! Clear space to claim finished goods.";
                return false;
            }

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
