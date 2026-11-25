# Wet Island layout authoring — 22 November 2026

## Scope and evidence

The owner requires different asset sizes for 3/6/10 hex, editable asset palettes in the layout Inspector, and composition guided by the four supplied diorama references. Existing Art prefabs and model import scales are preserved. Layout instances receive measured uniform scales instead.

`Logs/art_scale_report_dryrun.csv` contains 431 prefabs. Its **Before** columns are measurements; **After** columns are normalization proposals, not measurements of a completed change. The 409 flagged rows include transform cleanup, so they are not 409 oversized models.

Examples from Before (metres): Mossy cliffs are 3.712–3.887 high; Fab_PalmTree_01 is 6.588 high; StylizedTropical_PalmTree_02 is 7.203 high; NatureEnv_ReedCattail_01 is 2.988 high; Lotus_Cluster_01 spans 2.871; Field_Grass_03 spans 3.101. The newer iris is already 0.500 high and golden reed bush 0.998 high. A uniform library-wide shrink would make appropriate assets too small.

## Workflow in Unity

1. Let Unity finish compiling, then run **LumiWorld > WetIsland > Prepare Measured Tier Presets**. This explicitly migrates the four profiles once. Re-running does not reset edited presets. It does not regenerate or save the 12 biome prefabs.
2. Select a biome instance in `Jungle_Asset_Test` or open its Prefab Mode. Its Inspector now shows **Asset Presets — 3 / 6 / 10 Hex**. Expand a role to edit Enabled, Count, Asset Options and Dimensions (metres). Placement fields remain shared across tiers.
3. Use **Check Asset Sizes for This Tier** to see source and fitted dimensions. An incompatible shape is excluded; an empty compatible palette prevents generation and reports the offending role. All sizes are measured again when generating, so later source imports are not calibrated against stale CSV data.
4. **Regenerate Layout** uses the object's own tier and existing surface. The tabs edit presets; they do not rebuild a 6-hex mesh into 3 hex. Use the matching biome prefab for another tier.
5. **Create 3 Seed Preview Options** makes three scene copies beside the source. **Make Independent Profile Option** creates a separate profile for an option when testing different asset lists or counts; otherwise options share the same profile. Preview copies are not automatically baked back to the original source prefab.
6. Compare from the same camera with a 0.27–0.36 m creature and gizmos hidden. Lock hand-adjusted props; **Reroll Unlocked Only** preserves their transforms. Bake only after choosing the result.
7. After baking, **Validate 12 Diorama Prefabs** checks stored sizes, links, surfaces and regeneration. It can now reject the old oversized baked props. Passing is not visual approval.

## Initial size proposals

These are editable starting bands, not visually approved final art. Most roles use factors 0.86 / 1.00 / 1.14 for 3/6/10. The canopy uses 0.84 / 1.00 / 1.12. Role-specific height and footprint limits are applied together; proportions are never stretched to force a fit.

| Role / main dimension | 3 hex | 6 hex | 10 hex |
| --- | --- | --- | --- |
| Iris height | 0.37–0.48 | 0.43–0.56 | 0.49–0.64 |
| Golden reed core height | 0.77–0.93 | 0.90–1.08 | 1.03–1.23 |
| Roundleaf bush height | 0.37–0.47 | 0.43–0.55 | 0.49–0.63 |
| Flower clump footprint | 0.30–0.47 | 0.35–0.55 | 0.40–0.63 |
| Low grass footprint | 0.39–0.58 | 0.45–0.68 | 0.51–0.78 |
| Pebble footprint | 0.28–0.41 | 0.32–0.48 | 0.36–0.55 |
| Lotus island footprint | 0.45–0.62 | 0.52–0.72 | 0.59–0.82 |
| Headwater cliff height | 1.20–1.42 | 1.40–1.65 | 1.60–1.88 |
| Canopy height | 2.02–2.35 | 2.40–2.80 | 2.69–3.14 |
| Palm height | 1.42–1.85 | 1.65–2.15 | 1.88–2.45 |

`WetIslandSizeAudit.csv` lists the compatible fitted range of each palette entry at each tier, calculated by the actual sizing policy using the supplied CSV. Cross-axis limits can narrow these bands. Bridge length is chosen from the bank span and must also fit its tier limits.

Low groundcover uses footprint sizing with a small height floor; scaling a flat carpet to the same height as a tuft would make it excessively wide. Thin, tall flowers are not mixed blindly with ground-cover flowers. Mangrove variants are the initial canopy palette because their measured width/height ratios are broader than the tall narrow Ceiba variants; the exact silhouette still needs visual selection.

## Code responsibilities

- `PrefabRenderMetrics`: active mesh/skin bounds with LOD0 selection and authored transforms. Reused by the existing Art normalizer and layout calibration.
- `DioramaSizing`: pure size feasibility and uniform scale interval.
- `BiomeLayoutProfile`: common placement rules and independent tier counts, palettes and metre limits.
- `BiomeLayoutPreparation`: resolves the active tier, measures assets, rejects incompatible entries, and aligns the generated model to its surface.
- `DioramaPlan`: pure deterministic placement. Asset/size is selected before position. Visual footprint affects same-role crowding; solid footprint is separate. This is a soft composition preference, not a complete foliage-overlap solver.
- `BiomeSurfaceReader` and `BiomeActivityZoneBuilder`: existing mesh sampling and zone output, separate from object creation.
- `BiomeLayoutGenerator`: staged creation, preservation of locked props, and commit. Optional Editor undo callbacks keep UnityEditor out of runtime code.
- `BiomeTierInspector`, `WetIslandTierPresetSetup`, `WetIslandLayoutValidation`: authoring, explicit migration and checks.

## Verification and remaining work

Runtime and Editor sources are compiled against the project's Unity references with Roslyn. Managed checks use the existing 12 ground/water meshes and report measurements: five seeds each, deterministic replay, locked rerolls, height/width limits, zero-obstacle vegetation and invalid-size rejection. These do not exercise Unity Undo, Inspector rendering or native prefab import.

Codex can read the Pipeline descriptor but the CLI connection fails with Windows socket permission error 10013. No live Editor command or player build was executed, and profiles/prefabs have not been migrated or rebaked by this turn. The source scripts and migration tools are ready for the workflow above.

Visual review must still confirm readability at the gameplay camera, plant bases after tilt, the headwater waterfall relative to the resized cliff, clear water, and canopy clearance. Existing largest-region sampling is not proof that every intended dry path is connected. Grass keeps zero obstacles; bridges are not circular obstacles, but no new creature bridge-walking implementation is introduced here.
