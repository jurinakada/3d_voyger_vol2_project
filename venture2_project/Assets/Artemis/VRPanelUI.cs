using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Artemis
{
    /// <summary>
    /// VR空間内UI（World Space Canvas）。<see cref="SimulationHud"/> は Screen Space Overlay のため
    /// HMDの視界には映らない。同じ情報（Orion/月の座標・経過時間・フェーズ）と操作
    /// （速度 1x/2x/3x・視点 Overview/Orion）を、手元のタブレット状パネルとして3D空間に出す。
    ///
    /// Canvas類はコード生成する（SimulationHudと同じ流儀）。空のGameObjectに付けるだけで、
    /// XR Origin・EventSystem・Tracked Device Graphic Raycaster まで自動で用意する。
    /// </summary>
    public class VRPanelUI : MonoBehaviour
    {
        [Header("依存（未指定なら自動検出）")]
        public OrbitPlayer player;
        [Tooltip("IViewpointSwitcher を実装したコンポーネント。未指定なら VRViewpointRig → ViewpointController の順に探す。")]
        public MonoBehaviour viewpointBehaviour;

        [Header("配置")]
        [Tooltip("パネルの親。未指定なら XR Origin の Camera Offset（無ければメインカメラ）。")]
        public Transform panelAnchor;
        [Tooltip("親から見たパネル位置[m]。既定は目線の少し下・前方85cm。")]
        public Vector3 panelLocalPosition = new Vector3(0f, -0.30f, 0.85f);
        [Tooltip("パネルの傾き[deg]。X+で手前に寝かせる（タブレットを持つ角度）。")]
        public Vector3 panelLocalEuler = new Vector3(20f, 0f, 0f);
        [Tooltip("1pxあたりのワールドサイズ[m]。0.001なら900px≒0.9m幅。")]
        public float metersPerPixel = 0.001f;

        [Header("挙動")]
        [Tooltip("頭の向き（ヨー）に緩やかに追従させる。OFFでプレイエリアに固定。")]
        public bool followHeadYaw = true;
        [Tooltip("この角度[deg]を超えて頭を振ったときだけ追従を始める（常時追従は酔いと押しにくさの原因）。")]
        public float followDeadZoneDeg = 40f;
        [Tooltip("追従の速さ[deg/s]。")]
        public float followSpeed = 90f;
        [Tooltip("非VR（Game画面）でマウス確認できるよう Graphic Raycaster も併設する。")]
        public bool alsoEnableMouse = true;

        private IViewpointSwitcher _viewpoint;
        private Camera _eventCamera;
        private Transform _yawHolder;

        private Text _clockText, _coordText;
        private Button _speed1Btn, _speed2Btn, _speed3Btn;
        private Button _overviewBtn, _orionViewBtn;

        private float _holderYaw;

        private static readonly Color ActiveColor = new Color(0.25f, 0.55f, 1f);
        private static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.15f);

        void Awake()
        {
            if (player == null) player = FindAnyObjectByType<OrbitPlayer>();
            ResolveViewpoint();
            EnsureEventSystem();
            BuildPanel();
        }

        void Update()
        {
            if (player == null) return;

            var s = player.Current;
            _coordText.text =
                $"ORION   X {s.ox,9:F0}   Y {s.oy,9:F0}   Z {s.oz,9:F0}  km\n" +
                $"MOON    X {s.mx,9:F0}   Y {s.my,9:F0}   Z {s.mz,9:F0}  km";
            _clockText.text = player.MissionClock();

            SetActiveButton(_speed1Btn, Mathf.Approximately(player.speed, 1f));
            SetActiveButton(_speed2Btn, Mathf.Approximately(player.speed, 2f));
            SetActiveButton(_speed3Btn, Mathf.Approximately(player.speed, 3f));

            if (_viewpoint != null)
            {
                SetActiveButton(_overviewBtn, _viewpoint.Current == Viewpoint.Overview);
                SetActiveButton(_orionViewBtn, _viewpoint.Current == Viewpoint.OrionView);
            }
        }

        void LateUpdate()
        {
            // §タスクC 視界追従：頭を大きく振ったときだけヨーを追いかける（デッドゾーン付き）。
            if (!followHeadYaw || _yawHolder == null || _eventCamera == null) return;

            var parent = _yawHolder.parent;
            if (parent == null || parent == _eventCamera.transform) return;

            Vector3 headFwd = parent.InverseTransformDirection(_eventCamera.transform.forward);
            headFwd.y = 0f;
            if (headFwd.sqrMagnitude < 1e-6f) return;

            float headYaw = Quaternion.LookRotation(headFwd, Vector3.up).eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(_holderYaw, headYaw)) > followDeadZoneDeg)
                _holderYaw = Mathf.MoveTowardsAngle(_holderYaw, headYaw, followSpeed * Time.deltaTime);

            _yawHolder.localRotation = Quaternion.Euler(0f, _holderYaw, 0f);
        }

        // ------------------------------------------------------------------
        // 依存の解決
        // ------------------------------------------------------------------
        void ResolveViewpoint()
        {
            if (viewpointBehaviour is IViewpointSwitcher assigned)
            {
                _viewpoint = assigned;
                return;
            }
            if (viewpointBehaviour != null)
                Debug.LogWarning($"[VRPanelUI] {viewpointBehaviour.GetType().Name} は IViewpointSwitcher ではありません。自動検出に切り替えます。", this);

            var vrRig = FindAnyObjectByType<VRViewpointRig>();
            if (vrRig != null) _viewpoint = vrRig;
            else _viewpoint = FindAnyObjectByType<ViewpointController>();

            viewpointBehaviour = _viewpoint as MonoBehaviour;
            if (_viewpoint == null)
                Debug.LogWarning("[VRPanelUI] 視点切替コンポーネントが見つかりません。VIEWボタンは無効になります。", this);
        }

        /// <summary>タスクB-2：XRのレイでUIを押すには EventSystem + XR UI Input Module が要る。</summary>
        void EnsureEventSystem()
        {
            var es = FindAnyObjectByType<EventSystem>();
            if (es == null)
                es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

            var xrModule = es.GetComponent<XRUIInputModule>();
            if (xrModule == null)
            {
                // EventSystemが同時に使う入力モジュールは1つ。既存モジュールがあれば止めてから差し替える。
                foreach (var m in es.GetComponents<BaseInputModule>())
                {
                    m.enabled = false;
                    Debug.Log($"[VRPanelUI] {m.GetType().Name} を無効化し XRUIInputModule に切り替えました。", es);
                }
                xrModule = es.gameObject.AddComponent<XRUIInputModule>();
            }
            xrModule.enabled = true;
            xrModule.enableXRInput = true;
            xrModule.enableMouseInput = alsoEnableMouse;
        }

        /// <summary>パネルの親とイベントカメラを決める。XR Origin があれば Camera Offset 配下に置く。</summary>
        Transform ResolveAnchor()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            _eventCamera = (origin != null && origin.Camera != null) ? origin.Camera : Camera.main;

            if (panelAnchor != null) return panelAnchor;
            if (origin != null && origin.CameraFloorOffsetObject != null)
                return origin.CameraFloorOffsetObject.transform;
            if (_eventCamera != null) return _eventCamera.transform;   // 非VRフォールバック
            return transform;
        }

        // ------------------------------------------------------------------
        // UI構築（コード生成）
        // ------------------------------------------------------------------
        void BuildPanel()
        {
            var anchor = ResolveAnchor();

            var holderGo = new GameObject("VRPanelYawHolder");
            _yawHolder = holderGo.transform;
            _yawHolder.SetParent(anchor, false);

            const float w = 900f, h = 520f;
            var canvasGo = new GameObject("VRPanelCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            canvasGo.transform.SetParent(_yawHolder, false);
            canvasGo.transform.localPosition = panelLocalPosition;
            canvasGo.transform.localRotation = Quaternion.Euler(panelLocalEuler);
            canvasGo.transform.localScale = Vector3.one * metersPerPixel;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _eventCamera;
            var canvasRt = (RectTransform)canvasGo.transform;
            canvasRt.sizeDelta = new Vector2(w, h);

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3f;   // ワールド空間の文字をぼかさない

            if (alsoEnableMouse) canvasGo.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("Background", typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)bg.transform);
            bg.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.09f, 0.82f);

            // 経過時間＋フェーズ
            var clockRow = CreateRow(canvasGo.transform, "ClockRow", 24f, 70f);
            _clockText = CreateText(clockRow, "ClockText", 46, TextAnchor.MiddleLeft);
            _clockText.fontStyle = FontStyle.Bold;
            Stretch(_clockText.rectTransform);

            // Orion / 月の座標
            var coordRow = CreateRow(canvasGo.transform, "CoordRow", 104f, 110f);
            _coordText = CreateText(coordRow, "CoordText", 30, TextAnchor.UpperLeft);
            Stretch(_coordText.rectTransform);

            // 速度切替
            var speedRow = CreateRow(canvasGo.transform, "SpeedRow", 232f, 116f);
            AddRowLayout(speedRow);
            CreateRowLabel(speedRow, "SPEED");
            _speed1Btn = CreateButton(speedRow, "1x", () => player?.SetSpeed(1f));
            _speed2Btn = CreateButton(speedRow, "2x", () => player?.SetSpeed(2f));
            _speed3Btn = CreateButton(speedRow, "3x", () => player?.SetSpeed(3f));

            // 視点切替
            var viewRow = CreateRow(canvasGo.transform, "ViewRow", 368f, 116f);
            AddRowLayout(viewRow);
            CreateRowLabel(viewRow, "VIEW");
            _overviewBtn = CreateButton(viewRow, "Overview", () => _viewpoint?.SetOverview());
            _orionViewBtn = CreateButton(viewRow, "Orion View", () => _viewpoint?.SetOrionView());
        }

        /// <summary>パネル上端から topOffset[px] の位置に、左右24px余白の横一列を作る。</summary>
        RectTransform CreateRow(Transform parent, string name, float topOffset, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-48f, height);
            rt.anchoredPosition = new Vector2(0f, -topOffset);
            return rt;
        }

        void AddRowLayout(RectTransform row)
        {
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(0, 0, 8, 8);
            layout.childAlignment = TextAnchor.MiddleLeft;
        }

        void CreateRowLabel(RectTransform row, string label)
        {
            var t = CreateText(row, label + "Label", 30, TextAnchor.MiddleLeft);
            t.text = label;
            t.color = new Color(1f, 1f, 1f, 0.6f);
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 150f;
            le.flexibleWidth = 0f;
        }

        void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;
            t.text = "";
            return t;
        }

        Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = IdleColor;

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            var txt = CreateText(go.transform, "Label", 34, TextAnchor.MiddleCenter);
            Stretch(txt.rectTransform);
            txt.text = label;
            txt.raycastTarget = false;   // 文字がレイを吸ってボタンが反応しないのを防ぐ

            return btn;
        }

        void SetActiveButton(Button b, bool active)
        {
            if (b == null) return;
            var colors = b.colors;
            var target = active ? ActiveColor : IdleColor;
            if (colors.normalColor == target) return;
            colors.normalColor = target;
            b.colors = colors;
        }
    }
}
