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

	[ToolsWindowPanel("Light Probes/Search Dark")]
	public class LightProbesToolPanelSearchDark : AbstractToolPanel {
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

		private static Vector3[] mustTestDirections = GenerateMustTestDirections(3).Distinct().ToArray();
		private static Vector3[] analysisRndDirection;
		private static Color[] analysisRndColors;

		public int directionSamples = 1000;
		public float darkThreshold = 0.05f; // [0, 1]
		public int topProbes = 10;
		public bool topProbesFold = false;

		public bool fixTooDarkReRunAnalysis = true;
		public float fixTooDarkPrecision = 0.01f;
		public float fixTooDarkDirectionalVsAmbient = 0.5f;

		[NonSerialized] public List<ProbeMetadata> allProbes;

		[NonSerialized] public Vector2 darkProbesRange;
		[NonSerialized] public List<ProbeMetadata> darkProbes;

		public float displaySize = 0.1f;
		public bool displayFancy = false;
		public Vector2 displaySelectionRange = new Vector2(-10, 0);
		public Vector2 displayMapRange = new Vector2(-1, 2);
		[NonSerialized] public List<ProbeMetadata> displayProbes;
		[NonSerialized] public float displayComponentMinMin;
		[NonSerialized] public float displayComponentMinMax;

		private static IEnumerable<Vector3> GenerateMustTestDirections(int step = 1) {
			// Направления, которые обязателно нужно исследовать.
			// На самом деле по диагоналям плотность намного больше, чем по осям.
			// Но это ок. Т.к. например для L1 часто достаточно 6 единичных векторов по осям.
			// Но с L2 не так все очевидно и в комбинаци с L0 и L1 минимумы и максимумы могут быть на диагоналях.
			for (var x = -step; x <= step; ++x) {
				for (var y = -step; y <= step; ++y) {
					for (var z = -step; z <= step; ++z) {
						if (x == 0 && z == 0 && y == 0)
							continue;
						yield return new Vector3(x, y, z).normalized;
					}
				}
			}
		}

		public bool ShouldDrawFancy() => displayFancy && LightProbesVisualizer.Prepare();

		public override bool ShouldCallSceneGUIDrawMesh(SceneView sceneView) => displayFancy;

		public override void DrawGizmos() {
			if (displayProbes == null || displayProbes.Count < 1)
				return;

			var noFancy = !ShouldDrawFancy();

			foreach (var data in displayProbes) {
				var factor = Mathf.InverseLerp(displayComponentMinMax, displayComponentMinMin, data.componentMin);
				var base_size = Mathf.Lerp(0.1f, 1.0f, factor) * displaySize;
				Gizmos.color = Color.Lerp(Color.yellow, Color.red, factor);
				if (noFancy) {
					Gizmos.DrawWireSphere(data.position, base_size / 2);
				}
				Gizmos.DrawLine(data.position, data.position + data.directionMin * base_size * 2);
			}
		}

		public override void OnSceneGUI(SceneView sceneView) {
			if (displayProbes == null || displayProbes.Count < 1)
				return;
			if (Event.current.type != EventType.Repaint || !sceneView.camera)
				return;
			if (!ShouldDrawFancy())
				return;
			// Debug.Log($"displayMapRange = {displayMapRange}");
			foreach (var data in displayProbes) {
				var pos = sceneView.camera.WorldToViewportPoint(data.position);
				if (pos.x < 0 || pos.x > 1 || pos.y < 0 || pos.y > 1)
					continue; // Not visible by camera
				var factor = Mathf.InverseLerp(displayComponentMinMax, displayComponentMinMin, data.componentMin);
				var base_size = Mathf.Lerp(0.1f, 1, factor) * displaySize;
				var matrix = Matrix4x4.TRS(data.position, Quaternion.identity, Vector3.one * base_size);
				LightProbesVisualizer.DrawProbesSphereNow(matrix, data.probe, displayMapRange);
			}

		}

		private void AnalyzeLightProbePrepareArrays() {
			// TODO 
			directionSamples = mustTestDirections.Length;

			if (analysisRndDirection == null || analysisRndDirection.Length != directionSamples) {
				analysisRndDirection = new Vector3[directionSamples];
			}
			if (analysisRndColors == null || analysisRndColors.Length != analysisRndDirection.Length) {
				analysisRndColors = new Color[analysisRndDirection.Length];
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

			if (allProbes == null) {
				allProbes = new List<ProbeMetadata>(positions.Length);
			} else {
				allProbes.Clear();
				allProbes.Capacity = Mathf.Max(allProbes.Capacity, positions.Length);
			}

			if (darkProbes == null) {
				darkProbes = new List<ProbeMetadata>(positions.Length / 4);
			} else {
				darkProbes.Clear();
				darkProbes.Capacity = Mathf.Max(allProbes.Capacity, positions.Length / 4);
			}

			displayProbes?.Clear();

			float time = -1;
			for (var i = 0; i < analysisTotalProbes; ++i) {
				if (time < Time.realtimeSinceStartup) {
					time = Time.realtimeSinceStartup + 1f;
					if (EditorUtility.DisplayCancelableProgressBar("Analyzing Light Probes...", $"{i}/{positions.Length}...", 1.0f * (i + 1) / (positions.Length + 1)))
						break;
				}
				var data = AnalyzeLightProbeSingle(i, positions[i], bakedProbes[i]);
				allProbes.Add(data);
				if (data.componentMin < data.componentMax * darkThreshold) {
					darkProbes.Add(data);
				}
			}
			EditorUtility.DisplayProgressBar("Analyzing Light Probes...", "Sorting...", 1.0f);
			if (darkProbes.Count > 0) {
				darkProbes.Sort((a, b) => Comparer<float>.Default.Compare(a.componentMin, b.componentMin));
				darkProbesRange.x = darkProbes.First().componentMin;
				darkProbesRange.y = darkProbes.Last().componentMin;
			} else {
				darkProbesRange.x = float.MaxValue;
				darkProbesRange.y = float.MinValue;
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
			return displaySelectionRange.x < data.componentMax && data.componentMin < displaySelectionRange.y;
		}

		public void UpdateDisplayProbes() {
			if (displayProbes == null) {
				displayProbes = new List<ProbeMetadata>(darkProbes.Where(RangeSelector));
			} else {
				displayProbes.Clear();
				displayProbes.AddRange(darkProbes.Where(RangeSelector));
			}
			if (displayProbes.Count > 1) {
				displayComponentMinMin = displayProbes.First().componentMin;
				displayComponentMinMax = displayProbes.Last().componentMin;
			}
			SceneView.RepaintAll();
		}

		public static void SplitSH(SphericalHarmonicsL2 input, out SphericalHarmonicsL2 ambient, out SphericalHarmonicsL2 amplitude) {
			ambient = new SphericalHarmonicsL2();
			amplitude = new SphericalHarmonicsL2();
			for (var rgb = 0; rgb < 3; ++rgb) {
				ambient[rgb, 0] = input[rgb, 0];
				for (var i = 1; i < 9; ++i)
					amplitude[rgb, i] = input[rgb, i];
			}
		}

		public SphericalHarmonicsL2 FixDarkSingle_Iterative2(ProbeMetadata data) {
			// А ЭТА ХУИТА УЖЕ РАБОТАЕТ

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

			if (data.componentMin >= data.componentMax * darkThreshold) {
				// Все ОК
				return data.probe;
			}

			SplitSH(data.probe, out var originalAmbientSH, out var originalDirectionalSH);

			if (darkThreshold >= 1) {
				// Только Ambient
				return originalAmbientSH;
			}

			var scale = 1.0f;
			var protection = 10_000;
			while (true) {
				// Комбинируем части исходной пробы
				var upscaledAmbientProbe = originalAmbientSH * scale + originalDirectionalSH;
				var downscaleDirectionalProbe = originalAmbientSH + originalDirectionalSH * (1.0f / scale);
				var fixedProbe = upscaledAmbientProbe * fixTooDarkDirectionalVsAmbient + downscaleDirectionalProbe * (1.0f - fixTooDarkDirectionalVsAmbient);
				// Проверяем что получилось
				--protection;
				var fixedData = AnalyzeLightProbeSingle(data.index, data.position, fixedProbe, false);
				if (fixedData.componentMin >= fixedData.componentMax * darkThreshold || protection < 0) {
					// Если условие на darkThreshold выполнено или достигли защиты от цикла, то значит пофикшено.
					return fixedProbe;
				}
				// Если не исправлено, меняем combination.
				scale *= 1.0f + fixTooDarkPrecision;
			}
		}

		public SphericalHarmonicsL2 FixDarkSingle_Iterative(ProbeMetadata data, bool reanalyze = true) {
			// ЭТА ХУИТА ТОЖЕ НЕ РАБОТАЕТ

			if (data.samples == null || data.samples.Length != analysisRndDirection.Length || reanalyze)
				data = AnalyzeLightProbeSingle(data.index, data.position, data.probe, true);

			var modAmbientMn = Mathf.Max(data.componentMax * darkThreshold, data.componentMin);

			var fixedProbe = new SphericalHarmonicsL2();
			fixedProbe.Clear();
			for (var i = 0; i < data.samples.Length; ++i) {
				var sample = data.samples[i];

				// Семплы, в которых усилен Ambient так,
				// что колебания Directional не дадут свету стать темнее analysisDarkThreshold
				var modAmbientSample = LightProbesUtility.MapColor(data.componentMin, data.componentMax, modAmbientMn, data.componentMax, sample);

				// Проба, в которой ослаблен Directional  так,
				// что колебания Directional не дадут свету стать темнее analysisDarkThreshold
				// TODO
				var modDirectionalSample = sample;

				var modCombinedSample = modAmbientSample * fixTooDarkDirectionalVsAmbient + modDirectionalSample * (1 - fixTooDarkDirectionalVsAmbient);
				fixedProbe.AddDirectionalLight(analysisRndDirection[i], modCombinedSample / analysisRndDirection.Length, 1);
			}
			return fixedProbe;
		}

		public SphericalHarmonicsL2 FixDarkSingle_Analytical(ProbeMetadata data) {
			// ЭТА ХУИТА НЕ РАБОТАЕТ

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

			if (data.componentMin >= data.componentMax * darkThreshold) {
				// Все ОК
				return data.probe;
			}

			// Разделение пробы на только Ambient и только Directional
			//var originalAmbientSH = new SphericalHarmonicsL2();
			//originalAmbientSH.Clear();
			//originalAmbientSH.AddAmbientLight(data.colorAmbient);
			//var originalDirectionalSH = data.probe + originalAmbientSH * -1;
			SplitSH(data.probe, out var originalAmbientSH, out var originalDirectionalSH);

			if (darkThreshold >= 1) {
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
			var modDirectionalScale = originalScaleAmplitude * (darkThreshold - 1) / (darkThreshold + 1) / (data.componentMin - originalScaleAmplitude);
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
			var modAmbientComponentMin = originalScaleAmplitude * 2 * darkThreshold / (1 - darkThreshold);
			var modAmbientComponentMax = originalScaleAmplitude + modAmbientComponentMin;
			// Далее нужно понять, на что умножать Ambient
			// По-сути среднее между целевым componentMin и componentMax делим на иходную
			var modAmbientScale = (modAmbientComponentMin + modAmbientComponentMax) / (data.componentMin + data.componentMax);
			// Проба с сильным Ambient
			var modAmbientSH = originalAmbientSH * modAmbientScale + originalDirectionalSH;

			// Комбинируем
			return modAmbientSH * fixTooDarkDirectionalVsAmbient + modDirectionalSH * (1 - fixTooDarkDirectionalVsAmbient);
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

			var shFix = FixDarkSingle_Iterative2(data);
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
			try {
				FixDarkApplyInternal();
			} finally {
				EditorUtility.ClearProgressBar();
			}
		}

		private void FixDarkApplyInternal() {
			// Probes must be re-analyzed before fix
			if (fixTooDarkReRunAnalysis) {
				AnalyzeLightProbes();
			}

			Debug.Log("Applying Dark LightProbes fix...");
			EditorUtility.DisplayProgressBar("Applying Dark LightProbes fix...", "Preparing...", 0f);
			var bakedProbes = LightmapSettings.lightProbes.bakedProbes;
			var newBakedProbes = new SphericalHarmonicsL2[bakedProbes.Length];
			Array.Copy(bakedProbes, newBakedProbes, bakedProbes.Length);
			for (var i = 0; i < darkProbes.Count; ++i) {
				if (EditorUtility.DisplayCancelableProgressBar("Applying LightProbes fix...", $"{i + 1}/{darkProbes.Count}", (i + 1.0f) / (darkProbes.Count + 1.0f))) {
					Debug.LogWarning($"Applying Dark LightProbes fix cancelled by user at {i}!");
					return;
				}
				var data = darkProbes[i];
				if (newBakedProbes[data.index] != data.probe) {
					throw new InvalidOperationException($"Probe #{data.index} mismatch between LightmapSettings.lightProbes.bakedProbes and analysisDisplayProbes!");
				}
				newBakedProbes[data.index] = FixDarkSingle_Iterative2(data);
			}
			EditorUtility.DisplayProgressBar("Applying Dark LightProbes fix...", "Saving changes...", 1f);

			LightProbesUtility.RegisterLightingUndo("Fix Dark LightProbes");
			LightmapSettings.lightProbes.bakedProbes = newBakedProbes;
			LightProbesUtility.SetLightingDirty();
			Debug.Log($"Applied LightProbes fix!");

			if (fixTooDarkReRunAnalysis) {
				AnalyzeLightProbes();
				UpdateDisplayProbes();
			} else {
				allProbes.Clear();
				darkProbes.Clear();
				displayProbes.Clear();
			}
		}

		private const string ToolsGUI_Analysis_Info =
			"Light Probes in which minimum brightness is less than this threshold " +
			"multiplied by maximum brightness are considered as \"dark\" light probes." +
			"\n\n" +
			"Dark probes can be fixed below.";

		private readonly GUIContent ToolsGUI_Analysis_TotalProbesAnalyzed = new GUIContent("Total Probes Analyzed");
		private readonly GUIContent ToolsGUI_Analysis_TotalDarkProbes = new GUIContent("Total Dark Probes");

		private void ToolsGUI_Analysis() {
			EditorGUILayout.LabelField("Light Probes Analysis:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				if (allProbes == null || allProbes.Count < 0) {
					EditorGUILayout.HelpBox("Light Probes not yet analyzed or there is no Light Probes on active scene.", MessageType.Warning);
				} else {
					{
						var rect = EditorGUILayout.GetControlRect();
						rect = EditorGUI.PrefixLabel(rect, ToolsGUI_Analysis_TotalProbesAnalyzed);
						EditorGUI.SelectableLabel(rect, $"{allProbes.Count}");
					}
					{
						var rect = EditorGUILayout.GetControlRect();
						rect = EditorGUI.PrefixLabel(rect, ToolsGUI_Analysis_TotalDarkProbes);
						EditorGUI.SelectableLabel(rect, $"{darkProbes?.Count ?? 0} / {allProbes.Count}");
					}
				}
				darkThreshold = EditorGUILayout.Slider("Dark Threshold", darkThreshold, 0f, 1f);
				using (new EditorGUI.IndentLevelScope(1)) {
					EditorGUILayout.HelpBox(ToolsGUI_Analysis_Info, MessageType.Info);
				}
				var buttonRect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2));
				if (GUI.Button(buttonRect, "Analyze Light Probes")) {
					AnalyzeLightProbes();
				}
			}
		}

		private void ToolsGUI_HighlightDarkProbes_TopDarkest() {
			var n = Mathf.Min(topProbes, displayProbes?.Count ?? 0);
			topProbes = EditorGUILayout.IntField("List Most Darkest Probes", topProbes);
			topProbesFold = EditorGUILayout.Foldout(topProbesFold, $"{n} Darkest Probes:");
			if (!topProbesFold)
				return;
			using (new EditorGUI.IndentLevelScope(1)) {
				if (n < 1) {
					EditorGUILayout.LabelField("Nothing to display.");
					return;
				}
				{
					// Title
					var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
					var rects = rect.RectSplitHorisontal(1, 3, 5, 2).ToArray();
					using (new KawaGUIUtility.ZeroIndentScope()) {
						EditorGUI.LabelField(rects[0], "№");
						EditorGUI.LabelField(rects[1], "Min. Component");
						EditorGUI.LabelField(rects[2], "Position");
						EditorGUI.LabelField(rects[3], "Move");
					}
				}
				for (var i = 0; i < n; ++i) {
					var probeData = displayProbes[i];
					var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
					var rects = rect.RectSplitHorisontal(1, 3, 5, 2).ToArray();
					using (new KawaGUIUtility.ZeroIndentScope()) {
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
		}

		private void ToolsGUI_HighlightDarkProbes_Gizmos() {
			using (var check = new EditorGUI.ChangeCheckScope()) {
				displaySize = EditorGUILayout.FloatField("Gizmo size", displaySize);
				displayFancy = EditorGUILayout.ToggleLeft("Fancy Gizmos (Work in Progress: Laggy & Buggy)", displayFancy);
				using (new EditorGUI.IndentLevelScope(1))
				using (new EditorGUI.DisabledScope(!displayFancy)) {
					var hasDisplay = displayProbes != null && displayProbes.Count > 0;
					if (hasDisplay) {
						var min = Mathf.Min(displayComponentMinMin, 0);
						var max = Mathf.Max(displayComponentMinMax, 1);
						{
							var rect = EditorGUILayout.GetControlRect(false);
							rect = EditorGUI.PrefixLabel(rect, new GUIContent("Dynamic Range"));
							var rects = rect.RectSplitHorisontal(4, 1, 1).ToArray();
							using (new KawaGUIUtility.ZeroIndentScope()) {
								EditorGUI.MinMaxSlider(rects[0], ref displayMapRange.x, ref displayMapRange.y, min, max);
								displayMapRange.x = EditorGUI.FloatField(rects[1], displayMapRange.x);
								displayMapRange.y = EditorGUI.FloatField(rects[2], displayMapRange.y);
								displayMapRange.x = Mathf.Clamp(displayMapRange.x, min, max);
								displayMapRange.y = Mathf.Clamp(displayMapRange.y, min, max);
							}
						}
						{
							var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false));
							if (GUI.Button(rect, "Normal 0..1 Range")) {
								displayMapRange.x = 0;
								displayMapRange.y = 1;
							}
						}
						//EditorGUILayout.MinMaxSlider("Dynamic Range", ref displayMapRange.x, ref displayMapRange.y, -10, 20);
					} else {
						EditorGUILayout.LabelField("Dynamic Range", "No Probes");
					}
				}
				if (check.changed) {
					SceneView.RepaintAll();
				}
			}
		}

		private const string ToolsGUI_HighlightDarkProbes_Info =
			"This feature shows darkest light probes as spheres and it's darkest side as line. " +
			"Useful for searching baking issues. Larger gizmos means darker probes.";
		private readonly GUIContent ToolsGUI_HighlightDarkProbes_CurrentlyHighlighted = new GUIContent("Currently Highlighted");
		private readonly GUIContent ToolsGUI_HighlightDarkProbes_ProbeColorMinMax = new GUIContent("Range of Darkest Values");

		private void ToolsGUI_HighlightDarkProbes() {
			EditorGUILayout.LabelField("Highlight Dark Light Probes on Scene:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				EditorGUILayout.HelpBox(ToolsGUI_HighlightDarkProbes_Info, MessageType.Info);
				if (darkProbes == null || darkProbes.Count < 1) {
					EditorGUILayout.HelpBox("Light Probes not yet analyzed or there is no Dark Light Probes on active scene.", MessageType.Info);
					return;
				}
				{
					var rect = EditorGUILayout.GetControlRect();
					rect = EditorGUI.PrefixLabel(rect, ToolsGUI_HighlightDarkProbes_CurrentlyHighlighted);
					EditorGUI.SelectableLabel(rect, $"{displayProbes?.Count ?? 0} / {darkProbes?.Count ?? 0}");
				}
				{
					var rect = EditorGUILayout.GetControlRect();
					rect = EditorGUI.PrefixLabel(rect, ToolsGUI_HighlightDarkProbes_ProbeColorMinMax);
					EditorGUI.SelectableLabel(rect, $"{darkProbesRange.x} .. {darkProbesRange.y}");
				}
				{
					var rect = EditorGUILayout.GetControlRect(false);
					EditorGUI.MinMaxSlider(rect, ref displaySelectionRange.x, ref displaySelectionRange.y, darkProbesRange.x, darkProbesRange.y);
				}
				{
					var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false));
					using (new KawaGUIUtility.ZeroIndentScope()) {
						var rects = rect.RectSplitHorisontal(1, 1, 1, 1).ToArray();
						EditorGUI.SelectableLabel(rects[0], $"{darkProbesRange.x}");
						displaySelectionRange.x = EditorGUI.DelayedFloatField(rects[1], GUIContent.none, displaySelectionRange.x);
						displaySelectionRange.y = EditorGUI.DelayedFloatField(rects[2], GUIContent.none, displaySelectionRange.y);
						EditorGUI.SelectableLabel(rects[3], $"{darkProbesRange.y}");
					}
				}
				displaySelectionRange.x = Mathf.Clamp(displaySelectionRange.x, darkProbesRange.x, darkProbesRange.y);
				displaySelectionRange.y = Mathf.Clamp(displaySelectionRange.y, darkProbesRange.x, darkProbesRange.y);
				displaySelectionRange.x = Mathf.Min(displaySelectionRange.x, displaySelectionRange.y);
				displaySelectionRange.y = Mathf.Max(displaySelectionRange.x, displaySelectionRange.y);
				{
					var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2));
					var rects = rect.RectSplitHorisontal(5, 5, 5, 5).ToArray();
					if (GUI.Button(rects[0], "Update Filter")) {
						UpdateDisplayProbes();
					}
					if (GUI.Button(rects[1], "Hide All")) {
						displayProbes.Clear();
					}
					if (GUI.Button(rects[2], "Show All")) {
						displaySelectionRange.x = darkProbesRange.x;
						displaySelectionRange.y = darkProbesRange.y;
						UpdateDisplayProbes();
					}
					if (GUI.Button(rects[3], "Show All Negative")) {
						displaySelectionRange.x = darkProbesRange.x;
						displaySelectionRange.y = Mathf.Min(darkProbesRange.y, 0);
						UpdateDisplayProbes();
					}
				}

				ToolsGUI_HighlightDarkProbes_Gizmos();
				ToolsGUI_HighlightDarkProbes_TopDarkest();
			}
		}

		private const string ToolsGUI_FixDarkProbes_Info_DownscaleDirectional =
			"Scale down Directional coefficients. Preserves energy and exposure, but looses contrast. Can be too flat and smooth. ";
		private const string ToolsGUI_FixDarkProbes_Info_UpscaleAmbient =
			"Scale up Ambient coefficients. Preserves contrast, but not looses energy balance. Can cause major over-exposure.";

		private void ToolsGUI_FixDarkProbes() {
			EditorGUILayout.LabelField("Fix Dark Light Probes:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				if (darkProbes == null || darkProbes.Count < 1) {
					EditorGUILayout.HelpBox("Light Probes not yet analyzed or there is no Dark Light Probes on active scene.", MessageType.Info);
					return;
				}

				EditorGUILayout.LabelField("There is two ways to fix too dark light probes:");
				{
					var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 4));
					var rects = rect.RectSplitHorisontal(6, 1, 6).ToArray();
					using (new KawaGUIUtility.ZeroIndentScope()) {
						EditorGUI.LabelField(rects[0], ToolsGUI_FixDarkProbes_Info_DownscaleDirectional, KawaGUIUtility.GetMultilineLabel());
						EditorGUI.LabelField(rects[1], "vs", EditorStyles.label);
						EditorGUI.LabelField(rects[2], ToolsGUI_FixDarkProbes_Info_UpscaleAmbient, KawaGUIUtility.GetMultilineLabel());
					}
				}
				EditorGUILayout.LabelField("Combine these two variants:");
				{
					var percentDownDirectional = Mathf.RoundToInt((1 - fixTooDarkDirectionalVsAmbient) * 100);
					var percentUpAmbient = Mathf.RoundToInt(fixTooDarkDirectionalVsAmbient * 100);
					EditorGUILayout.LabelField($"Downscale Directional ({percentDownDirectional}%) vs Upscale Ambient ({percentUpAmbient}%)");
					var rect = EditorGUILayout.GetControlRect(false);
					fixTooDarkDirectionalVsAmbient = EditorGUI.Slider(rect, fixTooDarkDirectionalVsAmbient, 0, 1);
				}

				fixTooDarkPrecision = EditorGUILayout.Slider("Precision", fixTooDarkPrecision, 0.001f, 0.1f);

				fixTooDarkReRunAnalysis = EditorGUILayout.ToggleLeft("Auto Re-run Analysis", fixTooDarkReRunAnalysis);

				EditorGUILayout.LabelField("Don't forget to back up LightProbes asset! Undo (Ctrl+Z) does NOT work here!", EditorStyles.boldLabel);

				{
					var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2));
					if (GUI.Button(rect, "Apply Fix")) {
						FixDarkApply();
					}
				}

				//{
				//	var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2));
				//	if (GUI.Button(rect, "Test Fix")) {
				//		FixDarkTest();
				//	}
				//}

			}
		}

		public override void ToolsGUI() {
			ToolsGUI_Analysis();
			EditorGUILayout.Space();
			ToolsGUI_HighlightDarkProbes();
			EditorGUILayout.Space();
			ToolsGUI_FixDarkProbes();
		}
	}
}
#endif // UNITY_EDITOR
