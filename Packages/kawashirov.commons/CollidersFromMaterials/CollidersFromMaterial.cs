#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace Kawashirov.CollidersFromMaterials {
	[ExecuteAlways]
	public class CollidersFromMaterial : KawaEditorBehaviour {
		private static MeshColliderCookingOptions mesh_options =
			MeshColliderCookingOptions.CookForFasterSimulation |
			MeshColliderCookingOptions.EnableMeshCleaning |
			MeshColliderCookingOptions.WeldColocatedVertices;

		private static List<Bounds> bounds = new List<Bounds>();
		private static List<int> triangles = new List<int>();
		private static List<Vector3> vertices = new List<Vector3>();

		public enum ColliderType { None, Box, Convex, Mesh } // , Sphere
		public enum SelectType { MaterialAsset, MaterialName, ObjectName }

		public SelectType selectType = SelectType.MaterialAsset;
		public Material material = null;
		public string regularExpression = "";
		public bool ignoreCase = false;

		[Space]
		public bool TryMakeReadable = false;

		[Space]
		public bool applyLayer = false;
		[Layer] public int layer;
		public ColliderType colliderType = ColliderType.Mesh;

		[Header("Properties below are auto-generated")]
		public List<MeshRenderer> OriginalRenderers = new List<MeshRenderer>();
		public List<Collider> ConfiguredColliders = new List<Collider>();

		// 
		internal Regex regex;
		internal string regexPattern;

		public static void FreeBuffers() {
			bounds.Clear();
			triangles.Clear();
			vertices.Clear();
			bounds.Capacity = 1;
			triangles.Capacity = 1;
			vertices.Capacity = 1;
		}

		protected static C EnsureSingle<C>(GameObject gobj) where C : Component {
			C component = null;
			var components = new List<C>();
			gobj.GetComponents(components);
			if (components.Count > 0)
				component = components[0];
			if (components.Count > 1)
				for (var i = 1; i < components.Count; ++i)
					DestroyImmediate(components[i]);
			if (component == null)
				component = gobj.AddComponent<C>();
			return component;
		}

		protected static List<C> EnsureMultiple<C>(GameObject gobj, int count) where C : Component {
			var components = new List<C>(count + 1);
			gobj.GetComponents(components);
			if (components.Count > count) {
				for (var i = count; i < components.Count; ++i)
					DestroyImmediate(components[i]);
				components = components.GetRange(0, count);
			}
			while (components.Count < count)
				components.Add(gobj.AddComponent<C>());
			return components;
		}

		public void ClearOutputs() {
			OriginalRenderers.Clear();
			ConfiguredColliders.Clear();
		}

		public Regex GetRegex() {
			if (regex == null || !string.Equals(regexPattern, regularExpression)) {
				var options = RegexOptions.Compiled;
				if (ignoreCase)
					options |= RegexOptions.IgnoreCase;
				regexPattern = regularExpression;
				regex = new Regex(regexPattern, options);
			}
			return regex;
		}

		public bool IsNone() => colliderType == ColliderType.None;
		public bool IsAnyMesh() => colliderType == ColliderType.Mesh || colliderType == ColliderType.Convex;

		public MatchResult MatchMeshRenderer(MeshRenderer mr) {
			if (selectType == SelectType.MaterialAsset) {
				var mats = mr.sharedMaterials;
				if (mats == null || mats.Length < 1)
					return MatchResult.Miss;
				bool any_match = false, any_miss = false;
				foreach (var mat in mats)
					if (mat == material)
						any_match = true;
					else
						any_miss = true;
				return !any_match ? MatchResult.Miss : !any_miss ? MatchResult.Match : MatchResult.Partial;

			} else if (selectType == SelectType.MaterialName) {
				var mats = mr.sharedMaterials;
				if (mats == null || mats.Length < 1)
					return MatchResult.Miss;
				bool any_match = false, any_miss = false;
				foreach (var mat in mats)
					if (mat != null && GetRegex().IsMatch(mat.name))
						any_match = true;
					else
						any_miss = true;
				return !any_match ? MatchResult.Miss : !any_miss ? MatchResult.Match : MatchResult.Partial;

			} else if (selectType == SelectType.ObjectName) {
				return GetRegex().IsMatch(mr.gameObject.name) ? MatchResult.Match : MatchResult.Miss;
			}
			return MatchResult.Miss;
		}

		protected bool TryApplyMesh(MeshRenderer renderer) {
			if (!TryGetMesh(renderer, out var mesh, out var sub_count))
				return false;

			var collider = EnsureSingle<MeshCollider>(renderer.gameObject);
			collider.sharedMesh = mesh;

			if (!collider.enabled)
				collider.enabled = true;

			if (collider.isTrigger)
				collider.isTrigger = false;

			var is_convex = colliderType == ColliderType.Convex;
			if (collider.convex != is_convex)
				collider.convex = is_convex;

			if (collider.cookingOptions != mesh_options)
				collider.cookingOptions = mesh_options;

			foreach (var other in renderer.gameObject.GetComponents<Collider>())
				if (collider != other)
					DestroyImmediate(other);

			OriginalRenderers.Add(renderer);
			ConfiguredColliders.Add(collider);
			return true;
		}

		protected bool TryApplyBox(MeshRenderer renderer) {
			if (!TryGetMesh(renderer, out var mesh, out var sub_count))
				return false;

			bounds.Clear();
			triangles.Clear();
			vertices.Clear();

			mesh.GetVertices(vertices);
			for (var i = 0; i < sub_count; ++i) {
				triangles.Clear();
				mesh.GetTriangles(triangles, i);
				if (triangles.Count < 1)
					continue;
				var bbox = new Bounds(vertices[triangles[0]], Vector3.zero);
				for (var j = 1; j < triangles.Count; ++j)
					bbox.Encapsulate(vertices[triangles[j]]);
				var size = bbox.size;
				if (size.x > Vector3.kEpsilon && size.y > Vector3.kEpsilon && size.z > Vector3.kEpsilon)
					bounds.Add(bbox);
			}

			foreach (var collider in renderer.gameObject.GetComponents<Collider>())
				if (!(collider is BoxCollider))
					DestroyImmediate(collider);

			var boxes = EnsureMultiple<BoxCollider>(renderer.gameObject, bounds.Count);
			for (var i = 0; i < boxes.Count; ++i) {
				if (!boxes[i].enabled)
					boxes[i].enabled = true;

				if (boxes[i].isTrigger)
					boxes[i].isTrigger = false;

				boxes[i].size = bounds[i].size;
				boxes[i].center = bounds[i].center;
			}

			OriginalRenderers.Add(renderer);
			ConfiguredColliders.AddRange(boxes);
			return true;
		}

		public bool TryGetMesh(MeshRenderer renderer, out Mesh mesh, out int sub_count) {
			mesh = null;
			sub_count = -1;

			var materials = renderer.sharedMaterials;

			if (!renderer.TryGetComponent<MeshFilter>(out var mf)) {
				LogWarning("MeshRenderer does not have MeshFilter!", renderer);
				return false;
			}

			mesh = mf.sharedMesh;
			if (mesh == null) {
				LogWarning("MeshFilter does not have Mesh!", mf);
				return false;
			}

			if (mesh.subMeshCount != materials.Length) {
				LogWarning($"Materials count ({materials.Length}) and Mesh.subMeshCount ({mesh.subMeshCount}) " +
					$"does not match on collider mesh!", renderer);
			}
			sub_count = Math.Min(mesh.subMeshCount, materials.Length);

			if (!mesh.isReadable) {
				if (TryMakeReadable) {
					var path = AssetDatabase.GetAssetPath(mesh);
					var importer = string.IsNullOrWhiteSpace(path) ? null : AssetImporter.GetAtPath(path);
					if (importer is ModelImporter model_importer) {
						model_importer.isReadable = true;
						EditorUtility.SetDirty(model_importer);
						model_importer.SaveAndReimport();
						LogWarning($"Set {mesh} as readable. path={path}, importer={importer}", renderer);
					} else {
						LogWarning($"Can't set {mesh} as readable. path={path}, importer={importer}", renderer);
					}
				} else {
					LogWarning($"Mesh {mesh} isn't readable. Can be issues at run time.", renderer);
				}
			}

			return true;
		}

		public virtual bool TryApply(MeshRenderer renderer) {
			if (IsNone()) {
				foreach (var collider in renderer.gameObject.GetComponents<Collider>())
					DestroyImmediate(collider);
			} else if (IsAnyMesh()) {
				if (!TryApplyMesh(renderer))
					return false;
			} else if (colliderType == ColliderType.Box) {
				if (TryApplyBox(renderer))
					return false;
			}
			/*
			else if (colliderType == ColliderType.Sphere) {
				// TODO
			}
			*/

			if (applyLayer && renderer.gameObject.layer != layer)
				renderer.gameObject.layer = layer;

			if (renderer.enabled)
				renderer.enabled = false;

			if (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) != 0)
				GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, 0);

			return true;
		}
	}
}

#endif