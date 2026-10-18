using UnityEditor;
using UnityEngine;

namespace Pioka.ShaderPropOverrider.Editor
{
    /// <summary>
    /// <see cref="PropertyOverrideEntry"/> をマテリアルに適用する処理。
    /// ビルド時のパス（<see cref="ShaderPropertyOverriderPass"/>）と
    /// NDMF Preview（<see cref="ShaderPropertyOverriderPreview"/>）で共有する。
    /// </summary>
    internal static class PropertyOverrideApplier
    {
        /// <summary>
        /// 1エントリをマテリアルに適用する。
        /// プロパティが存在しない、または型が一致しない場合は適用せず false を返す。
        /// </summary>
        public static bool Apply(Material material, PropertyOverrideEntry entry)
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
