#!/bin/bash
set -euo pipefail

#GH_PAGES_BASE_URL='<will be given from ci variable>'
#GITHUB_REPOSITORY='<will be given from ci variable>'

PACKAGE_NAME="$(jq -r '.displayName' package.json)"
PACKAGE_ID="$(jq -r '.name' package.json)"
PACKAGE_AUTHOR="$(jq -r '.author.name' package.json)"
PACKAGE_DESCRIPTION="$(jq -r '.description // ""' package.json | sed 's/"/\&quot;/g')"

rm -rf public && mkdir public
pushd public

# vpm.json: listing関連キーの設定
vpm_json="{}"
vpm_json="$(echo "$vpm_json" | jq --arg v "$PACKAGE_NAME" '.name = $v')"
vpm_json="$(echo "$vpm_json" | jq --arg v "$PACKAGE_ID-repo" '.id = $v')"
vpm_json="$(echo "$vpm_json" | jq --arg v "$PACKAGE_AUTHOR" '.author = $v')"
vpm_json="$(echo "$vpm_json" | jq --arg v "$GH_PAGES_BASE_URL/vpm.json" '.url = $v')"

# vpm.json: リリースのpackage.jsonとzipファイルのURLを組み込み
# 全ページをまとめて取得し変数に受ける（API取得失敗時は set -e でここで停止する）。
# --slurp は各ページのJSON配列を [[...],[...]] の配列として返すため、後段で .[][] で展開する。
releases_json="$(gh api --paginate --slurp "repos/${GITHUB_REPOSITORY}/releases")"

imported=0
while IFS= read -r release; do
  tag="$(echo "$release" | jq -r '.tag_name')"
  package_url="$(echo "$release" | jq -r 'first(.assets[] | select(.name == "package.json") | .browser_download_url) // ""')"
  zip_name="$(echo "$release" | jq -r 'first(.assets[] | select(.name | endswith(".zip")) | .name) // ""')"
  zip_sha256="$(echo "$release" | jq -r 'first(.assets[] | select(.name | endswith(".zip")) | .digest) // ""' | cut -d: -f2)"

  if [ -n "$package_url" ] && [ -n "$zip_name" ]; then
    # package.jsonはリリースアセットのURLから直接取得する（取得失敗時は set -e で停止）
    package_content="$(curl -fsSL "$package_url")"
    package_content="$(echo "$package_content" | jq --arg v "https://github.com/${GITHUB_REPOSITORY}/releases/download/$tag/$zip_name" '.url = $v')"
    package_content="$(echo "$package_content" | jq --arg v "$zip_sha256" '.zipSHA256 = $v')"

    vpm_json="$(echo "$vpm_json" | jq --argjson d "$package_content" ".packages.\"${PACKAGE_ID}\".versions.\"${tag:1}\" = \$d")"
    imported=$((imported + 1))
  fi
done < <(echo "$releases_json" | jq -c '.[][] | select(.draft == false)')

# 1件も取り込めなかった場合は、空リスティングの配信を防ぐためビルドを失敗させる
if [ "$imported" -eq 0 ]; then
  echo "ERROR: no releases were imported into vpm.json." >&2
  exit 1
fi

echo "$vpm_json" > vpm.json

cat > index.html <<HTML
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="description" content="${PACKAGE_DESCRIPTION}">
  <meta http-equiv="refresh" content="0;URL=vcc://vpm/addRepo?url=${GH_PAGES_BASE_URL}/vpm.json">
  <title>${PACKAGE_NAME}</title>
</head>
<body>
  <main>
    <h1>${PACKAGE_NAME}</h1>
    <table>
      <tr>
        <td>${GH_PAGES_BASE_URL}/vpm.json</td>
        <td><a href="vcc://vpm/addRepo?url=${GH_PAGES_BASE_URL}/vpm.json">[Add to VCC]</a></td>
      </tr>
    </table>
  </main>
</body>
</html>
HTML
popd
