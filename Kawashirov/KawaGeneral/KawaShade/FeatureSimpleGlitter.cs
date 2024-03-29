using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;
using YamlDotNet.Core;

namespace Kawashirov.KawaShade {

	public class FeatureSimpleGlitter : AbstractFeature {
		public enum GlitterMode { SolidColor, SolidAlbedo }

		public string ShaderTag_Glitter = "KawaShade_Feature_Glitter";
		public string ShaderTag_GlitterMode = "KawaShade_Feature_GlitterMode";

		public static readonly Dictionary<GlitterMode, string> glitterModeDesc = new Dictionary<GlitterMode, string>() {
			{ GlitterMode.SolidColor, "Use given Color as glitter color. No randomness." },
			{ GlitterMode.SolidAlbedo, "Use Albedo as glitter color. Brightness can be adjusted. No randomness." },
		};

		private static readonly GUIContent gui_feature_glitter = new GUIContent("Simple Glitter Feature");
		private static readonly GUIContent _Gltr_Mask_label = new GUIContent("Mask", "Masks Glitter effect (R)");

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(ShaderTag_Glitter);
			tags.Add(ShaderTag_GlitterMode);
		}

		public override void ConfigureShader(KawaShadeGenerator generator, ShaderSetup shader) {
			IncludeFeatureDirect(shader, "FeatureSimpleGlitter.hlsl");

			shader.TagBool(ShaderTag_Glitter, generator.glitter);
			if (generator.glitter) {
				shader.Define("GLITTER_ON 1");
				generator.needRandomFrag = true;

				shader.Define(generator.glitterUniform ? "GLITTER_UNIFORM 1" : "GLITTER_NONUNIFORM 1");

				if (generator.glitterMask) {
					shader.Define("GLITTER_MASK_ON 1");
					shader.properties.Add(new Property2D() { name = "_Gltr_Mask" });
				}

				if (generator.glitterUniform) {
					shader.Define("GLITTER_UNIFORM 1");
					shader.properties.Add(new PropertyFloat() { name = "_Gltr_Dnst", defualt = 0.01f, range = Vector2.up, power = 5 });
				} else {
					shader.Define("GLITTER_NONUNIFORM 1");
					shader.properties.Add(new PropertyFloat() { name = "_Gltr_DnstFront", defualt = 0.005f, range = Vector2.up, power = 6 });
					shader.properties.Add(new PropertyFloat() { name = "_Gltr_DnstRim", defualt = 0.02f, range = Vector2.up, power = 6 });
					shader.properties.Add(new PropertyFloat() { name = "_Gltr_DnstBack", defualt = 0.0f, range = Vector2.up, power = 6 });
					shader.properties.Add(new PropertyFloat() { name = "_Gltr_DnstPow", defualt = 1f, range = Vector2.up * 100, power = 4 });
				}

				shader.TagEnum(ShaderTag_GlitterMode, generator.glitterMode);
				switch (generator.glitterMode) {
					case GlitterMode.SolidColor:
						shader.Define("GLITTER_MODE_SOLID_COLOR 1");
						shader.properties.Add(new PropertyColor() { name = "_Gltr_Color", defualt = Color.white });
						break;
					case GlitterMode.SolidAlbedo:
						shader.Define("GLITTER_MODE_SOLID_ALBEDO 1");
						break;
				}

				shader.properties.Add(new PropertyFloat() { name = "_Gltr_Brght", defualt = 1, range = Vector2.up });

				shader.properties.Add(new PropertyFloat() { name = "_Gltr_Em", defualt = 1, range = Vector2.up });
			} else {
				shader.Define("GLITTER_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var obj = editor.serializedObject;
			var glitter = obj.FindProperty("glitter");
			KawaGUIUtility.ToggleLeft(glitter, gui_feature_glitter);
			using (new EditorGUI.DisabledScope(glitter.hasMultipleDifferentValues || !glitter.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(editor, "glitterMask", "Mask");
					var glitterMode = obj.FindProperty("glitterMode");
					KawaGUIUtility.DefaultPrpertyField(glitterMode, "Mode");
					if (!glitterMode.hasMultipleDifferentValues) {
						var glitterModeE = (GlitterMode)glitterMode.intValue;
						glitterModeDesc.TryGetValue(glitterModeE, out var message);
						if (!string.IsNullOrWhiteSpace(message)) {
							using (new EditorGUI.IndentLevelScope()) {
								EditorGUILayout.HelpBox(message, MessageType.None);
							}
						}
					}
					KawaGUIUtility.DefaultPrpertyField(editor, "glitterUniform", "Uniform Glitter");
				}
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _Gltr_Mask = editor.FindProperty("_Gltr_Mask");
			var _Gltr_Dnst = editor.FindProperty("_Gltr_Dnst");
			var _Gltr_DnstFront = editor.FindProperty("_Gltr_DnstFront");
			var _Gltr_DnstRim = editor.FindProperty("_Gltr_DnstRim");
			var _Gltr_DnstBack = editor.FindProperty("_Gltr_DnstBack");
			var _Gltr_DnstPow = editor.FindProperty("_Gltr_DnstPow");
			var _Gltr_Color = editor.FindProperty("_Gltr_Color");
			var _Gltr_Brght = editor.FindProperty("_Gltr_Brght");
			var _Gltr_Em = editor.FindProperty("_Gltr_Em");

			var f_nonuniform = KawaUtilities.AnyNotNull(_Gltr_DnstFront, _Gltr_DnstRim, _Gltr_DnstBack, _Gltr_DnstPow);
			var f_glitter = KawaUtilities.AnyNotNull(_Gltr_Mask, _Gltr_Dnst, _Gltr_Color, _Gltr_Brght, _Gltr_Em) || f_nonuniform;

			using (new EditorGUI.DisabledScope(!f_glitter)) {
				EditorGUILayout.LabelField("Simple Glitter Feature", f_glitter ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_glitter) {
						editor.TexturePropertySingleLineDisabled(_Gltr_Mask_label, _Gltr_Mask);
						editor.ShaderPropertyDisabled(_Gltr_Dnst, "Uniform Density");
						if (f_nonuniform) {
							editor.ShaderPropertyDisabled(_Gltr_DnstFront, "Front Density");
							editor.ShaderPropertyDisabled(_Gltr_DnstRim, "Rim Density");
							editor.ShaderPropertyDisabled(_Gltr_DnstBack, "Back Density");
							editor.ShaderPropertyDisabled(_Gltr_DnstPow, "Power Density");
						}
						editor.ShaderPropertyDisabled(_Gltr_Color, "Color");
						editor.ShaderPropertyDisabled(_Gltr_Brght, "Brightness");
						editor.ShaderPropertyDisabled(_Gltr_Em, "Emission");
					}
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool glitter = false;
		public bool glitterMask = false;
		public FeatureSimpleGlitter.GlitterMode glitterMode = FeatureSimpleGlitter.GlitterMode.SolidColor;
		public bool glitterUniform = false;
	}
}
