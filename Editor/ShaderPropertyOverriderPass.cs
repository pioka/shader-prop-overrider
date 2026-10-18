using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEngine;

namespace Pioka.ShaderPropOverrider.Editor
{
    /// <summary>
    /// ビルド時にアバター内の全マテリアルへシェーダープロパティを一括上書きするNDMFパス。
    /// <see cref="ShaderPropertyOverriderPlugin"/> からインライン形式
    /// (<c>.Run("...", Execute)</c>) で呼び出される。
    /// </summary>
    internal static class ShaderPropertyOverriderPass
    {
        /// <summary>
        /// パス本体。<c>Action&lt;BuildContext&gt;</c> に合致するシグネチャ。
        /// </summary>
        public static void Execute(BuildContext context)
        {
            var component = context.AvatarRootObject
                .GetComponentInChildren<ShaderPropertyOverrider>(includeInactive: true);

            if (component == null) return;

            // アバター内の全 Renderer を収集
            var renderers = context.AvatarRootObject
                .GetComponentsInChildren<Renderer>(includeInactive: true);

            // 同一マテリアルを重複処理しないためのキャッシュ
            // キー: 元のマテリアル、値: 編集対象（複製、既存の一時アセット、または上書き不要なら元のまま）
            var materialCache = new Dictionary<Material, Material>();

            // 少なくとも1つのマテリアルに適用できたエントリの集合（typo検出用）
            var appliedEntries = new HashSet<PropertyOverrideEntry>();

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                bool modified = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    var src = materials[i];
                    if (src == null) continue;

                    if (!materialCache.TryGetValue(src, out var dst))
                    {
                        // 既に一時クローン（先行プラグインが複製済み）ならそのまま編集。
                        // そうでなければ複製して編集し、元→複製の対応を ObjectRegistry に登録する。
                        // 上書きが1つも当たらないマテリアルは複製せず元のまま使う。
                        if (context.IsTemporaryAsset(src))
                        {
                            dst = src;
                            PropertyOverrideApplier.ApplyAll(dst, component.overrides, appliedEntries);
                        }
                        else if (PropertyOverrideApplier.TryCreateOverriddenCopy(
                                     src, component.overrides, appliedEntries, out dst))
                        {
                            ObjectRegistry.RegisterReplacedObject(src, dst);
                        }
                        else
                        {
                            dst = src;
                        }

                        materialCache[src] = dst;
                    }

                    // 参照が変わった場合（複製した場合）のみ書き戻す
                    if (!ReferenceEquals(materials[i], dst))
                    {
                        materials[i] = dst;
                        modified = true;
                    }
                }

                if (modified)
                    renderer.sharedMaterials = materials;
            }

            // どのマテリアルにも適用されなかったエントリを警告（typo・型不一致の検出）
            foreach (var entry in component.overrides)
            {
                if (string.IsNullOrEmpty(entry.propertyName)) continue;
                if (appliedEntries.Contains(entry)) continue;

                ErrorReport.ReportError(
                    SpoLocalizer.Instance,
                    ErrorSeverity.NonFatal,
                    SpoLocalizer.NotApplied,
                    entry.propertyName,
                    entry.propertyType.ToString(),
                    component);
            }

            // 処理完了後にコンポーネントを除去し、後段プラグインの考慮対象から外す
            Object.DestroyImmediate(component);
        }
    }
}
