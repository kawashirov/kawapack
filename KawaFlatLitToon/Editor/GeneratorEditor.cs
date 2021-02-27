using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

using KGC = Kawashirov.StaticCommons;
using GUIL = UnityEngine.GUILayout;
using EGUIL = UnityEditor.EditorGUILayout;
using KFLTC = Kawashirov.FLT.Commons;

using static UnityEditor.EditorGUI;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/

namespace Kawashirov.FLT  
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Generator))]
	public class GeneratorEditor : ShaderBaking.BaseGenerator.Editor<Generator> {
		private static readonly GUIContent gui_feature_matcap = new GUIContent("Matcap Feature");
		private static readonly GUIContent gui_feature_dstfd = new GUIContent("Distance Dithering Fade Feature");
		private static readonly GUIContent gui_feature_wnoise = new GUIContent("White Noise Feature");
		private static readonly GUIContent gui_feature_fps = new GUIContent("FPS Feature");
		private static readonly GUIContent gui_feature_outline = new GUIContent("Outline Feature");
		private static readonly GUIContent gui_feature_iwd = new GUIContent("Infinity War Decimation Feature");
		private static readonly GUIContent gui_feature_pcw = new GUIContent("Poly ColorWave Feature");
		
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			var error = false;

			var complexity_VGF = false;
			var complexity_VHDGF = false;

			EGUIL.Space();
			var debug = serializedObject.FindProperty("debug");
			DefaultPrpertyField(debug, "Debug Build");
			var debug_bool = !debug.hasMultipleDifferentValues ? debug.boolValue : (bool?)null;
			var debug_true = debug_bool.HasValue && debug_bool.Value;
			if (debug_true) {
				EGUIL.HelpBox(
					"Debug mode is On! In debug mode:\n" +
					"- d3d11 debug symbols included into shader build;\n" +
					"- Some configuration errors are warnings;\n" +
					"- Some checks disabled at generating process, so shader is baked \"as-is\";\n" +
					"Please, do not use debug shaders outside of Unity Editor!",
					MessageType.Warning
				);
			}

			EGUIL.Space();
			EGUIL.LabelField("General Shader Options");
			using (new IndentLevelScope()) {
				var complexity = serializedObject.FindProperty("complexity");
				PropertyEnumPopupCustomLabels(complexity, "DX11 Pipeline Stages", KFLTC.shaderComplexityNames);

				complexity_VGF = !complexity.hasMultipleDifferentValues && complexity.intValue == (int)ShaderComplexity.VGF;
				complexity_VHDGF = !complexity.hasMultipleDifferentValues && complexity.intValue == (int)ShaderComplexity.VHDGF;

				using (new DisabledScope(!complexity_VHDGF)) {
					using (new IndentLevelScope()) {
						DefaultPrpertyField("tessPartitioning", "Tessellation Partitioning");
						DefaultPrpertyField("tessDomain", "Tessellation Domain (Primitive Topology)");
					}
				}

				var mode = serializedObject.FindProperty("mode");
				DefaultPrpertyField(mode, "Blending Mode");
				var mode_int = !mode.hasMultipleDifferentValues ? mode.intValue : (int?)null;
				var mode_Opaque = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Opaque;
				var mode_Cutout = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Cutout;
				var mode_Fade = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Fade;

				if (!mode.hasMultipleDifferentValues && mode.intValue == (int)BlendTemplate.Custom) {
					EGUIL.HelpBox("Custom belding options currently in-dev and not yet supported.", MessageType.Error);
					error = true;
				}

				DefaultPrpertyField("cull");

				DefaultPrpertyField("instancing");

				var queueOffset = serializedObject.FindProperty("queueOffset");
				DefaultPrpertyField(queueOffset);
				var queueOffset_int = !queueOffset.hasMultipleDifferentValues ? queueOffset.intValue : (int?)null;
				using (new DisabledScope(true)) {
					using (new IndentLevelScope()) {
						var queueOffset_str = "Mixed Values";
						if (queueOffset_int.HasValue && mode_int.HasValue) {
							string q = null;
							if (mode_int.Value == (int)BlendTemplate.Opaque) {
								q = "Geometry";
							} else if (mode_int.Value == (int)BlendTemplate.Cutout) {
								q = "AlphaTest";
							} else if (KGC.AnyEq(mode_int.Value, (int)BlendTemplate.Fade, (int)BlendTemplate.FadeCutout)) {
								q = "Transparent";
							}
							queueOffset_str = string.Format("{0}{1:+#;-#;+0}", q, queueOffset_int.Value);
						}
						EGUIL.TextField("Queue", queueOffset_str);
					}
				}

				using (new DisabledScope(true)) {
					DefaultPrpertyField("disableBatching");
				}

				var forceNoShadowCasting = serializedObject.FindProperty("forceNoShadowCasting");
				DefaultPrpertyField(forceNoShadowCasting);
				var forceNoShadowCasting_bool = !forceNoShadowCasting.hasMultipleDifferentValues ? forceNoShadowCasting.boolValue : (bool?)null;
				if (forceNoShadowCasting_bool.HasValue && mode_int.HasValue && !forceNoShadowCasting_bool.Value && mode_int.Value == (int)BlendTemplate.Fade) {
					EGUIL.HelpBox(
						"Blending mode is \"Fade\", but \"Force No Shadow Casting\" is Off.\n" +
						"Usually transparent modes does not cast shadows, but this shader can use \"Cutout\" shadow caster for transparent modes.\n" +
						"It's better to disable shadow casting for \"Fade\" at all, unless you REALLY need it.",
						MessageType.Warning
					);
				}

				DefaultPrpertyField("ignoreProjector");
			}

			EGUIL.Space();
			EGUIL.LabelField("General Rendering Features");
			using (new IndentLevelScope()) {
				var mainTex = serializedObject.FindProperty("mainTex");
				PropertyEnumPopupCustomLabels(mainTex, "Main (Albedo) Texture", KFLTC.mainTexKeywordsNames);

				var cutout = serializedObject.FindProperty("cutout");
				PropertyEnumPopupCustomLabels(cutout, "Cutout Mode", KFLTC.cutoutModeNames);

				var emission = serializedObject.FindProperty("emission");
				DefaultPrpertyField(emission);
				using (new DisabledScope(!emission.boolValue)) {
					using (new IndentLevelScope()) {
						var emissionMode = serializedObject.FindProperty("emissionMode");
						PropertyEnumPopupCustomLabels(emissionMode, "Mode", KFLTC.emissionMode);
					}
				}
				DefaultPrpertyField("bumpMap");
			}

			EGUIL.Space();
			EGUIL.LabelField("PRNG Settings");
			using (new IndentLevelScope()) {
				EGUIL.HelpBox(
					"Some features using Pseudo-Random Number Generator.\n" +
					"These options affects it's behaivor.",
					MessageType.None
				);
				DefaultPrpertyField("rndMixTime", "Use Time where possible");
				DefaultPrpertyField("rndMixCords", "Use Screen-Space coords where possible");
				DefaultPrpertyField("rndScreenScale", "Screen-Space scaling");
				using (new GUIL.HorizontalScope()) {
					var rndDefaultTexture = serializedObject.FindProperty("rndDefaultTexture");
					DefaultPrpertyField(rndDefaultTexture, "Default noise texture.");
					if (GUIL.Button("Default")) {
						rndDefaultTexture.objectReferenceValue = Generator.GetRndDefaultTexture();
					}
				}
			}

			EGUIL.Space();
			var shading = serializedObject.FindProperty("shading");
			PropertyEnumPopupCustomLabels(shading, "Shading Method", KFLTC.shadingModeNames);

			EGUIL.Space();
			var matcap = serializedObject.FindProperty("matcap");
			ToggleLeft(matcap, gui_feature_matcap);
			using (new DisabledScope(matcap.hasMultipleDifferentValues || !matcap.boolValue)) {
				using (new IndentLevelScope()) {
					DefaultPrpertyField("matcapMode", "Mode");
				}
			}

			EGUIL.Space();
			var distanceFade = serializedObject.FindProperty("distanceFade");
			ToggleLeft(distanceFade, gui_feature_dstfd);
			using (new DisabledScope(distanceFade.hasMultipleDifferentValues || !distanceFade.boolValue)) {
				using (new IndentLevelScope()) {
					DefaultPrpertyField("distanceFadeMode", "Mode");
				}
			}

			EGUIL.Space();
			var wnoise = serializedObject.FindProperty("wnoise");
			ToggleLeft(wnoise, gui_feature_wnoise);
			using (new DisabledScope(wnoise.hasMultipleDifferentValues || !wnoise.boolValue)) {
				using (new IndentLevelScope()) {
					// 
				}
			}

			EGUIL.Space();
			var FPS = serializedObject.FindProperty("FPS");
			ToggleLeft(FPS, gui_feature_fps);
			using (new DisabledScope(FPS.hasMultipleDifferentValues || !FPS.boolValue)) {
				using (new IndentLevelScope()) {
					DefaultPrpertyField("FPSMode", "Mode");
				}
			}

			EGUIL.Space();
			using (new DisabledScope(!complexity_VGF && !complexity_VHDGF)) {
				var outline = serializedObject.FindProperty("outline");
				ToggleLeft(outline, gui_feature_outline);
				using (new DisabledScope(
					outline.hasMultipleDifferentValues || !outline.boolValue || (!complexity_VGF && !complexity_VHDGF)
				)) {
					using (new IndentLevelScope()) {
						DefaultPrpertyField("outlineMode", "Mode");
					}
				}
			}

			EGUIL.Space();
			using (new DisabledScope(!complexity_VGF && !complexity_VHDGF)) {
				var iwd = serializedObject.FindProperty("iwd");
				ToggleLeft(iwd, gui_feature_iwd);
				using (new DisabledScope(
					iwd.hasMultipleDifferentValues || !iwd.boolValue || (!complexity_VGF && !complexity_VHDGF)
				)) {
					using (new IndentLevelScope()) {
						DefaultPrpertyField("iwdDirections", "Directions");
					}
				}
			}

			EGUIL.Space();
			using (new DisabledScope(!complexity_VGF && !complexity_VHDGF)) {
				var pcw = serializedObject.FindProperty("pcw");
				ToggleLeft(pcw, gui_feature_pcw);
				using (new DisabledScope(
					pcw.hasMultipleDifferentValues || !pcw.boolValue || (!complexity_VGF && !complexity_VHDGF)
				)) {
					using (new IndentLevelScope()) {
						DefaultPrpertyField("pcwMode", "Mode");
					}
				}
			}

			EGUIL.Space();
			using (new DisabledScope(error)) {
				if (GUIL.Button("(Re)Bake Shader")) {
					if (error)
						return;
					foreach (var t in targets) {
						var generator = t as Generator;
						if (generator)
							generator.Refresh();
					}
					Repaint();
				}	
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}
