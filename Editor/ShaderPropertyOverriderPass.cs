using System.Collections.Generic;
using nadena.dev.ndmf;
using UnityEditor;
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

            // コンポーネントが存在しない、またはエントリが空の場合は何もしない
            if (component == null || component.overrides == null || component.overrides.Count == 0)
                return;

            // アバター内の全 Renderer を収集
            var renderers = context.AvatarRootObject
                .GetComponentsInChildren<Renderer>(includeInactive: true);

            // 同一マテリアルを重複処理しないためのキャッシュ
            // キー: 元のマテリアル、値: 編集対象（複製、または既存の一時アセット）
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
                        // そうでなければ複製し、元→複製の対応を ObjectRegistry に登録する。
                        if (context.IsTemporaryAsset(src))
                        {
                            dst = src;
                        }
                        else
                        {
                            dst = Object.Instantiate(src);
                            dst.name = src.name;
                            ObjectRegistry.RegisterReplacedObject(src, dst);
                        }

                        materialCache[src] = dst;

                        // 編集対象1つにつき1回だけ適用（後のエントリが勝つ）
                        foreach (var entry in component.overrides)
                        {
                            if (string.IsNullOrEmpty(entry.propertyName)) continue;
                            if (ApplyEntry(dst, entry))
                                appliedEntries.Add(entry);
                        }
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

        /// <summary>
        /// 1エントリをマテリアルに適用する。
        /// プロパティが存在しない、または型が一致しない場合は適用せず false を返す。
        /// </summary>
        private static bool ApplyEntry(Material material, PropertyOverrideEntry entry)
        {
            if (!material.HasProperty(entry.propertyName)) return false;

            // ShaderUtil でシェーダー側の実際の型を取得して検証（EditorOnly API）
            var shader = material.shader;
            int propIndex = shader.FindPropertyIndex(entry.propertyName);
            if (propIndex < 0) return false;

            var shaderPropType = ShaderUtil.GetPropertyType(shader, propIndex);

            switch (entry.propertyType)
            {
                case OverridePropertyType.Float:
                    if (shaderPropType != ShaderUtil.ShaderPropertyType.Float &&
                        shaderPropType != ShaderUtil.ShaderPropertyType.Range)
                        return false;
                    material.SetFloat(entry.propertyName, entry.floatValue);
                    return true;

                case OverridePropertyType.Int:
                    if (shaderPropType != ShaderUtil.ShaderPropertyType.Int)
                        return false;
                    material.SetInt(entry.propertyName, entry.intValue);
                    return true;

                case OverridePropertyType.Color:
                    if (shaderPropType != ShaderUtil.ShaderPropertyType.Color)
                        return false;
                    material.SetColor(entry.propertyName, entry.colorValue);
                    return true;

                case OverridePropertyType.Vector4:
                    if (shaderPropType != ShaderUtil.ShaderPropertyType.Vector)
                        return false;
                    material.SetVector(entry.propertyName, entry.vector4Value);
                    return true;

                default:
                    return false;
            }
        }
    }
}
