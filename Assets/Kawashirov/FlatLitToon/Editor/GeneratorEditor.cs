using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Kawashirov;

using DisabledScope = UnityEditor.EditorGUI.DisabledScope;
using IndentLevelScope = UnityEditor.EditorGUI.IndentLevelScope;
using KGC = Kawashirov.GeneralCommons;
using EGUIL = UnityEditor.EditorGUILayout;
using KFLTC = Kawashirov.FLT.Commons;


// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/

namespace Kawashirov.FLT  
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Generator))]
	public class GeneratorEditor : KawaEditor {

		public override void OnInspectorGUI()
		{
			var error = false;

			var complexity_VGF = false;
			var complexity_VHDGF = false;

			EGUIL.LabelField("Shader");
			using (new IndentLevelScope()) {
				this.DefaultPrpertyField("shaderName", "Name");
				using (new DisabledScope(error)) {
					var result = this.serializedObject.FindProperty("result");
					if (result.hasMultipleDifferentValues) {
						using (new DisabledScope(true)) {
							EGUIL.LabelField("Shader Asset", "Mixed Values");
						}
					} else if (result.objectReferenceValue == null) {
						using (new DisabledScope(true)) {
							EGUIL.LabelField("Shader Asset", "Not Yet Generated");
						}
					} else {
						this.DefaultPrpertyField(result, "Shader Asset");
					}
				}
			}

			EGUIL.Space();
			var debug = this.serializedObject.FindProperty("debug");
			this.DefaultPrpertyField(debug, "Debug Build");
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
				var complexity = this.serializedObject.FindProperty("complexity");
				PropertyEnumPopupCustomLabels(complexity, "DX11 Pipeline Stages", KFLTC.shaderComplexityNames);

				complexity_VGF = !complexity.hasMultipleDifferentValues && complexity.intValue == (int)ShaderComplexity.VGF;
				complexity_VHDGF = !complexity.hasMultipleDifferentValues && complexity.intValue == (int)ShaderComplexity.VHDGF;

				using (new DisabledScope(!complexity_VHDGF)) {
					using (new IndentLevelScope()) {
						this.DefaultPrpertyField("tessPartitioning", "Tessellation Partitioning");
						this.DefaultPrpertyField("tessDomain", "Tessellation Domain (Primitive Topology)");
					}
				}

				var mode = this.serializedObject.FindProperty("mode");
				this.DefaultPrpertyField(mode, "Blending Mode");
				var mode_int = !mode.hasMultipleDifferentValues ? mode.intValue : (int?)null;
				var mode_Opaque = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Opaque;
				var mode_Cutout = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Cutout;
				var mode_Fade = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Fade;

				if (!mode.hasMultipleDifferentValues && mode.intValue == (int)BlendTemplate.Custom) {
					EGUIL.HelpBox("Custom belding options currently in-dev and not yet supported.", MessageType.Error);
					error = true;
				}

				this.DefaultPrpertyField("cull");

				this.DefaultPrpertyField("instancing");

				var queueOffset = this.serializedObject.FindProperty("queueOffset");
				this.DefaultPrpertyField(queueOffset);
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
					this.DefaultPrpertyField("disableBatching");
				}

				var forceNoShadowCasting = this.serializedObject.FindProperty("forceNoShadowCasting");
				this.DefaultPrpertyField(forceNoShadowCasting);
				var forceNoShadowCasting_bool = !forceNoShadowCasting.hasMultipleDifferentValues ? forceNoShadowCasting.boolValue : (bool?)null;
				if (forceNoShadowCasting_bool.HasValue && mode_int.HasValue && !forceNoShadowCasting_bool.Value && mode_int.Value == (int)BlendTemplate.Fade) {
					EGUIL.HelpBox(
						"Blending mode is \"Fade\", but \"Force No Shadow Casting\" is Off.\n" +
						"Usually transparent modes does not cast shadows, but this shader can use \"Cutout\" shadow caster for transparent modes.\n" +
						"It's better to disable shadow casting for \"Fade\" at all, unless you REALLY need it.",
						MessageType.Warning
					);
				}

				this.DefaultPrpertyField("ignoreProjector");
			}

			EGUIL.Space();
			EGUIL.LabelField("General Rendering Features");
			using (new IndentLevelScope()) {
				var mainTex = this.serializedObject.FindProperty("mainTex");
				PropertyEnumPopupCustomLabels(mainTex, "Main (Albedo) Texture", KFLTC.mainTexKeywordsNames);

				var cutout = this.serializedObject.FindProperty("cutout");
				PropertyEnumPopupCustomLabels(cutout, "Cutout Mode", KFLTC.cutoutModeNames);

				var emission = this.serializedObject.FindProperty("emission");
				this.DefaultPrpertyField(emission);
				using (new DisabledScope(!emission.boolValue)) {
					using (new IndentLevelScope()) {
						var emissionMode = this.serializedObject.FindProperty("emissionMode");
						PropertyEnumPopupCustomLabels(emissionMode, "Mode", KFLTC.emissionMode);
					}
				}
				this.DefaultPrpertyField("bumpMap");
			}

			EGUIL.Space();
			EGUIL.LabelField("PRNG Settings");
			using (new IndentLevelScope()) {
				EGUIL.HelpBox(
					"Some features using Pseudo-Random Number Generator.\n" +
					"These options affects it's behaivor.",
					MessageType.None
				);
				this.DefaultPrpertyField("rndMixTime", "Use Time where possible");
				this.DefaultPrpertyField("rndMixCords", "Use Screen-Space coords where possible");
			}

			EGUIL.Space();
			var shading = this.serializedObject.FindProperty("shading");
			PropertyEnumPopupCustomLabels(shading, "Shading Method", KFLTC.shadingModeNames);

			EGUIL.Space();
			var distanceFade = this.serializedObject.FindProperty("distanceFade");
			this.DefaultPrpertyField(distanceFade, "Distance Dithering Fade Feature");
			using (new DisabledScope(distanceFade.hasMultipleDifferentValues || !distanceFade.boolValue)) {
				using (new IndentLevelScope()) {
					this.DefaultPrpertyField("distanceFadeMode", "Mode");
				}
			}

			EGUIL.Space();
			var FPS = this.serializedObject.FindProperty("FPS");
			this.DefaultPrpertyField(FPS, "FPS Feature");
			using (new DisabledScope(FPS.hasMultipleDifferentValues || !FPS.boolValue)) {
				using (new IndentLevelScope()) {
					this.DefaultPrpertyField("FPSMode", "Mode");
				}
			}

			EGUIL.Space();
			using (new DisabledScope(!complexity_VGF && !complexity_VHDGF)) {
				var outline = this.serializedObject.FindProperty("outline");
				this.DefaultPrpertyField(outline, "Outline Feature");
				using (new DisabledScope(
					outline.hasMultipleDifferentValues || !outline.boolValue || (!complexity_VGF && !complexity_VHDGF)
				)) {
					using (new IndentLevelScope()) {
						this.DefaultPrpertyField("outlineMode", "Mode");
					}
				}
			}

			EGUIL.Space();
			using (new DisabledScope(!complexity_VGF && !complexity_VHDGF)) {
				var iwd = this.serializedObject.FindProperty("iwd");
				this.DefaultPrpertyField(iwd, "Infinity War Decimation Feature");
				using (new DisabledScope(
					iwd.hasMultipleDifferentValues || !iwd.boolValue || (!complexity_VGF && !complexity_VHDGF)
				)) {
					using (new IndentLevelScope()) {
						this.DefaultPrpertyField("iwdDirections", "Directions");
					}
				}
			}

			EGUIL.Space();
			using (new DisabledScope(!complexity_VGF && !complexity_VHDGF)) {
				var pcw = this.serializedObject.FindProperty("pcw");
				this.DefaultPrpertyField(pcw, "Poly ColorWave Feature");
				using (new DisabledScope(
					pcw.hasMultipleDifferentValues || !pcw.boolValue || (!complexity_VGF && !complexity_VHDGF)
				)) {
					using (new IndentLevelScope()) {
						this.DefaultPrpertyField("pcwMode", "Mode");
					}
				}
			}

			EGUIL.Space();
			using (new DisabledScope(error)) {
				if (GUILayout.Button("(Re)Bake Shader")) {
					if (error)
						return;
					foreach (var t in this.targets) {
						var generator = t as Generator;
						if (generator)
							generator.Generate();
					}
				}	
			}
			this.serializedObject.ApplyModifiedProperties();
		}
	}
}