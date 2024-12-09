using Kawashirov.Refreshables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov {
	public static class KawaGUIUtility {
#if UNITY_EDITOR

		public static readonly Lazy<Texture2D> kawaIcon = new Lazy<Texture2D>(GetKawaIcon);

		private static Texture2D GetKawaIcon() {
			var path = AssetDatabase.GUIDToAssetPath("302691306fd300648a26254d75364f60");
			return string.IsNullOrWhiteSpace(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		}

		private static MethodInfo EditorGUIUtility_GetHelpIcon;
		public static Texture2D GetHelpIcon(MessageType type) {
			if (EditorGUIUtility_GetHelpIcon == null) {
				EditorGUIUtility_GetHelpIcon = typeof(EditorGUIUtility).GetMethod("GetHelpIcon", BindingFlags.NonPublic | BindingFlags.Static);
			}
			return (Texture2D)EditorGUIUtility_GetHelpIcon.Invoke(null, new object[] { type });
		}

		private static readonly Lazy<GUIStyle> richHelpBox =
			new Lazy<GUIStyle>(() => new GUIStyle(EditorStyles.helpBox) { richText = true });

		public static void HelpBoxRich(string msg) {
			EditorGUILayout.TextArea(msg, richHelpBox.Value);
		}

		public static bool PropertyEnumPopupCustomLabels<E>(
			SerializedProperty property, string label, Dictionary<E, string> labels = null,
			GUILayoutOption[] options = null
		) where E : struct, IConvertible, IComparable, IFormattable {
			// TODO ревизия
			var e_display = property.enumDisplayNames;
			var e_type = typeof(E);

			if (labels != null && e_type.IsEnum && labels.Count > 0) {
				var e_values = Enum.GetValues(e_type);
				var e_names = Enum.GetNames(e_type);
				for (var e_index = 0; e_index < e_names.Length; ++e_index) {
					var e_object = (E)Enum.Parse(typeof(E), e_names[e_index]);
					string custom_label;
					labels.TryGetValue(e_object, out custom_label);
					if (!string.IsNullOrEmpty(custom_label)) {
						e_display[e_index] = custom_label;
					}
				}
			}

			EditorGUI.BeginChangeCheck();
			var enumValueIndex = EditorGUILayout.Popup(
				label, (!property.hasMultipleDifferentValues) ? property.enumValueIndex : -1, e_display, options
			);
			if (EditorGUI.EndChangeCheck()) {
				property.enumValueIndex = enumValueIndex;
				return true;
			}
			return false;
		}

		public static void DefaultPrpertyField(Editor editor, string name, string label = null) {
			DefaultPrpertyField(editor.serializedObject.FindProperty(name), label);
		}

		public static void DefaultPrpertyField(SerializedProperty property, string label = null) {
			if (string.IsNullOrEmpty(label)) {
				EditorGUILayout.PropertyField(property);
			} else {
				EditorGUILayout.PropertyField(property, new GUIContent(label));
			}
		}

		public static void ToggleLeft(SerializedProperty property, GUIContent label) {
			var position = EditorGUILayout.GetControlRect(true);
			using (var prop_scope = new EditorGUI.PropertyScope(position, label, property)) {
				using (var change_scope = new EditorGUI.ChangeCheckScope()) {
					var value = EditorGUI.ToggleLeft(position, label, property.boolValue);
					if (change_scope.changed) {
						property.boolValue = value;
					}
				}
			}
		}

		public static void ShaderEditorFooter() {
			var style = new GUIStyle { richText = true };

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("This thing made by <b>kawashirov</b>; My Contacts:", style);
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Discord server:");
			if (GUILayout.Button("pEugvST")) {
				Application.OpenURL("https://discord.gg/pEugvST");
			}
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.LabelField("Discord tag: kawashirov#8363");
		}

		public class ZeroIndentScope : GUI.Scope {
			// Work around to avoid bugs
			// Даже если у тебя есть конкретный rect, где ты хочешь рисовать
			// IndentedRect применяется на многих Field управляшках.
			readonly int m_OldIndentLevel;

			public ZeroIndentScope() {
				m_OldIndentLevel = EditorGUI.indentLevel;
				EditorGUI.indentLevel = 0;
			}

			protected override void CloseScope() {
				EditorGUI.indentLevel = m_OldIndentLevel;
			}
		}

		public static int KawaIndent = 0;

		public class KawaIndentScope : GUI.Scope {
			// Indent независимый от EditorGUI.indentLevel
			public readonly int offset;

			public KawaIndentScope(int increment) {
				offset = increment;
				KawaIndent += offset;
			}

			public Rect GetControlRect() {
				return EditorGUILayout.GetControlRect().IndentedRect(KawaIndent);
			}

			protected override void CloseScope() {
				KawaIndent -= offset;
			}

		}

		public static void OpenInspector() {
			const string menuItemPath =
#if UNITY_2018_1_OR_NEWER
"Window/General/Inspector";
#else
"Window/Inspector";
#endif
			EditorApplication.ExecuteMenuItem(menuItemPath);
		}

#endif
	}
}
