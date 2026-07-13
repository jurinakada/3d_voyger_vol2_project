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

## 月データ(Phase 3 第一歩・実装済み)

- `scripts/fetch_moon_ephemeris.py` が JPL Horizons から地心・ICRF/J2000(=EME2000と同一視可)の月位置を取得
  → `data/processed/moon_ephemeris_horizons.csv`(10分刻み・OEM全期間カバー)。
- `nasa_to_unity_csv.py` が Orion 各時刻へ線形補間して moon 列を実データで充填。phase も実ジオメトリ(月距離)で再ラベル。
- 検証結果: 月最接近高度 6,546 km(t≈4.88日)。NASA公式発表 6,545 km と1km差、物理版 6,541 km とも整合。
  座標系の整合が取れている証拠であり、Phase 3 本丸(物理 vs 実データ重ね合わせ)に進める。
- MainScene_NASA は地球・月を実寸表示(Earth scale 12.742 / Moon 3.474)。NASA軌道は近地点6,564km・
  再突入6,515kmまで地球に接近するため、誇張表示(scale 16)だと軌道が地球内部に潜って見える。実寸なら正しく地表ぎわを通る。
  物理版 MainScene は従来どおり誇張表示のまま。

## 残る制約

- 物理版との重ね合わせ比較は未実装: 物理版は平面(z=0)モデル・独自元期なので、同一シーンに重ねるには
  面合わせ(回転)と t=0 対応付けの設計判断が必要。次のステップ。
- Horizons の時刻系は TDB(UTCと約69秒差)。補正済みだが、残差は月位置で~数十km(距離38万kmの0.02%)で可視化には無影響。

## 再現

```bash
# data/processed/artemis2_trajectory.csv から再生成
python3 scripts/nasa_to_unity_csv.py
```
