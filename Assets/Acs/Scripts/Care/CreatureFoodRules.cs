using System;
using System.Collections.Generic;

namespace LumiWorld.Acs
{
    public sealed class CreatureFoodRule
    {
        public string ResourceId { get; }
        public double SatietyPerUnit { get; }
        public CreatureFoodRule(string resourceId, double satietyPerUnit)
        {
            if (string.IsNullOrWhiteSpace(resourceId)) throw new ArgumentException("Food ID is required.");
            CreatureCareRules.Positive(satietyPerUnit);
            ResourceId = resourceId; SatietyPerUnit = satietyPerUnit;
        }
    }

    public sealed class SpeciesDietRule
    {
        public string SpeciesId { get; }
        public IReadOnlyList<string> AllowedFoods { get; }
        public IReadOnlyList<string> FavoriteFoods { get; }
        public double FavoriteMultiplier { get; }
        public SpeciesDietRule(string speciesId, IEnumerable<string> allowedFoods,
            IEnumerable<string> favoriteFoods, double favoriteMultiplier)
        {
            if (string.IsNullOrWhiteSpace(speciesId)) throw new ArgumentException("Species ID is required.");
            CreatureCareRules.Positive(favoriteMultiplier);
            if (favoriteMultiplier <= 1) throw new ArgumentException("Favorite multiplier must be greater than one.");
            SpeciesId = speciesId;
            AllowedFoods = new List<string>(allowedFoods ?? Array.Empty<string>()).AsReadOnly();
            FavoriteFoods = new List<string>(favoriteFoods ?? Array.Empty<string>()).AsReadOnly();
            FavoriteMultiplier = favoriteMultiplier;
        }
    }

    public sealed class CreatureFoodChoice
    {
        public string ResourceId { get; }
        public double SatietyPerUnit { get; }
        public bool IsFavorite { get; }
        internal CreatureFoodChoice(string id, double satiety, bool favorite)
        { ResourceId = id; SatietyPerUnit = satiety; IsFavorite = favorite; }
    }

    // Immutable gameplay data. Unknown species use the catalog foods (seed/test default).
    // Explicit species profiles whitelist allowed foods; favorites must be in that whitelist.
    public sealed class CreatureFoodRules
    {
        public IReadOnlyList<CreatureFoodRule> Foods { get; }
        private readonly Dictionary<string, SpeciesDietRule> diets = new Dictionary<string, SpeciesDietRule>(StringComparer.Ordinal);
        public CreatureFoodRules(IEnumerable<CreatureFoodRule> foods, IEnumerable<SpeciesDietRule> speciesDiets)
        {
            var list = new List<CreatureFoodRule>(foods ?? throw new ArgumentNullException(nameof(foods)));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var food in list)
                if (food == null || !ids.Add(food.ResourceId)) throw new ArgumentException("Food ID missing or duplicated.");
            list.Sort((a, b) => string.CompareOrdinal(a.ResourceId, b.ResourceId));
            Foods = list.AsReadOnly();
            foreach (var diet in speciesDiets ?? Array.Empty<SpeciesDietRule>())
            {
                if (diet == null || diets.ContainsKey(diet.SpeciesId)) throw new ArgumentException("Species diet missing or duplicated.");
                foreach (string id in diet.AllowedFoods)
                    if (!ids.Contains(id)) throw new ArgumentException("Allowed food is not in the food catalog: " + id);
                foreach (string id in diet.FavoriteFoods)
                    if (!Contains(diet.AllowedFoods, id)) throw new ArgumentException("Favorite food must also be allowed: " + id);
                diets.Add(diet.SpeciesId, diet);
            }
        }
        public bool ContainsFood(string id)
        { foreach (var food in Foods) if (food.ResourceId == id) return true; return false; }

        public CreatureFoodChoice Choose(string speciesId, Func<string, long> available)
        {
            diets.TryGetValue(speciesId ?? string.Empty, out var diet);
            CreatureFoodChoice fallback = null;
            foreach (var food in Foods)
            {
                if (available(food.ResourceId) <= 0 || diet != null && !Contains(diet.AllowedFoods, food.ResourceId)) continue;
                bool favorite = diet != null && Contains(diet.FavoriteFoods, food.ResourceId);
                var choice = new CreatureFoodChoice(food.ResourceId,
                    food.SatietyPerUnit * (favorite ? diet.FavoriteMultiplier : 1), favorite);
                if (favorite) return choice;
                if (fallback == null) fallback = choice;
            }
            return fallback;
        }
        private static bool Contains(IReadOnlyList<string> values, string id)
        { foreach (string value in values) if (value == id) return true; return false; }
    }
}
