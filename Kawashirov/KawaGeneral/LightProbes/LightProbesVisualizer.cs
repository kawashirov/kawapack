#if UNITY_EDITOR
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
	static class LightProbesVisualizer {

		private static Mesh sphereMesh;
		private static Material visMaterial;
		private static Shader visShader;
		private static int[] SHProperties;
		private static int? RangePropery = null;

		public static bool Prepare() {

			if (sphereMesh == null) {
				// Init 
				Debug.Log($"Loading FancyGizmosMesh...");
				var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				try {
					sphereMesh = p.GetComponent<MeshFilter>()?.sharedMesh;
					Debug.Log($"FancyGizmosMesh = {sphereMesh}");
				} finally {
					if (EditorApplication.isPlaying)
						Object.DestroyImmediate(p);
					else
						Object.Destroy(p);
				}
				if (sphereMesh == null) {
					Debug.LogWarning("Can not get FancyGizmosMesh...");
					return false;
				}
			}

			if (visShader == null) {
				var path = AssetDatabase.GUIDToAssetPath("e3d8323a2de95ce42bdca1c77cc510ee");
				if (string.IsNullOrWhiteSpace(path)) {
					Debug.LogWarning("Can not find KawaProbesVisualizer...");
					return false;
				}
				visShader = AssetDatabase.LoadAssetAtPath<Shader>(path);
				if (visShader == null) {
					Debug.LogWarning("Can not load KawaProbesVisualizer...");
					return false;
				}
				Debug.Log($"FancyGizmosShader = {visShader}");
			}

			if (visMaterial == null) {
				visMaterial = new Material(visShader);
				Debug.Log($"FancyGizmosMaterial = {visMaterial}");
			} else if (visMaterial.shader != visShader) {
				visMaterial.shader = visShader;
			}

			if (SHProperties == null) {
				SHProperties = new int[7];
				SHProperties[0] = Shader.PropertyToID("kawa_SHAr");
				SHProperties[1] = Shader.PropertyToID("kawa_SHAg");
				SHProperties[2] = Shader.PropertyToID("kawa_SHAb");
				SHProperties[3] = Shader.PropertyToID("kawa_SHBr");
				SHProperties[4] = Shader.PropertyToID("kawa_SHBg");
				SHProperties[5] = Shader.PropertyToID("kawa_SHBb");
				SHProperties[6] = Shader.PropertyToID("kawa_SHC");
			}

			if (!RangePropery.HasValue) {
				RangePropery = Shader.PropertyToID("kawa_Range");
			}

			return true;
		}

		public static void DrawProbesSphereNow(Matrix4x4 matrix, SphericalHarmonicsL2 sh, Vector2? dynamicRange = null) {
			for (var i = 0; i < 3; i++) {
				// Constant + Linear
				visMaterial.SetVector(SHProperties[i], new Vector4(sh[i, 3], sh[i, 1], sh[i, 2], sh[i, 0] - sh[i, 6]));
				// Quadratic polynomials
				visMaterial.SetVector(SHProperties[i + 3], new Vector4(sh[i, 4], sh[i, 6], sh[i, 5] * 3, sh[i, 7]));
			}
			// Final quadratic polynomial
			visMaterial.SetVector(SHProperties[6], new Vector4(sh[0, 8], sh[2, 8], sh[1, 8], 1));

			var rangeVector = new Vector4(0, 1, 0, 0);
			if (dynamicRange.HasValue) {
				rangeVector.x = dynamicRange.Value.x;
				rangeVector.y = dynamicRange.Value.y;
			}
			visMaterial.SetVector(RangePropery.Value, rangeVector);

			if (visMaterial.SetPass(0)) {
				Graphics.DrawMeshNow(sphereMesh, matrix, 0);
			}
		}

	}
}
#endif // UNITY_EDITOR
