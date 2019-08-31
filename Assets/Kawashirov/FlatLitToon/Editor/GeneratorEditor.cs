using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// Имя файла длжно совпадать с именем типа.
// https://forum.unity.com/threads/solved-blank-scriptableobject-on-import.511527/

namespace Kawashirov.FLT  
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(Generator))]
	public class GeneratorEditor : Editor {

		public static bool PropertyEnumPopupCustomLabels<E>(
			SerializedProperty property, string label, Dictionary<E, string> labels = null,
			GUILayoutOption[] options = null
		) where E : struct, IConvertible, IComparable, IFormattable
		{
			var e_display = property.enumDisplayNames;
			var e_type = typeof(E);

			if (labels != null && e_type.IsEnum && labels.Count > 0) {
				var e_values = Enum.GetValues(e_type);
				var e_names = Enum.GetNames(e_type);
				for (var e_index = 0; e_index < e_names.Length; ++e_index) {
					var e_object = (E) Enum.Parse(typeof(E), e_names[e_index]);
					string custom_label;
					labels.TryGetValue(e_object, out custom_label);
					if (!string.IsNullOrEmpty(custom_label)) {
						e_display[e_index] = custom_label;
					}
				}
			}

			EditorGUI.BeginChangeCheck();
			var enumValueIndex = EditorGUILayout.Popup(
				label, (!property.hasMultipleDifferentValues) ? property.enumValueIndex : -1, e_display, options
			);
			if (EditorGUI.EndChangeCheck()) {
				property.enumValueIndex = enumValueIndex;
				return true;
			}
			return false;
		}

		public static int PropertyMaskPopupCustomLabels(
			string label, SerializedProperty property, Type enum_t, Dictionary<int, string> labels = null,
			GUILayoutOption[] options = null
		) {
				

			return 0;
		}

		public void DefaultPrpertyField(SerializedProperty property, string label = null)
		{
			if (string.IsNullOrEmpty(label)) {
				EditorGUILayout.PropertyField(property);
			} else {
				EditorGUILayout.PropertyField(property, new GUIContent(label));
			}
		}

		public void DefaultPrpertyField(string name, string label = null)
		{
			this.DefaultPrpertyField(this.serializedObject.FindProperty(name), label);
		}

		public override void OnInspectorGUI()
		{
			var error = false;

			var complexity_VGF = false;
			var complexity_VHDGF = false;

			EditorGUILayout.LabelField("Shader");
			using (new EditorGUI.IndentLevelScope()) {
				this.DefaultPrpertyField("shaderName", "Name");
				using (new EditorGUI.DisabledScope(error)) {
					this.DefaultPrpertyField("result", "Bound Asset");
				}
			}

			EditorGUILayout.Space();
			var debug = this.serializedObject.FindProperty("debug");
			this.DefaultPrpertyField(debug, "Debug Build");
			var debug_bool = !debug.hasMultipleDifferentValues ? debug.boolValue : (bool?)null;
			var debug_true = debug_bool.HasValue && debug_bool.Value;
			if (debug_true) {
				EditorGUILayout.HelpBox(
					"Debug mode is On! In debug mode:\n" +
					"- d3d11 debug symbols included into shader build;\n" +
					"- Some configuration errors are warnings;\n" +
					"- Some checks disabled at generating process, so shader is baked \"as-is\";\n" +
					"Please, do not use debug shaders outside of Unity Editor!",
					MessageType.Warning
				);
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("General Shader Options");
			using (new EditorGUI.IndentLevelScope()) {
				var complexity = this.serializedObject.FindProperty("complexity");
				//this.DefaultPrpertyField(complexity, "DX11 Pipeline Stages");
				PropertyEnumPopupCustomLabels(complexity, "DX11 Pipeline Stages", Commons.shaderComplexityNames);

				complexity_VGF = !complexity.hasMultipleDifferentValues && complexity.intValue == (int)ShaderComplexity.VGF;
				complexity_VHDGF = !complexity.hasMultipleDifferentValues && complexity.intValue == (int)ShaderComplexity.VHDGF;

				using (new EditorGUI.DisabledScope(!complexity_VHDGF)) {
					using (new EditorGUI.IndentLevelScope()) {
						this.DefaultPrpertyField("tessPartitioning", "Tessellation Partitioning");
						this.DefaultPrpertyField("tessDomain", "Tessellation Domain (Primitive Topology)");
					}
				}

				var mode = this.serializedObject.FindProperty("mode");
				this.DefaultPrpertyField(mode, "Blending Mode");
				var mode_int = !mode.hasMultipleDifferentValues ? mode.intValue : (int?)null;
				var mode_Opaque = mode_int.HasValue && mode_int.Value == (int) BlendTemplate.Opaque;
				var mode_Cutout = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Cutout;
				var mode_Fade = mode_int.HasValue && mode_int.Value == (int)BlendTemplate.Fade;

				if (!mode.hasMultipleDifferentValues && mode.intValue == (int)BlendTemplate.Custom) {
					EditorGUILayout.HelpBox("Custom belding options currently in-dev and not yet supported.", MessageType.Error);
					error = true;
				}

				this.DefaultPrpertyField("cull");

				this.DefaultPrpertyField("instancing");

				var queueOffset = this.serializedObject.FindProperty("queueOffset");
				this.DefaultPrpertyField(queueOffset);
				var queueOffset_int = !queueOffset.hasMultipleDifferentValues ? queueOffset.intValue : (int?)null;
				using (new EditorGUI.DisabledScope(true)) {
					using (new EditorGUI.IndentLevelScope()) {
						//var mode_ = this.serializedObject.FindProperty("mode");
						var queueOffset_str = "-";
						if (!queueOffset_int.HasValue && mode_int.HasValue) {
							string q = null;
							switch (mode_int.Value) {
								case (int) BlendTemplate.Opaque:
									q = "Geometry";
									break;
								case (int) BlendTemplate.Cutout:
									q = "AlphaTest";
									break;
								case (int) BlendTemplate.Fade:
									q = "Transparent";
									break;
							}
							queueOffset_str = string.Format("{0}{1:+#;-#;+0}", q, queueOffset_int.Value);
						}
						EditorGUILayout.TextField("Queue", queueOffset_str);
					}
				}

				using (new EditorGUI.DisabledScope(true)) {
					this.DefaultPrpertyField("disableBatching");
				}

				var forceNoShadowCasting = this.serializedObject.FindProperty("forceNoShadowCasting");
				this.DefaultPrpertyField(forceNoShadowCasting);
				var forceNoShadowCasting_bool = !forceNoShadowCasting.hasMultipleDifferentValues ? forceNoShadowCasting.boolValue : (bool?)null;
				if (forceNoShadowCasting_bool.HasValue && mode_int.HasValue && !forceNoShadowCasting_bool.Value && mode_int.Value == (int)BlendTemplate.Fade) {
					EditorGUILayout.HelpBox(
						"Blending mode is \"Fade\", but \"Force No Shadow Casting\" is Off.\n" +
						"Usually transparent modes does not cast shadows, but this shader can use \"Cutout\" shadow caster for transparent modes.\n" +
						"It's better to disable shadow casting for \"Fade\" at all, unless you REALLY need it.",
						MessageType.Warning
					);
				}

				this.DefaultPrpertyField("ignoreProjector");

				/*
				var zWrite = this.serializedObject.FindProperty("zWrite");
				this.DefaultPrpertyField(zWrite, "Z-Write");
				var zWrite_bool = !zWrite.hasMultipleDifferentValues ? zWrite.boolValue : (bool?)null;
				var zWrite_true = zWrite_bool.HasValue && zWrite_bool.Value;
				var zWrite_false = zWrite_bool.HasValue && !zWrite_bool.Value;
				if (zWrite_true && mode_Fade) {
					EditorGUILayout.HelpBox(
						"Blending mode is \"Fade\", but Z-Write is On.\n"+
						"Do you need it?\n" +
						"Usually semi-transparent modes needs Z-Write Off.",
						debug_true ? MessageType.Warning : MessageType.Error
					);
				}
				if (zWrite_false && (mode_Opaque || mode_Cutout)) {
					EditorGUILayout.HelpBox(
						"Blending mode is \"Opaque\"/\"Cutout\", but Z-Write is Off.\n" +
						"Are you sure?\n" +
						"Usually solid modes needs Z-Write On.",
						debug_true ? MessageType.Warning : MessageType.Error
					);
				}
				*/

			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("General Rendering Features");
			using (new EditorGUI.IndentLevelScope()) {
				var mainTex = this.serializedObject.FindProperty("mainTex");
				PropertyEnumPopupCustomLabels(mainTex, "Main (Albedo) Texture", Commons.mainTexKeywordsNames);

				var cutout = this.serializedObject.FindProperty("cutout");
				PropertyEnumPopupCustomLabels(cutout, "Cutout Mode", Commons.cutoutModeNames);

				var emission = this.serializedObject.FindProperty("emission");
				this.DefaultPrpertyField(emission);
				using (new EditorGUI.DisabledScope(!emission.boolValue)) {
					using (new EditorGUI.IndentLevelScope()) {
						var emissionMode = this.serializedObject.FindProperty("emissionMode");
						PropertyEnumPopupCustomLabels(emissionMode, "Mode", Commons.emissionMode);
					}
				}
				this.DefaultPrpertyField("bumpMap");
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("PRNG Settings");
			using (new EditorGUI.IndentLevelScope()) {
				EditorGUILayout.HelpBox(
					"Some features using Pseudo-Random Number Generator.\n" +
					"These options affects it's behaivor.",
					MessageType.None
				);
				this.DefaultPrpertyField("rndMixTime", "Use Time where possible");
				this.DefaultPrpertyField("rndMixCords", "Use Screen-Space coords where possible");
			}

			EditorGUILayout.Space();
			var shading = this.serializedObject.FindProperty("shading");
			PropertyEnumPopupCustomLabels(shading, "Shading Method", Commons.shadingModeNames);

			EditorGUILayout.Space();
			var distanceFade = this.serializedObject.FindProperty("distanceFade");
			this.DefaultPrpertyField(distanceFade, "Feature: Distance Dithering Fade");
			using (new EditorGUI.DisabledScope(distanceFade.hasMultipleDifferentValues || !distanceFade.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					this.DefaultPrpertyField("distanceFadeMode", "Mode");
				}
			}

			EditorGUILayout.Space();
			var FPS = this.serializedObject.FindProperty("FPS");
			this.DefaultPrpertyField(FPS, "Feature: FPS");
			using (new EditorGUI.DisabledScope(FPS.hasMultipleDifferentValues || !FPS.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					this.DefaultPrpertyField("FPSMode", "Mode");
				}
			}

			EditorGUILayout.Space();
			using (new EditorGUI.DisabledScope(!complexity_VGF && !complexity_VHDGF)) {
				var outline = this.serializedObject.FindProperty("outline");
				this.DefaultPrpertyField(outline, "VGF/VHDGF Feature: Outline");
				using (new EditorGUI.DisabledScope(
					outline.hasMultipleDifferentValues || !outline.boolValue || (!complexity_VGF && !complexity_VHDGF)
				)) {
					using (new EditorGUI.IndentLevelScope()) {
						this.DefaultPrpertyField("outlineMode", "Mode");
					}
				}
			}

			EditorGUILayout.Space();
			using (new EditorGUI.DisabledScope(!complexity_VGF && !complexity_VHDGF)) {
				var iwd = this.serializedObject.FindProperty("iwd");
				this.DefaultPrpertyField(iwd, "VGF/VHDGF Feature: Infinity War Decimation");
				using (new EditorGUI.DisabledScope(
					iwd.hasMultipleDifferentValues || !iwd.boolValue || (!complexity_VGF && !complexity_VHDGF)
				)) {
					using (new EditorGUI.IndentLevelScope()) {
						this.DefaultPrpertyField("iwdDirections", "Directions");
					}
				}
			}

			EditorGUILayout.Space();
			using (new EditorGUI.DisabledScope(!complexity_VGF && !complexity_VHDGF)) {
				var pcw = this.serializedObject.FindProperty("pcw");
				this.DefaultPrpertyField(pcw, "VGF/VHDGF Feature: Poly ColorWave");
				using (new EditorGUI.DisabledScope(
					pcw.hasMultipleDifferentValues || !pcw.boolValue || (!complexity_VGF && !complexity_VHDGF)
				)) {
					using (new EditorGUI.IndentLevelScope()) {
						this.DefaultPrpertyField("pcwMode", "Mode");
					}
				}
			}

			EditorGUILayout.Space();
			using (new EditorGUI.DisabledScope(error)) {
				if (GUILayout.Button("(Re)Generate Shader")) {
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