using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;

namespace Kawashirov.KawaShade {

	public class FeatureWhiteNoise : AbstractFeature {
		internal static readonly string F_WNoise = "KawaShade_Feature_WNoise";
		internal static readonly GUIContent gui_feature_wnoise = new GUIContent("White Noise Feature");

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_WNoise);
		}

		public override void ConfigureShader(KawaShadeGenerator generator, ShaderSetup shader) {
			IncludeFeatureDirect(shader, "kawa_feature_white_noise.cginc");

			shader.TagBool(F_WNoise, generator.wnoise);
			if (generator.wnoise) {
				generator.needRandomFrag = true;
				shader.Define("WNOISE_ON 1");
				shader.properties.Add(new PropertyFloat() { name = "_WNoise_Albedo", defualt = 1, range = new Vector2(0, 1) });
				shader.properties.Add(new PropertyFloat() { name = "_WNoise_Em", defualt = 1, range = new Vector2(0, 1) });
			} else {
				shader.Define("WNOISE_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var wnoise = editor.serializedObject.FindProperty("wnoise");
			KawaGUIUtility.ToggleLeft(wnoise, gui_feature_wnoise);
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _WNoise_Albedo = editor.FindProperty("_WNoise_Albedo");
			var _WNoise_Em = editor.FindProperty("_WNoise_Em");
			var f_wnoise = KawaUtilities.AnyNotNull(_WNoise_Albedo, _WNoise_Em);
			using (new EditorGUI.DisabledScope(!f_wnoise)) {
				EditorGUILayout.LabelField("White Noise Feature", f_wnoise ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_wnoise) {
						editor.ShaderPropertyDisabled(_WNoise_Albedo, "Noise on Albedo");
						editor.ShaderPropertyDisabled(_WNoise_Em, "Noise on Emission");
					}
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool wnoise = false;
	}
}
