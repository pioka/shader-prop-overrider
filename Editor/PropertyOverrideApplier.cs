using System.Collections.Generic;
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
        /// <paramref name="src"/> を複製して全エントリを適用する。
        /// 1つも適用されなかった場合は複製を破棄して false を返す。
        /// </summary>
        public static bool TryCreateOverriddenCopy(
            Material src,
            IEnumerable<PropertyOverrideEntry> entries,
            ISet<PropertyOverrideEntry> appliedEntries,
            out Material copy)
        {
            copy = Object.Instantiate(src);
            copy.name = src.name;

            if (ApplyAll(copy, entries, appliedEntries))
                return true;

            Object.DestroyImmediate(copy);
            copy = null;
            return false;
        }

        /// <summary>
        /// 全エントリをマテリアルに適用し、1つでも適用できたら true を返す（後のエントリが勝つ）。
        /// <paramref name="appliedEntries"/> が null でなければ、適用できたエントリを追加する。
        /// </summary>
        public static bool ApplyAll(
            Material material,
            IEnumerable<PropertyOverrideEntry> entries,
            ISet<PropertyOverrideEntry> appliedEntries)
        {
            bool applied = false;
            foreach (var entry in entries)
            {
                if (!Apply(material, entry)) continue;

                applied = true;
                appliedEntries?.Add(entry);
            }
            return applied;
        }

        /// <summary>
        /// 1エントリをマテリアルに適用する。
        /// プロパティが存在しない（名前が空の場合を含む）、または型が一致しない場合は適用せず false を返す。
        /// </summary>
        private static bool Apply(Material material, PropertyOverrideEntry entry)
        {
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
