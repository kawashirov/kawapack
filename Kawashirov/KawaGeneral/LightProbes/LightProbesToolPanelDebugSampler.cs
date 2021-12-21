﻿#if UNITY_EDITOR
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
		private static GUIContent label_color = new GUIContent("Color");
		private static GUIContent label_avg_color = new GUIContent("Average Color");
		private static GUIContent label_as_color = new GUIContent("As Color");
		private static GUIContent analysisLabelLines = new GUIContent("Lines");
		private static GUIContent analysisLabelSpheres = new GUIContent("Spheres");

		public float gizmoSpheresSize = 0.05f;
		public float gizmoDirectionsSize = 0.2f;

		public struct SampleState {
			private static readonly Color MinColor = new Color(float.MinValue, float.MinValue, float.MinValue, 1f);
			private static readonly Color MaxColor = new Color(float.MaxValue, float.MaxValue, float.MaxValue, 1f);
			private const int evaluateN = 10;
			private static Vector3[] evaluateDirections = new Vector3[evaluateN];
			private static Color[] evaluateColor = new Color[evaluateN];

			public SphericalHarmonicsL2 probe;
			public Vector3 rndDirection;
			public Color colorMin;
			public float componentMin;
			public Vector3 directionMin;
			public Color colorMax;
			public float componentMax;
			public Vector3 directionMax;
			public Color colorAvg;
			public long samples;

			public void Reset() {
				componentMin = float.MaxValue;
				componentMax = float.MinValue;
				colorMin = MaxColor;
				colorMax = MinColor;
				samples = 0;
			}

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

			public void Sample() {
				for (var i = 0; i < evaluateDirections.Length; ++i)
					evaluateDirections[i] = rndDirection = UnityEngine.Random.onUnitSphere;
				probe.Evaluate(evaluateDirections, evaluateColor);
				for (var i = 0; i < evaluateColor.Length; ++i)
					ApplyEvaluated(evaluateDirections[i], evaluateColor[i]);
			}
		}

		[NonSerialized] public SampleState sample;

		public override void DrawGizmos() {
			var proxy = ToolsWindow.ValidateProxy();
			var transform = proxy.transform;
			var position = transform.position;

			var position_min = position + sample.directionMin * gizmoDirectionsSize;
			var position_max = position + sample.directionMax * gizmoDirectionsSize;
			var position_rnd = position + sample.rndDirection * gizmoDirectionsSize * 0.2f;

			Color color;

			color = sample.colorAvg;
			color.a = 1.0f;
			Gizmos.color = color;
			Gizmos.DrawSphere(position, gizmoSpheresSize);

			color = sample.colorMin;
			color.a = 1.0f;
			Gizmos.color = color;
			Gizmos.DrawSphere(position_min, gizmoSpheresSize);

			color = sample.colorMax;
			color.a = 1.0f;
			Gizmos.color = color;
			Gizmos.DrawSphere(position_max, gizmoSpheresSize);

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
			var position = transform.position;

			if (transform.hasChanged) {
				LightProbes.GetInterpolatedProbe(position, null, out sample.probe);
				transform.hasChanged = false;
				sample.Reset();
			}

			sample.Sample();
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

		public override void ToolsGUI() {
			EditorGUILayout.LabelField("Gizmo sizes:");
			using (new EditorGUI.IndentLevelScope(1)) {
				gizmoSpheresSize = EditorGUILayout.FloatField(analysisLabelSpheres, gizmoSpheresSize);
				gizmoDirectionsSize = EditorGUILayout.FloatField(analysisLabelLines, gizmoDirectionsSize);
			}

			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Minimal (Darkest) side:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				EditorGUILayout.ColorField(label_color, sample.colorMin, false, false, true);
				{
					var guiColor = GUI.color;
					if (sample.componentMin < 0)
						GUI.color = Color.red;
					EditorGUILayout.FloatField("Min Component", sample.componentMin);
					//	ToolsGUI_HSVLine(sample.colorMin);
					GUI.color = guiColor;
				}
				ToolsGUI_Vector3Fix("Direction", sample.directionMin);
				EditorGUILayout.FloatField("Of Maximum", sample.componentMin / sample.componentMax);
			}
			EditorGUILayout.LabelField("Maximum (Brightest) side:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				EditorGUILayout.ColorField(label_color, sample.colorMax, false, false, true);
				{
					var guiColor = GUI.color;
					if (sample.colorMax.r < 0 || sample.colorMax.g < 0 || sample.colorMax.b < 0)
						GUI.color = Color.red;
					EditorGUILayout.FloatField("Max Component", sample.componentMax);
					//	ToolsGUI_HSVLine(sample.colorMax);
					GUI.color = guiColor;
				}
				ToolsGUI_Vector3Fix("Direction", sample.directionMax);
				EditorGUILayout.FloatField("Of Minimum", sample.componentMax / sample.componentMin);
			}

			EditorGUILayout.Space();

			EditorGUILayout.ColorField(label_avg_color, sample.colorAvg, false, false, true);

			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Direction probes:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				ToolsGUI_Vector3Fix("Last", sample.rndDirection);
				EditorGUILayout.FloatField("Total", sample.samples);
			}

			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Current Sampled SphericalHarmonicsL2:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				var probe = sample.probe;
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
			{
				var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2);
				var rects = rect.RectSplitHorisontal(1, 1).ToArray();

				if (GUI.Button(rects[0], "Focus on Gizmo"))
					ToolsWindow.ValidateProxy().Focus();

				if (GUI.Button(rects[1], "Reset"))
					sample.Reset();
			}
		}
	}
}
#endif // UNITY_EDITOR
