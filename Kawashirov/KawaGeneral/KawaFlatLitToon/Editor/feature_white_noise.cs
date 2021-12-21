using System;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using Kawashirov.FLT;

using GUIL = UnityEngine.GUILayout;
using EGUIL = UnityEditor.EditorGUILayout;
using EU = UnityEditor.EditorUtility;
using KST = Kawashirov.ShaderTag;
using KSBC = Kawashirov.ShaderBaking.Commons;
using KFLTC = Kawashirov.FLT.Commons;
using SC = Kawashirov.KawaUtilities;

using static UnityEditor.EditorGUI;

namespace Kawashirov.FLT {
	internal static partial class Commons {
		internal static readonly string F_WNoise = "KawaFLT_Feature_WNoise";
	}

	public partial class Generator {
		public bool wnoise = false;

		private void ConfigureFeatureWNoise(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_WNoise, wnoise);
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

	public partial class GeneratorEditor {
		private static readonly GUIContent gui_feature_wnoise = new GUIContent("White Noise Feature");
		private void WhiteNoise() {
			var wnoise = serializedObject.FindProperty("wnoise");
			KawaGUIUtility.ToggleLeft(wnoise, gui_feature_wnoise);
		}
	}
}

internal partial class KawaFLTShaderGUI {
	protected void OnGUI_WNoise() {
		// Commons.MaterialTagBoolCheck(this.target, Commons.KawaFLT_Feature_FPS);
		var _WNoise_Albedo = FindProperty("_WNoise_Albedo");
		var _WNoise_Em = FindProperty("_WNoise_Em");
		var f_wnoise = SC.AnyNotNull(_WNoise_Albedo, _WNoise_Em);
		using (new DisabledScope(!f_wnoise)) {
			EGUIL.LabelField("White Noise Feature", f_wnoise ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_wnoise) {
					ShaderPropertyDisabled(_WNoise_Albedo, "Noise on Albedo");
					ShaderPropertyDisabled(_WNoise_Em, "Noise on Emission");
				}
			}
		}
	}
}
