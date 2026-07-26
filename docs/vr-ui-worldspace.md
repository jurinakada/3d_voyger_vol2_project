# VR空間内UI（World Space Canvas）— 実装メモ

引き継ぎ書 `texts/引き継ぎ書_VR-UI実装.md` のタスクA・B・Cに対応する実装。

## 使い方（Unity Editor で3手）

1. Unity で `Assets/Scenes/VRScene.unity` を開く。
2. メニュー **Artemis → Build VR Scene** を実行する。
   - 既存の XR Origin を Starter Assets の完成品リグに置き換えるか確認ダイアログが出る → **置き換える**。
   - 元の VRScene のリグはコントローラの3Dモデルだけで、`TrackedPoseDriver`（位置追従）も
     インタラクタ（レイ）も入っていないため、そのままではUIを押せない。
3. Play して HMD をかぶる。

何度実行しても結果は同じ（既存オブジェクトは名前で見つけて設定を上書きするだけ）。
`MainScene` / `MainScene_NASA` には一切触れない。

## Build VR Scene が作るもの

| オブジェクト | 内容 |
|---|---|
| Earth / Moon / Orion | 球。表示半径は `ScaleConfig` の誇張値（8 / 3 / 0.6 unit）をそのまま使用。色分けマテリアルを `Assets/Artemis/Materials` に生成 |
| TrajectoryTrail | `LineRenderer` + `TrajectoryRenderer`。線幅 1.5 unit（=1500km 相当） |
| OrbitPlayer | `orion_trajectory.csv` と `ScaleConfig.asset` を割当て、playbackSeconds=180 |
| XR Origin (XR Rig) | Starter Assets の完成品。トラッキング・Near-Far Interactor（UI操作有効）・移動が設定済み |
| VR UI | `VRViewpointRig` + `VRPanelUI` |

XRカメラの far clip は 5000 に上げている（地球‑月系は 400 unit 超あり、既定 1000 だと復路が切れる）。

## 追加したスクリプト

| ファイル | 役割 |
|---|---|
| `Assets/Artemis/VRPanelUI.cs` | World Space Canvas をコード生成。座標・経過時間・フェーズの表示と、速度 1x/2x/3x・視点 Overview/Orion のボタン。`TrackedDeviceGraphicRaycaster` と `EventSystem`＋`XRUIInputModule` も自動で用意する |
| `Assets/Artemis/VRViewpointRig.cs` | VR用の視点切替。XR Origin 側を動かす |
| `Assets/Artemis/IViewpointSwitcher.cs` | 視点切替の共通口。非VR用 `ViewpointController` とVR用 `VRViewpointRig` を同じUIから扱う |
| `Assets/Artemis/Editor/VRSceneBuilder.cs` | 上記の組み立てメニュー |

`OrbitPlayer` の公開APIシグネチャは変更していない。`1 unit = 1000 km`（`ScaleConfig`）と
CSVの数値定義にも触れていない。既存の `SimulationHud`（Screen Space）は非VR確認用にそのまま残している。

## 設計上の判断

### 視点切替でカメラを直接動かさない
`ViewpointController` は Main Camera の `transform` を毎フレーム書く。HMDのカメラは
`TrackedPoseDriver` が姿勢を書くため、VRでこれを使うと競合する。
`VRViewpointRig` は代わりに **XR Origin 自体**を動かす。

### 「大きさ」は XR Origin の localScale で表現する
1 unit = 1000 km のままだと、人間サイズの利用者から見て地球‑月系（384 unit）は広すぎる。
XR Origin を拡大して利用者を巨人にすることで、系全体が模型サイズに見える
（俯瞰=30倍、Orion視点=1倍。インスペクタで調整可）。**CSVもScaleConfigも変更していない。**

### 重力を切る
Starter Assets のリグは**床のある部屋**を前提に `GravityProvider` が有効になっている。
宇宙空間には床が無く、俯瞰視点は切替時にしか位置を書かないため、そのままだと自由落下し続ける
（Orion視点は毎フレーム位置を書くので症状が出ない）。
`VRViewpointRig.disableRigGravity`（既定ON）が実行時に切り、`Build VR Scene` はシーン側にも保存する。
上下方向にも移動したい場合は `enableFlyMovement` をON。

### 酔い対策
- 俯瞰視点の位置は**切替時に一度だけ**適用する。毎フレーム上書きすると Starter Assets の
  スティック移動・テレポートを打ち消してしまうため。
- Orion視点のヨーは切替時のみ整列させ、その後は回さない（連続回転は酔いの主因）。
  毎フレーム進行方向へ追従させたい場合は `continuousYawAlign` を ON。
- 情報パネルは頭のヨーに追従するが、`followDeadZoneDeg`（既定40°）を超えて頭を振ったときだけ
  ゆっくり動く。常時追従はボタンが押しにくくなるため。

## 既知の注意点

- **ラベルは英語**（Overview / Orion View / SPEED / VIEW）。Unity組み込みフォントは日本語グリフを
  持たず豆腐（□）になるため。日本語にする場合は TextMeshPro の日本語フォントアセット作成が別途必要。
- **Orion視点の月フライバイ**：月の表示半径は誇張値 3 unit（=3000km、実半径1737km）なので、
  最接近時にカメラが月の球体の内側に入る。発表で月面接近を見せるなら
  `ScaleConfig.moonDisplayRadiusUnit` を実寸寄りに下げる必要がある（俯瞰での見え方とトレードオフ）。
- **NASA実データ版**を使う場合は `VRSceneBuilder.k_CsvPath` を `nasa_orion_trajectory.csv` に変えるか、
  Play前に OrbitPlayer の `csvFile` を差し替える。
- `InputActionManager` が無いとコントローラ入力が来ない。Starter Assets のリグには同梱されているが、
  リグを置き換えなかった場合は警告がコンソールに出る。
