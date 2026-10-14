using System;
using UnityEngine;

namespace LumiWorld.Economy.Crafting
{
    /// <summary>
    /// Thông tin về một mẻ chế biến đang chạy hoặc đã hoàn thành chờ nhận trong hàng đợi.
    /// </summary>
    [Serializable]
    public class ActiveCraftingJob
    {
        public string jobId;
        public string recipeId;
        public int quantity = 1;
        public string timerId;
        public double durationSeconds;
        public bool isCompleted;
        public bool isClaimed;

        public CraftingRecipeData Recipe => CraftingCatalog.Find(recipeId);

        public int TotalOutputAmount
        {
            get
            {
                CraftingRecipeData r = Recipe;
                return r != null ? r.outputAmount * quantity : quantity;
            }
        }
    }
}
