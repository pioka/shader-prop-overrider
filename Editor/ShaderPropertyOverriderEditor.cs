using Pioka.ShaderPropOverrider;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Pioka.ShaderPropOverrider.Editor
{
    [CustomEditor(typeof(ShaderPropertyOverrider))]
    internal class ShaderPropertyOverriderEditor : UnityEditor.Editor
    {
        private ReorderableList _list;
        private SerializedProperty _overridesProp;

        // 各行の推奨高さ
        private const float RowPadding     = 2f;
        private static float RowHeight      => EditorGUIUtility.singleLineHeight;
        private static float TotalRowHeight => RowHeight + RowPadding * 2;

        private void OnEnable()
        {
            _overridesProp = serializedObject.FindProperty("overrides");
            BuildList();
        }

        private void BuildList()
        {
            _list = new ReorderableList(
                serializedObject,
                _overridesProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true
            );

            _list.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Property Overrides");
            };

            _list.elementHeightCallback = _ => TotalRowHeight;

            _list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element  = _overridesProp.GetArrayElementAtIndex(index);
                var propName = element.FindPropertyRelative("propertyName");
                var propType = element.FindPropertyRelative("propertyType");

                rect.y      += RowPadding;
                rect.height  = RowHeight;

                // 列幅の割り当て: [プロパティ名 40%] [型 22%] [値 残り]
                float totalWidth = rect.width;
                float nameWidth  = totalWidth * 0.40f;
                float typeWidth  = totalWidth * 0.22f;
                float spacing    = 4f;
                float valueWidth = totalWidth - nameWidth - typeWidth - spacing * 2;

                var nameRect  = new Rect(rect.x,                                   rect.y, nameWidth,  rect.height);
                var typeRect  = new Rect(rect.x + nameWidth + spacing,             rect.y, typeWidth,  rect.height);
                var valueRect = new Rect(rect.x + nameWidth + typeWidth + spacing * 2, rect.y, valueWidth, rect.height);

                // プロパティ名
                EditorGUI.PropertyField(nameRect, propName, GUIContent.none);

                // 型ドロップダウン
                EditorGUI.PropertyField(typeRect, propType, GUIContent.none);

                // 値フィールド（型に応じて切替）
                var type = (OverridePropertyType)propType.enumValueIndex;
                switch (type)
                {
                    case OverridePropertyType.Float:
                    {
                        var p = element.FindPropertyRelative("floatValue");
                        EditorGUI.PropertyField(valueRect, p, GUIContent.none);
                        break;
                    }
                    case OverridePropertyType.Int:
                    {
                        var p = element.FindPropertyRelative("intValue");
                        EditorGUI.PropertyField(valueRect, p, GUIContent.none);
                        break;
                    }
                    case OverridePropertyType.Color:
                    {
                        var p = element.FindPropertyRelative("colorValue");
                        EditorGUI.BeginChangeCheck();
                        var newColor = EditorGUI.ColorField(
                            valueRect,
                            GUIContent.none,
                            p.colorValue,
                            showEyedropper: true,
                            showAlpha: true,
                            hdr: false
                        );
                        if (EditorGUI.EndChangeCheck())
                            p.colorValue = newColor;
                        break;
                    }
                    case OverridePropertyType.Vector4:
                    {
                        // SerializedPropertyにvector4Valueは存在しないため Vector4Field で直接描画する
                        var p = element.FindPropertyRelative("vector4Value");
                        EditorGUI.BeginChangeCheck();
                        var newVec = EditorGUI.Vector4Field(valueRect, GUIContent.none, p.vector4Value);
                        if (EditorGUI.EndChangeCheck())
                            p.vector4Value = newVec;
                        break;
                    }
                }
            };

            _list.onAddCallback = list =>
            {
                _overridesProp.arraySize++;
                var e = _overridesProp.GetArrayElementAtIndex(_overridesProp.arraySize - 1);
                e.FindPropertyRelative("propertyName").stringValue       = "";
                e.FindPropertyRelative("propertyType").enumValueIndex    = 0;
                e.FindPropertyRelative("floatValue").floatValue          = 0f;
                e.FindPropertyRelative("intValue").intValue              = 0;
                e.FindPropertyRelative("colorValue").colorValue          = Color.white;
                e.FindPropertyRelative("vector4Value").vector4Value      = Vector4.zero;
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "ビルド時にアバター内の全マテリアルへシェーダープロパティを一括上書きします。\n" +
                "指定した名称/型がマテリアルに存在しない場合はスキップされます。",
                MessageType.Info
            );
            EditorGUILayout.Space(4);

            _list.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
