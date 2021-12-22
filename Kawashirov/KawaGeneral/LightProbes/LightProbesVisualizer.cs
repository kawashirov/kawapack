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
using System;

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
					if (EditorApplication.isPlaying) {
						UnityEngine.Object.Destroy(p);
					} else {
						UnityEngine.Object.DestroyImmediate(p);
					}
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

		private static int[] TetrahedralizatonIndicies;
		private static Vector3[] TetrahedralizatonPositions;
		private static int[] TetrahedralizatonIndiciesOrder;

		public static void ResetTetrahedralization() {
			TetrahedralizatonIndicies = null;
			TetrahedralizatonPositions = null;
			TetrahedralizatonIndiciesOrder = null;
		}

		public static void Tetrahedralize() {
			var lightProbes = LightmapSettings.lightProbes;
			if (!lightProbes) {
				TetrahedralizatonIndicies = new int[0];
				TetrahedralizatonPositions = new Vector3[0];
				TetrahedralizatonIndiciesOrder = new int[0];
				return;
			}
			Lightmapping.Tetrahedralize(lightProbes.positions, out TetrahedralizatonIndicies, out TetrahedralizatonPositions);
			if (TetrahedralizatonIndiciesOrder == null || TetrahedralizatonIndiciesOrder.Length != TetrahedralizatonIndicies.Length / 4) {
				TetrahedralizatonIndiciesOrder = new int[TetrahedralizatonIndicies.Length / 4];
				for (var i = 0; i < TetrahedralizatonIndiciesOrder.Length; ++i)
					TetrahedralizatonIndiciesOrder[i] = i;
			}
		}

		public struct SolvedTetrahedron {
			public int tetrahedronIndex;
			public Vector3 corner1;
			public Vector3 corner2;
			public Vector3 corner3;
			public Vector3 corner4;
			public Plane plane1;
			public Plane plane2;
			public Plane plane3;
			public Plane plane4;
			public Vector3 surface1;
			public Vector3 surface2;
			public Vector3 surface3;
			public Vector3 surface4;

			public void MakePlanes() {
				plane1 = new Plane(corner2, corner3, corner4);
				plane2 = new Plane(corner1, corner3, corner4);
				plane3 = new Plane(corner1, corner2, corner4);
				plane4 = new Plane(corner1, corner2, corner3);
			}


			public static bool SameSideOrClose(Plane plane, Vector3 position, Vector3 reference) {
				var distancePosition = plane.GetDistanceToPoint(position);
				var distanceReference = plane.GetDistanceToPoint(reference);
				return Math.Sign(distancePosition) == Math.Sign(distanceReference) || Mathf.Abs(distancePosition) < Vector3.kEpsilon;
			}

			public bool IsInside(Vector3 position) =>
				SameSideOrClose(plane1, position, corner1) &&
				SameSideOrClose(plane2, position, corner2) &&
				SameSideOrClose(plane3, position, corner3) &&
				SameSideOrClose(plane4, position, corner4);

			public void MakeSurfacePoints(Vector3 position) {
				surface1 = plane1.ClosestPointOnPlane(position);
				surface2 = plane2.ClosestPointOnPlane(position);
				surface3 = plane3.ClosestPointOnPlane(position);
				surface4 = plane4.ClosestPointOnPlane(position);
			}

			public void Debug() {
				var corners = $"corner1={corner1}\ncorner1={corner2}\ncorner3={corner3}\ncorner4={corner4}";
				var planes = $"plane1={plane1}\nplane2={plane2}\nplane3={plane3}\nplane4={plane4}";
				var surfaces = $"surface1={surface1}\nsurface2={surface2}\nsurface3={surface2}\nsurface4={surface4}";
				UnityEngine.Debug.Log($"Tetrahedron: tetrahedronIndex={tetrahedronIndex}\n{corners}\n{planes}\n{surfaces}");
			}

		}

		public static SolvedTetrahedron TetrahedronIndex(Vector3 position) {
			var st = new SolvedTetrahedron();
			var orderIndex = 0;
			var solved = false;
			for (; orderIndex < TetrahedralizatonIndiciesOrder.Length; ++orderIndex) {
				st.tetrahedronIndex = TetrahedralizatonIndiciesOrder[orderIndex];
				st.corner1 = TetrahedralizatonPositions[TetrahedralizatonIndicies[st.tetrahedronIndex * 4 + 0]];
				st.corner2 = TetrahedralizatonPositions[TetrahedralizatonIndicies[st.tetrahedronIndex * 4 + 1]];
				st.corner3 = TetrahedralizatonPositions[TetrahedralizatonIndicies[st.tetrahedronIndex * 4 + 2]];
				st.corner4 = TetrahedralizatonPositions[TetrahedralizatonIndicies[st.tetrahedronIndex * 4 + 3]];

				st.MakePlanes();

				if (st.IsInside(position)) {
					st.MakeSurfacePoints(position);
					solved = true;
					break;
				}
			}

			if (!solved) {
				st.tetrahedronIndex = -1;
				return st;
			}

			if (st.tetrahedronIndex > 0) {
				// перенос индекса в начало
				Array.Copy(TetrahedralizatonIndiciesOrder, 0, TetrahedralizatonIndiciesOrder, 1, orderIndex);
				TetrahedralizatonIndiciesOrder[0] = st.tetrahedronIndex;
			}

			return st;
		}

		public static void DrawProbesSphereNow(Vector3 position, float scale) {
			var m1 = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * scale);
			LightProbes.GetInterpolatedProbe(position, null, out var sh);
			DrawProbesSphereNow(m1, sh);
		}

		public static bool DrawLightProbeTetrahedra(Vector3 position, float? sphereScale = null) {
			if (TetrahedralizatonIndiciesOrder == null || TetrahedralizatonIndicies == null || TetrahedralizatonPositions == null)
				Tetrahedralize();

			var st = TetrahedronIndex(position);

			// st.Debug();

			if (st.tetrahedronIndex < 0)
				return false;

			Gizmos.color = Color.yellow;

			Gizmos.DrawLine(st.corner1, st.corner2);
			Gizmos.DrawLine(st.corner1, st.corner3);
			Gizmos.DrawLine(st.corner1, st.corner4);
			Gizmos.DrawLine(st.corner2, st.corner3);
			Gizmos.DrawLine(st.corner2, st.corner4);
			Gizmos.DrawLine(st.corner3, st.corner4);

			Gizmos.DrawLine(st.surface1, position);
			Gizmos.DrawLine(st.surface2, position);
			Gizmos.DrawLine(st.surface3, position);
			Gizmos.DrawLine(st.surface4, position);

			if (sphereScale.HasValue) {
				DrawProbesSphereNow(st.corner1, sphereScale.Value);
				DrawProbesSphereNow(st.corner2, sphereScale.Value);
				DrawProbesSphereNow(st.corner3, sphereScale.Value);
				DrawProbesSphereNow(st.corner4, sphereScale.Value);
			}

			return true;
		}


	}
}
#endif // UNITY_EDITOR
