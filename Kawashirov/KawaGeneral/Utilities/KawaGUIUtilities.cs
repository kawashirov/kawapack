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
	public static class KawaGUIUtilities {
#if UNITY_EDITOR

		private static readonly MethodInfo EditorGUIUtility_GetHelpIcon;

		static KawaGUIUtilities() {
			EditorGUIUtility_GetHelpIcon = typeof(EditorGUIUtility).GetMethod("GetHelpIcon", BindingFlags.NonPublic | BindingFlags.Static);
		}

		public static Texture2D GetHelpIcon(MessageType type) {
			return (Texture2D)EditorGUIUtility_GetHelpIcon.Invoke(null, new object[] { type });
		}

		private static GUIStyle richHelpBox = null;

		public static GUIStyle GetRichHelpBox() {
			if (richHelpBox == null) {
				// по требованию, т.к. стили инициализирутся не сразу 
				var helpbox = GUI.skin.GetStyle("HelpBox");
				richHelpBox = new GUIStyle(helpbox) { richText = true };
			}
			return richHelpBox;
		}

		public static void HelpBoxRich(string msg) {
			EditorGUILayout.TextArea(msg, GetRichHelpBox());
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

		public static void BehaviourRefreshGUI(this Editor editor) {
			var refreshables = editor.targets.OfType<IRefreshable>().ToList();

			if (refreshables.Count < 1)
				return;

			if (GUILayout.Button("Only refresh this")) {
				refreshables.RefreshMultiple();
			}

			var scenes = editor.targets.OfType<Component>().Select(r => r.gameObject.scene).Distinct().ToList();
			var scene_str = string.Join(", ", scenes.Select(s => s.name));

			var types = editor.targets.Select(t => t.GetType()).Distinct().ToList();
			var types_str = string.Join(", ", types.Select(t => t.Name));

			var types_btn = string.Format("Refresh every {0} on scene: {1}", types_str, scene_str);
			if (GUILayout.Button(types_btn)) {
				var all_targets = scenes.SelectMany(s => s.GetRootGameObjects())
						.SelectMany(g => types.SelectMany(t => g.GetComponentsInChildren(t, true)))
						.Distinct().OfType<IRefreshable>().ToList();
				all_targets.RefreshMultiple();
			}

			var scene_btn = string.Format("Refresh every Behaviour on scene: {0}", scene_str);
			if (GUILayout.Button(scene_btn)) {
				var all_targets = scenes.SelectMany(s => s.GetRootGameObjects())
						.SelectMany(g => g.GetComponentsInChildren<IRefreshable>(true)).ToList();
				all_targets.RefreshMultiple();
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
#endif
	}
}
