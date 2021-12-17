#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using UnityEditor;
using Kawashirov;
using Kawashirov.ToolsGUI;

namespace Kawashirov.LightProbesTools {

	[ToolsWindowPanel("Light Probes/Search Negative")]
	public class LightProbesToolPanelSearchNegative : AbstractToolPanel {
		public struct ProbeMetadata {
			public int index;
			public Vector3 position;
			public SphericalHarmonicsL2 probe;
			//
			public Color colorMin;
			public float componentMin;
			public Vector3 directionMin;
			public Vector3 directionMinPosition;
			//
			public Color colorMax;
			public float componentMax;
			public Vector3 directionMax;
			public Vector3 directionMaxPosition;
			//
			public Color colorAmbient;
			public float componentAmbient;
			//
			public Color[] samples; // match analysisRndDirection
		}

		private static GUIContent analysisLabelSamples = new GUIContent("Number of dir samples");
		private static GUIContent analysisLabelSpheres = new GUIContent("Spheres");
		private static GUIContent analysisLabelLines = new GUIContent("Lines");
		private static GUIContent analysisLabelListDarkest = new GUIContent("List Most Darkest Probes");

		private static Vector3[] mustTestDirections = GenerateMustTestDirections().Distinct().ToArray();
		private static Vector3[] analysisRndDirection = new Vector3[1024];
		private static Color[] analysisRndColors = new Color[1024];

		public int analysisDirectionSamples = 1000;
		public float analysisDarkThreshold = 0.05f; // [0, 1]
		public float analysisGizmoSpheresSize = 0.05f;
		public float analysisGizmoDirectionsSize = 0.2f;
		public int analysisTopProbes = 10;
		public bool analysisTopProbesFold = false;

		public bool analysisFixTooDarkReRunAnalysis = true;
		public float analysisFixTooDarkDirectionalVsAmbient = 0.5f;

		[NonSerialized] public List<ProbeMetadata> analysisAllProbes;
		public Vector2 analysisProbeColorMinMax;
		[NonSerialized] public List<ProbeMetadata> analysisDarkProbes;

		public Vector2 analysisDisplayRange;
		[NonSerialized] public List<ProbeMetadata> analysisDisplayProbes;

		private static IEnumerable<Vector3> GenerateMustTestDirections() {
			// Направления, которые обязателно нужно исследовать.
			// На самом деле по диагоналям плотность намного больше, чем по осям.
			// Но это ок. Т.к. например для L1 часто достаточно 6 единичных векторов по осям.
			// Но с L2 не так все очевидно и в комбинаци с L0 и L1 минимумы и максимумы могут быть на диагоналях.
			for (var x = -3; x <= 3; ++x) {
				for (var y = -3; y <= 3; ++y) {
					for (var z = -3; z <= 3; ++z) {
						if (x == 0 && z == 0 && y == 0)
							continue;
						yield return new Vector3(x, y, z).normalized;
					}
				}
			}
		}

		public void DrawGizmosSelected_SearchNegative() {
			if (analysisDisplayProbes == null || analysisDisplayProbes.Count < 1)
				return;

			var componentMinMin = analysisDisplayProbes.First().componentMin;
			var componentMinMax = analysisDisplayProbes.Last().componentMin;

			foreach (var data in analysisDisplayProbes) {
				var base_size = 0.1f + 0.9f * Mathf.Abs(data.componentMin);
				Gizmos.color = Color.Lerp(Color.red, Color.yellow, Mathf.InverseLerp(componentMinMin, componentMinMax, data.componentMin));
				Gizmos.DrawWireSphere(data.position, base_size * analysisGizmoSpheresSize);
				Gizmos.DrawLine(data.position, data.position + data.directionMin * base_size * analysisGizmoDirectionsSize);
			}
		}

		private void AnalyzeLightProbePrepareArrays() {
			if (analysisRndDirection.Length != analysisDirectionSamples) {
				analysisRndDirection = new Vector3[analysisDirectionSamples];
				analysisRndColors = new Color[analysisDirectionSamples];
			}
			for (var i = 0; i < analysisRndDirection.Length; ++i) {
				analysisRndDirection[i] = (i < mustTestDirections.Length) ? mustTestDirections[i] : UnityEngine.Random.onUnitSphere;
			}
		}

		private ProbeMetadata AnalyzeLightProbeSingle(int index, Vector3 position, SphericalHarmonicsL2 probe, bool keepSamples = false) {
			var data = new ProbeMetadata();
			data.index = index;
			data.position = position;
			data.probe = probe;
			data.colorMin = Color.white;
			data.componentMin = float.MaxValue;
			data.colorAmbient = Color.black;
			data.probe.Evaluate(analysisRndDirection, analysisRndColors);
			if (keepSamples) {
				data.samples = new Color[analysisRndColors.Length];
				Array.Copy(analysisRndColors, data.samples, analysisRndColors.Length);
			}
			for (var j = 0; j < analysisRndColors.Length; ++j) {
				var color = analysisRndColors[j];
				var cMin = Mathf.Min(color.r, color.g, color.b);
				var cMax = Mathf.Max(color.r, color.g, color.b);
				if (data.componentMin > cMin) {
					data.componentMin = cMin;
					data.colorMin = color;
					data.directionMin = analysisRndDirection[j];
					data.directionMinPosition = data.position + data.directionMin;
				}
				if (data.componentMax < cMax) {
					data.componentMax = cMax;
					data.colorMax = color;
					data.directionMax = analysisRndDirection[j];
					data.directionMaxPosition = data.position + data.directionMax;
				}
				data.colorAmbient += color / analysisRndColors.Length;
			}
			data.componentAmbient = data.colorAmbient.r / 3 + data.colorAmbient.g / 3 + data.colorAmbient.b / 3;
			return data;
		}

		private void AnalyzeLightProbesInternal() {
			AnalyzeLightProbePrepareArrays();

			var positions = LightmapSettings.lightProbes.positions;
			var bakedProbes = LightmapSettings.lightProbes.bakedProbes;

			var analysisTotalProbes = positions.Length;

			EditorUtility.DisplayProgressBar("Analyzing Light Probes...", "...", 1.0f / (analysisTotalProbes + 1));

			if (analysisAllProbes == null) {
				analysisAllProbes = new List<ProbeMetadata>(positions.Length);
			} else {
				analysisAllProbes.Clear();
				analysisAllProbes.Capacity = Mathf.Max(analysisAllProbes.Capacity, positions.Length);
			}

			if (analysisDarkProbes == null) {
				analysisDarkProbes = new List<ProbeMetadata>(positions.Length / 4);
			} else {
				analysisDarkProbes.Clear();
				analysisDarkProbes.Capacity = Mathf.Max(analysisAllProbes.Capacity, positions.Length / 4);
			}

			analysisDisplayProbes?.Clear();

			float time = -1;
			for (var i = 0; i < analysisTotalProbes; ++i) {
				if (time < Time.realtimeSinceStartup) {
					time = Time.realtimeSinceStartup + 1f;
					if (EditorUtility.DisplayCancelableProgressBar("Analyzing Light Probes...", $"{i}/{positions.Length}...", 1.0f * (i + 1) / (positions.Length + 1)))
						break;
				}
				var data = AnalyzeLightProbeSingle(i, positions[i], bakedProbes[i]);
				analysisAllProbes.Add(data);
				if (data.componentMin < data.componentMax * analysisDarkThreshold) {
					analysisDarkProbes.Add(data);
				}
			}
			EditorUtility.DisplayProgressBar("Analyzing Light Probes...", "Sorting...", 1.0f);
			if (analysisDarkProbes.Count > 0) {
				analysisDarkProbes.Sort((a, b) => Comparer<float>.Default.Compare(a.componentMin, b.componentMin));
				analysisProbeColorMinMax.x = analysisDarkProbes.First().componentMin;
				analysisProbeColorMinMax.y = analysisDarkProbes.Last().componentMin;
			} else {
				analysisProbeColorMinMax.x = float.MaxValue;
				analysisProbeColorMinMax.y = float.MinValue;
			}
		}

		public void AnalyzeLightProbes() {
			try {
				AnalyzeLightProbesInternal();
			} finally {
				EditorUtility.ClearProgressBar();
			}
		}

		private bool RangeSelector(ProbeMetadata data) {
			return analysisDisplayRange.x < data.componentMax && data.componentMin < analysisDisplayRange.y;
		}

		public void AnalyzeUpdateDisplayProbes() {
			if (analysisDisplayProbes == null) {
				analysisDisplayProbes = new List<ProbeMetadata>(analysisDarkProbes.Where(RangeSelector));
			} else {
				analysisDisplayProbes.Clear();
				analysisDisplayProbes.AddRange(analysisDarkProbes.Where(RangeSelector));
			}
		}

		public void SplitSH(SphericalHarmonicsL2 input, out SphericalHarmonicsL2 ambient, out SphericalHarmonicsL2 amplitude) {
			ambient = new SphericalHarmonicsL2();
			amplitude = new SphericalHarmonicsL2();
			for (var rgb = 0; rgb < 3; ++rgb) {
				ambient[rgb, 0] = input[rgb, 0];
				for (var i = 1; i < 9; ++i)
					amplitude[rgb, i] = input[rgb, i];
			}
		}


		private Color FixDarkSingle_Iterative_MapColor(float fromMn, float fromMx, float toMn, float toMx, Color color) {
			color.r = Mathf.Lerp(toMn, toMx, Mathf.InverseLerp(fromMn, fromMx, color.r));
			color.g = Mathf.Lerp(toMn, toMx, Mathf.InverseLerp(fromMn, fromMx, color.g));
			color.b = Mathf.Lerp(toMn, toMx, Mathf.InverseLerp(fromMn, fromMx, color.b));
			return color;
		}

		public SphericalHarmonicsL2 FixDarkSingle_Iterative(ProbeMetadata data, bool reanalyze = true) {
			if (data.samples == null || data.samples.Length != analysisRndDirection.Length || reanalyze)
				data = AnalyzeLightProbeSingle(data.index, data.position, data.probe, true);

			var fixedProbe = new SphericalHarmonicsL2();
			fixedProbe.Clear();
			for (var i = 0; i < data.samples.Length; ++i) {
				var sample = data.samples[i];

				// Семплы, в которых усилен Ambient так,
				// что колебания Directional не дадут свету стать темнее analysisDarkThreshold
				var modAmbientMn = Mathf.Max(data.componentMax * analysisDarkThreshold, data.componentMin);
				var modAmbientSample = FixDarkSingle_Iterative_MapColor(data.componentMin, data.componentMax, modAmbientMn, data.componentMax, sample);

				// Проба, в которой ослаблен Directional  так,
				// что колебания Directional не дадут свету стать темнее analysisDarkThreshold
				// TODO
				var modDirectionalSample = sample;

				var modCombinedSample = modAmbientSample * analysisFixTooDarkDirectionalVsAmbient + modDirectionalSample * (1 - analysisFixTooDarkDirectionalVsAmbient);
				fixedProbe.AddDirectionalLight(analysisRndDirection[i], modCombinedSample / analysisRndDirection.Length, 1);
			}
			return fixedProbe;
		}

		public SphericalHarmonicsL2 FixDarkSingle_Analytical(ProbeMetadata data) {
			// Исправляет слишком темный и/или отрицательный лайтпроб

			// Такое крайне маловероятно, но мы ничего не можем сделать, если:
			// - componentMin и componentMax равны, т.е. скорее всего Directional отсутствует или принебрежимо мал,
			//   тогда можно пологаться только на Ambient
			// - Среднее componentMin и componentMax <= 0, т.е. скорее всего Ambient отсутствует или отрицательный,
			//   тогда можно пологаться только на Directional
			var originalComponentAvg = data.componentMin * 0.5f + data.componentMax * 0.5f;

			var badSubZeroAmbient = originalComponentAvg <= Vector3.kEpsilon;
			if (badSubZeroAmbient) {
				// Плохой Ambient, Хуй знает как тут быть
				var modFixedSH = new SphericalHarmonicsL2();
				modFixedSH.Clear();
				return modFixedSH; // Black
			}

			var originalComponentDiff = data.componentMax - data.componentMin;
			var badFlatDirectional = originalComponentDiff <= Vector3.kEpsilon * 2.0f;
			if (badFlatDirectional) {
				// Плохой Directional
				// На самом деле это означает, что проба исправна, просто там только Ambient
				return data.probe;
			}

			if (data.componentMin >= data.componentMax * analysisDarkThreshold) {
				// Все ОК
				return data.probe;
			}

			// Разделение пробы на только Ambient и только Directional
			//var originalAmbientSH = new SphericalHarmonicsL2();
			//originalAmbientSH.Clear();
			//originalAmbientSH.AddAmbientLight(data.colorAmbient);
			//var originalDirectionalSH = data.probe + originalAmbientSH * -1;
			SplitSH(data.probe, out var originalAmbientSH, out var originalDirectionalSH);

			if (analysisDarkThreshold >= 1) {
				// Только Ambient
				return originalAmbientSH;
			}

			// Амплитуда Directional
			var originalScaleAmplitude = originalComponentDiff * 0.5f;

			// Проба, в которой ослаблен Directional  так,
			// что колебания Directional не дадут свету стать темнее analysisDarkThreshold
			// Сохраняется энергия, теряется контраст.
			// Нужно найти множитель Directional
			// Для этого нужно решить систему:
			//    modAmbientComponentMin = modAmbientComponentMax * analysisDarkThreshold
			//    (modAmbientComponentMin - originalScaleAmplitude) = (componentMin - originalScaleAmplitude) * modDirectionalScale
			//    (modAmbientComponentMax - originalScaleAmplitude) = (componentMax - originalScaleAmplitude) * modDirectionalScale
			// Решение относительно modDirectionalScale выходит следующее:
			var modDirectionalScale = originalScaleAmplitude * (analysisDarkThreshold - 1) / (analysisDarkThreshold + 1) / (data.componentMin - originalScaleAmplitude);
			// Проба со слабым Directional
			var modDirectionalSH = originalAmbientSH + originalDirectionalSH * modDirectionalScale;

			// Проба, в которой училен Ambient так,
			// что колебания Directional не дадут свету стать темнее analysisDarkThreshold
			// Для начла находим новые сomponentMin и сomponentMax
			// Для этого нужно решить систему:
			//    modAmbientComponentMin = modAmbientComponentMax * analysisDarkThreshold
			//    modAmbientComponentMin = componentMin + U
			//    modAmbientComponentMax = componentMax + U
			// где U - величина подъема, но она в явном виде нам не нужна.
			// Решение относительно modAmbientComponentMin и modAmbientComponentMax:
			var modAmbientComponentMin = originalScaleAmplitude * 2 * analysisDarkThreshold / (1 - analysisDarkThreshold);
			var modAmbientComponentMax = originalScaleAmplitude + modAmbientComponentMin;
			// Далее нужно понять, на что умножать Ambient
			// По-сути среднее между целевым componentMin и componentMax делим на иходную
			var modAmbientScale = (modAmbientComponentMin + modAmbientComponentMax) / (data.componentMin + data.componentMax);
			// Проба с сильным Ambient
			var modAmbientSH = originalAmbientSH * modAmbientScale + originalDirectionalSH;

			// Комбинируем
			return modAmbientSH * analysisFixTooDarkDirectionalVsAmbient + modDirectionalSH * (1 - analysisFixTooDarkDirectionalVsAmbient);
		}

		public string SHL2S(SphericalHarmonicsL2 probe) {
			var builder = new StringBuilder();
			builder.Append("SphericalHarmonicsL2{ ");
			for (var rgb = 0; rgb < 3; ++rgb) {
				for (var i = 0; i < 9; ++i) {
					builder.Append($"[{rgb},{i}]={probe[rgb, i]} ");
				}
			}
			builder.Append("}");
			return builder.ToString();
		}

		public void FixDarkTest() {
			var sh = new SphericalHarmonicsL2();
			sh.Clear();
			sh[0, 0] = sh[1, 0] = sh[2, 0] = 1;
			sh[0, 1] = sh[1, 1] = sh[2, 1] = -2;

			AnalyzeLightProbePrepareArrays();
			var data = AnalyzeLightProbeSingle(0, Vector3.zero, sh);

			Debug.Log($"FixDarkTest in: {SHL2S(sh)}");
			Debug.Log($"Data in: {data.componentMin}, {data.colorAmbient}, {data.componentMax}");

			var shAmbient = new SphericalHarmonicsL2();
			shAmbient.Clear();
			shAmbient.AddAmbientLight(data.colorAmbient);
			var shDir = data.probe + shAmbient * -1;

			Debug.Log($"Split test: {SHL2S(sh)} -> {SHL2S(shAmbient)} + {SHL2S(shDir)}");

			var shFix = FixDarkSingle_Iterative(data, true);
			var data2 = AnalyzeLightProbeSingle(0, Vector3.zero, shFix);

			Debug.Log($"FixDarkTest out: {SHL2S(shFix)}");
			Debug.Log($"Data out: {data2.componentMin}, {data2.colorAmbient}, {data2.componentMax}");

			var shTestDir = new SphericalHarmonicsL2();
			shTestDir.Clear();
			shTestDir.AddAmbientLight(Color.white);
			shTestDir.AddDirectionalLight(Vector3.right, Color.white * -1, 1);
			Debug.Log($"shTestDir: {SHL2S(shTestDir)}");

		}

		public void FixDarkApply() {
			// Probes must be re-analyzed before fix
			if (analysisFixTooDarkReRunAnalysis) {
				AnalyzeLightProbes();
			}

			var bakedProbes = LightmapSettings.lightProbes.bakedProbes;
			for (var i = 0; i < analysisDarkProbes.Count; ++i) {
				var data = analysisDarkProbes[i];
				if (bakedProbes[data.index] != data.probe) {
					throw new InvalidOperationException($"Probe #{data.index} mismatch between LightmapSettings.lightProbes.bakedProbes and analysisDisplayProbes!");
				}
				bakedProbes[data.index] = FixDarkSingle_Analytical(data);
			}
			LightmapSettings.lightProbes.bakedProbes = bakedProbes;

			if (analysisFixTooDarkReRunAnalysis) {
				AnalyzeLightProbes();
				AnalyzeUpdateDisplayProbes();
			} else {
				analysisAllProbes.Clear();
				analysisDarkProbes.Clear();
				analysisDisplayProbes.Clear();
			}
		}

		private void OnGUI_SearchNegative_Serialized() {
			analysisDirectionSamples = EditorGUILayout.IntField(analysisLabelSamples, analysisDirectionSamples);
			EditorGUILayout.LabelField("Gizmo sizes:");
			using (new EditorGUI.IndentLevelScope(1)) {
				analysisGizmoSpheresSize = EditorGUILayout.FloatField(analysisLabelSpheres, analysisGizmoSpheresSize);
				analysisGizmoDirectionsSize = EditorGUILayout.FloatField(analysisLabelLines, analysisGizmoDirectionsSize);
			}
			analysisTopProbes = EditorGUILayout.IntField(analysisLabelListDarkest, analysisTopProbes);
		}

		private void OnGUI_SearchNegative_Analysis_TopDarkest() {
			var n = Mathf.Min(analysisTopProbes, analysisDisplayProbes?.Count ?? 0);
			analysisTopProbes = EditorGUILayout.IntField("List Most Darkest Probes", analysisTopProbes);
			analysisTopProbesFold = EditorGUILayout.Foldout(analysisTopProbesFold, $"Most {n} Darkest Probes:");
			if (!analysisTopProbesFold)
				return;
			using (new EditorGUI.IndentLevelScope(1)) {
				if (n < 1) {
					EditorGUILayout.LabelField("Nothing to display.");
					return;
				}
				{
					// Title
					var rect = EditorGUILayout.GetControlRect();
					var rects = rect.RectSplitHorisontal(1, 3, 5, 1).ToArray();
					EditorGUI.LabelField(rects[0], "№");
					EditorGUI.LabelField(rects[1], "Min. Component");
					EditorGUI.LabelField(rects[2], "Position");
					EditorGUI.LabelField(rects[3], "Move");
				}
				for (var i = 0; i < n; ++i) {
					var probeData = analysisDisplayProbes[i];
					var rect = EditorGUILayout.GetControlRect();
					var rects = rect.RectSplitHorisontal(1, 3, 5, 1).ToArray();
					EditorGUI.LabelField(rects[0], $"№{i}");
					EditorGUI.FloatField(rects[1], probeData.componentMin);
					EditorGUI.Vector3Field(rects[2], GUIContent.none, probeData.position);
					if (GUI.Button(rects[3], "Move")) {
						var proxy = ToolsWindow.ValidateProxy();
						proxy.transform.position = probeData.position;
						proxy.Focus();
					}
				}
			}
		}

		private static string OnGUI_SearchNegative_Analysis_FixDarkest_Info = string.Join("\n", new string[] {
		"Basicaly, there is two ways to fix too dark light probes:",
		"",
		"- Scale down Directional coefficients",
		"  Preserves energy and exposure, but looses contrast. ",
		"  Can be too flat and smooth.",
		"",
		"- Scale up Ambient coefficient",
		"  Preserves contrast, but not energy balance.",
		"  Can cause major over-exposure.",
		"",
		"You can balance between these two methods.",
	});

		private void OnGUI_SearchNegative_Analysis_FixDarkest() {
			EditorGUILayout.LabelField("Total Dark Probes", $"{analysisDarkProbes.Count} ({100 * analysisDarkProbes.Count / analysisAllProbes.Count}%)");

			EditorGUILayout.LabelField("There is a way to fix it:");
			using (new EditorGUI.IndentLevelScope(1)) {

				EditorGUILayout.LabelField("Downscale Directional vs Upscale Ambient");
				using (new EditorGUI.IndentLevelScope(1)) {
					var rect = EditorGUILayout.GetControlRect(false);
					analysisFixTooDarkDirectionalVsAmbient = EditorGUI.Slider(rect, analysisFixTooDarkDirectionalVsAmbient, 0, 1);
					EditorGUILayout.HelpBox(OnGUI_SearchNegative_Analysis_FixDarkest_Info, MessageType.Info);
				}

				analysisFixTooDarkReRunAnalysis = EditorGUILayout.ToggleLeft("Auto Re-run Analysis", analysisFixTooDarkReRunAnalysis);

				var rectB = EditorGUILayout.GetControlRect(false);
				if (GUI.Button(rectB, "Apply Fix")) {
					FixDarkApply();
				}
				var rectC = EditorGUILayout.GetControlRect(false);
				if (GUI.Button(rectC, "Test Fix")) {
					FixDarkTest();
				}
				EditorGUILayout.HelpBox("Don't forget to back up LightProbes asset!", MessageType.Warning);
			}
		}

		private void OnGUI_SearchNegative_Analysis() {
			EditorGUILayout.IntField("Total Probes Analyzed", analysisAllProbes?.Count ?? 0);

			EditorGUILayout.LabelField("Highlight Probes on Scene:");
			using (new EditorGUI.IndentLevelScope(1)) {
				EditorGUILayout.IntField("Currently Highlighted Probes:", analysisDisplayProbes?.Count ?? 0);
				EditorGUILayout.FloatField("Darkest Value", analysisProbeColorMinMax.x);
				EditorGUILayout.FloatField("Brightest Value", analysisProbeColorMinMax.y);
				{
					var rect = EditorGUILayout.GetControlRect(false);
					EditorGUI.MinMaxSlider(rect, ref analysisDisplayRange.x, ref analysisDisplayRange.y, analysisProbeColorMinMax.x, analysisProbeColorMinMax.y);
				}
				{
					var rect = EditorGUILayout.GetControlRect(false);
					var rects = rect.RectSplitHorisontal(1, 1, 1, 1).ToArray();
					EditorGUI.FloatField(rects[0], analysisProbeColorMinMax.x);
					analysisDisplayRange.x = EditorGUI.DelayedFloatField(rects[1], analysisDisplayRange.x);
					analysisDisplayRange.y = EditorGUI.DelayedFloatField(rects[2], analysisDisplayRange.y);
					EditorGUI.FloatField(rects[3], analysisProbeColorMinMax.y);
				}
				{
					var rect = EditorGUILayout.GetControlRect(false);
					var rects = rect.RectSplitHorisontal(10, 1, 10).ToArray();
					if (GUI.Button(rects[0], "Update Filter")) {
						AnalyzeUpdateDisplayProbes();
					}
					if (GUI.Button(rects[2], "Hide")) {
						analysisDisplayProbes.Clear();
					}
				}

				EditorGUILayout.LabelField("Gizmo sizes:");
				using (new EditorGUI.IndentLevelScope(1)) {
					analysisGizmoDirectionsSize = EditorGUILayout.FloatField(analysisLabelLines, analysisGizmoDirectionsSize);
					analysisGizmoSpheresSize = EditorGUILayout.FloatField(analysisLabelSpheres, analysisGizmoSpheresSize);
				}

				OnGUI_SearchNegative_Analysis_TopDarkest();
			}

			EditorGUILayout.Space();

			OnGUI_SearchNegative_Analysis_FixDarkest();
		}

		public override void ToolsGUI() {
			OnGUI_SearchNegative_Serialized();
			EditorGUILayout.Space();

			if (GUILayout.Button("Analyze Light Probes")) {
				AnalyzeLightProbes();
			}

			EditorGUILayout.Space();

			if (analysisAllProbes == null || analysisAllProbes.Count < 1) {
				EditorGUILayout.LabelField("Light Probes Data Not Yet Analyzied");
			} else {
				OnGUI_SearchNegative_Analysis();
			}
		}
	}
}
#endif // UNITY_EDITOR
