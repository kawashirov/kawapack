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

		private static readonly MethodInfo EditorGUIUtility_GetHelpIcon;

		static KawaGUIUtility() {
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


		private static GUIStyle multilineLabel = null;

		public static GUIStyle GetMultilineLabel() {
			if (multilineLabel == null) {
				multilineLabel = new GUIStyle(EditorStyles.label) { wordWrap = true };
			}
			return multilineLabel;
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

		public static Rect IndentedRect(this Rect rect, int indentLevel) {
			var oldIndentLevel = EditorGUI.indentLevel;
			try {
				EditorGUI.indentLevel = indentLevel;
				rect = EditorGUI.IndentedRect(rect);
			} finally {
				EditorGUI.indentLevel = oldIndentLevel;
			}
			return rect;
		}

		public static IEnumerable<Rect> RectSplitVerticalUniform(this Rect rect, int lines) {
			var weights = new float[lines];
			for (var i = 0; i < weights.Length; ++i)
				weights[i] = 1;
			return RectSplitVertical(rect, weights);
		}

		public static IEnumerable<Rect> RectSplitVertical(this Rect rect, params float[] weights) {
			var spacing = 2f;
			var weights_sum = weights.Sum();
			var cell = new Rect(rect);
			var reducedHeight = rect.height - spacing * (weights.Length - 1);
			for (var i = 0; i < weights.Length; ++i) {
				cell.height = reducedHeight * weights[i] / weights_sum;
				yield return cell;
				cell.y += cell.height + spacing;
			}
			yield break;
		}

		public static IEnumerable<Rect> RectSplitHorisontal(this Rect rect, params float[] weights) {
			var spacing = 2f;
			var weights_sum = weights.Sum();
			var cell = new Rect(rect);
			var reducedWidth = rect.width - spacing * (weights.Length - 1);
			for (var i = 0; i < weights.Length; ++i) {
				cell.width = reducedWidth * weights[i] / weights_sum;
				yield return cell;
				cell.x += cell.width + spacing;
			}
			yield break;
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
