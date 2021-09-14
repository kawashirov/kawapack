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
using SC = Kawashirov.StaticCommons;

using static UnityEditor.EditorGUI;

namespace Kawashirov.FLT {
	public enum FPSMode { ColorTint, DigitsTexture, DigitsMesh }

	internal static partial class Commons {
		internal static readonly string F_FPS = "KawaFLT_Feature_FPS";
		internal static readonly string F_FPSMode = "KawaFLT_Feature_FPSMode";
	}

	public partial class Generator {
		public bool FPS = false;
		public FPSMode FPSMode = FPSMode.ColorTint;

		private void ConfigureFeatureFPS(ShaderSetup shader) {
			shader.TagBool(KFLTC.F_FPS, FPS);
			if (FPS) {
				shader.TagEnum(KFLTC.F_FPSMode, FPSMode);
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

	public partial class GeneratorEditor {
		private static readonly GUIContent gui_feature_fps = new GUIContent("FPS Feature");

		private void FPSGUI() {
			var FPS = serializedObject.FindProperty("FPS");
			ToggleLeft(FPS, gui_feature_fps);
			using (new DisabledScope(FPS.hasMultipleDifferentValues || !FPS.boolValue)) {
				using (new IndentLevelScope()) {
					DefaultPrpertyField("FPSMode", "Mode");
				}
			}
		}

	}
}

internal partial class KawaFLTShaderGUI {
	protected void OnGUI_FPS() {
		// Commons.MaterialTagBoolCheck(this.target, Commons.KawaFLT_Feature_FPS);
		var _FPS_TLo = FindProperty("_FPS_TLo");
		var _FPS_THi = FindProperty("_FPS_THi");
		var f_FPS = SC.AnyNotNull(_FPS_TLo, _FPS_THi);
		using (new DisabledScope(!f_FPS)) {
			EGUIL.LabelField("FPS Indication Feature", f_FPS ? "Enabled" : "Disabled");
			using (new IndentLevelScope()) {
				if (f_FPS) {
					LabelEnumDisabledFromTagMixed<FPSMode>("Mode", KFLTC.F_FPSMode);
					ShaderPropertyDisabled(_FPS_TLo, "Low FPS tint");
					ShaderPropertyDisabled(_FPS_THi, "High FPS tint");
				}
			}
		}
	}
}
