using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {

	public class FeaturePolyColorWave : AbstractFeature {
		internal static readonly string F_PCW = "KawaShade_Feature_PCW";
		internal static readonly string F_PCWMode = "KawaShade_Feature_PCWMode";
		internal static readonly GUIContent gui_feature_pcw = new GUIContent("Poly ColorWave Feature");

		public enum PolyColorWaveMode { Classic, KawaColorfulWaves }

		public override int GetOrder() => (int)Order.VGF;

		public override void PopulateShaderTags(List<string> tags) {
			tags.Add(F_PCW);
			tags.Add(F_PCWMode);
		}

		public override void ConfigureShader(KawaShadeGenerator generator, ShaderSetup shader) {
			IncludeFeatureDirect(shader, "FeaturePolyColorWave.hlsl");

			shader.TagBool(F_PCW, generator.pcw);
			if (generator.pcw) {
				generator.needRandomVert = true;
				shader.Define("PCW_ON 1");
				shader.TagEnum(F_PCWMode, generator.pcwMode);
				if (generator.pcw) {
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmLo", defualt = 4 });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmAs", defualt = 0.25f });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmHi", defualt = 0.5f });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmDe", defualt = 0.25f });
					shader.properties.Add(new PropertyVector() { name = "_PCW_WvTmUV", defualt = new Vector4(0, 10, 0, 0) });
					shader.properties.Add(new PropertyVector() { name = "_PCW_WvTmVtx", defualt = new Vector4(0, 10, 0, 0) });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_WvTmRnd", defualt = 5 });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_Em", defualt = 0.5f, range = new Vector2(0, 1), power = 2 });
					shader.properties.Add(new PropertyColor() { name = "_PCW_Color", defualt = Color.white });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_RnbwTm", defualt = 0.5f });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_RnbwTmRnd", defualt = 0.5f });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_RnbwStrtn", defualt = 1, range = new Vector2(0, 1) });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_RnbwBrghtnss", defualt = 0.5f, range = new Vector2(0, 1) });
					shader.properties.Add(new PropertyFloat() { name = "_PCW_Mix", defualt = 0.5f, range = new Vector2(0, 1) });
				}
			} else {
				shader.Define("PCW_OFF 1");
			}
		}

		public override void GeneratorEditorGUI(KawaShadeGeneratorEditor editor) {
			using (new EditorGUI.DisabledScope(!editor.complexity_VGF && !editor.complexity_VHDGF)) {
				var pcw = editor.serializedObject.FindProperty("pcw");
				KawaGUIUtility.ToggleLeft(pcw, gui_feature_pcw);
				using (new EditorGUI.DisabledScope(pcw.hasMultipleDifferentValues || !pcw.boolValue || (!editor.complexity_VGF && !editor.complexity_VHDGF))) {
					using (new EditorGUI.IndentLevelScope()) {
						KawaGUIUtility.DefaultPrpertyField(editor, "pcwMode", "Mode");
					}
				}
			}
		}

		protected static double gcd(double a, double b) {
			return a < b ? gcd(b, a) : Math.Abs(b) < 0.001 ? a : gcd(b, a - (Math.Floor(a / b) * b));
		}

		private float? WvTmHelper(MaterialProperty _PCW_WvTmLo, MaterialProperty _PCW_WvTmAs, MaterialProperty _PCW_WvTmHi, MaterialProperty _PCW_WvTmDe) {
			if (
				_PCW_WvTmLo != null && !_PCW_WvTmLo.hasMixedValue &&
				_PCW_WvTmAs != null && !_PCW_WvTmAs.hasMixedValue &&
				_PCW_WvTmHi != null && !_PCW_WvTmHi.hasMixedValue &&
				_PCW_WvTmDe != null && !_PCW_WvTmDe.hasMixedValue
			) {
				var t0 = 0.0f;
				var t1 = t0 + _PCW_WvTmLo.floatValue;
				var t2 = t1 + _PCW_WvTmAs.floatValue;
				var t3 = t2 + _PCW_WvTmHi.floatValue;
				var t4 = t3 + _PCW_WvTmDe.floatValue;

				var time_curve = new AnimationCurve();
				time_curve.AddKey(t0, 0);
				time_curve.AddKey(t1, 0);
				time_curve.AddKey(t2, 1);
				time_curve.AddKey(t3, 1);
				time_curve.AddKey(t4, 0);
				for (var i = 0; i < time_curve.keys.Length; ++i) {
					AnimationUtility.SetKeyLeftTangentMode(time_curve, i, AnimationUtility.TangentMode.Linear);
					AnimationUtility.SetKeyRightTangentMode(time_curve, i, AnimationUtility.TangentMode.Linear);
				}
				EditorGUILayout.CurveField("Preview amplitude (read-only)", time_curve);
				KawaGUIUtility.HelpBoxRich(string.Format("Time for singe wave cycle: <b>{0:f}</b> sec. ", t4));

				return t4;
			} else {
				return null;
			}
		}

		public override void ShaderEditorGUI(KawaShadeGUI editor) {
			var _PCW_WvTmLo = editor.FindProperty("_PCW_WvTmLo");
			var _PCW_WvTmAs = editor.FindProperty("_PCW_WvTmAs");
			var _PCW_WvTmHi = editor.FindProperty("_PCW_WvTmHi");
			var _PCW_WvTmDe = editor.FindProperty("_PCW_WvTmDe");
			var _PCW_WvTmRnd = editor.FindProperty("_PCW_WvTmRnd");
			var _PCW_WvTmUV = editor.FindProperty("_PCW_WvTmUV");
			var _PCW_WvTmVtx = editor.FindProperty("_PCW_WvTmVtx");

			var _PCW_Em = editor.FindProperty("_PCW_Em");
			var _PCW_Color = editor.FindProperty("_PCW_Color");
			var _PCW_RnbwTm = editor.FindProperty("_PCW_RnbwTm");
			var _PCW_RnbwTmRnd = editor.FindProperty("_PCW_RnbwTmRnd");
			var _PCW_RnbwStrtn = editor.FindProperty("_PCW_RnbwStrtn");
			var _PCW_RnbwBrghtnss = editor.FindProperty("_PCW_RnbwBrghtnss");
			var _PCW_Mix = editor.FindProperty("_PCW_Mix");

			var f_PCW = KawaUtilities.AnyNotNull(
				_PCW_WvTmLo, _PCW_WvTmAs, _PCW_WvTmHi, _PCW_WvTmDe, _PCW_WvTmRnd, _PCW_WvTmUV, _PCW_WvTmVtx,
				_PCW_Em, _PCW_Color, _PCW_RnbwTm, _PCW_RnbwTmRnd, _PCW_RnbwStrtn, _PCW_RnbwBrghtnss, _PCW_Mix
			);
			using (new EditorGUI.DisabledScope(!f_PCW)) {
				EditorGUILayout.LabelField("Poly Color Wave Feature", f_PCW ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_PCW) {
						editor.LabelShaderTagEnumValue<PolyColorWaveMode>(F_PCWMode, "Mode", "Unknown");

						EditorGUILayout.LabelField("Wave timings:");
						float? time_period = null;
						using (new EditorGUI.IndentLevelScope()) {
							editor.ShaderPropertyDisabled(_PCW_WvTmLo, "Hidden");
							editor.ShaderPropertyDisabled(_PCW_WvTmAs, "Fade-in");
							editor.ShaderPropertyDisabled(_PCW_WvTmHi, "Shown");
							editor.ShaderPropertyDisabled(_PCW_WvTmDe, "Fade-out");
							time_period = WvTmHelper(_PCW_WvTmLo, _PCW_WvTmAs, _PCW_WvTmHi, _PCW_WvTmDe);

							editor.ShaderPropertyDisabled(_PCW_WvTmRnd, "Random per tris");

							EditorGUILayout.LabelField("Time offset from UV0 (XY) and UV1 (ZW):");
							editor.ShaderPropertyDisabled(_PCW_WvTmUV, "");

							EditorGUILayout.LabelField("Time offset from mesh-space coords: ");
							editor.ShaderPropertyDisabled(_PCW_WvTmVtx, "");
						}

						EditorGUILayout.LabelField("Wave coloring:");
						using (new EditorGUI.IndentLevelScope()) {
							editor.ShaderPropertyDisabled(_PCW_Em, "Emissiveness");
							editor.ShaderPropertyDisabled(_PCW_Color, "Color");
							editor.ShaderPropertyDisabled(_PCW_RnbwTm, "Rainbow time");
							editor.ShaderPropertyDisabled(_PCW_RnbwTmRnd, "Rainbow time random");
							editor.ShaderPropertyDisabled(_PCW_RnbwStrtn, "Rainbow saturation");
							editor.ShaderPropertyDisabled(_PCW_RnbwBrghtnss, "Rainbow brightness");
							editor.ShaderPropertyDisabled(_PCW_Mix, "Color vs. Rainbow");
						}
						if (time_period.HasValue && _PCW_RnbwTm != null && !_PCW_RnbwTm.hasMixedValue) {
							var time_rainbow = _PCW_RnbwTm.floatValue;
							var gcd_t = gcd(time_rainbow, time_period.Value);
							var lcm_t = time_rainbow * time_period / gcd_t;
							KawaGUIUtility.HelpBoxRich(string.Format(
								"Period of the wave <b>{0:f1}</b> sec. and period of Rainbow <b>{1:f1}</b> sec. produces total cycle of ~<b>{2:f1}</b> sec. (GCD: ~<b>{3:f}</b>)",
								time_period, time_rainbow, lcm_t, gcd_t
							));
						}
					}
				}
			}
		}
	}

	public partial class KawaShadeGenerator {
		public bool pcw = false;
		public FeaturePolyColorWave.PolyColorWaveMode pcwMode = FeaturePolyColorWave.PolyColorWaveMode.Classic;
	}
}
