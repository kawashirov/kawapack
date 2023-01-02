using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {

	public class FeatureCutout : AbstractFeature {
		internal static readonly string F_Cutout_Forward = "KawaShade_Feature_Cutout_Forward";
		internal static readonly string F_Cutout_ShadowCaster = "KawaShade_Feature_Cutout_ShadowCaster";

		public enum Mode { None, Classic, Range }

		[Serializable]
		public struct PerPass {
			public Mode mode;
			public bool remap; // remap everything is not cut [t, 1] -> [0, 1].
			public bool h01; // apply cubic Hermite spline smooth on range
			public bool random; // random or bayer dither on range
		}

		internal static readonly Dictionary<Mode, string> cutoutModeNames = new Dictionary<Mode, string>() {
			{ Mode.None, "No any alpha cut-off." },
			{ Mode.Classic, "Classic (Single alpha value as threshold)" },
			{ Mode.Range, "Range (Two alpha values defines range where texture fades)" },
		};

		public override int GetOrder() => (int)Order.GENERAL + 20;

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_Cutout_Forward);
			tags.Add(F_Cutout_ShadowCaster);
		}

		private void ConfigurePassDefines(KawaShadeGenerator gen, PassSetup pass, PerPass perpass) {
			if (perpass.mode != Mode.None) {
				pass.defines.Add("CUTOFF_ON 1");

				if (perpass.mode == Mode.Classic) {
					pass.defines.Add("CUTOFF_CLASSIC 1");

				} else if (perpass.mode == Mode.Range) {
					pass.defines.Add("CUTOFF_RANGE 1");

					if (perpass.h01)
						pass.defines.Add("CUTOFF_H01 1");

					if (perpass.random) {
						gen.needRandomFrag = true;
						pass.defines.Add("CUTOFF_RANDOM 1");
					} else {
						pass.defines.Add("CUTOFF_BAYER 1");
					}
				}

				if (perpass.remap)
					pass.defines.Add("CUTOFF_REMAP 1");
			} else {
				pass.defines.Add("CUTOFF_OFF 1");
			}
		}

		public override void ConfigureShader(KawaShadeGenerator gen, ShaderSetup shader) {
			ConfigurePassDefines(gen, shader.forward, gen.cutoutForward);
			ConfigurePassDefines(gen, shader.forward_add, gen.cutoutForward);
			shader.TagEnum(F_Cutout_Forward, gen.cutoutForward.mode);

			ConfigurePassDefines(gen, shader.shadowcaster, gen.cutoutShadowcaster);
			shader.TagEnum(F_Cutout_ShadowCaster, gen.cutoutShadowcaster.mode);

			if (gen.cutoutForward.mode == Mode.Classic || gen.cutoutShadowcaster.mode == Mode.Classic) {
				shader.properties.Add(new PropertyFloat() { name = "_Cutoff", defualt = 0.5f, range = new Vector2(0, 1) });
			}

			if (gen.cutoutForward.mode == Mode.Range || gen.cutoutShadowcaster.mode == Mode.Range) {
				shader.properties.Add(new PropertyFloat() { name = "_CutoffMin", defualt = 0.4f, range = new Vector2(0, 1) });
				shader.properties.Add(new PropertyFloat() { name = "_CutoffMax", defualt = 0.6f, range = new Vector2(0, 1) });
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			var cutoutForward = editor.serializedObject.FindProperty("cutoutForward");
			var cutoutShadowcaster = editor.serializedObject.FindProperty("cutoutShadowcaster");
			EditorGUILayout.LabelField("Cutout Features", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope()) {
				EditorGUILayout.PropertyField(cutoutForward, new GUIContent("Cutout at Forward pass"), true);
				EditorGUILayout.PropertyField(cutoutShadowcaster, new GUIContent("Cutout at Shadowcaster pass"), true);
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			editor.LabelEnumDisabledFromTagMixed<Mode>("Forward Pass Cutout Mode", F_Cutout_Forward);
			editor.LabelEnumDisabledFromTagMixed<Mode>("Shadow Caster Cutout Mode", F_Cutout_ShadowCaster);

			var _CutoffMin = editor.FindProperty("_CutoffMin");
			var _CutoffMax = editor.FindProperty("_CutoffMax");
			if (KawaUtilities.AnyNotNull(_CutoffMin, _CutoffMax)) {
				editor.ShaderPropertyDisabled(_CutoffMin, "Cutout Min");
				editor.ShaderPropertyDisabled(_CutoffMax, "Cutout Max");
			}

			var _Cutoff = editor.FindProperty("_Cutoff");
			if (_Cutoff != null) {
				editor.ShaderPropertyDisabled(_Cutoff, "Cutout (Classic)");
			}

		}
	}

	public partial class KawaShadeGenerator {
		public FeatureCutout.PerPass cutoutForward = default;
		public FeatureCutout.PerPass cutoutShadowcaster = default;
	}
}
