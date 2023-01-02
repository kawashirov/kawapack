using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;
using System.Collections.Generic;

namespace Kawashirov.KawaShade {
	public class FeatureFPS : AbstractFeature {
		internal static readonly string F_FPS = "KawaShade_Feature_FPS";
		internal static readonly string F_FPSMode = "KawaShade_Feature_FPSMode";

		internal static readonly GUIContent gui_feature_fps = new GUIContent("FPS Feature");

		public enum Mode { ColorTint, DigitsTexture, DigitsMesh }

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_FPS);
			tags.Add(F_FPSMode);
		}

		public override void ConfigureShader(KawaShadeGenerator gen, ShaderSetup shader) {
			IncludeFeatureDirect(shader, "FeatureFPS.hlsl");

			shader.TagBool(F_FPS, gen.FPS);
			if (gen.FPS) {
				shader.TagEnum(F_FPSMode, gen.FPSMode);
				switch (gen.FPSMode) {
					case Mode.ColorTint:
						shader.Define("FPS_COLOR 1");
						break;
					case Mode.DigitsTexture:
						shader.Define("FPS_TEX 1");
						break;
					case Mode.DigitsMesh:
						shader.Define("FPS_MESH 1");
						shader.Define("NEED_VERT_CULL 1");
						break;
				}
				shader.properties.Add(new PropertyColor() { name = "_FPS_TLo", defualt = new Color(1, 0.5f, 0.5f, 1) });
				shader.properties.Add(new PropertyColor() { name = "_FPS_THi", defualt = new Color(0.5f, 1, 0.5f, 1) });
			} else {
				shader.Define("FPS_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var FPS = editor.serializedObject.FindProperty("FPS");
			KawaGUIUtility.ToggleLeft(FPS, gui_feature_fps);
			using (new EditorGUI.DisabledScope(FPS.hasMultipleDifferentValues || !FPS.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					KawaGUIUtility.DefaultPrpertyField(editor, "FPSMode", "Mode");
				}
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _FPS_TLo = editor.FindProperty("_FPS_TLo");
			var _FPS_THi = editor.FindProperty("_FPS_THi");
			var f_FPS = KawaUtilities.AnyNotNull(_FPS_TLo, _FPS_THi);
			using (new EditorGUI.DisabledScope(!f_FPS)) {
				EditorGUILayout.LabelField("FPS Indication Feature", f_FPS ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_FPS) {
						editor.LabelEnumDisabledFromTagMixed<Mode>("Mode", F_FPSMode);
						editor.ShaderPropertyDisabled(_FPS_TLo, "Low FPS tint");
						editor.ShaderPropertyDisabled(_FPS_THi, "High FPS tint");
					}
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool FPS = false;
		public FeatureFPS.Mode FPSMode = FeatureFPS.Mode.ColorTint;
	}

}
