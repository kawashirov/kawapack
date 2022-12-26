using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Kawashirov;
using Kawashirov.ShaderBaking;

namespace Kawashirov.KawaShade {
	public enum PolyColorWaveMode { Classic, KawaColorfulWaves }

	internal static partial class KawaShadeCommons {
		internal static readonly string F_PCW = "KawaShade_Feature_PCW";
		internal static readonly string F_PCWMode = "KawaShade_Feature_PCWMode";
	}

	public partial class KawaShadeGenerator {
		public bool pcw = false;
		public PolyColorWaveMode pcwMode = PolyColorWaveMode.Classic;

		private void ConfigureFeaturePolyColorWave(ShaderSetup shader) {
			shader.TagBool(KawaShadeCommons.F_PCW, pcw);
			if (pcw) {
				needRandomVert = true;
				shader.Define("PCW_ON 1");
				shader.TagEnum(KawaShadeCommons.F_PCWMode, pcwMode);
				if (pcw) {
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
	}

	public partial class KawaShadeGeneratorEditor {
		private static readonly GUIContent gui_feature_pcw = new GUIContent("Poly ColorWave Feature");

		private void PolyColorWaveGUI() {
			using (new EditorGUI.DisabledScope(!complexity_VGF && !complexity_VHDGF)) {
				var pcw = serializedObject.FindProperty("pcw");
				KawaGUIUtility.ToggleLeft(pcw, gui_feature_pcw);
				using (new EditorGUI.DisabledScope(pcw.hasMultipleDifferentValues || !pcw.boolValue || (!complexity_VGF && !complexity_VHDGF))) {
					using (new EditorGUI.IndentLevelScope()) {
						KawaGUIUtility.DefaultPrpertyField(this, "pcwMode", "Mode");
					}
				}
			}
		}
	}

	internal partial class KawaShadeGUI {
		protected static double gcd(double a, double b) {
			return a < b ? gcd(b, a) : Math.Abs(b) < 0.001 ? a : gcd(b, a - (Math.Floor(a / b) * b));
		}

		private float? OnGUI_PolyColorWave_WvTmHelper(MaterialProperty _PCW_WvTmLo, MaterialProperty _PCW_WvTmAs, MaterialProperty _PCW_WvTmHi, MaterialProperty _PCW_WvTmDe) {
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

		private void OnGUI_PolyColorWave() {
			var _PCW_WvTmLo = FindProperty("_PCW_WvTmLo");
			var _PCW_WvTmAs = FindProperty("_PCW_WvTmAs");
			var _PCW_WvTmHi = FindProperty("_PCW_WvTmHi");
			var _PCW_WvTmDe = FindProperty("_PCW_WvTmDe");
			var _PCW_WvTmRnd = FindProperty("_PCW_WvTmRnd");
			var _PCW_WvTmUV = FindProperty("_PCW_WvTmUV");
			var _PCW_WvTmVtx = FindProperty("_PCW_WvTmVtx");

			var _PCW_Em = FindProperty("_PCW_Em");
			var _PCW_Color = FindProperty("_PCW_Color");
			var _PCW_RnbwTm = FindProperty("_PCW_RnbwTm");
			var _PCW_RnbwTmRnd = FindProperty("_PCW_RnbwTmRnd");
			var _PCW_RnbwStrtn = FindProperty("_PCW_RnbwStrtn");
			var _PCW_RnbwBrghtnss = FindProperty("_PCW_RnbwBrghtnss");
			var _PCW_Mix = FindProperty("_PCW_Mix");

			var f_PCW = shaderTags[KawaShadeCommons.F_PCW].IsTrue();
			using (new EditorGUI.DisabledScope(!f_PCW)) {
				EditorGUILayout.LabelField("Poly Color Wave Feature", f_PCW ? "Enabled" : "Disabled");
				using (new EditorGUI.IndentLevelScope()) {
					if (f_PCW) {
						LabelShaderTagEnumValue<PolyColorWaveMode>(KawaShadeCommons.F_PCWMode, "Mode", "Unknown");

						EditorGUILayout.LabelField("Wave timings:");
						float? time_period = null;
						using (new EditorGUI.IndentLevelScope()) {
							ShaderPropertyDisabled(_PCW_WvTmLo, "Hidden");
							ShaderPropertyDisabled(_PCW_WvTmAs, "Fade-in");
							ShaderPropertyDisabled(_PCW_WvTmHi, "Shown");
							ShaderPropertyDisabled(_PCW_WvTmDe, "Fade-out");
							time_period = OnGUI_PolyColorWave_WvTmHelper(_PCW_WvTmLo, _PCW_WvTmAs, _PCW_WvTmHi, _PCW_WvTmDe);

							ShaderPropertyDisabled(_PCW_WvTmRnd, "Random per tris");

							EditorGUILayout.LabelField("Time offset from UV0 (XY) and UV1 (ZW):");
							ShaderPropertyDisabled(_PCW_WvTmUV, "");

							EditorGUILayout.LabelField("Time offset from mesh-space coords: ");
							ShaderPropertyDisabled(_PCW_WvTmVtx, "");
						}

						EditorGUILayout.LabelField("Wave coloring:");
						using (new EditorGUI.IndentLevelScope()) {
							ShaderPropertyDisabled(_PCW_Em, "Emissiveness");
							ShaderPropertyDisabled(_PCW_Color, "Color");
							ShaderPropertyDisabled(_PCW_RnbwTm, "Rainbow time");
							ShaderPropertyDisabled(_PCW_RnbwTmRnd, "Rainbow time random");
							ShaderPropertyDisabled(_PCW_RnbwStrtn, "Rainbow saturation");
							ShaderPropertyDisabled(_PCW_RnbwBrghtnss, "Rainbow brightness");
							ShaderPropertyDisabled(_PCW_Mix, "Color vs. Rainbow");
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
}
