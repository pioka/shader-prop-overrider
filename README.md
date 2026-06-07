# vpm-example-package
VRChat Package Manager (VPM) パッケージ用のテンプレートリポジトリです。以下を含みます。

* サンプル用のpackage.jsonとC#スクリプト
* リリース公開用のCI
    * リリース作成
    * GitHub Pages でのVPMリポジトリ公開

## CIによるリリースの公開
### 初回設定
リポジトリの **Settings → Pages → Build and deployment → Source** を
**「GitHub Actions」** に設定してください。

### CIの実行
`Actions` タブから、`main` ブランチをターゲットとして、 `Release` ワークフローを手動実行（`workflow_dispatch`）します。
選択可能なオプションは以下の通りです。

* `target:full` (デフォルト)
    * リリースを作成し、GitHub Pages のサイトコンテンツを更新します。通常はこちらを利用します。
* `target:pages_only`
    * リリースの作成は行わず、GitHub Pages のサイトコンテンツを更新します。リリースは作成されているがPagesへのデプロイが上手くいかなかった場合などに利用します。

#### リリース作成
`package.json` の `version` の値をもとにタグと GitHub Release が自動で作成されます。
リリースには`package.json`と公開用にzip圧縮されたソースがAssetとして添付されます。

#### GitHub Pages でのVPMリポジトリ公開
過去の全リリースを走査し、各リリースに添付された `package.json` の内容と `.zip` のダウンロードURLから、  
VPM Listing (`vpm.json`) と最小限の `index.html` を自動生成し、GitHub Pages へ公開します。

URLはデフォルトで `https://<owner>.github.io/<repo>/vpm.json` です。
カスタムドメインにも対応しています。
