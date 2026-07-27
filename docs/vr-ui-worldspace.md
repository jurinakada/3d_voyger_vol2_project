# VR空間内UI（World Space Canvas）— 実装メモ

引き継ぎ書 `texts/引き継ぎ書_VR-UI実装.md` のタスクA・B・Cに対応する実装。

## 使い方（Unity Editor で3手）

1. Unity で `Assets/Scenes/VRScene.unity` を開く。
2. メニュー **Artemis → Build VR Scene** を実行する。
   - 既存の XR Origin を Starter Assets の完成品リグに置き換えるか確認ダイアログが出る → **置き換える**。
   - 元の VRScene のリグはコントローラの3Dモデルだけで、`TrackedPoseDriver`（位置追従）も
     インタラクタ（レイ）も入っていないため、そのままではUIを押せない。
3. Play して HMD をかぶる。

## 操作

| 入力 | 動作 |
|---|---|
| 左スティック | 見ている方向へ飛ぶ（上下含む3D。倒した方向へ移動、左右で横滑り） |
| 右スティック 左右 | スナップターン（座ったままでも向きを変えられる） |
| トリガー | パネルのボタンを押す（レイを当てて引く） |
| 頭を振る | 見回す。パネルは40°を超えたときだけゆっくり追従する |

テレポートは無効にしている（宇宙空間には飛び先の床が無く、狙っている間はUIのレイが消えてしまうため）。
移動の速さは `VRViewpointRig.moveSpeed`（既定2.5）。実際の速さには XR Origin の拡大率が掛かるので、
俯瞰視点では 75 unit/s ＝ 地球‑月間（384 unit）を約5秒で横断する。

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
| 星空スカイボックス | 既定の青い空を星空に差し替え（下記） |

XRカメラの far clip は 5000 に上げている（地球‑月系は 400 unit 超あり、既定 1000 だと復路が切れる）。

## 追加したスクリプト

| ファイル | 役割 |
|---|---|
| `Assets/Artemis/VRPanelUI.cs` | World Space Canvas をコード生成。座標・経過時間・フェーズの表示と、速度 1x/2x/3x・視点 Overview/Orion のボタン。`TrackedDeviceGraphicRaycaster` と `EventSystem`＋`XRUIInputModule` も自動で用意する |
| `Assets/Artemis/VRViewpointRig.cs` | VR用の視点切替。XR Origin 側を動かす |
| `Assets/Artemis/IViewpointSwitcher.cs` | 視点切替の共通口。非VR用 `ViewpointController` とVR用 `VRViewpointRig` を同じUIから扱う |
| `Assets/Artemis/Editor/VRSceneBuilder.cs` | 上記の組み立てメニュー |
| `Assets/Artemis/Editor/StarfieldSkyboxBuilder.cs` | 星空キューブマップの生成とスカイボックス設定 |

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

### リグを「部屋」向けから「宇宙空間」向けに設定し直す
Starter Assets のリグは**床のある部屋**を前提にしている。宇宙空間ではそのままだと3つ壊れる。

| 既定の挙動 | 宇宙での症状 | `VRViewpointRig` の対処（既定ON） |
|---|---|---|
| `GravityProvider` が有効 | 床が無いので落下し続ける（俯瞰視点は切替時にしか位置を書かないため症状が出る。Orion視点は毎フレーム書くので出ない） | `disableRigGravity` |
| スティック＝テレポート（`ControllerInputActionManager.smoothMotionEnabled` が false で **Moveアクション自体が無効**） | テレポート先の床が無いので、スティックを倒しても何も起きない | `useContinuousMove` / `enableFlyMovement` / `disableTeleport` |
| レイの到達距離がワールド固定値（`castDistance` 10 unit） | 俯瞰視点は XR Origin を30倍にするためパネルが約27 unit 先に来て、**レイがボタンまで届かない** | `scaleInteractorReach` |

3つ目が分かりにくい。パネルは XR Origin の子なので拡大率ぶん遠ざかるが、
`CurveInteractionCaster.castDistance` と `CurveVisualController.maxVisualCurveDistance` は
ワールド空間の固定値なので一緒には伸びない。視点を切り替えるたびに拡大率を掛け直している
（線の太さ `LineRenderer.widthMultiplier` も同様）。

`Build VR Scene` は重力・飛行・スティック割当をシーン側にも保存するので、
インスペクタでも設定が見える。

### 背景は星空キューブマップを自前生成する
既定は Unity 組み込みの青いプロシージャル空で、宇宙空間に見えない。
アセットストアからのダウンロードに依存させたくないため、`StarfieldSkyboxBuilder` が
星空のキューブマップ（1面512px×6面）をコードで描いて `Skybox/Cubemap` マテリアルに割り当てる。

- **Artemis → Build Starfield Skybox** で単独実行できる（`Build VR Scene` からも自動で呼ばれる）。
- 生成物は `Assets/Artemis/Materials/StarfieldCubemap.asset` と `StarfieldSkybox.mat`。
  メニューから実行したときは**毎回描き直す**（乱数の種もずらすので星並びが変わる）。
  `Build VR Scene` 経由では既存があれば再利用するため、シーンを組み直しても空は変わらない。
- 星の細かさは `k_FaceSize`、密度は `k_StarsPerFace`（既定220／面、全天で約1300個）で変わる。
  明暗の偏りは `SplatStar` の `magnitude` の指数（既定3。上げるほど暗い星が増える）。
- 面の中央から離れた画素ほど立体角が小さいため、`(1+s²+t²)^-1.5` の確率で間引いている
  （等確率で置くとキューブの角に星が密集して見える）。
- 空が暗くなると環境光もほぼ0になり天体の影側が真っ黒になるので、
  `AmbientMode.Flat` で弱い環境光（0.10, 0.11, 0.14）を別に入れている。

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
