using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using System;
using System.Linq;

namespace Kawashirov.KawaShade {
	public class FeatureEmission : AbstractFeature {
		internal static readonly string F_Emission = "KawaShade_Feature_Emission";
		internal static readonly string F_EmissionMode = "KawaShade_Feature_EmissionMode";
		public enum EmissionMode { AlbedoNoMask, AlbedoMask, Custom }

		internal static readonly Dictionary<EmissionMode, string> emissionModeNames = new Dictionary<EmissionMode, string> {
			{ EmissionMode.AlbedoNoMask, "Emission from Main Texture without Mask" },
			{ EmissionMode.AlbedoMask, "Emission from Main Texture with Mask" },
			{ EmissionMode.Custom, "Custom Emission Texture" },
		};

		public override int GetOrder() => (int)Order.GENERAL + 510;

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_Emission);
			tags.Add(F_EmissionMode);
		}

		public override void ConfigureShader(KawaShadeGenerator gen, ShaderSetup shader) {
			shader.TagBool(F_Emission, gen.emission);
			if (gen.emission) {
				shader.forward.defines.Add("EMISSION_ON 1");
				shader.TagEnum(F_EmissionMode, gen.emissionMode);
				switch (gen.emissionMode) {
					case EmissionMode.AlbedoNoMask:
						shader.forward.defines.Add("EMISSION_ALBEDO_NOMASK 1");
						break;
					case EmissionMode.AlbedoMask:
						shader.forward.defines.Add("EMISSION_ALBEDO_MASK 1");
						break;
					case EmissionMode.Custom:
						shader.forward.defines.Add("EMISSION_CUSTOM 1");
						break;
				}
				if (gen.emissionMode == EmissionMode.AlbedoMask) {
					shader.properties.Add(new Property2D() { name = "_EmissionMask", defualt = "white" });
				}
				if (gen.emissionMode == EmissionMode.Custom) {
					shader.properties.Add(new Property2D() { name = "_EmissionMap", defualt = "white" });
				}
				shader.properties.Add(new PropertyColor() { name = "_EmissionColor", defualt = Color.black });
			} else {
				shader.forward.defines.Add("EMISSION_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var emission = editor.serializedObject.FindProperty("emission");
			KawaGUIUtility.DefaultPrpertyField(emission);
			using (new EditorGUI.DisabledScope(!emission.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					var emissionMode = editor.serializedObject.FindProperty("emissionMode");
					KawaGUIUtility.PropertyEnumPopupCustomLabels(emissionMode, "Mode", emissionModeNames);
				}
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _EmissionMask = editor.FindProperty("_EmissionMask");
			var _EmissionMap = editor.FindProperty("_EmissionMap");
			var _EmissionColor = editor.FindProperty("_EmissionColor");
			var f_emission = KawaUtilities.AnyNotNull(_EmissionMask, _EmissionMap, _EmissionColor);

			using (new EditorGUI.DisabledScope(!f_emission)) {
				EditorGUILayout.LabelField("Emission Feature", f_emission ? "Enabled" : "Disabled");
				if (f_emission) {
					using (new EditorGUI.IndentLevelScope()) {
						editor.LabelEnumDisabledFromTagMixed("Emission Mode", F_EmissionMode, emissionModeNames);

						var _EmissionMask_label = new GUIContent("Emission Mask", "Mask for Emission by Albedo Main Texture (R)");
						editor.TexturePropertySingleLineDisabled(_EmissionMask_label, _EmissionMask);

						var _EmissionMap_label = new GUIContent("Emission Texture", "Custom Emission Texture (RGB)");
						editor.TexturePropertySingleLineDisabled(_EmissionMap_label, _EmissionMap);

						editor.ShaderPropertyDisabled(_EmissionColor, new GUIContent("Emission Color (Tint)", "Emission Color Tint (RGB)"));
						var _EmissionColor_value = _EmissionColor.colorValue;
						var intencity = (_EmissionColor_value.r + _EmissionColor_value.g + _EmissionColor_value.b) * _EmissionColor_value.a;
						if (intencity < 0.05) {
							EditorGUILayout.HelpBox(
								"Emission Color is too dark! disable emission feature in shader generator, if you don't need emission.",
								MessageType.Warning
							);
						}

					}
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool emission = true;
		public FeatureEmission.EmissionMode emissionMode = FeatureEmission.EmissionMode.Custom;
	}
}
