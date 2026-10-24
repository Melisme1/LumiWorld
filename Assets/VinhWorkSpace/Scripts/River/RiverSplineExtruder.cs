using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Quản lý việc đùn dải nước mượt mà liên tục từ SplineContainer
/// sử dụng hoàn toàn component SplineExtrude của Unity 6 (package com.unity.splines).
/// Tự động cấu hình Road profile (mặt nước phẳng có vát nhẹ vào bờ) và gán Material cuộn sóng.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SplineContainer))]
[RequireComponent(typeof(SplineExtrude))]
public class RiverSplineExtruder : MonoBehaviour
{
    [Header("Extrusion Settings")]
    [SerializeField] private float waterWidth = 0.22f;
    [SerializeField] private float segmentsPerUnit = 24f;
    [SerializeField] private Material waterMaterial;

    private SplineContainer _splineContainer;
    private SplineExtrude _splineExtrude;
    private MeshRenderer _meshRenderer;

    public SplineContainer Container
    {
        get
        {
            if (_splineContainer == null) EnsureComponents();
            return _splineContainer;
        }
    }

    public SplineExtrude ExtrudeComponent
    {
        get
        {
            if (_splineExtrude == null) EnsureComponents();
            return _splineExtrude;
        }
    }

    public float WaterWidth
    {
        get => waterWidth;
        set
        {
            waterWidth = value;
            if (_splineExtrude != null) _splineExtrude.Radius = value * 0.5f;
        }
    }

    public float SegmentsPerUnit
    {
        get => segmentsPerUnit;
        set
        {
            segmentsPerUnit = value;
            if (_splineExtrude != null) _splineExtrude.SegmentsPerUnit = value;
        }
    }

    private void Awake()
    {
        EnsureComponents();
    }

    private void OnEnable()
    {
        EnsureComponents();
    }

    /// <summary>
    /// Đảm bảo đầy đủ các component SplineContainer, SplineExtrude và MeshRenderer
    /// </summary>
    public void EnsureComponents()
    {
        if (_splineContainer == null)
            _splineContainer = GetComponent<SplineContainer>();
        if (_splineContainer == null)
            _splineContainer = gameObject.AddComponent<SplineContainer>();

        if (_splineExtrude == null)
            _splineExtrude = GetComponent<SplineExtrude>();
        if (_splineExtrude == null)
            _splineExtrude = gameObject.AddComponent<SplineExtrude>();

        if (_meshRenderer == null)
            _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshRenderer == null)
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (waterMaterial == null)
        {
            waterMaterial = Resources.Load<Material>("StylizedWater_Mat");
#if UNITY_EDITOR
            if (waterMaterial == null)
            {
                waterMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/VinhWorkSpace/Materials/StylizedRiverWater_Mat.mat");
            }
#endif
        }

        if (_meshRenderer != null && waterMaterial != null)
        {
            _meshRenderer.sharedMaterial = waterMaterial;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = true;
        }

        ConfigureNativeSplineExtrude();
    }

    /// <summary>
    /// Cấu hình component Unity native SplineExtrude với Road profile (mặt nước phẳng, vát nhẹ vào bờ)
    /// </summary>
    public void ConfigureNativeSplineExtrude()
    {
        if (_splineExtrude == null) return;

        _splineExtrude.Container = _splineContainer;
        _splineExtrude.Radius = Mathf.Max(0.01f, waterWidth * 0.5f);
        _splineExtrude.SegmentsPerUnit = Mathf.Max(1f, segmentsPerUnit);
        _splineExtrude.Capped = false;
        _splineExtrude.RebuildOnSplineChange = true;
        _splineExtrude.FlipNormals = false;

        // Gán Extrusion Shape sang Road để tạo mặt phẳng nước có bờ vát mượt
        try
        {
            var shapeField = typeof(SplineExtrude).GetField("m_Shape", BindingFlags.NonPublic | BindingFlags.Instance);
            if (shapeField != null)
            {
                var curShape = shapeField.GetValue(_splineExtrude);
                if (curShape == null || curShape is UnityEngine.Splines.ExtrusionShapes.Circle)
                {
                    shapeField.SetValue(_splineExtrude, new UnityEngine.Splines.ExtrusionShapes.Road());
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RiverSplineExtruder] Không thể gán Shape Road cho SplineExtrude: {ex.Message}");
        }
    }

    /// <summary>
    /// Kích hoạt Rebuild lại toàn bộ mesh dòng nước từ SplineContainer
    /// </summary>
    public void RebuildWater()
    {
        EnsureComponents();

        if (_splineExtrude != null)
        {
            ConfigureNativeSplineExtrude();
            _splineExtrude.Rebuild();
        }

        if (_meshRenderer != null && waterMaterial != null)
        {
            _meshRenderer.sharedMaterial = waterMaterial;
        }
    }

    public void SetMaterial(Material mat)
    {
        if (mat != null)
        {
            waterMaterial = mat;
            if (_meshRenderer != null)
            {
                _meshRenderer.sharedMaterial = mat;
            }
        }
    }
}
