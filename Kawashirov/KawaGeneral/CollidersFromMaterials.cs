using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Kawashirov.Refreshables;
using System;
using System.Linq;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Kawashirov {
	public class CollidersFromMaterials : KawaEditorBehaviour {
		private static MeshColliderCookingOptions mesh_options =
			MeshColliderCookingOptions.CookForFasterSimulation |
			MeshColliderCookingOptions.EnableMeshCleaning |
			MeshColliderCookingOptions.WeldColocatedVertices;

		// Эти используются глобально, что бы избежать множества лишних перевыделений.
		private static readonly List<Bounds> bounds = new List<Bounds>();
		private static readonly List<int> triangles = new List<int>();
		private static readonly List<Vector3> vertices = new List<Vector3>();

		public enum ColliderType { None, Box, Convex, Mesh, Sphere }
		public enum SelectType { MaterialAsset, MaterialName, ObjectName }
		public enum MatchResult { Match, Miss, Partial }

		[Serializable]
		public struct Mapping {
			public SelectType selectType;
			public Material material;
			public string regularExpression;
			public bool ignoreCase;
			[Space]
			public bool applyLayer;
			[Layer] public int layer;
			public ColliderType colliderType;
			// 
			[NonSerialized] private Regex regex;
			[NonSerialized] private string regex_pattern;


#if UNITY_EDITOR

			public bool IsNone() => colliderType == ColliderType.None;
			public bool IsAnyMesh() => colliderType == ColliderType.Mesh || colliderType == ColliderType.Convex;
			public bool IsPrimitive() => colliderType == ColliderType.Box || colliderType == ColliderType.Sphere;

			public Regex GetRegex() {
				if (regex == null || regex_pattern != regularExpression) {
					var options = RegexOptions.Compiled;
					if (ignoreCase)
						options |= RegexOptions.IgnoreCase;
					regex_pattern = regularExpression;
					regex = new Regex(regex_pattern, options);
				}
				return regex;
			}

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

			public bool TryApply(MeshRenderer mr) {
				var hpath = mr.transform.KawaGetHierarchyPath();
				var materials = mr.sharedMaterials;

				if (IsNone()) {
					foreach (var collider in mr.gameObject.GetComponents<Collider>())
						DestroyImmediate(collider);
				} else {
					var mf = mr.GetComponent<MeshFilter>();
					if (mf == null) {
						Debug.LogWarningFormat(mf, "MeshRenderer does not have MeshFilter! @ <i>{0}</i>", hpath);
						return false;
					}

					var mesh = mf.sharedMesh;
					if (mesh == null) {
						Debug.LogWarningFormat(mf, "MeshFilter does not have Mesh! @ <i>{0}</i>", hpath);
						return false;
					}

					if (mesh.subMeshCount != materials.Length) {
						Debug.LogWarningFormat(mr,
							"Materials count ({1}) and Mesh.subMeshCount ({2}) does not match on collider mesh! @ <i>{0}</i>",
							hpath, materials.Length, mesh.subMeshCount
						);
						return false;
					}
					var submeshes = Math.Min(mesh.subMeshCount, materials.Length);

					if (IsAnyMesh() && submeshes > 1) {
						Debug.LogWarningFormat(
							"MeshRenderer has more than one material slot. " +
							"Whole mesh will be used as collider and submeshes will be ignored! @ <i>{0}</i>",
							hpath
						);
					}

					if (IsAnyMesh()) {
						var meshc = EnsureSingle<MeshCollider>(mr.gameObject);
						meshc.sharedMesh = mesh;
						if (!meshc.enabled)
							meshc.enabled = true;
						if (meshc.isTrigger)
							meshc.isTrigger = false;
						var convex = colliderType == ColliderType.Convex;
						if (meshc.convex != convex)
							meshc.convex = convex;
						if (meshc.cookingOptions != mesh_options)
							meshc.cookingOptions = mesh_options;

						foreach (var other_c in mr.gameObject.GetComponents<Collider>())
							if (meshc != other_c)
								DestroyImmediate(other_c);
					} else if (colliderType == ColliderType.Box) {
						bounds.Clear();
						vertices.Clear();

						mesh.GetVertices(vertices);
						for (var i = 0; i < submeshes; ++i) {
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

						foreach (var collider in mr.gameObject.GetComponents<Collider>())
							if (!(collider is BoxCollider))
								DestroyImmediate(collider);

						var boxes = EnsureMultiple<BoxCollider>(mr.gameObject, bounds.Count);
						for (var i = 0; i < boxes.Count; ++i) {
							if (!boxes[i].enabled)
								boxes[i].enabled = true;
							if (boxes[i].isTrigger)
								boxes[i].isTrigger = false;
							boxes[i].size = bounds[i].size;
							boxes[i].center = bounds[i].center;
						}

					} else if (colliderType == ColliderType.Sphere) {



					}

				}

				if (applyLayer && mr.gameObject.layer != layer)
					mr.gameObject.layer = layer;
				if (mr.enabled)
					mr.enabled = false;
				if (GameObjectUtility.GetStaticEditorFlags(mr.gameObject) != 0)
					GameObjectUtility.SetStaticEditorFlags(mr.gameObject, 0);

				return true;
			}

#endif // UNITY_EDITOR

		}

		public Mapping[] mappings;

		private List<int> match_mappings = new List<int>();

#if UNITY_EDITOR

		[CanEditMultipleObjects, CustomEditor(typeof(CollidersFromMaterials), true)]
		public class Editor : UnityEditor.Editor {
			public override void OnInspectorGUI() {
				DrawDefaultInspector();
				EditorGUILayout.Space();
				this.BehaviourRefreshGUI();
			}
		}

		public override void Refresh() {
			var mrs = GetComponentsInChildren<MeshRenderer>(true);
			Debug.LogFormat(this,
				"Processing colliders on {1} mesh renderers with {2} mappings... @ <i>{0}</i>",
				kawaHierarchyPath, mrs.Length, mappings.Length
			);
			var counter = 0;
			foreach (var mr in mrs)
				if (RefreshMeshRenderer(mr))
					++counter;
			Debug.LogFormat(this, "Processed {1} colliders. @ <i>{0}</i>", kawaHierarchyPath, counter);
		}

		public bool RefreshMeshRenderer(MeshRenderer mr) {
			var hpath = mr.transform.KawaGetHierarchyPath();

			match_mappings.Clear();
			var any_partial = false;
			for (var i = 0; i < mappings.Length; ++i) {
				var match = mappings[i].MatchMeshRenderer(mr);
				if (match == MatchResult.Match)
					match_mappings.Add(i);
				else if (match == MatchResult.Partial)
					any_partial = true;
			}

			if (match_mappings.Count > 1)
				Debug.LogWarningFormat(mr,
					"MeshRenderer match <b>{1}</b> collider material mappings: <b>{2}</b>! Only first one will be used! @ <i>{0}</i>",
					hpath, match_mappings.Count, string.Join(", ", match_mappings.Select(i => string.Format("#{0}", i)))
				);
			else if (any_partial)
				Debug.LogWarningFormat(mr, "MeshRenderer has both matching and missmatching materials for colliders! @ <i>{0}</i>", hpath);

			if (match_mappings.Count < 1)
				return false;

			return mappings[match_mappings[0]].TryApply(mr);
		}

		protected static C EnsureSingle<C>(GameObject go) where C : Component {
			C component = null;
			var components = new List<C>();
			go.GetComponents(components);
			if (components.Count > 0)
				component = components[0];
			if (components.Count > 1)
				for (var i = 1; i < components.Count; ++i)
					DestroyImmediate(components[i]);
			if (component == null)
				component = go.AddComponent<C>();
			return component;
		}

		protected static List<C> EnsureMultiple<C>(GameObject go, int count) where C : Component {
			var components = new List<C>(count + 1);
			go.GetComponents(components);
			if (components.Count > count) {
				for (var i = count; i < components.Count; ++i)
					DestroyImmediate(components[i]);
				components = components.GetRange(0, count);
			}
			while (components.Count < count)
				components.Add(go.AddComponent<C>());
			return components;
		}



#endif // UNITY_EDITOR
	}
}
