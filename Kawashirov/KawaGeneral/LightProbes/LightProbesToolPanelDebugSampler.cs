#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.SceneManagement;
using UnityEditor;
using Kawashirov;
using Kawashirov.ToolsGUI;

namespace Kawashirov.LightProbesTools {

	[ToolsWindowPanel("Light Probes/Sample")]
	public partial class LightProbesToolPanelDebugSampler : AbstractToolPanel {
		private const int evaluateN = 10;
		private static readonly GUIContent label_color = new GUIContent("Color");
		private static readonly GUIContent label_avg_color = new GUIContent("Average Color");
		private static readonly Color MinColor = new Color(float.MinValue, float.MinValue, float.MinValue, 1f);
		private static readonly Color MaxColor = new Color(float.MaxValue, float.MaxValue, float.MaxValue, 1f);

		public bool displayFancy = false;
		public float displaySize = 0.1f;
		public bool displayTetrahedron = true;

		[NonSerialized] public Vector3? position;
		[NonSerialized] public SphericalHarmonicsL2 probe;
		[NonSerialized] public Color colorMin;
		[NonSerialized] public float componentMin;
		[NonSerialized] public Vector3 directionMin;
		[NonSerialized] public Color colorMax;
		[NonSerialized] public float componentMax;
		[NonSerialized] public Vector3 directionMax;
		[NonSerialized] public Color colorAvg;
		[NonSerialized] public long samples;
		[NonSerialized] private Vector3[] evaluateDirections = new Vector3[evaluateN];
		[NonSerialized] private Color[] evaluateColor = new Color[evaluateN];

		private void ApplyEvaluated(Vector3 direction, Color color) {
			var cMin = Mathf.Min(color.r, color.g, color.b);
			var cMax = Mathf.Max(color.r, color.g, color.b);

			if (cMin < componentMin) {
				componentMin = cMin;
				colorMin = color;
				directionMin = direction;
				colorAvg = colorMin * 0.5f + colorMax * 0.5f;
			}
			if (cMax > componentMax) {
				componentMax = cMax;
				colorMax = color;
				directionMax = direction;
				colorAvg = colorMin * 0.5f + colorMax * 0.5f;
			}
			++samples;
		}

		public void Evaluate() {
			for (var i = evaluateDirections.Length - 1; i >= 0; --i)
				evaluateDirections[i] = UnityEngine.Random.onUnitSphere;
			probe.Evaluate(evaluateDirections, evaluateColor);
			for (var i = 0; i < evaluateColor.Length; ++i)
				ApplyEvaluated(evaluateDirections[i], evaluateColor[i]);
		}

		public override void DrawGizmos() {
			var proxy = ToolsWindow.ValidateProxy();
			var transform = proxy.transform;
			var position = transform.position;

			var position_min = position + directionMin * displaySize * 3 / 2;
			var position_max = position + directionMax * displaySize * 3 / 2;
			var position_rnd = position + evaluateDirections[0] * displaySize;

			Color color = colorAvg;
			color.a = 1.0f;
			Gizmos.color = color;

			// LightProbesVisualizer.Tetrahedralize();

			var fancy = displayFancy && LightProbesVisualizer.Prepare();
			if (fancy) {
				var matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * displaySize);
				LightProbesVisualizer.DrawProbesSphereNow(matrix, probe);
				if (displayTetrahedron) {
					LightProbesVisualizer.DrawLightProbeTetrahedra(position, displaySize / 2);
				}
			} else {
				Gizmos.DrawSphere(position, displaySize / 2);
				if (displayTetrahedron) {
					LightProbesVisualizer.DrawLightProbeTetrahedra(position);
				}
			}

			color = colorMin;
			color.a = 1.0f;
			Gizmos.color = color;
			Gizmos.DrawSphere(position_min, displaySize / 4);

			color = colorMax;
			color.a = 1.0f;
			Gizmos.color = color;
			Gizmos.DrawSphere(position_max, displaySize / 4);

			Gizmos.color = Color.green;
			Gizmos.DrawLine(position, position_rnd);

			Gizmos.color = Color.blue;
			Gizmos.DrawLine(position, position_min);

			Gizmos.color = Color.red;
			Gizmos.DrawLine(position, position_max);
		}

		public override void Update() {
			var proxy = ToolsWindow.ValidateProxy();
			var transform = proxy.transform;

			if (transform.hasChanged) {
				transform.hasChanged = false;
				position = null;
			}

			if (!position.HasValue) {
				position = transform.position;
				LightProbes.GetInterpolatedProbe(position.Value, null, out probe);
				componentMin = float.MaxValue;
				componentMax = float.MinValue;
				colorMin = MaxColor;
				colorMax = MinColor;
				samples = 0;
			}

			Evaluate();
		}

		private static string[,] probe_data_names = new string[,]
		{
			{ "0", "L0+0", "DC" },
			{ "1", "L1-1", "y" }, {"2", "L1+0", "z" }, {"3", "L1+1", "x" },
			{ "4", "L2-2", "xy" }, {"5", "L2-1", "yz" }, {"6", "L2+0", "zz" }, {"7", "L2+1", "xz" }, {"8", "L2+2", "xx-yy" },
		};

		public void ToolsGUI_Vector3Fix(string label, Vector3 vector) {
			var rect = EditorGUILayout.GetControlRect();
			rect = EditorGUI.PrefixLabel(rect, new GUIContent(label));
			using (new KawaGUIUtility.ZeroIndentScope()) {
				EditorGUI.Vector3Field(rect, GUIContent.none, vector);
			}
		}

		public void ToolsGUI_Vector2Fix(string label, Vector2 vector) {
			var rect = EditorGUILayout.GetControlRect();
			rect = EditorGUI.PrefixLabel(rect, new GUIContent(label));
			using (new KawaGUIUtility.ZeroIndentScope()) {
				EditorGUI.Vector2Field(rect, GUIContent.none, vector);
			}
		}

		public void ToolsGUI_HSVLine(Color sampleColor) {
			var HSV = Vector2.zero;
			Color.RGBToHSV(sampleColor, out HSV.x, out HSV.y, out _);
			ToolsGUI_Vector2Fix("Hue Sat", HSV);
		}

		// private static GUIContent analysisLabelSpheres = new GUIContent("Spheres");

		public override GUIContent GetMenuButtonContent() {
			var image = EditorGUIUtility.IconContent("LightProbes Icon").image;
			var guiContent = new GUIContent("Sample Light Probes", image);
			return guiContent;
		}

		public override void ToolsGUI() {

			displaySize = EditorGUILayout.Slider("Gizmo size", displaySize, 0.01f, 1);
			displayFancy = EditorGUILayout.ToggleLeft("Fancy Gizmo (Work in Progress)", displayFancy);
			displayTetrahedron = EditorGUILayout.ToggleLeft("Show Light Probes Tetrahedron (Work in Progress)", displayTetrahedron);

			EditorGUILayout.Space();

			{
				var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2);
				var rects = rect.RectSplitHorisontal(1, 1).ToArray();

				if (GUI.Button(rects[0], "Focus on Gizmo")) {
					ToolsWindow.ValidateProxy().Focus();
				}

				if (GUI.Button(rects[1], "Reset")) {
					position = null;
				}
			}

			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Minimal (Darkest) side (Read-only):", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				EditorGUILayout.ColorField(label_color, colorMin, false, false, true);
				{
					var guiColor = GUI.color;
					if (componentMin < 0)
						GUI.color = Color.red;
					var rect = EditorGUILayout.GetControlRect();
					rect = EditorGUI.PrefixLabel(rect, new GUIContent("Min Component"));
					EditorGUI.SelectableLabel(rect, $"{componentMin}");
					//	ToolsGUI_HSVLine(sample.colorMin);
					GUI.color = guiColor;
				}
				ToolsGUI_Vector3Fix("Direction", directionMin);
				{
					var rect = EditorGUILayout.GetControlRect();
					rect = EditorGUI.PrefixLabel(rect, new GUIContent("Of Maximum"));
					EditorGUI.SelectableLabel(rect, $"{componentMin / componentMax}");
				}
			}
			EditorGUILayout.LabelField("Maximum (Brightest) side (Read-only):", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				EditorGUILayout.ColorField(label_color, colorMax, false, false, true);
				{
					var guiColor = GUI.color;
					if (colorMax.r < 0 || colorMax.g < 0 || colorMax.b < 0)
						GUI.color = Color.red;
					var rect = EditorGUILayout.GetControlRect();
					rect = EditorGUI.PrefixLabel(rect, new GUIContent("Max Component"));
					EditorGUI.SelectableLabel(rect, $"{componentMax}");
					//	ToolsGUI_HSVLine(sample.colorMax);
					GUI.color = guiColor;
				}
				ToolsGUI_Vector3Fix("Direction", directionMax);
				{
					var rect = EditorGUILayout.GetControlRect();
					rect = EditorGUI.PrefixLabel(rect, new GUIContent("Of Minimum"));
					EditorGUI.SelectableLabel(rect, $"{componentMax / componentMin}");
				}
			}

			EditorGUILayout.Space();

			EditorGUILayout.ColorField(label_avg_color, colorAvg, false, false, true);

			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Sampling status (Read-only):", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				ToolsGUI_Vector3Fix("Last Direction", evaluateDirections[0]);
				var rect = EditorGUILayout.GetControlRect();
				rect = EditorGUI.PrefixLabel(rect, new GUIContent("Total Samples"));
				EditorGUI.SelectableLabel(rect, $"{samples}");
			}

			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Current Sampled SphericalHarmonicsL2:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				var as_color = Color.white;
				for (var i = 0; i < 9; ++i) {
					Vector3 values;
					as_color.r = values.x = probe[0, i];
					as_color.g = values.y = probe[1, i];
					as_color.b = values.z = probe[2, i];
					var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
					var rects = rect.RectSplitHorisontal(2, 3, 3, 16, 4).ToArray();
					using (new KawaGUIUtility.ZeroIndentScope()) {
						EditorGUI.LabelField(rects[0], probe_data_names[i, 0]);
						EditorGUI.LabelField(rects[1], probe_data_names[i, 1]);
						EditorGUI.LabelField(rects[2], probe_data_names[i, 2]);
						EditorGUI.Vector3Field(rects[3], GUIContent.none, values);
						EditorGUI.ColorField(rects[4], GUIContent.none, as_color, false, false, true);
					}
				}
			}

		}
	}
}
#endif // UNITY_EDITOR
