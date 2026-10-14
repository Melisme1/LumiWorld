using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LumiWorld.Economy.Crafting
{
    /// <summary>
    /// Lõi tính toán thuần túy (Pure Logic) cho hệ thống chế biến:
    /// - Phân rã đệ quy cây công thức thành danh sách nguyên liệu thô ban đầu (Raw Material Breakdown).
    /// - Tính toán số lượng tối đa có thể chế biến dựa trên kho đồ hiện tại.
    /// - Kiểm tra điều kiện chế biến và định dạng hiển thị.
    /// </summary>
    public static class CraftingCalculator
    {
        /// <summary>
        /// Phân rã đệ quy một công thức thành các nguyên liệu thô ban đầu cùng số lượng cần thiết cho 1 mẻ.
        /// </summary>
        public static Dictionary<string, int> CalculateRawBreakdown(CraftingRecipeData recipe)
        {
            var rawTotals = new Dictionary<string, int>(StringComparer.Ordinal);
            if (recipe == null || recipe.inputs == null) return rawTotals;

            var visitedRecipes = new HashSet<string>(StringComparer.Ordinal);
            DecomposeInputs(recipe.inputs, 1, rawTotals, visitedRecipes);
            return rawTotals;
        }

        private static void DecomposeInputs(
            List<ResourceStack> inputs,
            int multiplier,
            Dictionary<string, int> rawTotals,
            HashSet<string> visitedRecipes)
        {
            if (inputs == null) return;

            foreach (var input in inputs)
            {
                if (input == null || string.IsNullOrEmpty(input.resourceId) || input.amount <= 0) continue;

                int requiredAmount = input.amount * multiplier;

                // Tìm xem nguyên liệu này có phải là thành phẩm của một công thức chế biến khác không
                CraftingRecipeData parentRecipe = CraftingCatalog.FindByOutput(input.resourceId);

                if (parentRecipe != null && !visitedRecipes.Contains(parentRecipe.recipeID))
                {
                    visitedRecipes.Add(parentRecipe.recipeID);
                    // Tỉ lệ mẻ cần làm: tính theo outputAmount của công thức đó (thường là 1)
                    int batchesNeeded = Mathf.CeilToInt((float)requiredAmount / Mathf.Max(1, parentRecipe.outputAmount));
                    DecomposeInputs(parentRecipe.inputs, batchesNeeded, rawTotals, visitedRecipes);
                    visitedRecipes.Remove(parentRecipe.recipeID);
                }
                else
                {
                    // Là nguyên liệu thô tự nhiên (hoặc đã chạm đỉnh chuỗi)
                    rawTotals.TryGetValue(input.resourceId, out int current);
                    rawTotals[input.resourceId] = current + requiredAmount;
                }
            }
        }

        /// <summary>
        /// Tính số lần tối đa có thể chế biến công thức này dựa vào nguyên liệu trực tiếp trong kho.
        /// </summary>
        public static int GetMaxCraftableCount(CraftingRecipeData recipe, ResourceInventory inventory)
        {
            if (recipe == null || recipe.inputs == null || recipe.inputs.Count == 0 || inventory == null) return 0;

            int maxCount = int.MaxValue;
            foreach (var input in recipe.inputs)
            {
                if (input == null || string.IsNullOrEmpty(input.resourceId) || input.amount <= 0) continue;

                int available = inventory.GetAmount(input.resourceId);
                int countForThis = available / input.amount;
                if (countForThis < maxCount) maxCount = countForThis;
            }

            // Đồng thời kiểm tra sức chứa tối đa của kho cho thành phẩm đầu ra
            if (recipe.outputResource != null)
            {
                int freeSpace = inventory.GetFreeSpace(recipe.outputResource);
                int spaceCapacity = freeSpace / Mathf.Max(1, recipe.outputAmount);
                if (spaceCapacity < maxCount) maxCount = spaceCapacity;
            }

            return maxCount == int.MaxValue ? 0 : Mathf.Max(0, maxCount);
        }

        /// <summary>
        /// Kiểm tra kho có đủ nguyên liệu trực tiếp và đủ chỗ chứa để chế biến số lượng chỉ định không.
        /// </summary>
        public static bool CanCraft(CraftingRecipeData recipe, ResourceInventory inventory, int quantity = 1)
        {
            if (recipe == null || inventory == null || quantity <= 0) return false;

            // 1. Kiểm tra nguyên liệu đầu vào
            var scaledInputs = GetScaledInputs(recipe, quantity);
            if (!inventory.HasAll(scaledInputs)) return false;

            // 2. Kiểm tra sức chứa kho cho thành phẩm
            if (recipe.outputResource != null)
            {
                int freeSpace = inventory.GetFreeSpace(recipe.outputResource);
                if (freeSpace < recipe.outputAmount * quantity) return false;
            }

            return true;
        }

        /// <summary>
        /// Nhân hệ số số lượng nguyên liệu đầu vào theo số mẻ muốn chế biến.
        /// </summary>
        public static List<ResourceStack> GetScaledInputs(CraftingRecipeData recipe, int quantity)
        {
            var result = new List<ResourceStack>();
            if (recipe == null || recipe.inputs == null || quantity <= 0) return result;

            foreach (var input in recipe.inputs)
            {
                if (input == null || string.IsNullOrEmpty(input.resourceId)) continue;
                result.Add(new ResourceStack
                {
                    resourceId = input.resourceId,
                    amount = input.amount * quantity
                });
            }
            return result;
        }

        /// <summary>
        /// Trả về chuỗi mô tả danh sách nguyên liệu thô ban đầu để hiển thị trên UI.
        /// </summary>
        public static string FormatRawBreakdown(CraftingRecipeData recipe, int quantity = 1)
        {
            if (recipe == null) return string.Empty;

            var rawMap = CalculateRawBreakdown(recipe);
            if (rawMap.Count == 0) return "Không có nguyên liệu yêu cầu";

            var sb = new StringBuilder();
            int index = 0;
            foreach (var pair in rawMap)
            {
                string name = CraftingCatalog.GetResourceName(pair.Key);
                int totalAmount = pair.Value * quantity;

                if (index > 0) sb.Append(" + ");
                sb.Append($"{totalAmount} {name}");
                index++;
            }
            return sb.ToString();
        }
    }
}
