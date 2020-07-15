using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Kawashirov.ShaderBaking;

#if UNITY_EDITOR
using UnityEditor;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/

namespace Kawashirov {
	public class MaterialEditor : UnityEditor.MaterialEditor {


		// Использовать только для чтения!
		// Для записи использовать serializedObject
		protected Material targetMaterial;
		protected Material[] targetMaterials;
		protected IDictionary<string, MaterialProperty> materialProperties;
		protected IDictionary<string, ShaderTag> shaderTags;

		// По-нормальному юнити не должен допускать несколько шейдеров, но мало ли чё.
		protected Shader[] targetShaders;

		public virtual IEnumerable<string> GetShaderTagsOfIntrest() => new string[0];

		public override void OnEnable() {
			base.OnEnable();
			targetMaterial = target as Material;
			targetMaterials = targets.OfType<Material>().ToArray();
			targetShaders = targetMaterials.Select(m => m.shader).Distinct().ToArray();

			materialProperties = GetMaterialProperties(targetMaterials).ToDictionary(p => p.name, p => p);
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
			using (new EditorGUI.DisabledScope(prop == null)) {
				if (prop != null) {
					this.TexturePropertySmol(label, prop, compatibility);
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
				ShaderProperty(property, label);
			} else {
				using (new EditorGUI.DisabledScope(true)) {
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


		protected void LabelEnumDisabledFromTagMixed<E>(
			string label, string tag, Dictionary<E, string> display = null
		) where E : Enum {
			var values = shaderTags[tag].GetMultipleValues();
			if (values.Count < 1) {
				using (new EditorGUI.DisabledScope(true)) {
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

	}
}

#endif // UNITY_EDITOR
