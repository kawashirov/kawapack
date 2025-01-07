using Kawashirov.Refreshables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

using Object = UnityEngine.Object;
using UnityEngine.Assertions;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov {
	public static class KawaGUIUtility {
#if UNITY_EDITOR

		public static readonly Lazy<Texture2D> kawaIcon = new Lazy<Texture2D>(GetKawaIcon);

		public static readonly GUILayoutOption expandWidth = GUILayout.ExpandWidth(true);
		public static readonly Lazy<float> doubleLineHeight =
			new Lazy<float>(() => EditorGUIUtility.singleLineHeight * 2);

		public static readonly Lazy<GUILayoutOption> doubleLineHeightMin =
			new Lazy<GUILayoutOption>(() => GUILayout.MinHeight(doubleLineHeight.Value));

		public static readonly Lazy<GUILayoutOption> doubleLineHeightMax =
			new Lazy<GUILayoutOption>(() => GUILayout.MaxHeight(doubleLineHeight.Value));

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

		public static bool HelpBoxWithButton(GUIContent messageContent, GUIContent buttonContent) {
			// static copy of MaterialEditor.HelpBoxWithButton
			var rect = GUILayoutUtility.GetRect(messageContent, richHelpBox.Value);
			GUILayoutUtility.GetRect(1f, 25f);
			rect.height += 25f;
			GUI.Label(rect, messageContent, richHelpBox.Value);
			var position = new Rect(rect.xMax - 60f - 4f, rect.yMax - 20f - 4f, 60f, 20f);
			return GUI.Button(position, buttonContent);
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

		public static void ToggleLeft(SerializedProperty property, GUIContent label = null) {
			var position = EditorGUILayout.GetControlRect(true);
			label ??= new GUIContent(property.displayName);
			using (var prop_scope = new EditorGUI.PropertyScope(position, label, property)) {
				using (var change_scope = new EditorGUI.ChangeCheckScope()) {
					var value = EditorGUI.ToggleLeft(position, label, property.boolValue);
					if (change_scope.changed) {
						property.boolValue = value;
					}
				}
			}
		}

		private static MethodInfo FindMethod(Type clazz, MethodBase template) {
			var parameter_types = template.GetParameters().Select(p => p.ParameterType).ToArray();
			return clazz.GetMethod(template.Name, BindingFlags.NonPublic | BindingFlags.Static, null, parameter_types, null);
		}

		private static MethodInfo GetSliderRect_m1 = null;
		public static Rect GetSliderRect(bool hasLabel, params GUILayoutOption[] options) {
			// Proxy to internal EditorGUILayout.GetSliderRect
			GetSliderRect_m1 ??= FindMethod(typeof(EditorGUILayout), MethodBase.GetCurrentMethod());
			if (GetSliderRect_m1 == null) {
				Debug.LogWarning($"Can't find {nameof(GetSliderRect_m1)} in {typeof(EditorGUILayout)} by {MethodBase.GetCurrentMethod()}.");
				return EditorGUILayout.GetControlRect(options);
			} else {
				return (Rect)GetSliderRect_m1.Invoke(null, new object[] { hasLabel, options });
			}
		}

		private static MethodInfo PowerSlider_m1 = null;
		public static float PowerSlider(string label, float value,
			float leftValue, float rightValue, float power, params GUILayoutOption[] options) {
			// Proxy to internal EditorGUILayout.PowerSlider
			PowerSlider_m1 ??= FindMethod(typeof(EditorGUILayout), MethodBase.GetCurrentMethod());
			if (PowerSlider_m1 == null) {
				Debug.LogWarning($"Can't find {nameof(PowerSlider_m1)} in {typeof(EditorGUILayout)} by {MethodBase.GetCurrentMethod()}.");
				return EditorGUILayout.Slider(label, value, leftValue, rightValue, options);
			} else {
				return (float)PowerSlider_m1.Invoke(null, new object[] {
					label, value, leftValue, rightValue, power, options });
			}
		}

		private static MethodInfo PowerSlider_m2 = null;
		public static float PowerSlider(GUIContent label, float value,
			float leftValue, float rightValue, float power, params GUILayoutOption[] options) {
			// Proxy to internal EditorGUILayout.PowerSlider
			PowerSlider_m2 ??= FindMethod(typeof(EditorGUILayout), MethodBase.GetCurrentMethod());
			if (PowerSlider_m2 == null) {
				Debug.LogWarning($"Can't find {nameof(PowerSlider_m2)} in {typeof(EditorGUILayout)} by {MethodBase.GetCurrentMethod()}.");
				return EditorGUILayout.Slider(label, value, leftValue, rightValue, options);
			} else {
				return (float)PowerSlider_m2.Invoke(null, new object[] {
					label, value, leftValue, rightValue, power, options });
			}
		}

		private static MethodInfo PowerSlider_m3 = null;
		public static float PowerSlider(Rect position, GUIContent label, float sliderValue,
			float leftValue, float rightValue, GUIStyle textfieldStyle, float power) {
			// Proxy to internal EditorGUI.PowerSlider
			PowerSlider_m3 ??= FindMethod(typeof(EditorGUI), MethodBase.GetCurrentMethod());
			if (PowerSlider_m3 == null) {
				Debug.LogWarning($"Can't find {nameof(PowerSlider_m3)} in {typeof(EditorGUILayout)} by {MethodBase.GetCurrentMethod()}.");
				return EditorGUI.Slider(position, label, sliderValue, leftValue, rightValue);
			} else {
				return (float)PowerSlider_m3.Invoke(null, new object[] {
					position, label, sliderValue, leftValue, rightValue, textfieldStyle, power });
			}
		}

		public static bool PowerSlider(SerializedProperty property, float leftValue, float rightValue, float power, params GUILayoutOption[] options) {
			var position = GetSliderRect(hasLabel: true, options);
			var label = EditorGUI.BeginProperty(position, new GUIContent(property.displayName), property);
			EditorGUI.BeginChangeCheck();
			float value = property.propertyType == SerializedPropertyType.Integer ? property.intValue : property.floatValue;
			value = PowerSlider(position, label, value, leftValue, rightValue, EditorStyles.numberField, power);
			var changed = false;
			if (EditorGUI.EndChangeCheck()) {
				changed = true;
				if (property.propertyType == SerializedPropertyType.Integer) {
					property.intValue = Mathf.RoundToInt(value);
				} else {
					property.floatValue = value;
				}
			}
			EditorGUI.EndProperty();
			return changed;
		}

		public static bool Contains(SerializedProperty ref_array, Object item) {
			Assert.IsTrue(ref_array.isArray);
			for (var i = 0; i < ref_array.arraySize; i++) {
				var prop_i = ref_array.GetArrayElementAtIndex(i);
				if (prop_i.objectReferenceValue == item)
					return true;
			}
			return false;
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
