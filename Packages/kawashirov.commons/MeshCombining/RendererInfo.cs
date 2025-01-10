#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Kawashirov.MeshCombining {
	public class RendererInfo {
		public readonly string logToken;
		public readonly MeshCombiner combiner;
		public readonly MeshCombineGroupMeta group;
		public readonly int index;
		public readonly MeshRenderer renderer;
		public readonly GameObject gobj;

		public MeshFilter meshFilter = null;
		public Mesh meshOriginal = null;
		public Mesh meshModified = null;

		public readonly List<Vector2> lightmapUV = new List<Vector2>();
		public UVIsland lightmapIsland = UVIsland.singual;
		public float lightmapUVDistributionMetric = 1;

		public RendererInfo(MeshCombineGroupMeta group, int index, MeshRenderer renderer) {
			combiner = group.Combiner;
			this.group = group;
			this.index = index;
			this.renderer = renderer;
			gobj = renderer.gameObject;
			logToken = $"Source №{index} ({gobj.name})";
		}

		public void Init() {
			if (renderer == null) {
				group.LogWarning($"{logToken}: MeshRenderer doesn't exist anymore!");
				return;
			}

			var shared_materials = renderer.sharedMaterials;
			if (shared_materials == null || shared_materials.Length < 1) {
				group.LogWarning($"{logToken}: MeshRenderer have no Materials!", renderer);
				return;
			}

			if (!renderer.TryGetComponent(out meshFilter) || meshFilter == null) {
				meshFilter = null; // "true" null
				group.LogWarning($"{logToken}: There is no MeshFilter!", renderer);
				return;
			}

			meshOriginal = meshFilter.sharedMesh;
			if (meshOriginal == null) {
				group.LogWarning($"{logToken}: MeshFilter have no Mesh!", meshFilter);
				return;
			}

			// TODO
		}

		public bool LightmapUVPrepare() {
			if (!group.IsLightmapped)
				return false;
			if (combiner.LightmapUVCorrection == LightmapCorrectionMode.Disabled)
				return false;
			if (meshOriginal == null || meshFilter == null)
				return false;

			lightmapUV.Clear();
			var has_uv0 = meshOriginal.HasVertexAttribute(VertexAttribute.TexCoord0);
			var has_uv1 = meshOriginal.HasVertexAttribute(VertexAttribute.TexCoord1);

			if (!has_uv0 && !has_uv1) {
				group.LogWarning($"{logToken}: Mesh have no UV 0 or 1 for Lightmap UV!", meshFilter);
				return false;
			}

			if (has_uv1) {
				var dim_uv1 = meshOriginal.GetVertexAttributeDimension(VertexAttribute.TexCoord1);
				if (dim_uv1 != 2) {
					group.LogWarning($"{logToken}: Mesh have TexCoord1 dimension={dim_uv1}", meshFilter);
				} else {
					meshOriginal.GetUVs(1, lightmapUV);
					lightmapUVDistributionMetric = meshOriginal.GetUVDistributionMetric(1);
				}
			}

			if (lightmapUV.Count == 0 && has_uv0) {
				var dim_uv0 = meshOriginal.GetVertexAttributeDimension(VertexAttribute.TexCoord0);
				if (dim_uv0 != 2) {
					group.LogWarning($"{logToken}: Mesh have TexCoord0 dimension={dim_uv0}!", meshFilter);
				} else {
					meshOriginal.GetUVs(0, lightmapUV);
					lightmapUVDistributionMetric = meshOriginal.GetUVDistributionMetric(0);
				}
			}

			if (lightmapUV.Count < 1) {
				group.LogWarning($"{logToken}: was not able to get data for Lightmap UV!", meshFilter);
				return false;
			}

			var island = UVIsland.singual;
			foreach (var point in lightmapUV) {
				if (float.IsFinite(point.x) && float.IsFinite(point.y))
					island = island.ExpandByUVPoint(point);
			}
			if (island.IsFinite()) {
				group.LogDebug($"{logToken}: Found lightmap UV island: {island}", meshFilter);
				lightmapIsland = island;
			} else {
				group.LogWarning($"{logToken}: was not able to get finite Lightmap UV data ({island})!", meshFilter);
				lightmapUV.Clear();
				lightmapIsland = UVIsland.singual;
			}

			return true;
		}

		public void LightmapUVApply() {
			// Теперь кладём данные назад, но в копию меша.
			if (meshFilter == null || meshOriginal == null || lightmapUV.Count < 1)
				return;
			group.LogDebug($"{logToken}: Applying lightmap UV ({lightmapUV.Count} points) to new mesh...", meshFilter);
			var mesh_copy = meshModified = Object.Instantiate(meshOriginal);
			mesh_copy.name = $"TmpCopy_{combiner.gameObject.name}_{group.GroupIndex}_{meshOriginal.name}";
			mesh_copy.SetUVs(1, lightmapUV);
			mesh_copy.MarkModified();
			meshFilter.sharedMesh = mesh_copy;
			group.LogDebug($"{logToken}: Applied lightmap UV ({lightmapUV.Count} points) to new mesh {mesh_copy}.", meshFilter);
		}

		public Mesh GetMeshForCombining() => meshModified == null ? meshOriginal : meshModified;

		public void DestroyModified() {
			if (meshModified != null)
				Object.DestroyImmediate(meshModified);
		}

		public void DestroyOriginals() {
			if (renderer != null) {
				renderer.enabled = false;
				Object.DestroyImmediate(renderer);
			}
			if (meshFilter != null) {
				Object.DestroyImmediate(meshFilter);
			}
		}

	}
}
#endif