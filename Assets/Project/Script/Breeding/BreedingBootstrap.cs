using UnityEngine;

/// <summary>
/// Bootstrap MonoBehaviour that creates BreedingManager and CreationLabUI at runtime.
/// Attach this to a persistent GameObject in the Game scene (e.g., the HUD root).
///
/// Setup in Inspector:
///   1. Drag your BreedingRecipe ScriptableObject assets into BreedingManager's Recipes list.
///   2. This script will create BreedingManager and CreationLabUI automatically on Start.
/// </summary>
public class BreedingBootstrap : MonoBehaviour
{
    [Header("Breeding Manager Settings")]
    [Tooltip("Breeding recipes available. Drag BreedingRecipe ScriptableObjects here.")]
    [SerializeField] private BreedingRecipe[] recipes;

    [Tooltip("Number of chambers unlocked at game start (1 = only the first tube).")]
    [SerializeField] private int unlockedChambers = 1;

    [Tooltip("Coin cost to instantly finish one incubation.")]
    [SerializeField] private int speedUpCoinCost = 50;

    private void Awake()
    {
        // Ensure BreedingManager exists
        if (BreedingManager.Instance == null)
        {
            var mgr = gameObject.AddComponent<BreedingManager>();
        }

        // Ensure CreationLabUI exists (it builds itself in Start)
        if (CreationLabUI.Instance == null)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                var uiGo = new GameObject("CreationLabUI", typeof(CreationLabUI));
                uiGo.transform.SetParent(canvas.transform, false);
            }
            else
            {
                Debug.LogError("[BreedingBootstrap] No Canvas found in scene. Cannot create CreationLabUI.");
            }
        }
    }

    private void Start()
    {
        if (BreedingManager.Instance != null && recipes != null && recipes.Length > 0)
        {
            BreedingManager.Instance.SetRecipes(recipes);
        }
    }
}
