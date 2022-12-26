using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	internal static partial class KawaShadeCommons {
		internal static readonly string F_WNoise = "KawaShade_Feature_WNoise";
	}

	public partial class KawaShadeGenerator {
		public bool wnoise = false;

		private void ConfigureFeatureWNoise(ShaderSetup shader) {
			shader.TagBool(KawaShadeCommons.F_WNoise, wnoise);
			if (wnoise) {
				needRandomFrag = true;
				shader.Define("WNOISE_ON 1");
				shader.properties.Add(new PropertyFloat() { name = "_WNoise_Albedo", defualt = 1, range = new Vector2(0, 1) });
				shader.properties.Add(new PropertyFloat() { name = "_WNoise_Em", defualt = 1, range = new Vector2(0, 1) });
			} else {
				shader.Define("WNOISE_OFF 1");
			}
		}
	}

	public partial class KawaShadeGeneratorEditor {
		private static readonly GUIContent gui_feature_wnoise = new GUIContent("White Noise Feature");
		private void WhiteNoise() {
			var wnoise = serializedObject.FindProperty("wnoise");
			KawaGUIUtility.ToggleLeft(wnoise, gui_feature_wnoise);
		}
	}

	internal partial class KawaShadeGUI {
		protected void OnGUI_WNoise() {
			// KawaShadeCommons.MaterialTagBoolCheck(this.target, KawaShadeCommons.KawaFLT_Feature_FPS);
			var _WNoise_Albedo = FindProperty("_WNoise_Albedo");
			var _WNoise_Em = FindProperty("_WNoise_Em");
			var f_wnoise = KawaUtilities.AnyNotNull(_WNoise_Albedo, _WNoise_Em);
			using (new EditorGUI.DisabledScope(!f_wnoise)) {
				EditorGUILayout.LabelField("White Noise Feature", f_wnoise ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_wnoise) {
						ShaderPropertyDisabled(_WNoise_Albedo, "Noise on Albedo");
						ShaderPropertyDisabled(_WNoise_Em, "Noise on Emission");
					}
				}
			}
		}
	}
}
