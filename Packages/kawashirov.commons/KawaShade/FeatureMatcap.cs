using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;

namespace Kawashirov.KawaShade {

	public class FeatureMatcap : AbstractFeature {
		internal static readonly string ShaderTag_Matcap = "KawaShade_Feature_Matcap";
		internal static readonly string ShaderTag_MatcapMode = "KawaShade_Feature_MatcapMode";
		internal static readonly string ShaderTag_MatcapKeepUp = "KawaShade_Feature_MatcapKeepUp";

		internal static readonly GUIContent gui_feature_matcap = new GUIContent("Matcap Feature");

		public enum Mode { Replace, Multiply, Add }

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(ShaderTag_Matcap);
			tags.Add(ShaderTag_MatcapMode);
			tags.Add(ShaderTag_MatcapKeepUp);
		}

		public override void ConfigureShader(KawaShadeGenerator gen, ShaderSetup shader) {
			IncludeFeatureDirect(shader, "FeatureMatcap.hlsl");

			shader.TagBool(ShaderTag_Matcap, gen.matcap);
			if (gen.matcap) {
				shader.Define("MATCAP_ON 1");
				shader.TagEnum(ShaderTag_MatcapMode, gen.matcapMode);
				switch (gen.matcapMode) {
					case Mode.Replace:
						shader.Define("MATCAP_REPLACE 1");
						break;
					case Mode.Multiply:
						shader.Define("MATCAP_MULTIPLY 1");
						break;
					case Mode.Add:
						shader.Define("MATCAP_ADD 1");
						break;
				}
				shader.TagBool(ShaderTag_MatcapKeepUp, gen.matcapKeepUp);
				if (gen.matcapKeepUp) {
					shader.Define("MATCAP_KEEPUP 1");
				}
				shader.properties.Add(new Property2D() { name = "_MatCap", defualt = "white" });
				shader.properties.Add(new PropertyFloat() { name = "_MatCap_Scale", defualt = 1f, range = new Vector2(0, 1) });
			} else {
				shader.Define("MATCAP_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var matcap = editor.serializedObject.FindProperty("matcap");
			KawaGUIUtility.ToggleLeft(matcap, gui_feature_matcap);
			using (new EditorGUI.DisabledScope(matcap.hasMultipleDifferentValues || !matcap.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(editor, "matcapMode", "Mode");
					KawaGUIUtility.DefaultPrpertyField(editor, "matcapKeepUp", "Keep Upward Direction");
				}
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _MatCap = editor.FindProperty("_MatCap");
			var _MatCap_Scale = editor.FindProperty("_MatCap_Scale");

			var f_matCap = KawaUtilities.AnyNotNull(_MatCap, _MatCap_Scale);
			using (new EditorGUI.DisabledScope(!f_matCap)) {
				EditorGUILayout.LabelField("MatCap Feature", f_matCap ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_matCap) {
						editor.LabelEnumDisabledFromTagMixed<Mode>("Mode", ShaderTag_MatcapMode);
						// TODO KeepUp bool label
						editor.ShaderPropertyDisabled(_MatCap, "MatCap Texture");
						editor.ShaderPropertyDisabled(_MatCap_Scale, "MatCap Power");
					}
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool matcap = false;
		public FeatureMatcap.Mode matcapMode = FeatureMatcap.Mode.Multiply;
		public bool matcapKeepUp = true;
	}
}
