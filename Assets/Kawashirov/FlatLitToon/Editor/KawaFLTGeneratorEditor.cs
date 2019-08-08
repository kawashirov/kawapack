using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kawashirov.FLT  
{
	[CustomEditor(typeof(Generator))]
	public class GeneratorEditor : Editor {

		public static void PropertyEnumPopupCustomLabels<E>(
			string label, SerializedProperty property, Dictionary<E, string> labels
		)
		{
			var enum_t = typeof(E);
			if (enum_t == null || !enum_t.IsEnum) throw new Exception(string.Format("E={0} is not enum type!", enum_t));

			var e_names = Enum.GetNames(enum_t);
			var e_values = Enum.GetValues(enum_t);


			var mixed = property.hasMultipleDifferentValues;
			var selected_before = mixed ? 0 : property.intValue;

			var options = new List<string>(e_values.Length + (mixed ? 1: 0));
			if (mixed)
				options.Add("-");
			foreach (var e_value in e_values) {
				string option;
				labels.TryGetValue((E) e_value, out option);
				if (string.IsNullOrEmpty(option)) {
					option = Enum.GetName(enum_t, e_value);
				}
				if (string.IsNullOrEmpty(option)) {
					option = "???";
				}
				options.Add(option);
			}

			var selected_after = EditorGUILayout.Popup(label, selected_before, options.ToArray(), null, new GUILayoutOption[] { });
			if (selected_before == selected_after)
				return;
			if (mixed && selected_after == 0)
				return;


			var new_index = mixed ? selected_after - 1 : selected_after;

			var new_value = (int) e_values.GetValue(new_index);

			Debug.LogFormat(
				"User changed {0} ({1}) from #{2} to #{3}...",
				property, label, selected_before, selected_after
			);
		}

		public void DefaultPrpertyField(string name, string label = null)
		{
			var property = this.serializedObject.FindProperty(name);
			if (string.IsNullOrEmpty(label)) {
				EditorGUILayout.PropertyField(property);
			} else {
				EditorGUILayout.PropertyField(property, new GUIContent(label));
			}
		}

		public override void OnInspectorGUI()
		{

			var error = false;


			EditorGUILayout.LabelField("Shader");
			using (new EditorGUI.IndentLevelScope()) {
				this.DefaultPrpertyField("shaderName", "Name");
				using (new EditorGUI.DisabledScope(error)) {
					this.DefaultPrpertyField("result", "Bound Asset");
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("General Shader Options");
			using (new EditorGUI.IndentLevelScope()) {
				this.DefaultPrpertyField("complexity");
				var complexity = this.serializedObject.FindProperty("complexity");
				var complexity_int = complexity.intValue;
				if (complexity_int == (int)ShaderComplexity.VHDGF) {
					EditorGUILayout.HelpBox("VHDGF currently in-dev and not yet supported.", MessageType.Error);
					error = true;
				}
				this.DefaultPrpertyField("mode");
				var mode = this.serializedObject.FindProperty("mode");
				var mode_int = mode.intValue;
				if (mode_int == (int)BlendTemplate.Custom) {
					EditorGUILayout.HelpBox("Custom belding options currently in-dev and not yet supported.", MessageType.Error);
					error = true;
				}
				this.DefaultPrpertyField("cull");
				this.DefaultPrpertyField("instancing");
				this.DefaultPrpertyField("queueOffset");
				using (new EditorGUI.DisabledScope(true)) {
					using (new EditorGUI.IndentLevelScope()) {
						var queueOffset = this.serializedObject.FindProperty("queueOffset");
						//var mode_ = this.serializedObject.FindProperty("mode");
						var queueOffset_str = "-";
						if (!queueOffset.hasMultipleDifferentValues) {

							string q = null;
							switch (mode_int) {
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
							queueOffset_str = string.Format("{0}{1:+#;-#;+0}", q, queueOffset.intValue);
						}
						EditorGUILayout.TextField("Queue", queueOffset_str);
					}
				}
				this.DefaultPrpertyField("disableBatching");
				this.DefaultPrpertyField("forceNoShadowCasting");
				this.DefaultPrpertyField("ignoreProjector");
				this.DefaultPrpertyField("zWrite");
				this.DefaultPrpertyField("debug");
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("General Rendering Features");
			using (new EditorGUI.IndentLevelScope()) {
				this.DefaultPrpertyField("mainTex");
				this.DefaultPrpertyField("cutout");
				this.DefaultPrpertyField("emission");
				var emission = this.serializedObject.FindProperty("emission");
				using (new EditorGUI.DisabledScope(!emission.boolValue)) {
					using (new EditorGUI.IndentLevelScope()) {
						this.DefaultPrpertyField("emissionMode", "Mode");
					}
				}
				this.DefaultPrpertyField("bumpMap");
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("PRNG Settings");
			using (new EditorGUI.IndentLevelScope()) {
				EditorGUILayout.HelpBox("Some features using Pseudo-Random Number Generator.\nThese options affects it's behaivor.", MessageType.None);
				this.DefaultPrpertyField("rndMixTime", "Use Time where possible");
				this.DefaultPrpertyField("rndMixCords", "Use Screen-Space coords where possible");
			}

			EditorGUILayout.Space();
			this.DefaultPrpertyField("shading", "Shading Method");

			EditorGUILayout.Space();
			this.DefaultPrpertyField("distanceFade", "VF Feature: Distance Dithering Fade");
			var distanceFade = this.serializedObject.FindProperty("distanceFade");
			using (new EditorGUI.DisabledScope(distanceFade.hasMultipleDifferentValues || !distanceFade.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					this.DefaultPrpertyField("distanceFadeMode", "Mode");
					this.DefaultPrpertyField("distanceFadeRandom", "Dither (Randomnss) Type");
				}
			}

			EditorGUILayout.Space();
			this.DefaultPrpertyField("FPS", "VF Feature: FPS");
			var FPS = this.serializedObject.FindProperty("FPS");
			using (new EditorGUI.DisabledScope(FPS.hasMultipleDifferentValues || !FPS.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					this.DefaultPrpertyField("FPSMode", "Mode");
				}
			}

			EditorGUILayout.Space();
			this.DefaultPrpertyField("outline", "VGF Feature: Outline");
			var outline = this.serializedObject.FindProperty("outline");
			using (new EditorGUI.DisabledScope(outline.hasMultipleDifferentValues || !outline.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					this.DefaultPrpertyField("outlineMode", "Mode");
				}
			}

			EditorGUILayout.Space();
			this.DefaultPrpertyField("infinityWarDecimation", "VGF Feature: Infinity War Decimation");

			EditorGUILayout.Space();
			this.DefaultPrpertyField("pcw", "VGF Feature: Poly ColorWave");
			var pcw = this.serializedObject.FindProperty("pcw");
			using (new EditorGUI.DisabledScope(pcw.hasMultipleDifferentValues || !pcw.boolValue)) {
				using (new EditorGUI.IndentLevelScope()) {
					this.DefaultPrpertyField("pcwMode", "Mode");
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