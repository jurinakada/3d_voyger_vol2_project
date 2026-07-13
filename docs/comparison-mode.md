# 重ね合わせ比較モード (Phase 3)

ブランチ `feature/comparison-mode`。物理シミュレーション軌道と NASA 実データ軌道を同一シーンで重ね、
UI で再生操作・表示切替できる。既存の C#・シーン・CSV は無変更(追加のみ)。

## 使い方

`Assets/Scenes/MainScene_Compare.unity` を開いて Play。

- 白/既存色 = 物理シミュレーション(orion_trajectory.csv、Nec0ta の3体計算)
- オレンジ = NASA 実データの Orion(整合済み)
- 水色の球 = NASA 実データの月(実際の月位置)。灰色 = 物理モデルの月(半径384,400km固定の円軌道)
- 下のボタン: Play/Pause・速度(x0.5〜x5)・先頭へ・±5%シーク・Physics/NASA の表示切替
- 左上: ミッション時計(物理タイムライン) と 物理vs NASA の Orion 間距離 [km]

## 座標整合の方法 (scripts/build_comparison_csv.py)

NASA(EME2000・実時刻)を物理フレーム(月軌道面=xy平面・独自時刻)へ剛体変換:

1. 面合わせ: Horizons 月の角運動量ベクトル(フライバイ±1日平均)を +z へ回転(傾き28.28°)
2. 方位合わせ: 両者の月最接近(CA)時点の月方位角を一致させる z 回転
3. 時刻合わせ: CA 同士を同期(物理 t=3.91日 ⇔ NASA t=4.88日、シフト -83,440秒)

回転+平行移動のみなので軌道形状は保存される(正直な重ね合わせ)。

## 残る差 = モデルの簡略化そのもの(発表ネタ)

- 月の距離: 物理=384,400km固定円 vs 実際=393,562〜404,970km(遠地点付近) → 月が1.2万〜2.1万kmずれる
  = 「円軌道近似」の効果が目で見える
- 面外成分: 整合後の NASA 月の |z| は平均30km(0.008%) → 9日間なら平面近似はほぼ妥当、という検証にもなる
- フライバイ高度: 物理 6,540km vs NASA 6,546km(公式発表 6,545km)

## 実装メモ

- `NasaOverlayPlayer.cs`(新規): 整合CSVを読み、OrbitPlayer.CurrentMissionTimeSec に同期して
  NASA Orion/月/軌跡を実行時生成・描画。既存 TrajectoryLoader / TrajectoryRenderer を再利用。
- `ComparisonUI.cs`(新規): OrbitPlayer の既存UIフック(TogglePlay/SetSpeed/StepSeek/MissionClock)を
  呼ぶだけの実行時生成UI。シーンにUIオブジェクトを持たないので、他メンバーのシーン作業と衝突しない。
- 制約: 参照枠は地球基準のみ(月基準時は NASA 系を自動非表示)。VR用UIは未対応(XRI 側と統合するなら別途)。

## 再現

```bash
python3 scripts/build_comparison_csv.py   # 整合CSVの再生成
```
