# NASA Artemis II 軌道データ(AROW)情報共有

チーム共有用メモ。Artemis II の「実データ」がどこから来て、何を表し、どう使っているかをまとめる。

## 概要

AROW = Artemis Real-time Orbit Website。NASA が Artemis II 中の Orion 宇宙船の位置・動きを
一般公開しているサイト。ミッション(約10日間・有人で月を周回)中、Mission Control(JSC)に届く
リアルタイムデータを誰でも見られる。打上げ約1分後〜地球再突入まで提供される。

重要なのは、NASA が state vector と ephemeris(軌道データ)を一般のデータ可視化・
物理モデル・アニメ・トラッキングアプリ用途に公開していること。つまり我々の教育用VR可視化での
利用は正規の使い方。

## 公式ソース

- ページ: Track NASA's Artemis II Mission in Real Time
- URL: https://www.nasa.gov/missions/artemis/artemis-2/track-nasas-artemis-ii-mission-in-real-time/
- 公開日: 2026-03-06 / 最終更新: 2026-05-01
- ダウンロード(ページ最下部): 「Artemis II Ephemeris」ZIP(1.51 MB)
- AROW 本体: www.nasa.gov/trackartemis / NASA アプリ(AR追従機能つき)

## ダウンロードZIPの中身

外側の「Artemis II Ephemeris」ZIP(1.51MB)の中に、日付別の OEM が入れ子で入っている:

```
2026.04.10 - Post-RTC3 to EI.zip
Artemis_II_OEM_2026_04_02_to_EI_v3.zip
Artemis_II_OEM_2026_04_03_to_EI.zip
Artemis_II_OEM_2026_04_04_to_EI.zip
Artemis_II_OEM_2026_04_06_Pre-OTC3_to_EI.zip
Artemis_II_OEM_2026_04_07_Pre-Lunar-Flyby_to_EI.zip
Artemis_II_OEM_2026_04_08_Post-ICPS-Sep_to_EI.zip
Artemis_II_OEM_2026_04_09_Post-ICPS-Sep_to_EI.zip
Artemis_II_OEM_2026_04_10_Post-ICPS-Sep-to-EI.zip
OEM - 2026.04.02 - post-USS-2 to EI.zip
```

OEM = CCSDS Orbit Ephemeris Message。時刻ごとの位置・速度を並べた標準フォーマットのテキスト。

## 我々が使っているファイル

```
Artemis_II_OEM_2026_04_10_Post-ICPS-Sep-to-EI.asc
```

選定理由: CCSDS OEM 2.0、地球中心 EME2000、Orion の位置+速度入り、ICPS分離後〜地球再突入(EI)を
カバー。ヘッダ実物:

```
CCSDS_OEM_VERS = 2.0
ORIGINATOR     = NASA/JSC/FOD/FDO
OBJECT_NAME    = EM2
OBJECT_ID      = 24
CENTER_NAME    = EARTH
REF_FRAME      = EME2000
TIME_SYSTEM    = UTC
START_TIME     = 2026-04-02T01:57:37.084
STOP_TIME      = 2026-04-10T23:53:16.723
```

データ行の並び: `UTC  x_km  y_km  z_km  vx_km/s  vy_km/s  vz_km/s`

## 観測される値(このファイルの実測)

- サンプル数: 3,262 点
- 期間: 2026-04-02 〜 2026-04-10(約8.91日)
- 地球からの最小距離: 約 6,515 km(2026-04-10、再突入interface=地表+約144km)
- 地球からの最大距離: 約 413,144 km(2026-04-06、フライバイ遠地点)
- 速度域: 約 0.41 〜 10.99 km/s

注意: 「最小距離6,515km」は地球への再突入時の値で、月への最接近ではない。月フライバイ高度を
出すには月の位置データ(別途取得)が必要。

## 取得・処理の流れ(このリポジトリ)

1. 上記ページから「Artemis II Ephemeris」ZIP(1.51MB)をダウンロード。
2. `scripts/build_artemis2_dataset.py` … ZIPを開き Post-ICPS-Sep-to-EI の .asc を抽出。
3. `scripts/convert_nasa_oem.py` … OEM(UTC・位置・速度)を Unity 用CSVへ変換。
   → `data/processed/artemis2_trajectory.csv`(+ manifest)
4. `scripts/nasa_to_unity_csv.py` … 既存プレイヤー形式へ整形。
   → `venture2_project/Assets/Artemis/nasa_orion_trajectory.csv`
5. Unity: `Assets/Scenes/MainScene_NASA.unity` を開いて Play(実データモード)。

詳細は `docs/nasa-realdata-mode.md`、データの素性は `docs/open-data-context.md`。

## 利用・出典の注意

- 出典表記: NASA Artemis II AROW(公開エフェメリス)。地球中心 EME2000。
- NASA が可視化・物理モデル用途に公開しているデータであり、私的・機密データではない。
- やってはいけない表現:
  - 簡易Python計算を「NASA実測軌道」と称する。
  - 実データと簡易モデルの差分を「Orion の内部誘導・姿勢制御を完全に再現/リバースエンジニアリングした」と称する。
- 位置づけ: 実データ(AROW)= 公開された実飛行由来の基準軌道。簡易Python = 教育用の比較モデル。

## 参考リンク

- AROW ページ: https://www.nasa.gov/missions/artemis/artemis-2/track-nasas-artemis-ii-mission-in-real-time/
- Artemis 全体: https://www.nasa.gov/artemis
- NASA SVS(可視化の見た目参考): https://svs.gsfc.nasa.gov/5632
- SPICE Toolkit: https://naif.jpl.nasa.gov/naif/toolkit.html
- JPL Horizons(月などの位置取得・Phase 3用): https://ssd-api.jpl.nasa.gov/doc/horizons.html
