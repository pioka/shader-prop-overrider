using System.Collections.Generic;
using nadena.dev.ndmf;
using Pioka.ShaderPropOverrider;
using UnityEditor;
using UnityEngine;

namespace Pioka.ShaderPropOverrider.Editor
{
    /// <summary>
    /// ビルド時にアバター内の全マテリアルへシェーダープロパティを一括上書きするNDMFパス。
    /// </summary>
    internal class ShaderPropertyOverriderPass : Pass<ShaderPropertyOverriderPass>
    {
        protected override void Execute(BuildContext context)
        {
            var component = context.AvatarRootObject
                .GetComponentInChildren<ShaderPropertyOverrider>(includeInactive: true);

            // コンポーネントが存在しない、またはエントリが空の場合は何もしない
            if (component == null || component.overrides == null || component.overrides.Count == 0)
                return;

            // アバター内の全 Renderer を収集
            var renderers = context.AvatarRootObject
                .GetComponentsInChildren<Renderer>(includeInactive: true);

            // 同一マテリアルを重複して複製しないためのキャッシュ
            // キー: 元のマテリアル、値: 複製済みマテリアル
            var materialCache = new Dictionary<Material, Material>();

            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                bool modified = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null) continue;

                    // 複製済みでなければここで複製してキャッシュ
                    if (!materialCache.TryGetValue(original, out var cloned))
                    {
                        cloned = Object.Instantiate(original);
                        cloned.name = original.name;
                        materialCache[original] = cloned;
                    }

                    // エントリを順番に適用（後のエントリが勝つ）
                    foreach (var entry in component.overrides)
                    {
                        if (string.IsNullOrEmpty(entry.propertyName)) continue;

                        ApplyEntry(cloned, entry);
                    }

                    materials[i] = cloned;
                    modified = true;
                }

                if (modified)
                    renderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// 1エントリをマテリアルに適用する。
        /// プロパティが存在しない、または型が一致しない場合はスキップ。
        /// </summary>
        private static void ApplyEntry(Material material, PropertyOverrideEntry entry)
        {
            if (!material.HasProperty(entry.propertyName)) return;

            // ShaderUtil でシェーダー側の実際の型を取得して検証（EditorOnly API）
            var shader = material.shader;
            int propIndex = shader.FindPropertyIndex(entry.propertyName);
            if (propIndex < 0) return;

            var shaderPropType = ShaderUtil.GetPropertyType(shader, propIndex);

            switch (entry.propertyType)
            {
                case OverridePropertyType.Float:
                    if (shaderPropType != ShaderUtil.ShaderPropertyType.Float &&
                        shaderPropType != ShaderUtil.ShaderPropertyType.Range)
                        return;
                    material.SetFloat(entry.propertyName, entry.floatValue);
                    break;

                case OverridePropertyType.Int:
                    if (shaderPropType != ShaderUtil.ShaderPropertyType.Int)
                        return;
                    material.SetInt(entry.propertyName, entry.intValue);
                    break;

                case OverridePropertyType.Color:
                    if (shaderPropType != ShaderUtil.ShaderPropertyType.Color)
                        return;
                    material.SetColor(entry.propertyName, entry.colorValue);
                    break;

                case OverridePropertyType.Vector4:
                    if (shaderPropType != ShaderUtil.ShaderPropertyType.Vector)
                        return;
                    material.SetVector(entry.propertyName, entry.vector4Value);
                    break;
            }
        }
    }
}
