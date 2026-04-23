using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace Parable.Rendering.Editor
{
    public static class ToonUIPrefabBuilder
    {
        static Font s_Font;
        static DefaultControls.Resources s_Res;

        [MenuItem("Parable/Build Toon Control Panel Prefab")]
        public static void Build()
        {
            s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            s_Res  = new DefaultControls.Resources
            {
                standard   = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                knob       = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark  = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown   = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask       = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            };

            // ── Canvas ──────────────────────────────────────────────
            var root   = new GameObject("ToonControlPanel");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            // ── Panel ────────────────────────────────────────────────
            var panel  = Sub("Panel", root.transform);
            panel.AddComponent<Image>().color = Hex(0.06f, 0.06f, 0.10f, 0.92f);
            var prt    = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0, 0);
            prt.anchorMax = new Vector2(0, 1);
            prt.pivot     = new Vector2(0, 0.5f);
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = new Vector2(190, 0);

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding               = new RectOffset(8, 8, 12, 8);
            vlg.spacing               = 3f;
            vlg.childControlWidth     = true;
            vlg.childControlHeight    = false;
            vlg.childForceExpandWidth = true;

            // ── Contents ─────────────────────────────────────────────
            MakeLbl(panel, "TOON CONTROL", 13, FontStyle.Bold,
                    Hex(0.88f,0.90f,1.00f), 28f, TextAnchor.MiddleCenter);
            MakeSep(panel);

            MakeLbl(panel, "OUTLINE", 9, FontStyle.Bold, Hex(0.45f,0.62f,0.80f), 15f);
            MakeBtn(panel, "Outline: ON",  "Btn_OutlineToggle",  Hex(0.18f,0.24f,0.42f));
            MakeSld(panel, "Outline Width", "Slider_OutlineWidth",
                    0f, 0.03f, 0.005f);
            MakeSep(panel);

            MakeLbl(panel, "RAMP", 9, FontStyle.Bold, Hex(0.45f,0.62f,0.80f), 15f);
            MakeBtn(panel, "Hard Edge",    "Btn_RampHard",  Hex(0.38f,0.18f,0.18f));
            MakeBtn(panel, "Soft Edge",    "Btn_RampSoft",  Hex(0.18f,0.35f,0.20f));
            MakeSld(panel, "Threshold",    "Slider_RampThreshold",
                    0f, 1f, 0.45f);
            MakeSep(panel);

            MakeLbl(panel, "FX", 9, FontStyle.Bold, Hex(0.45f,0.62f,0.80f), 15f);
            MakeBtn(panel, "ToonPass: ON", "Btn_ShadowToggle", Hex(0.28f,0.18f,0.42f));
            MakeBtn(panel, "Color Cycle",  "Btn_ColorCycle",   Hex(0.18f,0.32f,0.42f));
            MakeSep(panel);

            MakeLbl(panel, "SHADER", 9, FontStyle.Bold, Hex(0.45f,0.62f,0.80f), 15f);
            MakeBtn(panel, "→ Original",   "Btn_ShaderSwap",   Hex(0.35f,0.22f,0.12f));

            // ── Save prefab ──────────────────────────────────────────
            const string path =
                "Assets/ParablePortfolio/Runtime/Prefabs/ToonControlPanel.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Debug.Log("[Parable] ToonControlPanel.prefab saved → " + path);
        }

        // ── Helpers ──────────────────────────────────────────────────

        static Color Hex(float r, float g, float b, float a = 1f)
            => new Color(r, g, b, a);

        static GameObject Sub(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        static void MakeLbl(GameObject p, string text, int size,
                            FontStyle style, Color col, float h,
                            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go  = Sub(text, p.transform);
            var txt = go.AddComponent<Text>();
            txt.text = text; txt.font = s_Font; txt.fontSize = size;
            txt.fontStyle = style; txt.color = col; txt.alignment = anchor;
            go.AddComponent<LayoutElement>().preferredHeight = h;
        }

        static void MakeBtn(GameObject p, string label, string goName, Color accent)
        {
            var go  = Sub(goName, p.transform);
            var img = go.AddComponent<Image>();
            img.color  = accent;
            img.sprite = s_Res.standard;
            img.type   = Image.Type.Sliced;

            var btn = go.AddComponent<Button>();
            var cb  = btn.colors;
            cb.normalColor      = accent;
            cb.highlightedColor = accent * 1.35f;
            cb.pressedColor     = accent * 0.60f;
            cb.selectedColor    = accent * 1.15f;
            btn.colors         = cb;
            btn.targetGraphic  = img;

            var tgo = Sub("Text", go.transform);
            var txt = tgo.AddComponent<Text>();
            txt.text = label; txt.font = s_Font; txt.fontSize = 12;
            txt.color = Color.white; txt.alignment = TextAnchor.MiddleCenter;
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            go.AddComponent<LayoutElement>().preferredHeight = 32f;
        }

        static void MakeSld(GameObject p, string labelText,
                            string goName, float min, float max, float val)
        {
            // 레이블
            var lgo  = Sub(labelText, p.transform);
            var ltxt = lgo.AddComponent<Text>();
            ltxt.text = labelText; ltxt.font = s_Font; ltxt.fontSize = 10;
            ltxt.color = Hex(0.70f, 0.76f, 0.85f);
            ltxt.alignment = TextAnchor.MiddleLeft;
            lgo.AddComponent<LayoutElement>().preferredHeight = 14f;

            // DefaultControls.CreateSlider 로 표준 슬라이더 생성
            var sldGO = DefaultControls.CreateScrollbar(s_Res);
            sldGO.name = goName;
            sldGO.transform.SetParent(p.transform, false);

            // Scrollbar → Slider 교체 (DefaultControls에 CreateSlider 없으므로)
            Object.DestroyImmediate(sldGO.GetComponent<Scrollbar>());
            var slider    = sldGO.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value    = val;
            slider.direction = Slider.Direction.LeftToRight;

            // fill 연결
            var fillArea = sldGO.transform.Find("Sliding Area/Handle");
            if (fillArea != null)
                slider.handleRect = fillArea.GetComponent<RectTransform>();

            // 배경 색
            var bgImg = sldGO.GetComponent<Image>();
            if (bgImg) bgImg.color = Hex(0.14f, 0.14f, 0.20f);

            sldGO.AddComponent<LayoutElement>().preferredHeight = 24f;
        }

        static void MakeSep(GameObject p)
        {
            var go  = Sub("---", p.transform);
            go.AddComponent<Image>().color = Hex(0.22f, 0.28f, 0.38f);
            go.AddComponent<LayoutElement>().preferredHeight = 1f;
        }
    }
}