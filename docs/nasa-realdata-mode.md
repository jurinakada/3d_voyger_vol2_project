# NASA 実データモード (Phase 2.5)

ブランチ `feature/nasa-realdata-mode`。main は壊さず、追加のみ。C# は1行も変更していない。

## 何をしたか

NASA AROW OEM の実エフェメリス(Orion の実飛行由来 state vector)を、既存の
`Artemis.OrbitPlayer` / `Artemis.TrajectoryLoader` がそのまま再生できる列形式に変換した。
これにより「実データモード」は CSV を差し替えるだけで成立する(新規スクリプト不要 = コンパイルを壊さない)。

追加ファイル:
- `data/processed/artemis2_trajectory.csv` … NASA AROW OEM 由来の生CSV(+ manifest)。出所の一次データ。
- `scripts/nasa_to_unity_csv.py` … 生CSV → 再生用形式への変換器(再現用)。
- `venture2_project/Assets/Artemis/nasa_orion_trajectory.csv` … 再生用。既存形式 `t_sec,phase,orion_x..z,moon_x..z,orion_vx..vz`、3262行。

## データ出所

NASA AROW(Artemis Real-time Orbit Website)公開の Artemis II エフェメリス。
- OEM: `Artemis_II_OEM_2026_04_10_Post-ICPS-Sep-to-EI.asc`(CCSDS OEM 2.0)
- 発: NASA/JSC/FOD/FDO、OBJECT=EM2、地球中心 EME2000、UTC、km / km/s
- 区間: 2026-04-02 〜 2026-04-10(約8.91日)、3262点
- 用途: NASA が一般の可視化・物理モデル用途に公開しているもの

詳細は `data/processed/artemis2_trajectory_manifest.json`。

## Unity での検証手順(main を壊さない)

1. このブランチ `feature/nasa-realdata-mode` の `venture2_project` を Unity で開く。
2. `Assets/Scenes/MainScene.unity` を複製し、`MainScene_NASA.unity` として保存(元 MainScene は触らない)。
3. 複製シーンの `OrbitPlayer` を選び、`Csv File` に `Assets/Artemis/nasa_orion_trajectory.csv` を割り当てる(物理版 `orion_trajectory.csv` から差し替え)。
4. Play。Orion が NASA 実軌道(EME2000 の 3D 軌道、地球周回 → 外向き → フライバイ域 → 帰還)に沿って動く。

別法(さらに安全): 空のシーンに空 GameObject を置き、`OrbitPlayer` + `LineRenderer`(に `TrajectoryRenderer`)+ `ScaleConfig.asset` を割り当て、`Csv File` に上記NASA CSVを入れて Play。

## 既知の制約(Phase 3 で解消)

- 月が出ない/原点に居る: NASA OEM に月位置が無いため、再生用CSVの月列は 0 のプレースホルダ。実際の月を出すには Horizons/SPICE で同時刻・同フレーム(EME2000)の月エフェメリスが必要。
- 物理版との重ね合わせ比較は未対応: NASA=EME2000 3D、物理版=平面モデルで面・元期が違う。共通フレームへ載せる可否検証が Phase 3 の最初の一手。
- スケール/座標変換は既存 `ScaleConfig`(1 unit = 1000 km)をそのまま流用。NASA の z(面外成分)も `ScaleConfig.KmToUnityPos` 経由でそのまま 3D 表示される。

## 再現

```bash
# data/processed/artemis2_trajectory.csv から再生成
python3 scripts/nasa_to_unity_csv.py
```
