using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LumiWorld.Acs.Editor
{
    /// <summary>
    /// Visual 1-click GUI inside Unity Editor for artists and teammates:
    /// drag/pick card art -> click 0-3 Affinity crest buttons -> pick Rarity frame ->
    /// preview live -> 1-click Export PNG & wire onto CardData.
    /// Menu: LumiWorld > Cards > Card Art & Affinity Builder
    /// </summary>
    public sealed class CardArtBuilderWindow : EditorWindow
    {
        private static readonly string[] RarityLabels = { "Common", "Epic (Silver)", "Legend (Gold)" };
        private static readonly string[] StarterAffinities = { "Leafwood", "Bloomfield", "Windheath" };
        private static readonly string[] WetAffinities = { "Reedmarsh", "Lilywater", "Streamstone", "Rainleaf" };

        private const float LeftColumnWidth = 440f;

        private Texture2D sourceAsset;
        private string externalFilePath = string.Empty;
        private Texture2D readableSourceTex;

        private readonly List<string> selectedAffinities = new List<string>(3);
        private CardRarity selectedRarity = CardRarity.Common;
        private bool bakeFrameIntoPng = false;
        private bool wetAquaHairline = false;
        private bool previewTerraformArrows = false;

        private CardData targetCardData;

        private Texture2D previewTex;
        private Texture2D previewFrameTex;
        private bool previewDirty = true;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.None;

        private readonly Dictionary<string, Texture2D> badgeThumbnails = new Dictionary<string, Texture2D>();
        private Vector2 leftScroll;

        // Cached styles (created lazily inside OnGUI)
        private GUIStyle titleStyle;
        private GUIStyle wrappedMiniStyle;
        private GUIStyle affinityLabelStyle;
        private GUIStyle affinityBadgeNumStyle;
        private GUIStyle previewHintStyle;
        private GUIStyle previewFooterStyle;

        [MenuItem("LumiWorld/Cards/Card Art & Affinity Builder", priority = 10)]
        public static void OpenWindow()
        {
            var win = GetWindow<CardArtBuilderWindow>("Card Art Builder");
            win.minSize = new Vector2(880f, 660f);
            win.Show();
        }

        private void OnEnable()
        {
            LoadBadgeThumbnails();
            previewDirty = true;
        }

        private void OnDisable()
        {
            DisposeTextures();
        }

        private void OnDestroy()
        {
            DisposeTextures();
        }

        private void EnsureStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 15,
                    wordWrap = true
                };
            }

            if (wrappedMiniStyle == null)
            {
                wrappedMiniStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    wordWrap = true
                };
            }

            if (affinityLabelStyle == null)
            {
                affinityLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.LowerCenter,
                    fontSize = 10,
                    clipping = TextClipping.Clip,
                    normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.92f, 0.92f, 0.92f) : Color.black }
                };
            }

            if (affinityBadgeNumStyle == null)
            {
                affinityBadgeNumStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    normal = { textColor = Color.white }
                };
            }

            if (previewHintStyle == null)
            {
                previewHintStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 12,
                    wordWrap = true
                };
            }

            if (previewFooterStyle == null)
            {
                previewFooterStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 11,
                    normal = { textColor = new Color(0.24f, 0.20f, 0.16f, 1f) }
                };
            }
        }

        private void DisposeTextures()
        {
            if (previewTex != null) { DestroyImmediate(previewTex); previewTex = null; }
            if (previewFrameTex != null) { DestroyImmediate(previewFrameTex); previewFrameTex = null; }
            if (readableSourceTex != null) { DestroyImmediate(readableSourceTex); readableSourceTex = null; }
            foreach (var kv in badgeThumbnails)
            {
                if (kv.Value != null) DestroyImmediate(kv.Value);
            }
            badgeThumbnails.Clear();
        }

        private void LoadBadgeThumbnails()
        {
            foreach (var kv in badgeThumbnails)
            {
                if (kv.Value != null) DestroyImmediate(kv.Value);
            }
            badgeThumbnails.Clear();

            foreach (string aff in CardArtCompositorUtility.AllAffinities)
            {
                string path = $"{CardArtCompositorUtility.BadgesAssetFolder}/{aff}.png";
                Texture2D tex = CardArtCompositorUtility.LoadRawImageFile(path);
                if (tex != null)
                {
                    tex.filterMode = FilterMode.Bilinear;
                    badgeThumbnails[aff] = tex;
                }
            }
        }

        private void ReloadReadableSource()
        {
            if (readableSourceTex != null)
            {
                DestroyImmediate(readableSourceTex);
                readableSourceTex = null;
            }

            if (!string.IsNullOrEmpty(externalFilePath) && File.Exists(externalFilePath))
            {
                readableSourceTex = CardArtCompositorUtility.LoadRawImageFile(externalFilePath);
            }
            else if (sourceAsset != null)
            {
                readableSourceTex = CardArtCompositorUtility.LoadReadableSourceTexture(sourceAsset);
            }

            previewDirty = true;
        }

        private void RebuildPreviewIfNeeded()
        {
            if (!previewDirty) return;
            previewDirty = false;

            if (previewTex != null)
            {
                DestroyImmediate(previewTex);
                previewTex = null;
            }
            if (previewFrameTex != null)
            {
                DestroyImmediate(previewFrameTex);
                previewFrameTex = null;
            }

            if (readableSourceTex == null) return;

            bool useRuntimeOuterPreview = selectedRarity != CardRarity.Common && !bakeFrameIntoPng;
            previewTex = CardArtCompositorUtility.CompositeCard(
                readableSourceTex,
                selectedAffinities,
                selectedRarity,
                applyRarityFrame: bakeFrameIntoPng && selectedRarity != CardRarity.Common,
                wetAquaHairline: wetAquaHairline,
                previewTerraformArrows: previewTerraformArrows);

            if (useRuntimeOuterPreview)
            {
                CardArtCompositorUtility.ApplyRoundedCornerAlpha(
                    previewTex,
                    CardArtCompositorUtility.CardCornerRadiusPx,
                    CardArtCompositorUtility.CardCornerInsetPx);
                previewFrameTex = CardArtCompositorUtility.LoadRarityFrameTexture(selectedRarity);
            }
        }

        private void HandleWindowDragAndDrop()
        {
            Event evt = Event.current;
            if (evt == null) return;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform) return;

            Rect dropZone = new Rect(0f, 0f, position.width, position.height);
            if (!dropZone.Contains(evt.mousePosition)) return;

            bool hasSupported = false;
            Texture2D draggedTex = null;
            CardData draggedCard = null;
            string draggedFile = null;

            if (DragAndDrop.objectReferences != null)
            {
                foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                {
                    if (obj is Texture2D t) { draggedTex = t; hasSupported = true; break; }
                    if (obj is Sprite sp && sp.texture != null) { draggedTex = sp.texture; hasSupported = true; break; }
                    if (obj is CardData cd) { draggedCard = cd; hasSupported = true; break; }
                }
            }

            if (!hasSupported && DragAndDrop.paths != null)
            {
                foreach (string p in DragAndDrop.paths)
                {
                    string ext = Path.GetExtension(p)?.ToLowerInvariant();
                    if ((ext == ".png" || ext == ".jpg" || ext == ".jpeg") && File.Exists(p))
                    {
                        draggedFile = p;
                        hasSupported = true;
                        break;
                    }
                }
            }

            if (!hasSupported) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                if (draggedCard != null)
                {
                    targetCardData = draggedCard;
                    selectedRarity = draggedCard.rarity;
                    previewDirty = true;
                }
                else if (draggedTex != null)
                {
                    sourceAsset = draggedTex;
                    externalFilePath = string.Empty;
                    ReloadReadableSource();
                }
                else if (!string.IsNullOrEmpty(draggedFile))
                {
                    externalFilePath = draggedFile;
                    sourceAsset = null;
                    ReloadReadableSource();
                }
                statusMessage = string.Empty;
                Repaint();
            }

            evt.Use();
        }

        private void OnGUI()
        {
            EnsureStyles();
            RebuildPreviewIfNeeded();
            HandleWindowDragAndDrop();

            EditorGUILayout.BeginHorizontal();

            // ============================================================
            // LEFT COLUMN: CONTROLS (fixed width, vertical scroll only)
            // ============================================================
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftColumnWidth));
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll, GUILayout.Width(LeftColumnWidth));

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 128f;

            EditorGUILayout.BeginVertical(GUILayout.Width(LeftColumnWidth - 22f));
            GUILayout.Space(6f);

            DrawHeader();
            GUILayout.Space(6f);

            DrawSourceSection();
            GUILayout.Space(6f);

            DrawAffinitySection();
            GUILayout.Space(6f);

            DrawFrameAndStyleSection();
            GUILayout.Space(6f);

            DrawTargetCardSection();
            GUILayout.Space(6f);
            EditorGUILayout.EndVertical();

            EditorGUIUtility.labelWidth = prevLabelWidth;
            EditorGUILayout.EndScrollView();

            // Sticky footer: Export button & status always visible without scrolling
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftColumnWidth));
            Rect footerSep = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(footerSep, new Color(0f, 0f, 0f, 0.22f));
            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6f);
            EditorGUILayout.BeginVertical();
            DrawExportSection();
            if (!string.IsNullOrEmpty(statusMessage))
            {
                GUILayout.Space(4f);
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(6f);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(8f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();

            // Divider
            Rect divRect = EditorGUILayout.GetControlRect(false, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(divRect, new Color(0f, 0f, 0f, 0.25f));

            // ============================================================
            // RIGHT COLUMN: LIVE PREVIEW
            // ============================================================
            DrawPreviewColumn();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("LumiWorld — Card Art & Affinity Builder", titleStyle);
            EditorGUILayout.LabelField(
                "Gắn 0–3 Nature Affinity & xem/áp khung Bạc (Epic) / Vàng (Legend) chỉ bằng nút bấm. Kéo thả ảnh trực tiếp vào cửa sổ này cũng được!",
                wrappedMiniStyle);
        }

        private void DrawSourceSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("1. Ảnh thẻ gốc (Source Art — 1024×1536)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Ảnh từ Unity", GUILayout.Width(124f));
            Rect texRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Texture2D newAsset = (Texture2D)EditorGUI.ObjectField(texRect, sourceAsset, typeof(Texture2D), false);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                sourceAsset = newAsset;
                externalFilePath = string.Empty;
                ReloadReadableSource();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Chọn file PNG ngoài máy...", GUILayout.Height(25f)))
            {
                string picked = EditorUtility.OpenFilePanel("Select Card Artwork PNG/JPG", "", "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(picked))
                {
                    externalFilePath = picked;
                    sourceAsset = null;
                    ReloadReadableSource();
                    statusMessage = string.Empty;
                }
            }
            if (GUILayout.Button("Xóa ảnh", GUILayout.Width(72f), GUILayout.Height(25f)))
            {
                sourceAsset = null;
                externalFilePath = string.Empty;
                ReloadReadableSource();
            }
            EditorGUILayout.EndHorizontal();

            if (readableSourceTex != null)
            {
                string srcLabel = !string.IsNullOrEmpty(externalFilePath)
                    ? Path.GetFileName(externalFilePath)
                    : (sourceAsset != null ? sourceAsset.name : "Loaded");
                EditorGUILayout.LabelField($"Đang mở: {srcLabel} ({readableSourceTex.width}×{readableSourceTex.height})", wrappedMiniStyle);
            }
            else
            {
                EditorGUILayout.LabelField("Chưa chọn ảnh. Kéo file PNG từ máy tính hoặc Texture từ Unity thả vào đây.", wrappedMiniStyle);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawAffinitySection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"2. Chọn Nature Affinity ({selectedAffinities.Count}/3)", EditorStyles.boldLabel);
            GUI.enabled = selectedAffinities.Count > 0;
            if (GUILayout.Button("Xóa hết", GUILayout.Width(68f)))
            {
                selectedAffinities.Clear();
                previewDirty = true;
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (badgeThumbnails.Count < 7)
            {
                EditorGUILayout.HelpBox($"Thiếu ảnh crest trong {CardArtCompositorUtility.BadgesAssetFolder}. Bấm tải lại nếu vừa pull repo.", MessageType.Warning);
                if (GUILayout.Button("Tải lại 7 Affinity Crests")) LoadBadgeThumbnails();
            }

            EditorGUILayout.LabelField("Starter Island (3)", EditorStyles.miniBoldLabel);
            DrawAffinityButtonRow(StarterAffinities);

            GUILayout.Space(4f);

            EditorGUILayout.LabelField("Wet Island (4)", EditorStyles.miniBoldLabel);
            DrawAffinityButtonRow(WetAffinities);

            if (selectedAffinities.Count > 0)
            {
                GUILayout.Space(6f);
                EditorGUILayout.LabelField("Thứ tự xếp trên thẻ (trên → dưới):", EditorStyles.miniBoldLabel);
                string[] slotRoles = { "Slot 1 (Primary)", "Slot 2 (Secondary)", "Slot 3 (Tertiary)" };
                for (int i = 0; i < selectedAffinities.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{slotRoles[i]}:  {selectedAffinities[i]}", GUILayout.ExpandWidth(true));

                    GUI.enabled = i > 0;
                    if (GUILayout.Button("↑", GUILayout.Width(26f)))
                    {
                        string tmp = selectedAffinities[i - 1];
                        selectedAffinities[i - 1] = selectedAffinities[i];
                        selectedAffinities[i] = tmp;
                        previewDirty = true;
                    }
                    GUI.enabled = i < selectedAffinities.Count - 1;
                    if (GUILayout.Button("↓", GUILayout.Width(26f)))
                    {
                        string tmp = selectedAffinities[i + 1];
                        selectedAffinities[i + 1] = selectedAffinities[i];
                        selectedAffinities[i] = tmp;
                        previewDirty = true;
                    }
                    GUI.enabled = true;
                    if (GUILayout.Button("✕", GUILayout.Width(26f)))
                    {
                        selectedAffinities.RemoveAt(i);
                        previewDirty = true;
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAffinityButtonRow(string[] names)
        {
            const float btnHeight = 62f;
            const float gap = 4f;
            Rect rowRect = GUILayoutUtility.GetRect(0f, btnHeight, GUILayout.ExpandWidth(true));
            float cellW = (rowRect.width - gap * (names.Length - 1)) / names.Length;

            for (int i = 0; i < names.Length; i++)
            {
                string aff = names[i];
                Rect btnRect = new Rect(rowRect.x + i * (cellW + gap), rowRect.y, cellW, btnHeight);

                int slotIdx = selectedAffinities.IndexOf(aff);
                bool isSelected = slotIdx >= 0;

                Color prevBg = GUI.backgroundColor;
                if (isSelected) GUI.backgroundColor = new Color(0.50f, 0.88f, 0.52f, 1f);

                if (GUI.Button(btnRect, GUIContent.none))
                {
                    if (isSelected)
                    {
                        selectedAffinities.RemoveAt(slotIdx);
                        previewDirty = true;
                    }
                    else if (selectedAffinities.Count < 3)
                    {
                        selectedAffinities.Add(aff);
                        previewDirty = true;
                    }
                    else
                    {
                        statusMessage = "Mỗi thẻ gắn tối đa 3 Affinity. Bấm bỏ 1 ô trước khi chọn thêm.";
                        statusType = MessageType.Info;
                    }
                }

                GUI.backgroundColor = prevBg;

                // Draw crisp scaled crest icon inside button
                if (badgeThumbnails.TryGetValue(aff, out Texture2D icon) && icon != null)
                {
                    Rect iconRect = new Rect(btnRect.x + 4f, btnRect.y + 4f, btnRect.width - 8f, btnRect.height - 22f);
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                }

                // Draw affinity name at bottom
                Rect lblRect = new Rect(btnRect.x + 2f, btnRect.yMax - 17f, btnRect.width - 4f, 15f);
                GUI.Label(lblRect, aff, affinityLabelStyle);

                // Draw slot number badge (1 / 2 / 3) at top-right if selected
                if (isSelected)
                {
                    Rect pillRect = new Rect(btnRect.xMax - 19f, btnRect.y + 3f, 16f, 16f);
                    EditorGUI.DrawRect(pillRect, new Color(0.16f, 0.48f, 0.22f, 0.95f));
                    GUI.Label(pillRect, (slotIdx + 1).ToString(), affinityBadgeNumStyle);
                }
            }
        }

        private void DrawFrameAndStyleSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("3. Khung Hiếm (Epic Bạc / Legend Vàng)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            int rarityIdx = GUILayout.Toolbar((int)selectedRarity, RarityLabels, GUILayout.Height(25f));
            if (EditorGUI.EndChangeCheck())
            {
                selectedRarity = (CardRarity)rarityIdx;
                previewDirty = true;
            }

            EditorGUI.BeginChangeCheck();
            bakeFrameIntoPng = EditorGUILayout.ToggleLeft("Gộp cứng khung Bạc/Vàng vào file PNG xuất ra", bakeFrameIntoPng);
            wetAquaHairline = EditorGUILayout.ToggleLeft("Thêm viền ngọc lam mảnh (Wet Nature)", wetAquaHairline);
            previewTerraformArrows = EditorGUILayout.ToggleLeft("Xem thử 2 mũi tên Raise/Lower (chỉ Preview)", previewTerraformArrows);
            if (EditorGUI.EndChangeCheck())
            {
                previewDirty = true;
            }

            if (selectedRarity != CardRarity.Common)
            {
                string tip = bakeFrameIntoPng
                    ? "Đang BẬT gộp cứng: file PNG sẽ dính sẵn khung. Nếu dùng trên CardData, để CardData.Rarity = Common để khỏi trùng 2 lớp khung."
                    : "Khuyến nghị (TẮT gộp cứng): PNG giữ sạch không khung; Unity tự ốp khung Bạc/Vàng qua trường CardData.Rarity ở cả Hand Deck & Shop.";
                EditorGUILayout.HelpBox(tip, MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTargetCardSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("4. Gắn trực tiếp vào CardData (Tùy chọn)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            targetCardData = (CardData)EditorGUILayout.ObjectField("Target CardData", targetCardData, typeof(CardData), false);
            if (EditorGUI.EndChangeCheck() && targetCardData != null)
            {
                selectedRarity = targetCardData.rarity;
                previewDirty = true;
            }

            if (targetCardData != null)
            {
                EditorGUILayout.LabelField(
                    $"CardID: {targetCardData.cardID} · Type: {targetCardData.cardType} · Rarity: {targetCardData.rarity}",
                    wrappedMiniStyle);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawExportSection()
        {
            bool canExport = readableSourceTex != null;
            GUI.enabled = canExport;

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.42f, 0.78f, 0.42f, 1f);

            string btnLabel = targetCardData != null
                ? "Xuất PNG & Gắn ngay vào CardData"
                : "Xuất file PNG thẻ bài (1024×1536)...";

            if (GUILayout.Button(btnLabel, GUILayout.Height(36f)))
            {
                ExecuteExport();
            }

            GUI.backgroundColor = prevBg;
            GUI.enabled = true;
        }

        private void ExecuteExport()
        {
            if (readableSourceTex == null) return;

            string defaultFolder = "Assets/Acs/UI Cards/UI Cards";
            if (targetCardData != null && targetCardData.cardType != CardType.Creature)
            {
                defaultFolder = "Assets/Acs/UI Cards/Environment";
            }

            string defaultName = targetCardData != null
                ? targetCardData.name
                : (!string.IsNullOrEmpty(externalFilePath)
                    ? Path.GetFileNameWithoutExtension(externalFilePath)
                    : (sourceAsset != null ? sourceAsset.name : "NewCardArt"));

            string savePath = EditorUtility.SaveFilePanelInProject(
                "Save Composed Card PNG",
                defaultName,
                "png",
                "Chọn nơi lưu file PNG thẻ bài (tự động thiết lập Sprite chuẩn).",
                defaultFolder);

            if (string.IsNullOrEmpty(savePath)) return;

            Texture2D exportTex = CardArtCompositorUtility.CompositeCard(
                readableSourceTex,
                selectedAffinities,
                selectedRarity,
                applyRarityFrame: bakeFrameIntoPng,
                wetAquaHairline: wetAquaHairline,
                previewTerraformArrows: false);

            try
            {
                CardRarity? assignRarity = targetCardData != null
                    ? (bakeFrameIntoPng ? CardRarity.Common : selectedRarity)
                    : (CardRarity?)null;

                Sprite sp = CardArtCompositorUtility.ExportPngAndConfigureSprite(
                    exportTex,
                    savePath,
                    targetCardData,
                    assignRarity);

                if (sp != null)
                {
                    EditorGUIUtility.PingObject(sp);
                }

                statusMessage = targetCardData != null
                    ? $"Đã xuất {savePath} và gắn vào {targetCardData.name} (Rarity = {assignRarity})."
                    : $"Đã xuất Sprite chuẩn 1024×1536 tại: {savePath}";
                statusType = MessageType.Info;
            }
            catch (Exception ex)
            {
                statusMessage = "Lỗi khi xuất ảnh: " + ex.Message;
                statusType = MessageType.Error;
                Debug.LogException(ex);
            }
            finally
            {
                DestroyImmediate(exportTex);
            }
        }

        private void DrawPreviewColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Live Card Preview (2:3)", EditorStyles.boldLabel);

            Rect area = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            // Warm Stone background mimicking P-03 Hand Tray
            EditorGUI.DrawRect(area, new Color(0.92f, 0.90f, 0.86f, 1f));

            const float pad = 28f;
            float availW = Mathf.Max(80f, area.width - pad * 2f);
            float availH = Mathf.Max(120f, area.height - pad * 2f - 28f);
            float aspect = 2f / 3f;
            float drawH = availH;
            float drawW = drawH * aspect;
            if (drawW > availW)
            {
                drawW = availW;
                drawH = drawW / aspect;
            }

            float cx = area.x + (area.width - drawW) * 0.5f;
            float cy = area.y + (area.height - 28f - drawH) * 0.5f;

            Rect artRect = new Rect(cx, cy, drawW, drawH);
            bool showCommonChrome = selectedRarity == CardRarity.Common || previewTex == null;
            if (showCommonChrome)
            {
                // Simulated #FCFBF8 CardUI white outer frame
                const float frameBorder = 8f;
                Rect frameRect = new Rect(cx - frameBorder, cy - frameBorder, drawW + frameBorder * 2f, drawH + frameBorder * 2f);
                Rect shadowRect = new Rect(frameRect.x, frameRect.y + 4f, frameRect.width, frameRect.height);
                EditorGUI.DrawRect(shadowRect, new Color(0.16f, 0.12f, 0.08f, 0.16f));
                EditorGUI.DrawRect(frameRect, new Color(0.988f, 0.984f, 0.973f, 1f));
            }

            if (previewTex != null)
            {
                GUI.DrawTexture(artRect, previewTex, ScaleMode.StretchToFill, true);

                if (selectedRarity != CardRarity.Common && !bakeFrameIntoPng && previewFrameTex != null)
                {
                    float bleedX = drawW * CardRarityFrameUtility.FrameBleedFracX;
                    float bleedY = drawH * CardRarityFrameUtility.FrameBleedFracY;
                    Rect outerFrameRect = new Rect(cx - bleedX, cy - bleedY, drawW + bleedX * 2f, drawH + bleedY * 2f);
                    GUI.DrawTexture(outerFrameRect, previewFrameTex, ScaleMode.StretchToFill, true);
                }
            }
            else
            {
                EditorGUI.DrawRect(artRect, new Color(0.84f, 0.81f, 0.77f, 1f));
                GUI.Label(artRect, "Kéo thả ảnh PNG vào đây\nhoặc chọn ở cột bên trái", previewHintStyle);
            }

            string frameMode = selectedRarity == CardRarity.Common
                ? "Common"
                : (bakeFrameIntoPng ? $"{selectedRarity} (Baked PNG)" : $"{selectedRarity} (Runtime UI Preview)");
            string footer = $"1024×1536  ·  Affinities: {selectedAffinities.Count}/3  ·  Frame: {frameMode}";
            Rect footerRect = new Rect(area.x, area.yMax - 24f, area.width, 20f);
            GUI.Label(footerRect, footer, previewFooterStyle);

            EditorGUILayout.EndVertical();
        }
    }
}
