using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	public enum FPSMode { ColorTint, DigitsTexture, DigitsMesh }

	internal static partial class KawaShadeCommons {
		internal static readonly string F_FPS = "KawaShade_Feature_FPS";
		internal static readonly string F_FPSMode = "KawaShade_Feature_FPSMode";
	}

	public partial class KawaShadeGenerator {
		public bool FPS = false;
		public FPSMode FPSMode = FPSMode.ColorTint;

		private void ConfigureFeatureFPS(ShaderSetup shader) {
			shader.TagBool(KawaShadeCommons.F_FPS, FPS);
			if (FPS) {
				shader.TagEnum(KawaShadeCommons.F_FPSMode, FPSMode);
				switch (FPSMode) {
					case FPSMode.ColorTint:
						shader.Define("FPS_COLOR 1");
						break;
					case FPSMode.DigitsTexture:
						shader.Define("FPS_TEX 1");
						break;
					case FPSMode.DigitsMesh:
						shader.Define("FPS_MESH 1");
						break;
				}
				shader.properties.Add(new PropertyColor() { name = "_FPS_TLo", defualt = new Color(1, 0.5f, 0.5f, 1) });
				shader.properties.Add(new PropertyColor() { name = "_FPS_THi", defualt = new Color(0.5f, 1, 0.5f, 1) });
			} else {
				shader.Define("FPS_OFF 1");
			}
		}
	}

	public partial class KawaShadeGeneratorEditor {
		private static readonly GUIContent gui_feature_fps = new GUIContent("FPS Feature");

		private void FPSGUI() {
			var FPS = serializedObject.FindProperty("FPS");
			KawaGUIUtility.ToggleLeft(FPS, gui_feature_fps);
			using (new EditorGUI.DisabledScope(FPS.hasMultipleDifferentValues || !FPS.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(this, "FPSMode", "Mode");
				}
			}
		}

	}

	internal partial class KawaShadeGUI {
		protected void OnGUI_FPS() {
			// KawaShadeCommons.MaterialTagBoolCheck(this.target, KawaShadeCommons.KawaFLT_Feature_FPS);
			var _FPS_TLo = FindProperty("_FPS_TLo");
			var _FPS_THi = FindProperty("_FPS_THi");
			var f_FPS = KawaUtilities.AnyNotNull(_FPS_TLo, _FPS_THi);
			using (new EditorGUI.DisabledScope(!f_FPS)) {
				EditorGUILayout.LabelField("FPS Indication Feature", f_FPS ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_FPS) {
						LabelEnumDisabledFromTagMixed<FPSMode>("Mode", KawaShadeCommons.F_FPSMode);
						ShaderPropertyDisabled(_FPS_TLo, "Low FPS tint");
						ShaderPropertyDisabled(_FPS_THi, "High FPS tint");
					}
				}
			}
		}
	}
}
