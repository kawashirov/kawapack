
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
using EGUI = UnityEditor.EditorGUI;
using EGUIL = UnityEditor.EditorGUILayout;
#endif

namespace Kawashirov {

	public static class MaterialsCommons {
		
#if UNITY_EDITOR
		private static MethodInfo MaterialEditor_GetControlRectForSingleLine;
		private static MethodInfo MaterialEditor_TexturePropertyBody;
#endif

		static MaterialsCommons() {

#if UNITY_EDITOR
			var flags = BindingFlags.Instance | BindingFlags.NonPublic;
			MaterialEditor_GetControlRectForSingleLine = typeof(UnityEditor.MaterialEditor).GetMethod("GetControlRectForSingleLine", flags);
			MaterialEditor_TexturePropertyBody = typeof(UnityEditor.MaterialEditor).GetMethod("TexturePropertyBody", flags);
#endif
		}

#if UNITY_EDITOR

		public static Rect GetControlRectForSingleLine(this UnityEditor.MaterialEditor editor) {
			return (Rect)MaterialEditor_GetControlRectForSingleLine.Invoke(editor, new object[0]);
		}

		public static Texture TexturePropertyBody(this UnityEditor.MaterialEditor editor, Rect position, MaterialProperty prop) {
			return (Texture)MaterialEditor_TexturePropertyBody.Invoke(editor, new object[] { position, prop });
		}

		//
		//

		public static Texture TexturePropertySmol(
			this UnityEditor.MaterialEditor editor, GUIContent label, MaterialProperty prop, bool compatibility = false
		) {
			Texture result;
			try {
				var rect = editor.GetControlRectForSingleLine();
				var rect_ctrl = EditorGUI.PrefixLabel(rect, label);
				var min = Math.Min(rect_ctrl.height, rect_ctrl.width);
				rect_ctrl.width = Math.Min(rect_ctrl.width, min);
				rect_ctrl.height = Math.Min(rect_ctrl.height, min);
				result = editor.TexturePropertyBody(rect_ctrl, prop);
			} catch (Exception) {
				result = editor.TextureProperty(prop, label.text);
			}
			if (compatibility) {
				editor.TextureCompatibilityWarning(prop);
			}
			return result;
		}

		public static int PopupMaterialProperty(string label, MaterialProperty property, string[] displayedOptions, Action<bool> isChanged = null) {
			var value = (int)property.floatValue;
			EGUI.showMixedValue = property.hasMixedValue;
			EGUI.BeginChangeCheck();
			value = EGUIL.Popup(label, value, displayedOptions);
			if (EGUI.EndChangeCheck()) {
				property.floatValue = (float)value;
				isChanged?.Invoke(true);
			} else {
				isChanged?.Invoke(false);
			}
			EGUI.showMixedValue = false;
			return value;
		}

#endif

	}
}
