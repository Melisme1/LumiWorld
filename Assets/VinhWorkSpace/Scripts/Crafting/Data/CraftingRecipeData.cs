using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Economy.Crafting
{
    /// <summary>
    /// Định nghĩa công thức chế biến tài nguyên (Data-Driven qua ScriptableObject).
    /// </summary>
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "LumiWorld/Crafting Recipe", order = 20)]
    public class CraftingRecipeData : ScriptableObject
    {
        [Header("1. Định danh công thức")]
        [Tooltip("Mã định danh duy nhất (ví dụ: recipe.food.crispy_leaves)")]
        public string recipeID = "recipe.food.crispy_leaves";

        [Tooltip("Tên hiển thị của công thức")]
        public string recipeName = "Lá non rang giòn";

        [TextArea(2, 4)]
        [Tooltip("Mô tả về món ăn / sản phẩm chế biến")]
        public string description = "Lá non tuyển chọn từ Leafwood được rang giòn, là món ăn vặt yêu thích của các sinh vật.";

        [Tooltip("Phân loại công thức (Thức ăn, Tinh chế, Dược liệu, Thủ công)")]
        public CraftingCategory category = CraftingCategory.Food;

        [Header("2. Thành phẩm đầu ra (Output)")]
        [Tooltip("Icon đại diện công thức (nếu để trống sẽ tự lấy icon của thành phẩm đầu ra)")]
        public Sprite recipeIcon;

        [Tooltip("Tài nguyên thành phẩm")]
        public ResourceData outputResource;

        /// <summary>
        /// Lấy Sprite icon của công thức (ưu tiên recipeIcon nếu được gán riêng, nếu không lấy từ outputResource).
        /// </summary>
        public Sprite GetIcon() => recipeIcon != null ? recipeIcon : (outputResource != null ? outputResource.icon : null);

        [Tooltip("Số lượng thành phẩm tạo ra mỗi mẻ")]
        [Min(1)]
        public int outputAmount = 1;

        [Header("3. Nguyên liệu đầu vào (Inputs)")]
        [Tooltip("Danh sách nguyên liệu cần thiết để chế biến một mẻ")]
        public List<ResourceStack> inputs = new List<ResourceStack>();

        [Header("4. Thuộc tính & Thời gian")]
        [Tooltip("Có thể ăn trực tiếp hay không (dành cho chăm sóc / cho thú ăn)")]
        public bool isEdible = true;

        [Tooltip("Thời gian chờ chế biến tính bằng giây (0 = tức thì, >0 = đưa vào hàng đợi đếm ngược)")]
        [Min(0f)]
        public float craftDurationSeconds = 5f;

        [Tooltip("Điểm EXP thưởng khi dùng cho thú ăn (nếu là món ăn)")]
        public int feedingExpBonus = 25;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(recipeID))
            {
                recipeID = "recipe." + name.ToLowerInvariant();
            }
        }
    }
}
