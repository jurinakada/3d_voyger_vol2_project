using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Artemis
{
    /// <summary>
    /// 比較シーン用の最小UI(実行時生成・シーン非依存)。
    /// OrbitPlayer の既存UIフック(§9.3: TogglePlay / SetSpeed / StepSeek / MissionClock)を
    /// そのまま呼ぶだけで、既存コードは変更しない。
    /// 表示: 再生/停止・速度・シーク・物理/NASA表示切替・ミッション時計・両Orion間距離。
    /// </summary>
    public class ComparisonUI : MonoBehaviour
    {
        private OrbitPlayer orbit;
        private NasaOverlayPlayer overlay;
        private Text clockLabel, sepLabel;
        private Font font;
        private double missionStart, missionEnd;
        private bool physicsVisible = true;

        void Start()
        {
            orbit = Object.FindFirstObjectByType<OrbitPlayer>();
            if (orbit == null) { enabled = false; return; }
            overlay = Object.FindFirstObjectByType<NasaOverlayPlayer>();

            // シーク用に物理CSVの時間範囲を取得(OrbitPlayerは範囲を公開していないため同じCSVを読む)
            if (orbit.csvFile != null)
            {
                var d = TrajectoryLoader.Parse(orbit.csvFile.text);
                if (d.Count > 0) { missionStart = d[0].tSec; missionEnd = d[d.Count - 1].tSec; }
            }

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("ComparisonCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            // 下段ボタン列
            float x = 10f;
            x = AddButton(canvasGo.transform, x, "Play/Pause", () => orbit.TogglePlay());
            x = AddButton(canvasGo.transform, x, "x0.5", () => orbit.SetSpeed(0.5f));
            x = AddButton(canvasGo.transform, x, "x1", () => orbit.SetSpeed(1f));
            x = AddButton(canvasGo.transform, x, "x2", () => orbit.SetSpeed(2f));
            x = AddButton(canvasGo.transform, x, "x5", () => orbit.SetSpeed(5f));
            x = AddButton(canvasGo.transform, x, "|<", () => orbit.StepSeek(0f));
            x = AddButton(canvasGo.transform, x, "-5%", () => SeekRelative(-0.05f));
            x = AddButton(canvasGo.transform, x, "+5%", () => SeekRelative(0.05f));
            x = AddButton(canvasGo.transform, x, "Physics On/Off", TogglePhysics);
            if (overlay != null)
                AddButton(canvasGo.transform, x, "NASA On/Off", () => overlay.visible = !overlay.visible);

            // 上段ラベル
            clockLabel = AddLabel(canvasGo.transform, new Vector2(10, -10));
            sepLabel = AddLabel(canvasGo.transform, new Vector2(10, -36));
        }

        void Update()
        {
            if (orbit == null || clockLabel == null) return;
            clockLabel.text = orbit.MissionClock();
            if (sepLabel != null)
            {
                if (overlay != null && !double.IsNaN(overlay.CurrentSeparationKm))
                    sepLabel.text = $"Physics vs NASA separation: {overlay.CurrentSeparationKm:N0} km";
                else
                    sepLabel.text = "NASA overlay: hidden / out of range";
            }
        }

        void SeekRelative(float delta)
        {
            if (missionEnd <= missionStart) return;
            float frac = (float)((orbit.CurrentMissionTimeSec - missionStart) / (missionEnd - missionStart));
            orbit.StepSeek(Mathf.Clamp01(frac + delta));
        }

        void TogglePhysics()
        {
            physicsVisible = !physicsVisible;
            if (orbit.orion != null) orbit.orion.gameObject.SetActive(physicsVisible);
            if (orbit.moon != null) orbit.moon.gameObject.SetActive(physicsVisible);
            if (orbit.trailRenderer != null) orbit.trailRenderer.gameObject.SetActive(physicsVisible);
            // 地球は共通なので常時表示
        }

        float AddButton(Transform parent, float x, string label, UnityEngine.Events.UnityAction onClick)
        {
            float w = 24f + label.Length * 9f;
            var go = new GameObject("Btn_" + label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(x, 10);
            rt.sizeDelta = new Vector2(w, 30);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.12f, 0.16f, 0.25f, 0.85f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGo = new GameObject("Text", typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var txt = textGo.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = 14;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = label;

            return x + w + 6f;
        }

        Text AddLabel(Transform parent, Vector2 pos)
        {
            var go = new GameObject("Label", typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(640, 24);
            var txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = 16;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = Color.white;
            return txt;
        }
    }
}
