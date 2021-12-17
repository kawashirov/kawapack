using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Kawashirov.ShaderBaking;

#if UNITY_EDITOR
using UnityEditor;
using static UnityEditor.EditorGUI;
using static Kawashirov.MaterialsCommons;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/

namespace Kawashirov {
	public class KawaShaderGUI : UnityEditor.ShaderGUI {
		protected readonly static MaterialProperty[] EmptyMaterialProperty = new MaterialProperty[0];
		protected readonly static Material[] EmptyMaterials = new Material[0];
		protected readonly static string[] EmptyStrings = new string[0];

		protected MaterialEditor materialEditor;
		protected MaterialProperty[] materialPropertiesArray;
		protected Material targetMaterial;
		protected Material[] targetMaterials;
		protected Shader[] targetShaders;
		// Использовать только для чтения!
		// Для записи использовать serializedObject
		protected IDictionary<string, MaterialProperty> materialProperties;
		protected IDictionary<string, ShaderTag> shaderTags;

		public virtual IEnumerable<string> GetShaderTagsOfIntrest() => EmptyStrings;

		private void UpdateMaterialEditorFields() {
			targetMaterial = materialEditor?.target as Material;
			var targets = materialEditor?.targets;
			targetMaterials = targets == null || targets.Length == 0 ? EmptyMaterials : targets.OfType<Material>().ToArray();
			targetShaders = targetMaterials.Select(m => m.shader).Distinct().ToArray();

			materialProperties = materialPropertiesArray.ToDictionary(p => p.name, p => p);
			shaderTags = GetShaderTagsOfIntrest().ToDictionary(s => s, s => new ShaderTag(targetMaterials, s));

		}

		protected MaterialProperty FindProperty(string name) {
			materialProperties.TryGetValue(name, out var mp);
			// Юнити иногда возвращает какие-то сломанные объекты почему-то
			return mp != null && !string.IsNullOrEmpty(mp.name) && mp.targets != null && mp.targets.Length > 0 ? mp : null;
		}

		protected void LabelShaderTagEnumValue<E>(string name, string label, string invalid) where E : Enum {
			E domain = default;
			if (shaderTags[name].GetEnumValueSafe(ref domain)) {
				EditorGUILayout.LabelField(label, Enum.GetName(typeof(E), domain));
			} else {
				EditorGUILayout.LabelField(label, invalid);
			}
		}

		protected void TexturePropertySmolDisabled(GUIContent label, MaterialProperty prop, bool compatibility = true) {
			// TODO FIXME
			using (new DisabledScope(prop == null)) {
				if (prop != null) {
					materialEditor.TexturePropertySmol(label, prop, compatibility);
				} else {
					EditorGUILayout.LabelField(label, new GUIContent("Disabled"));
				}
			}
		}

		protected void TexturePropertySingleLineDisabled(GUIContent label, MaterialProperty prop, bool compatibility = true) {
			using (new DisabledScope(prop == null)) {
				if (prop != null) {
					materialEditor.TexturePropertySingleLine(label, prop);
					if (compatibility) {
						materialEditor.TextureCompatibilityWarning(prop);
					}
				} else {
					EditorGUILayout.LabelField(label, new GUIContent("Disabled"));
				}
			}
		}

		protected void ShaderPropertyDisabled(MaterialProperty property, string label = null) {
			var gui_label = new GUIContent(label); // null label is ok
			ShaderPropertyDisabled(property, gui_label);
		}

		protected void ShaderPropertyDisabled(MaterialProperty property, GUIContent label = null) {
			if (property != null) {
				materialEditor.ShaderProperty(property, label);
			} else {
				using (new DisabledScope(true)) {
					EditorGUILayout.LabelField(label, new GUIContent("Disabled"));
				}
			}
		}

		protected static void LabelEnum<E>(string label, E value, Dictionary<E, string> display = null) where E : struct {
			string label2 = null;
			if (display != null && display.Count > 0) {
				display.TryGetValue(value, out label2);
			}
			if (string.IsNullOrEmpty(label2)) {
				label2 = Enum.GetName(typeof(E), value);
			}
			EditorGUILayout.LabelField(label, label2);
		}


		protected void LabelEnumDisabledFromTagMixed<E>(string label, string tag, Dictionary<E, string> display = null) where E : Enum {
			var values = shaderTags[tag].GetMultipleValues().ToList();
			if (values.Count < 1) {
				using (new DisabledScope(true)) {
					EditorGUILayout.LabelField(label, "Disabled");
				}
				return;
			}
			if (values.Count > 1) {
				EditorGUILayout.LabelField(label, "Mixed Values");
				return;
			}
			try {
				var value = values.First();
				var evalue = (E)Enum.Parse(typeof(E), value, true);
				if (display == null || !display.TryGetValue(evalue, out var svalue))
					svalue = evalue.ToString();
				EditorGUILayout.LabelField(label, svalue);
			} catch (ArgumentException) {
				EditorGUILayout.LabelField(label, "Unknown");
			}
		}

		public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties) {
			materialEditor = editor;
			materialPropertiesArray = properties;
			UpdateMaterialEditorFields();
			try {
				// GUILayout.Label("AMOGUS");
				CustomGUI();
			} finally {
				materialEditor = editor;
				materialPropertiesArray = EmptyMaterialProperty;
				UpdateMaterialEditorFields();
			}
		}

		public virtual void CustomGUI() { }

	}
}

#endif // UNITY_EDITOR
