using UnityEngine;
using UnityEngine.UI;

namespace Artemis
{
    /// <summary>
    /// §9.3 UI要件：Orion/月の座標と経過日数のリアルタイム表示、再生速度(1x/2x/3x)切替、
    /// 視点(俯瞰/Orion視点)切替ボタン。Canvas等をコード側で生成するため、
    /// シーン内の空GameObjectにこのスクリプトを付けるだけで動作する。
    /// </summary>
    public class SimulationHud : MonoBehaviour
    {
        public OrbitPlayer player;
        public ViewpointController viewpoint;

        private Text coordText;
        private Text clockText;
        private Button speed1Btn, speed2Btn, speed3Btn;
        private Button overviewBtn, orionViewBtn;

        void Awake()
        {
            if (player == null) player = FindFirstObjectByType<OrbitPlayer>();
            if (viewpoint == null) viewpoint = FindFirstObjectByType<ViewpointController>();
            BuildUI();
        }

        void Update()
        {
            if (player == null) return;

            var s = player.Current;
            coordText.text =
                $"Orion  X:{s.ox,10:F0}  Y:{s.oy,10:F0}  Z:{s.oz,10:F0} km\n" +
                $"Moon   X:{s.mx,10:F0}  Y:{s.my,10:F0}  Z:{s.mz,10:F0} km";

            clockText.text = player.MissionClock();

            SetActiveButton(speed1Btn, Mathf.Approximately(player.speed, 1f));
            SetActiveButton(speed2Btn, Mathf.Approximately(player.speed, 2f));
            SetActiveButton(speed3Btn, Mathf.Approximately(player.speed, 3f));

            if (viewpoint != null)
            {
                SetActiveButton(overviewBtn, viewpoint.Current == Viewpoint.Overview);
                SetActiveButton(orionViewBtn, viewpoint.Current == Viewpoint.OrionView);
            }
        }

        void SetActiveButton(Button b, bool active)
        {
            if (b == null) return;
            var colors = b.colors;
            colors.normalColor = active ? new Color(0.25f, 0.55f, 1f) : new Color(1f, 1f, 1f, 0.15f);
            b.colors = colors;
        }

        // ------------------------------------------------------------------
        // UI構築（コード生成）
        // ------------------------------------------------------------------
        void BuildUI()
        {
            var canvasGo = new GameObject("SimulationHudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 左上：座標・経過時間パネル
            var infoPanel = CreatePanel(canvasGo.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -20), new Vector2(560, 130));
            coordText = CreateText(infoPanel.transform, "CoordText", 22, TextAnchor.UpperLeft);
            SetRect(coordText.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(10, 50), new Vector2(-10, 120));
            clockText = CreateText(infoPanel.transform, "ClockText", 26, TextAnchor.UpperLeft);
            clockText.fontStyle = FontStyle.Bold;
            SetRect(clockText.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(10, 10), new Vector2(-10, 40));

            // 下部中央：速度切替ボタン
            var speedPanel = CreatePanel(canvasGo.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(0, 20), new Vector2(300, 60));
            var speedLayout = speedPanel.AddComponent<HorizontalLayoutGroup>();
            speedLayout.spacing = 10;
            speedLayout.childControlWidth = true;
            speedLayout.childControlHeight = true;
            speedLayout.childForceExpandWidth = true;
            speedLayout.childForceExpandHeight = true;
            speedLayout.padding = new RectOffset(10, 10, 10, 10);
            speed1Btn = CreateButton(speedPanel.transform, "1x", () => player.SetSpeed(1f));
            speed2Btn = CreateButton(speedPanel.transform, "2x", () => player.SetSpeed(2f));
            speed3Btn = CreateButton(speedPanel.transform, "3x", () => player.SetSpeed(3f));

            // 右下：視点切替ボタン
            var viewPanel = CreatePanel(canvasGo.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-20, 20), new Vector2(320, 60));
            var viewLayout = viewPanel.AddComponent<HorizontalLayoutGroup>();
            viewLayout.spacing = 10;
            viewLayout.childControlWidth = true;
            viewLayout.childControlHeight = true;
            viewLayout.childForceExpandWidth = true;
            viewLayout.childForceExpandHeight = true;
            viewLayout.padding = new RectOffset(10, 10, 10, 10);
            overviewBtn = CreateButton(viewPanel.transform, "俯瞰視点", () => viewpoint?.SetOverview());
            orionViewBtn = CreateButton(viewPanel.transform, "Orion視点", () => viewpoint?.SetOrionView());
        }

        GameObject CreatePanel(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject("Panel", typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0.45f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
            return go;
        }

        void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }

        Text CreateText(Transform parent, string name, int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = "";
            return t;
        }

        Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.15f);
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            var txt = CreateText(go.transform, "Label", 22, TextAnchor.MiddleCenter);
            var trt = txt.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            txt.text = label;

            return btn;
        }
    }
}
