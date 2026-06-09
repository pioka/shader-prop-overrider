using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace Pioka.ShaderPropOverrider
{
    /// <summary>
    /// 上書き対象のシェーダープロパティの型
    /// </summary>
    public enum OverridePropertyType
    {
        Float,
        Int,
        Color,
        Vector4,
    }

    /// <summary>
    /// シェーダープロパティ上書きの1エントリ
    /// </summary>
    [Serializable]
    public class PropertyOverrideEntry
    {
        [Tooltip("上書き対象のシェーダープロパティ名（例: _Metallic）")]
        public string propertyName = "";

        [Tooltip("プロパティの型")]
        public OverridePropertyType propertyType = OverridePropertyType.Float;

        // 型ごとの値フィールド（インスペクターで型に応じて表示切替）
        public float   floatValue;
        public int     intValue;
        public Color   colorValue = Color.white;
        public Vector4 vector4Value;
    }

    /// <summary>
    /// アバター内の全マテリアルに対してシェーダープロパティを一括上書きするコンポーネント。
    /// NDMFによりビルド時に非破壊で適用されます。
    ///
    /// <see cref="IEditorOnly"/> を実装しているため、ビルド成果物には残りません
    /// (VRChat SDK によって除去されます)。
    /// </summary>
    [AddComponentMenu("Shader Property Overrider")]
    [DisallowMultipleComponent]
    public class ShaderPropertyOverrider : MonoBehaviour, IEditorOnly
    {
        [Tooltip("上書きするシェーダープロパティのリスト")]
        public List<PropertyOverrideEntry> overrides = new List<PropertyOverrideEntry>();
    }
}
