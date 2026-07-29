# 引き継ぎ書 — VR空間内UI（World Space Canvas）実装

> **実装状況（2026-07-27）**：タスクA・B・Cを実装済み。手順と設計判断は `docs/vr-ui-worldspace.md` を参照。
> Unity で VRScene を開き、メニュー **Artemis → Build VR Scene** を実行すれば組み上がる。
> 下記の記述からの差分：
> - `PlaybackControls.cs`（OnGUI）は存在せず、`SimulationHud.cs`（Screen Space uGUI）＋ `ViewpointController.cs` として実装済みだった。VRに映らない点は同じなので、World Space 版 `VRPanelUI.cs` を新設し、`SimulationHud` は非VR確認用に残した。
> - 「`CurrentSample` ゲッターを追加してよい」は `OrbitPlayer.Current` として既に存在した。
> - VRScene には軌道シミュ本体が入っておらず、コントローラも3Dモデルだけ（追従もレイも無し）だったため、シミュ一式の配置とXRリグの差し替えも併せて行った。
> - 視点切替はVRではカメラを直接動かせないため、XR Origin を動かす `VRViewpointRig.cs` を新設した。

## この作業のゴール
現在ある画面固定UI（座標・経過時間・速度ボタン・視点切替）を、**VRヘッドセットの視界に映る3D空間内のパネル（手元のタブレット状）**に変換する。VRを被った人が、コントローラーのレーザーポインタでボタンを操作できる状態にする。

## プロジェクトの前提・背景
- 内容：地球‑月の自由帰還軌道（Artemis Ⅱ 型）シミュレーションのUnity可視化。
- 軌道はPythonで事前計算し、CSV（約9千行：経過時間・区間名・Orion位置・月位置・Orion速度）をUnityが読み込んで再生している。**Unity側で重力計算はしていない（CSVリプレイ）。**
- スケール基準：**1 Unityユニット = 1000 km**（地球‑月 ≈ 384ユニット）。座標変換は `ScaleConfig` に集約。
- 名前空間はすべて `Artemis`。

## 既存スクリプト（Assets/Artemis 内）
| ファイル | 役割 |
|---|---|
| `ScaleConfig.cs` | km→Unityユニット変換（1unit=1000km）。ScriptableObject。 |
| `TrajectoryLoader.cs` | CSVをdoubleで解析＋線形補間。 |
| `OrbitPlayer.cs` | 再生の中核。時間スケーリング・参照枠切替・時刻表示。**UIから呼ぶ公開APIを保持（下記）。** |
| `TrajectoryRenderer.cs` | 軌跡のLineRenderer描画。 |
| `VRComfortRig.cs` | VR酔い対策（緩速移動・スナップ回転・固定枠・ビネット）。 |
| `PlaybackControls.cs` | 現在の再生UI。**OnGUIで画面固定＝VRには映らない。今回置き換え/追加対象。** |

## OrbitPlayer が公開しているAPI（UIから呼ぶ。シグネチャ変更しないこと）
```csharp
public void TogglePlay();                 // 再生/一時停止トグル
public void SetSpeed(float s);            // 速度倍率（等倍=1, 2倍, 3倍…）
public void StepSeek(float frac01);       // 0..1で時刻ジャンプ
public void SwitchFrame(ReferenceFrame f);// Earth / Moon 参照枠
public string MissionClock();             // 例 "T+3d 21:48 [flyby]"
public float speed;    // 現在速度（読み取り可）
public bool  playing;  // 再生中フラグ（読み取り可）
public double CurrentMissionTimeSec;      // 現在ミッション時刻[s]
public string CurrentPhase;               // outbound/flyby/return 等
```
※ 座標表示（Orion / Moon の X,Y,Z[km]）が必要なら、`OrbitPlayer` に現在サンプルを公開するゲッターを1つ追加してよい（例：`public TrajectorySample CurrentSample { get; private set; }`）。`ApplyState()` 内で代入するだけ。既存の再生ロジックは壊さないこと。

## 発表で必要なUI内容（既存踏襲）
- Orion座標 (X,Y,Z) [km]、Moon座標 (X,Y,Z) [km] をリアルタイム表示
- 経過時間（日+時刻）と現在フェーズ
- 速度切替ボタン：等倍 / 2倍 / 3倍
- 視点切替ボタン：俯瞰視点 / Orion視点（＝ReferenceFrame or カメラ切替。現行実装に合わせる）

## 実装タスク

### タスクA：uGUIのWorld Space パネルを新設（最優先・確実）
1. `Assets/Artemis` に **World Space Canvas** を作成（Render Mode = World Space）。
2. Canvas の Event Camera に XR Origin の Main Camera を割当。
3. Canvas サイズは大きいので Scale ≈ 0.001、プレイヤー前方やや下（例 localPosition (0, -0.3, 0.8)）に配置。
4. パネルに TextMeshPro でラベル（座標・時刻・フェーズ）、Button（等倍/2倍/3倍、俯瞰/Orion）を配置。
5. 表示更新用に新規 `VRPanelUI.cs`（MonoBehaviour, namespace Artemis）を作成し、`OrbitPlayer` を参照して毎フレーム
   - `MissionClock()` をラベルへ
   - 現在サンプルの Orion/Moon 座標をラベルへ（上記ゲッターを使用）
   - Button.onClick を `SetSpeed(1/2/3)`、視点切替へ配線
6. **OnGUI版 `PlaybackControls` は無効化 or 非VRデバッグ用として残す**（削除でなく共存可）。

### タスクB：VRでボタンを押せるようにする
1. World Space Canvas の Graphic Raycaster を **Tracked Device Graphic Raycaster**（XR Interaction Toolkit）に差し替え。
2. シーンに **XR UI Input Module**（EventSystem）を用意。
3. コントローラーに **XR Ray Interactor**＋Line Visual を付け、UIをレイで押せることを確認。

### タスクC：視界追従（HUD化）※任意
- 読むだけの情報パネルはカメラ（XR Origin の Camera Offset か Main Camera）の子にして視界に追従させる。
- 操作ボタンは空間固定のままでもよい（追従すると押しづらいため）。

## 制約・注意
- **1 unit = 1000 km の縮尺、CSVの数値定義、`OrbitPlayer` の公開APIシグネチャは変更しない。**
- VRの「Orion視点」は高速移動で酔いやすい。`VRComfortRig` を有効化し、Orion視点は短時間運用を想定。
- 依存パッケージ：XR Plugin Management / OpenXR / XR Interaction Toolkit / TextMeshPro。未導入なら Package Manager で追加。
- 発表フォールバック：XRレイ操作が間に合わない場合、情報パネル（読むだけ）だけVRに出し、ボタン操作はPC側の担当が行う分担で可。

## 完了条件（Definition of Done）
1. VRヘッドセット視界内に、座標・経過時間・フェーズが実時間で表示される。
2. 速度（等倍/2倍/3倍）と視点（俯瞰/Orion）をコントローラーのレイで切り替えられる。
3. 既存の軌道再生・スケール・CSV読み込みが従来どおり動作する（回帰なし）。
4. 非VR（Game画面）でも従来のUIまたは新パネルで最低限の確認ができる。

## 参考：最終発表ドキュメント
チームの最終発表ドキュメント（要件の一次ソース）を併せて参照すること。UIに出す項目・視点の定義はそちらを優先。
