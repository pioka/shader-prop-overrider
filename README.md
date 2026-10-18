# shader-prop-overrider

[![](https://img.shields.io/badge/Add_to-VCC-%230f91a9)](https://pioka.github.io/vpm-listing/)

非破壊で全マテリアルのシェーダープロパティを上書きするやつ

## できること

- アバター内に `Shader Property Overrider` コンポーネントを置き、プロパティ名・型・値を登録すると、ビルド時にアバター内の全マテリアルへ一括で上書きする
  - 元のマテリアルアセットは変更しない
  - 対応する型: Float (Range 含む) / Int / Color / Vector4
  - どのマテリアルにも適用されなかったエントリ（名前の誤り・型の不一致）はビルド時に警告する
- NDMF Preview に対応。ビルドしなくても Scene ビューで上書き後の見た目を確認できる
  - プレビューの対象は MeshRenderer / SkinnedMeshRenderer のみ（パーティクル等はビルド時にのみ反映）
- lilToon プリセットから値を取り込める

## 導入

[このリンク](https://pioka.github.io/vpm-listing/) から VCC にリポジトリを登録し、導入したいプロジェクトに本パッケージを追加してください。

## 動作環境

- Unity 2022.3
- NDMF 1.5.0 以上
