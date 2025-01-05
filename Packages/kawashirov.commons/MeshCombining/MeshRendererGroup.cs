#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace Kawashirov.MeshCombining {
	public class MeshRendererGroup {
		public readonly MeshCombiner combiner;

		// Эти 2 для логов и отладки
		public readonly int indexMRG;
		private readonly string logToken;

		public readonly List<MeshRenderer> originals;
		protected readonly List<SubMeshGroup> matGroups;
		protected bool isLightmapped = false;

		protected Mesh cmbMesh; // in PrepareCombinedGObj
		protected MeshFilter cmbMeshFilter = null; // in PrepareCombinedGObj
		protected MeshRenderer cmbMeshRenderer = null; // in PrepareCombinedGObj

		protected float maxScaleInLightmap = 0;
		protected int lightmapTableSize = 0;

		public List<Object> tempObjectsDestroyLater;

		public MeshRendererGroup(MeshCombiner parent, int index_mrg) {
			combiner = parent;
			indexMRG = index_mrg;
			logToken = $"{parent.gameObject.name}/№{index_mrg}";

			originals = new List<MeshRenderer>();
			matGroups = new List<SubMeshGroup>();

			tempObjectsDestroyLater = new List<Object>();
		}

		protected virtual bool MatchSimilarButDifferent(MeshRenderer x, MeshRenderer y) {
			// Должны быть разные объекты, но с теме же характеристиками.
			return x != y && combiner.IsSimilar(x, y);
		}

		public virtual bool Match(MeshRenderer mr) {
			// Тут пока так. 
			// Мы сравниваем меш с каждой в группе, не смотря на то, что можно было бы и
			// с любой одной, просто что бы однаружить возможные баги в IsSimilar.
			var eq_count = originals.Where(s => MatchSimilarButDifferent(s, mr)).Count();
			if (eq_count == originals.Count) {
				return true;
			} else if (eq_count == 0) {
				return false;
			} else {
				// Подробный баг-репорт
				var mr_str = mr.gameObject.KawaGetFullPath();
				var matched = originals.Where(s => MatchSimilarButDifferent(s, mr)).ToList();
				var matched_str = string.Join("\n", matched.Select(x => x.gameObject.KawaGetFullPath()));
				var miss_str = string.Join("\n", originals.Except(matched).Select(x => x.gameObject.KawaGetFullPath()));
				var msg = $"{logToken}: Matches {mr_str} with {eq_count} of {originals.Count} renderers, but must be all or nothing.\n" +
					$"\n\tMatched objects:\n{matched_str}\n\tMiss-matched objects:\n{miss_str}";
				combiner.ThrowException(new Exception(msg), mr);
				return false;
			}
		}

		public virtual void Add(MeshRenderer mr) {
			originals.Add(mr);
			isLightmapped |= combiner.IsLightmapped(mr);
		}

		protected virtual void PrepareCombinedGObj() {
			combiner.LogDebug($"{logToken}: Preparing combined objects...");
			var gobj = new GameObject($"CmbGroup_{indexMRG}");

			cmbMesh = new Mesh { name = $"Cmb_{combiner.gameObject.name}_group_{indexMRG}" };

			var transform = gobj.transform;
			transform.SetParent(combiner.GetContainer().transform, false);
			transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			transform.localScale = Vector3.one;

			cmbMeshFilter = gobj.AddComponent<MeshFilter>();
			cmbMeshFilter.sharedMesh = cmbMesh;

			cmbMeshRenderer = gobj.AddComponent<MeshRenderer>();
			combiner.Log($"{logToken}: Prepared combined objects: {cmbMeshFilter}, {cmbMeshRenderer}", cmbMeshRenderer);
		}

		protected virtual void ConfigureCombined() {
			var gobj = cmbMeshRenderer.gameObject;
			combiner.LogDebug($"{logToken}: Configuring combined mesh renderer...", gobj);

			// Копирование настроек MeshRenderer c любого из группы (они должны быть одинаковые у всех)
			var reference = originals.First();

			cmbMeshRenderer.shadowCastingMode = reference.shadowCastingMode;
			cmbMeshRenderer.receiveShadows = reference.receiveShadows;
			cmbMeshRenderer.motionVectorGenerationMode = reference.motionVectorGenerationMode;

			var flags = GameObjectUtility.GetStaticEditorFlags(reference.gameObject);
			GameObjectUtility.SetStaticEditorFlags(gobj, flags & MeshCombiner.STATIC_EQ_MASK);

			cmbMeshRenderer.receiveGI = reference.receiveGI;

			cmbMeshRenderer.lightProbeUsage = reference.lightProbeUsage;
			cmbMeshRenderer.reflectionProbeUsage = reference.reflectionProbeUsage;
			cmbMeshRenderer.probeAnchor = reference.probeAnchor;

			cmbMeshRenderer.allowOcclusionWhenDynamic = reference.allowOcclusionWhenDynamic;

			// Кроме scaleInLightmap, для него используем наибольшее значение, 
			// т.к. все остальные будут перескейлены 
			cmbMeshRenderer.scaleInLightmap = maxScaleInLightmap;

			combiner.LogDebug($"{logToken}: Configured combined mesh renderer.", gobj);
		}

		// Является ли эта группа лайтмапируемой (да, если хотя бы один, но на самом деле они все)
		protected virtual bool IsLightmapped() => originals.Any(mr => combiner.IsLightmapped(mr));

		protected void LightmapFindTableSize() {
			// Пока что используем простой алгоритм упаковки.
			// Разбиваем UV квадрат на сетку из квадратиков и каждую меш переносим в него.
			var table_size = 0;
			while (table_size * table_size < originals.Count)
				++table_size;
			combiner.Log($"{logToken}: Using Lightmap UV packing table of size {table_size}...");
			lightmapTableSize = table_size;
		}

		protected virtual bool GetFilterSafe(MeshRenderer mesh_renderer, int source_index,
			out MeshFilter mesh_filter, out Mesh mesh) {

			mesh_filter = null;
			mesh = null;
			var log_token = $"{logToken}: Source №{source_index}";

			if (mesh_renderer == null) {
				combiner.LogWarning($"{log_token}: MeshRenderer doesn't exist anymore!");
				return false;
			}

			var shared_materials = mesh_renderer.sharedMaterials;
			if (shared_materials == null || shared_materials.Length < 1) {
				combiner.LogWarning($"{log_token}: MeshRenderer have no Materials!", mesh_renderer);
				return false;
			}

			if (!mesh_renderer.TryGetComponent(out mesh_filter) || mesh_filter == null) {
				combiner.LogWarning($"{log_token}: There is no MeshFilter!", mesh_renderer);
				return false;
			}

			mesh = mesh_filter.sharedMesh;
			if (mesh == null) {
				combiner.LogWarning($"{log_token}: MeshFilter have no Mesh!", mesh_filter);
				return false;
			}

			return true;
		}

		protected virtual void LightmapNormalizeBounds(List<Vector2> data) {
			// Может получиться так, что данные на UV не помещаются в 0..1
			// Обычно юнити лайтмапер с этим справляется, 
			// но нам нужно привести это впорядок для корректной упаковки.

			float minX = float.MaxValue, minY = float.MaxValue;
			float maxX = float.MinValue, maxY = float.MinValue;
			foreach (var point in data) {
				if (point.x < minX)
					minX = point.x;
				if (point.y < minY)
					minY = point.y;
				if (point.x > maxX)
					maxX = point.x;
				if (point.y > maxY)
					maxY = point.y;
			}

			// Коэффициент масштабирования для сохранения пропорций из размеров по осям
			var scale = 1f / Mathf.Max(maxX - minX, maxY - minY);

			for (var i = 0; i < data.Count; ++i) {
				var point = data[i];
				data[i] = new Vector2(
					(point.x - minX) * scale,
					(point.y - minY) * scale
				);
			}
		}

		protected virtual void LightmapApplyPadding(List<Vector2> data, float padding) {
			// Может получиться так, что после перепаковки 
			// острова из соседних ячеек сетки будут ссоприкасаться.
			// Что бы этого не было добавляем отступы.
			var half_padding = padding / 2;
			var one_minus_padding = 1 - padding;
			for (var i = 0; i < data.Count; ++i) {
				var point = data[i];
				data[i] = new Vector2(
					half_padding + one_minus_padding * point.x,
					half_padding + one_minus_padding * point.y
				);
			}
		}

		protected virtual void LightmapApplyGrid(List<Vector2> data, int size, int gx, int gy) {
			// Преобразование сетки
			var min_x = 1f * gx / size;
			var min_y = 1f * gy / size;
			var width = 1f / size;
			for (var i = 0; i < data.Count; ++i) {
				var point = data[i];
				data[i] = new Vector2(
					min_x + width * point.x,
					min_y + width * point.y
				);
			}
		}

		protected virtual void LightmapApplyCorrections(MeshRenderer mesh_renderer, int source_index) {
			if (!GetFilterSafe(mesh_renderer, source_index, out var mesh_filter, out var mesh))
				return;
			var log_token = $"{logToken}: Source №{source_index} Lightmap correction: ";

			var has_uv0 = mesh.HasVertexAttribute(VertexAttribute.TexCoord0);
			var has_uv1 = mesh.HasVertexAttribute(VertexAttribute.TexCoord1);

			if (!has_uv0 && !has_uv1) {
				combiner.LogWarning($"{log_token}: Mesh have no UV 0 or 1!", mesh_filter);
				return;
			}

			var data = new List<Vector2>();

			if (has_uv1) {
				var dim_uv1 = mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord1);
				if (dim_uv1 != 2) {
					combiner.LogWarning($"{log_token}: Mesh have TexCoord1 dimension={dim_uv1}", mesh_filter);
				} else {
					mesh.GetUVs(1, data);
				}
			}

			if (data.Count == 0 && has_uv0) {
				var dim_uv0 = mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0);
				if (dim_uv0 != 2) {
					combiner.LogWarning($"{log_token}: Mesh have TexCoord0 dimension={dim_uv0}!", mesh_filter);
				} else {
					mesh.GetUVs(0, data);
				}
			}

			if (data.Count < 1) {
				combiner.LogError($"{log_token}: was not able to get UV data!", mesh_filter);
				return;
			}

			// Шаг 1: нормализация к 0..1
			LightmapNormalizeBounds(data);

			// Шаг 2: отступы
			LightmapApplyPadding(data, 0.1f);

			// Шаг 3: перенос в квадрат
			var gx = source_index % lightmapTableSize;
			var gy = source_index / lightmapTableSize;
			combiner.Log($"{log_token}: Grid coord is ({gx}, {gy})!", mesh_filter);
			LightmapApplyGrid(data, lightmapTableSize, gx, gy);

			// Теперь кладём данные назад, но в копию меша.
			var mesh_copy = Object.Instantiate(mesh);
			tempObjectsDestroyLater.Add(mesh_copy);
			mesh_copy.name = $"TmpCopy_{combiner.gameObject.name}_{indexMRG}_{mesh.name}";
			mesh_copy.SetUVs(1, data);
			mesh_copy.MarkModified();
			mesh_filter.sharedMesh = mesh_copy;
		}

		protected virtual void LightmapApplyCorrections() {
			// Мы не можем применять коррекцию LM UV во время комбинирования, 
			// т.к. UV это свойство всей меши, а мы комбайним саб меши (слоты материалов)
			// По этому нужно сначала применить скейлы и квадратную упаковку к LM UV, 
			// а потом комбинировать. Ну и так тупо проще.

			if (!IsLightmapped()) {
				combiner.LogDebug($"{logToken}: Assume is not light map able group. No UV corrections.", cmbMeshRenderer);
				return;
			}

			LightmapFindTableSize();

			for (var i = 0; i < originals.Count; ++i) {
				var mesh_renderer = originals[i];
				LightmapApplyCorrections(mesh_renderer, i);
			}
		}

		protected virtual void GroupSubMeshes(MeshRenderer mesh_renderer, int source_index) {
			if (!GetFilterSafe(mesh_renderer, source_index, out var mesh_filter, out var mesh))
				return;

			var shared_materials = mesh_renderer.sharedMaterials;

			var orig2world = mesh_renderer.transform.localToWorldMatrix;
			var world2container = combiner.GetContainer().transform.worldToLocalMatrix;
			var matrix = world2container * orig2world;

			var n = Math.Min(mesh.subMeshCount, shared_materials.Length);
			for (var i = 0; i < n; i++) {
				var material = shared_materials[i];
				var smi = new SubMeshInfo(mesh_renderer, mesh, i, matrix);
				// TODO можно оптимизировать?
				foreach (var smg in matGroups) {
					if (smg.material == material) {
						smg.originals.Add(smi);
						return;
					}
				}
				matGroups.Add(new SubMeshGroup(this, indexMRG, matGroups.Count, material, smi));
			}
		}

		protected virtual void GroupSubMeshes() {
			combiner.LogDebug($"{logToken}: Grouping sub meshes of {originals.Count} mesh renderers...");
			for (var i = 0; i < originals.Count; ++i)
				GroupSubMeshes(originals[i], i);
			combiner.LogDebug($"{logToken}: Grouped sub meshes to {matGroups.Count} material groups.");
		}

		protected virtual void CombineMaterialGroups() {
			combiner.LogDebug($"{logToken}: Combining {matGroups} material groups...");
			var cis = matGroups.Select(smg => smg.CombineInstance()).ToArray();
			cmbMesh.CombineMeshes(cis, false, false, false); // TODO hasLightmapData
			Assert.IsTrue(matGroups.Count == cmbMesh.subMeshCount, $"{matGroups.Count}, {cmbMesh.subMeshCount}");
			MeshUtility.Optimize(cmbMesh);
			cmbMesh.RecalculateBounds();
			cmbMesh.MarkModified();
			EditorUtility.SetDirty(cmbMesh);
			// cmbMesh уже забинджен в MeshFilter
			cmbMeshRenderer.sharedMaterials = matGroups.Select(g => g.material).ToArray();
			cmbMeshRenderer.ResetLocalBounds();
			cmbMeshRenderer.ResetBounds();
			combiner.LogDebug($"{logToken}: Combined into {cmbMesh.subMeshCount} sub meshes / materials.");
		}

		protected virtual void RemoveOriginals(MeshRenderer mesh_renderer, int source_index) {
			mesh_renderer.enabled = false;

			if (mesh_renderer.TryGetComponent<MeshFilter>(out var mesh_filter) && mesh_filter != null)
				Object.DestroyImmediate(mesh_filter);

			Object.DestroyImmediate(mesh_renderer);
		}

		protected virtual void RemoveOriginals() {
			combiner.Log($"{logToken}: Removing original mesh renderers...");
			for (var i = 0; i < originals.Count; i++) {
				var orig = originals[i];
				if (orig != null)
					RemoveOriginals(orig, i);
			}
			originals.Clear();
		}

		protected virtual void DestroyTemp() {
			foreach (var mat_group in matGroups)
				mat_group.DestroyTemp();
			foreach (var tmp in tempObjectsDestroyLater)
				Object.DestroyImmediate(tmp);
			matGroups.Clear();
		}

		public virtual void Process() {
			combiner.Log($"{logToken}: Begin processing...");

			maxScaleInLightmap = originals.Select(r => r.scaleInLightmap).Max();

			PrepareCombinedGObj();
			ConfigureCombined();
			LightmapApplyCorrections();
			// Лайтмап коррекция должна применяться до группировки, т.к. группы референсят 
			// меш объект и надо что бы они референсили копию с исправленым лайтмапом.
			GroupSubMeshes();
			CombineMaterialGroups();
			RemoveOriginals();
			DestroyTemp();
			combiner.Log($"{logToken}: Processed.");
		}
	}
}
#endif