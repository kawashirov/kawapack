using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


#if UNITY_EDITOR
using UnityEditor;

namespace Kawashirov {

	[CanEditMultipleObjects]
	public class CommonEditor : Editor {
		// Полезные штуки для Editorов
		// по возможности static this, что бы можно было использовать там,
		// где нет возможности наследовать CommonEditor

		private static GUIStyle richHelpBox = null;

		public static GUIStyle GetRichHelpBox() {
			if (richHelpBox == null) {
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

		public static int PropertyMaskPopupCustomLabels(
			string label, SerializedProperty property, Type enum_t, Dictionary<int, string> labels = null,
			GUILayoutOption[] options = null
		) {
			// TODO

			return 0;
		}

		public static void DefaultPrpertyField(SerializedProperty property, string label = null) {
			if (string.IsNullOrEmpty(label)) {
				EditorGUILayout.PropertyField(property);
			} else {
				EditorGUILayout.PropertyField(property, new GUIContent(label));
			}
		}

		public static void DefaultPrpertyField(string name, string label = null) {
			// DefaultPrpertyField(serializedObject.FindProperty(name), label);
		}

	}

}
#endif
