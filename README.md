# BOSS VAT Importer (Unity)

boss_voxelizer が Blender から書き出した **instance-VAT フォルダ**を取り込み、
**再生プレファブを自動生成**する Unity ツール。**Built-in / URP 自動対応** / GPU インスタンシング。

## インストール（Package Manager）

Unity の **Window > Package Manager > ＋ > Add package from git URL...** に:

```
https://github.com/darataBOSS/boss-vat-importer.git
```

> ローカルで使う場合は **Add package from disk...** でこのフォルダの `package.json` を選ぶ、
> または `Packages/` 配下にこのフォルダを置く（embedded package）。

## 使い方

1. Blender 側で **アニメ VAT（Unity/STYLY）** を bake → フォルダが出力される
   （`boss_vat_meta.json` / `vat_*.exr` / `palette.png` / `boss_cube.obj` を含む）。
2. Unity メニュー **BOSS > VAT Importer** を開く。
3. **① 書き出しフォルダ**＝Blender が出力した VAT フォルダを指定。
4. **② 出力先**＝Assets 内の好きなフォルダ（＋プレファブ名は空なら自動）。
5. **③ マテリアル毎の質感スタイル**＝マテリアルごとに質感を選ぶ。
6. **④ VAT プレファブを生成** を押す。
7. 生成されたプレファブを**シーンにドラッグするだけで再生**される。

## 質感スタイル（マテリアル毎に選択）

| スタイル | 見た目 |
|---|---|
| **不透明** | ソリッド。焼き込んだ色そのまま（既定）|
| **曇りガラス** | 半透明フロスト＋フレネル。中が薄く透ける |
| **クリアガラス** | 透明＋強いフレネル縁。ガラスらしい抜け感 |
| **発光** | 色を強度倍して出力。Bloom で光る |

## 自動でやること

- VAT 一式を出力先へ取り込み
- **EXR を正しい設定で取り込み**（Default / sRGB off / Point / Uncompressed RGBAFloat / Max 16384）
- **パイプライン（Built-in / URP）を自動検出**し、対応シェーダーを `Assets/BossVatShaders` に配置
- 選んだスタイルのシェーダー／パラメータでマテリアルを生成（同一スタイルは共有）
- `BossVatPlayer` 付きプレファブを生成（mesh・テクスチャ・スロット情報を全てシリアライズ。
  実行時は AssetDatabase 不使用なのでビルドでも動く）

## 注意

- **Built-in / URP 両対応**（取り込み時に自動選択）。HDRP は未対応。
  - URP シェーダーは URP 未導入プロジェクトでエラーにならないよう、パッケージ内では
    Unity 無視フォルダ（`Runtime/URP~` / `Runtime/Builtin~`）に置き、合致する方だけ Assets に配置する。
- 半透明（曇り／クリアガラス）は GPU インスタンシングのため個別の深度ソートはされない
  （boiling ボクセルでは破綻しにくい）。
- Mixamo など cm スケールのキャラはプレファブの Transform スケールを自動適用（meta の object_scale）。
- 大量スロットは GPU インスタンシングで描画（1 ドローコール 1023 体ずつ分割）。

GPL-3.0-or-later © 2026 darataBOSS
