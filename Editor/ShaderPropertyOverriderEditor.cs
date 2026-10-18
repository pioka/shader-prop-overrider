using System;
using System.Collections.Generic;
using System.Linq;
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
            // 再生時のビルド等でコンポーネントが破棄された直後に呼ばれると target が null になる
            if (target == null) return;

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
                if (type == OverridePropertyType.Vector4)
                {
                    // 1行の矩形に収めるため、PropertyField ではなく Vector4Field で直接描画する
                    var p = element.FindPropertyRelative("vector4Value");
                    EditorGUI.BeginChangeCheck();
                    var newVec = EditorGUI.Vector4Field(valueRect, GUIContent.none, p.vector4Value);
                    if (EditorGUI.EndChangeCheck())
                        p.vector4Value = newVec;
                }
                else
                {
                    var fieldName = type switch
                    {
                        OverridePropertyType.Float => "floatValue",
                        OverridePropertyType.Int   => "intValue",
                        OverridePropertyType.Color => "colorValue",
                        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
                    };
                    EditorGUI.PropertyField(valueRect, element.FindPropertyRelative(fieldName), GUIContent.none);
                }
            };

            _list.onAddCallback = list => AddEntry();
        }

        /// <summary>
        /// 末尾に既定値で初期化したエントリを追加して返す。
        /// (arraySize++ は直前の要素をコピーするため、明示的に初期化する)
        /// </summary>
        private SerializedProperty AddEntry()
        {
            _overridesProp.arraySize++;
            var e = _overridesProp.GetArrayElementAtIndex(_overridesProp.arraySize - 1);
            e.FindPropertyRelative("propertyName").stringValue       = "";
            e.FindPropertyRelative("propertyType").enumValueIndex    = 0;
            e.FindPropertyRelative("floatValue").floatValue          = 0f;
            e.FindPropertyRelative("intValue").intValue              = 0;
            e.FindPropertyRelative("colorValue").colorValue          = Color.white;
            e.FindPropertyRelative("vector4Value").vector4Value      = Vector4.zero;
            return e;
        }

        public override void OnInspectorGUI()
        {
            if (_list == null) return;

            serializedObject.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "ビルド時にアバター内の全マテリアルへシェーダープロパティを一括上書きします。\n" +
                "上書き後の見た目は NDMF Preview で確認できます。\n" +
                "指定した名称/型がマテリアルに存在しない場合はスキップされます。",
                MessageType.Info
            );
            EditorGUILayout.Space(4);

            _list.DoLayoutList();

            var importLabel = new GUIContent("lilToon プリセットから取り込み");
            var importRect  = GUILayoutUtility.GetRect(importLabel, EditorStyles.miniPullDown);
            if (EditorGUI.DropdownButton(importRect, importLabel, FocusType.Passive))
                ShowLilToonPresetMenu(importRect);

            serializedObject.ApplyModifiedProperties();
        }

        // ---- lilToon プリセット取り込み ----
        // lilToon.Editor への参照を持たずに済むよう、プリセットは型名で検索し SerializedObject 経由で読む。
        // lilToon 未導入の環境ではプリセットが見つからないだけで、コンパイルには影響しない。

        private const string LilToonPresetTypeName = "lilToonPreset";

        private void ShowLilToonPresetMenu(Rect rect)
        {
            var presets = new List<(int category, string menuPath, string name, ScriptableObject asset)>();

            foreach (var guid in AssetDatabase.FindAssets("t:" + LilToonPresetTypeName))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || asset.GetType().Name != LilToonPresetTypeName) continue;

                var so       = new SerializedObject(asset);
                var name     = GetPresetDisplayName(so, asset.name);
                var category = so.FindProperty("category");

                int    categoryIndex = -1;
                string categoryName  = "Other";
                if (category != null && category.propertyType == SerializedPropertyType.Enum &&
                    category.enumValueIndex >= 0 && category.enumValueIndex < category.enumDisplayNames.Length)
                {
                    categoryIndex = category.enumValueIndex;
                    categoryName  = category.enumDisplayNames[categoryIndex];
                }

                presets.Add((categoryIndex, categoryName + "/" + name, name, asset));
            }

            var menu = new GenericMenu();
            foreach (var p in presets.OrderBy(p => p.category).ThenBy(p => p.name))
            {
                var preset = p;
                menu.AddItem(new GUIContent(preset.menuPath), false, () => ImportLilToonPreset(preset.asset, preset.name));
            }

            if (presets.Count == 0)
                menu.AddDisabledItem(new GUIContent("lilToon プリセットが見つかりません"));

            menu.DropDown(rect);
        }

        /// <summary>
        /// プリセットの表示名を返す。日本語名 → 英語名 → アセット名 の順に採用する。
        /// </summary>
        private static string GetPresetDisplayName(SerializedObject preset, string fallback)
        {
            var bases = preset.FindProperty("bases");
            if (bases == null || !bases.isArray) return fallback;

            string english = null;
            for (int i = 0; i < bases.arraySize; i++)
            {
                var b    = bases.GetArrayElementAtIndex(i);
                var lang = b.FindPropertyRelative("language")?.stringValue;
                var name = b.FindPropertyRelative("name")?.stringValue;
                if (string.IsNullOrEmpty(name)) continue;

                if (lang == "Japanese") return name;
                if (lang == "English")  english = name;
            }
            return english ?? fallback;
        }

        /// <summary>
        /// プリセットの colors / vectors / floats をエントリとして取り込む。
        /// 同名のエントリが既にあれば型と値を上書きし、なければ末尾に追加する。
        /// テクスチャやシェーダー・描画モード等は本コンポーネントで扱えないため対象外。
        /// </summary>
        private void ImportLilToonPreset(ScriptableObject preset, string displayName)
        {
            if (preset == null) return;

            var so       = new SerializedObject(preset);
            var colors   = so.FindProperty("colors");
            var vectors  = so.FindProperty("vectors");
            var floats   = so.FindProperty("floats");
            var textures = so.FindProperty("textures");

            int propCount = ArraySize(colors) + ArraySize(vectors) + ArraySize(floats);
            int texCount  = ArraySize(textures);

            var message = $"lilToon プリセット「{displayName}」から {propCount} 件のプロパティを取り込みます。\n" +
                          "同名のエントリは上書きされます。";
            if (texCount > 0)
                message += $"\nテクスチャ ({texCount} 件) は対象外です。";

            if (!EditorUtility.DisplayDialog("lilToon プリセットの取り込み", message, "取り込む", "キャンセル"))
                return;

            serializedObject.Update();

            // 同名エントリは後のものが勝つため、最後の出現位置を上書き対象とする
            var indexByName = new Dictionary<string, int>();
            for (int i = 0; i < _overridesProp.arraySize; i++)
            {
                var name = _overridesProp.GetArrayElementAtIndex(i).FindPropertyRelative("propertyName").stringValue;
                if (!string.IsNullOrEmpty(name)) indexByName[name] = i;
            }

            ImportArray(colors, OverridePropertyType.Color, indexByName,
                (dst, v) => dst.FindPropertyRelative("colorValue").colorValue = v.colorValue);
            ImportArray(vectors, OverridePropertyType.Vector4, indexByName,
                (dst, v) => dst.FindPropertyRelative("vector4Value").vector4Value = v.vector4Value);
            ImportArray(floats, OverridePropertyType.Float, indexByName,
                (dst, v) => dst.FindPropertyRelative("floatValue").floatValue = v.floatValue);

            // ApplyModifiedProperties により Undo にも記録される
            serializedObject.ApplyModifiedProperties();
        }

        private void ImportArray(
            SerializedProperty src,
            OverridePropertyType type,
            Dictionary<string, int> indexByName,
            Action<SerializedProperty, SerializedProperty> copyValue)
        {
            if (src == null || !src.isArray) return;

            for (int i = 0; i < src.arraySize; i++)
            {
                var item  = src.GetArrayElementAtIndex(i);
                var name  = item.FindPropertyRelative("name")?.stringValue;
                var value = item.FindPropertyRelative("value");
                if (string.IsNullOrEmpty(name) || value == null) continue;

                SerializedProperty dst;
                if (indexByName.TryGetValue(name, out var index))
                {
                    dst = _overridesProp.GetArrayElementAtIndex(index);
                }
                else
                {
                    dst = AddEntry();
                    dst.FindPropertyRelative("propertyName").stringValue = name;
                    indexByName[name] = _overridesProp.arraySize - 1;
                }

                dst.FindPropertyRelative("propertyType").enumValueIndex = (int)type;
                copyValue(dst, value);
            }
        }

        private static int ArraySize(SerializedProperty p) => p != null && p.isArray ? p.arraySize : 0;
    }
}
