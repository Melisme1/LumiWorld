using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumiWorld.Acs
{
    [Serializable]
    public sealed class CreatureFoodSettings
    {
        public ResourceData resource;
        public string resourceId;
        [Min(.001f)] public float satietyPerUnit = 2;
        public string ResourceId => resource != null ? resource.resourceID : resourceId;
    }

    [Serializable]
    public sealed class SpeciesDietSettings
    {
        [Tooltip("Runtime species ID từ registry; không dùng tên hiển thị GDD.")]
        public string speciesId;
        public List<string> allowedFoodIds = new List<string>();
        public List<string> favoriteFoodIds = new List<string>();
        [Tooltip("Hệ số thử; favorite phải hồi nhiều hơn món thường. Không tự gán favorite cho species.")]
        [Min(1.01f)] public float favoriteMultiplier = 2;
    }

    [Serializable]
    public sealed class CreatureCareSettings
    {
        [Min(.01f)] public float rankIHungerSeconds = 60;
        [Min(.01f)] public float rankIIHungerSeconds = 90;
        [Min(.01f)] public float rankIIIHungerSeconds = 120;
        [Range(0, 99)] public float autoFeedAtSatiety = 50;
        [Min(.01f)] public float foodUnitsPerSecond = 10;
        [Tooltip("Seed để test, giữ reference cũ nếu đã gán. Bổ sung thực phẩm khác ở Additional Foods.")]
        public ResourceData food;
        public string fallbackFoodResourceId = "resource.leafwood.raspberries";
        [Min(1)] public int seedFoodUnitsToFull = 50;
        public List<CreatureFoodSettings> additionalFoods = new List<CreatureFoodSettings>();
        public List<SpeciesDietSettings> speciesDiets = new List<SpeciesDietSettings>();
        public string FoodResourceId => food != null ? food.resourceID : fallbackFoodResourceId;

        public bool TryGetRules(out CreatureCareRules rules, out string reason)
        {
            rules = null;
            try
            {
                rules = new CreatureCareRules(rankIHungerSeconds, rankIIHungerSeconds,
                    rankIIIHungerSeconds, autoFeedAtSatiety, foodUnitsPerSecond);
            }
            catch (ArgumentException exception) { reason = exception.Message; return false; }
            reason = "Độ no giảm theo work; tự ăn từ ngăn riêng từng cá thể. Chưa nối world save.";
            return true;
        }
        public bool TryGetFoodRules(out CreatureFoodRules rules, out string reason)
        {
            rules = null;
            try
            {
                if (seedFoodUnitsToFull < 1) throw new ArgumentException("Seed Food Units To Full phải >= 1.");
                var foods = new List<CreatureFoodRule>
                { new CreatureFoodRule(FoodResourceId, 100d / seedFoodUnitsToFull) };
                var ids = new HashSet<string>(StringComparer.Ordinal) { FoodResourceId };

                foreach (var entry in additionalFoods ?? new List<CreatureFoodSettings>())
                {
                    if (entry == null) throw new ArgumentException("Additional Foods có dòng trống.");
                    if (ids.Add(entry.ResourceId))
                    {
                        foods.Add(new CreatureFoodRule(entry.ResourceId, entry.satietyPerUnit));
                    }
                }

                // Các loại thực phẩm thu hoạch và chế biến thông dụng
                string[] knownFoodIds = new[]
                {
                    "resource.leafwood.raspberries",
                    "resource.leafwood.acorns",
                    "resource.leafwood.tender_leaves_crispy",
                    "resource.leafwood.roasted_acorns",
                    "resource.leafwood.dried_raspberries",
                    "resource.windheath.mountain_blueberries",
                    "resource.windheath.dried_mountain_blueberries",
                    "resource.food.dried_wild_berries_mix",
                    "resource.leafwood.honey_glazed_acorns",
                    "resource.food.wild_berry_nut_cake"
                };
                foreach (var kf in knownFoodIds)
                {
                    if (ids.Add(kf))
                    {
                        foods.Add(new CreatureFoodRule(kf, 2.0));
                    }
                }

                // Tự động nhận diện bổ sung từ xưởng chế biến nếu có trong assembly
                var craftingType = Type.GetType("LumiWorld.Economy.Crafting.CraftingCatalog, Assembly-CSharp");
                if (craftingType != null)
                {
                    var prop = craftingType.GetProperty("All", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop != null && prop.GetValue(null) is System.Collections.IEnumerable list)
                    {
                        foreach (var item in list)
                        {
                            if (item == null) continue;
                            var catProp = item.GetType().GetField("category") ?? (System.Reflection.MemberInfo)item.GetType().GetProperty("category");
                            object catVal = catProp is System.Reflection.FieldInfo fi ? fi.GetValue(item) : (catProp is System.Reflection.PropertyInfo pi ? pi.GetValue(item) : null);
                            if (catVal != null && catVal.ToString() == "Food")
                            {
                                var outProp = item.GetType().GetField("outputResource") ?? (System.Reflection.MemberInfo)item.GetType().GetProperty("outputResource");
                                object outVal = outProp is System.Reflection.FieldInfo ofi ? ofi.GetValue(item) : (outProp is System.Reflection.PropertyInfo opi ? opi.GetValue(item) : null);
                                if (outVal is ResourceData rd && !string.IsNullOrEmpty(rd.resourceID) && ids.Add(rd.resourceID))
                                {
                                    foods.Add(new CreatureFoodRule(rd.resourceID, 2.0));
                                }
                            }
                        }
                    }
                }

                var diets = new List<SpeciesDietRule>();
                foreach (var entry in speciesDiets ?? new List<SpeciesDietSettings>())
                {
                    if (entry == null) throw new ArgumentException("Species Diets có dòng trống.");
                    diets.Add(new SpeciesDietRule(entry.speciesId, entry.allowedFoodIds,
                        entry.favoriteFoodIds, entry.favoriteMultiplier));
                }
                rules = new CreatureFoodRules(foods, diets);
                reason = string.Empty; return true;
            }
            catch (ArgumentException exception) { reason = exception.Message; return false; }
        }
    }
}
