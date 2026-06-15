# BOSS VAT Importer (Unity)

boss_voxelizer が Blender から書き出した **instance-VAT フォルダ**を取り込み、
**再生プレファブを自動生成**する Unity ツール。Built-in パイプライン / GPU インスタンシング。

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
5. **③ VAT プレファブを生成** を押す。
6. 生成されたプレファブを**シーンにドラッグするだけで再生**される。

## 自動でやること

- VAT 一式を出力先へ取り込み
- **EXR を正しい設定で取り込み**（Default / sRGB off / Point / Uncompressed RGBAFloat）
- グループ別マテリアル（opaque / glass）を生成し、テクスチャ・pivot/scale を設定
- `BossVatPlayer` 付きプレファブを生成（mesh・テクスチャ・スロット情報を全てシリアライズ。
  実行時は AssetDatabase 不使用なのでビルドでも動く）

## 注意

- 対象は **Built-in レンダーパイプライン**（URP/HDRP は別シェーダーが必要）。
- Mixamo など cm スケールのキャラはプレファブの Transform スケールを 0.01 等に調整。
- 大量スロットは GPU インスタンシングで描画（1 ドローコール 1023 体ずつ分割）。

GPL-3.0-or-later © 2026 darataBOSS
