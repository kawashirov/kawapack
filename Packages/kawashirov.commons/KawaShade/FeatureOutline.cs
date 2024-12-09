using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;

namespace Kawashirov.KawaShade {

	public class FeatureOutline : AbstractFeature {
		internal static readonly string ShaderTag_Outline = "KawaShade_Feature_Outline";
		internal static readonly string ShaderTag_OutlineMode = "KawaShade_Feature_OutlineMode";
		internal static readonly GUIContent gui_feature_outline = new GUIContent("Outline Feature");

		public enum Mode { Tinted, Colored }

		public override int GetOrder() => (int)Order.VGF;

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(ShaderTag_Outline);
			tags.Add(ShaderTag_OutlineMode);
		}

		public override void ConfigureShader(KawaShadeGenerator gen, ShaderSetup shader) {
			IncludeFeatureDirect(shader, "FeatureOutline.hlsl");

			shader.TagBool(ShaderTag_Outline, gen.outline);
			if (gen.outline) {
				shader.Define("OUTLINE_ON 1");
				shader.TagEnum(ShaderTag_OutlineMode, gen.outlineMode);
				if (gen.outlineMode == Mode.Colored) {
					shader.Define("OUTLINE_COLORED 1");
				} else if (gen.outlineMode == Mode.Tinted) {
					shader.Define("OUTLINE_TINTED 1");
				}
				shader.properties.Add(new PropertyFloat() { name = "_outline_width", defualt = 0.2f, range = new Vector2(0, 1) });
				shader.properties.Add(new PropertyColor() { name = "_outline_color", defualt = new Color(0.5f, 0.5f, 0.5f, 1) });
				shader.properties.Add(new PropertyFloat() { name = "_outline_bias", defualt = 0, range = new Vector2(-1, 5) });
			} else {
				shader.Define("OUTLINE_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var complexity = editor.complexity_VGF || editor.complexity_VHDGF;
			using (new EditorGUI.DisabledScope(!complexity)) {
				var outline = editor.serializedObject.FindProperty("outline");
				KawaGUIUtility.ToggleLeft(outline, gui_feature_outline);
				using (new EditorGUI.DisabledScope(!outline.hasMultipleDifferentValues && !outline.boolValue)) {
					using (new EditorGUI.IndentLevelScope()) {
						KawaGUIUtility.DefaultPrpertyField(editor, "outlineMode", "Mode");
					}
				}
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _outline_width = editor.FindProperty("_outline_width");
			var _outline_color = editor.FindProperty("_outline_color");
			var _outline_bias = editor.FindProperty("_outline_bias");

			var f_Outline = KawaUtilities.AnyNotNull(_outline_width, _outline_color, _outline_bias);
			using (new EditorGUI.DisabledScope(!f_Outline)) {
				EditorGUILayout.LabelField("Outline Feature", f_Outline ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_Outline) {
						editor.LabelEnumDisabledFromTagMixed<Mode>("Mode", ShaderTag_OutlineMode);
						editor.ShaderPropertyDisabled(_outline_width, "Outline width (cm)");
						editor.ShaderPropertyDisabled(_outline_color, "Outline Color (Tint)");
						editor.ShaderPropertyDisabled(_outline_bias, "Outline Z-Bias");
					}
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool outline = false;
		public FeatureOutline.Mode outlineMode = FeatureOutline.Mode.Tinted;
	}
}
