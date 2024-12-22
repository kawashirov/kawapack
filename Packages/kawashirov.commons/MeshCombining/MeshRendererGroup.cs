#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace Kawashirov.MeshCombining {
	public class MeshRendererGroup {
		public readonly MeshCombineOp parent;

		// Эти 2 для логов и отладки
		public readonly int indexMRG;
		private readonly string logToken;

		public readonly List<MeshRenderer> sources;
		public readonly List<SubMeshGroup> materialGroups;
		public readonly Mesh targetMesh;

		public MeshFilter targetMeshFilter = null; //  in CreateTarget
		public MeshRenderer targetMeshRenderer = null; // in CreateTarget

		protected float maxScaleInLightmap = 0;
		protected int lightmapTableSize = 0;

		public List<Object> tempObjectsDestroyLater;

		public MeshRendererGroup(MeshCombineOp parent, int index_mrg, MeshRenderer init) {
			this.parent = parent;
			indexMRG = index_mrg;
			logToken = $"MeshCombine {parent.ID}/#{index_mrg}";

			sources = new List<MeshRenderer> { init };
			materialGroups = new List<SubMeshGroup>();
			targetMesh = new Mesh { name = $"CombinedGroup_{parent.ID}_{index_mrg}" };
			targetMesh.MarkDynamic();

			tempObjectsDestroyLater = new List<Object>();
		}

		protected virtual bool Equals(MeshRenderer x, MeshRenderer y) {
			// Должны быть разные объекты, но с теме же характеристиками.
			return x != y && parent.mre.Equals(x, y);
		}

		public virtual bool Match(MeshRenderer mr) {
			// Тут пока так. 
			// На всякий случай проверяем с каждым объектом, да бы обнвружить баги в MeshRendererEquality
			var eq = parent.mre;
			var eq_count = sources.Where(s => Equals(s, mr)).Count();
			if (eq_count == sources.Count) {
				return true;
			} else if (eq_count == 0) {
				return false;
			} else {
				// Подробный баг-репорт
				var mr_str = mr.gameObject.KawaGetFullPath();
				var matched = sources.Where(s => Equals(s, mr)).ToList();
				var matched_str = string.Join("\n", matched.Select(x => x.gameObject.KawaGetFullPath()));
				var miss_str = string.Join("\n", sources.Except(matched).Select(x => x.gameObject.KawaGetFullPath()));
				var msg1 = $"{logToken}: Matches {mr_str} with {eq_count} of {sources.Count} renderers, but must be all or nothing.";
				var msg2 = $"{msg1}\nMatched objects: {matched_str}\nMiss-matched objects: {miss_str}";
				Debug.LogError(msg2, mr);
				throw new Exception(msg1);
			}
		}

		protected virtual void CreateTarget() {
			Debug.Log($"{logToken}: Creatig target objects...");
			var gobj = new GameObject($"CombinedGroup_{indexMRG}");

			var transform = gobj.transform;
			transform.SetParent(parent.Target.transform, false);
			transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			transform.localScale = Vector3.one;

			targetMeshFilter = gobj.AddComponent<MeshFilter>();
			targetMeshFilter.sharedMesh = targetMesh;

			targetMeshRenderer = gobj.AddComponent<MeshRenderer>();
			Debug.Log($"{logToken}: Created target objects: {gobj.KawaGetFullPath()}", gobj);
		}

		protected virtual void ConfigureTarget() {
			var gobj = targetMeshRenderer.gameObject;
			Debug.Log($"{logToken}: Configuring target MeshRenderer: {gobj.KawaGetFullPath()}", gobj);

			// Копирование настроек MeshRenderer c любого из группы (они должны быть одинаковые у всех)
			var reference = sources.First();

			targetMeshRenderer.shadowCastingMode = reference.shadowCastingMode;
			targetMeshRenderer.receiveShadows = reference.receiveShadows;
			targetMeshRenderer.motionVectorGenerationMode = reference.motionVectorGenerationMode;

			var flags = GameObjectUtility.GetStaticEditorFlags(reference.gameObject);
			GameObjectUtility.SetStaticEditorFlags(gobj, flags & MeshRendererEquality.MASK);

			targetMeshRenderer.receiveGI = reference.receiveGI;

			targetMeshRenderer.lightProbeUsage = reference.lightProbeUsage;
			targetMeshRenderer.reflectionProbeUsage = reference.reflectionProbeUsage;
			targetMeshRenderer.probeAnchor = reference.probeAnchor;

			targetMeshRenderer.allowOcclusionWhenDynamic = reference.allowOcclusionWhenDynamic;

			// Кроме scaleInLightmap, для него используем наибольшее значение, 
			// т.к. все остальные будут перескейлены 
			targetMeshRenderer.scaleInLightmap = maxScaleInLightmap;

			Debug.Log($"{logToken}: Configured target MeshRenderer: {gobj.KawaGetFullPath()}", gobj);
		}

		// Является ли эта группа лайтмапируемой (да, если хотя бы один, но на самом деле они все)
		protected virtual bool IsLightmapped() => sources.Any(mr => parent.mre.IsLightmapped(mr));

		protected void FindTableSize() {
			// Пока что используем простой алгоритм упаковки.
			// Разбиваем UV квадрат на сетку из квадратиков и каждую меш переносим в него.
			var table_size = 0;
			while (table_size * table_size < sources.Count)
				++table_size;
			Debug.Log($"{logToken}: Using Lightmap UV packing table of size {table_size}...");
			lightmapTableSize = table_size;
		}

		protected virtual bool GetFilterSafe(MeshRenderer mesh_renderer, int source_index,
			out MeshFilter mesh_filter, out Mesh mesh) {

			mesh_filter = null;
			mesh = null;
			var log_token = $"{logToken}: Source #{source_index}";

			if (mesh_renderer == null) {
				Debug.LogError($"{log_token}: MeshRenderer doesn't exist anymore!", targetMesh);
				return false;
			}
			var gobj = mesh_renderer.gameObject;

			var shared_materials = mesh_renderer.sharedMaterials;
			if (shared_materials == null || shared_materials.Length < 1) {
				Debug.LogError($"{log_token}: MeshRenderer have no Materials at {gobj.KawaGetFullPath()}", mesh_renderer);
				return false;
			}

			if (!mesh_renderer.TryGetComponent<MeshFilter>(out mesh_filter) || mesh_filter == null) {
				Debug.LogError($"{log_token}: There is no MeshFilter at {gobj.KawaGetFullPath()}", mesh_renderer);
				return false;
			}

			mesh = mesh_filter.sharedMesh;
			if (mesh == null) {
				Debug.LogError($"{log_token}: MeshFilter have no Mesh at {gobj.KawaGetFullPath()}", mesh_filter);
				return false;
			}

			return true;
		}

		protected virtual void NormalizeBounds(List<Vector2> data) {
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

		protected virtual void ApplyPadding(List<Vector2> data, float padding) {
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

		protected virtual void ApplyGrid(List<Vector2> data, int size, int gx, int gy) {
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

		protected static Vector4 Bounds(List<Vector2> data) {
			var xmin = data.Select(v => v.x).Min();
			var ymin = data.Select(v => v.y).Min();
			var xmax = data.Select(v => v.x).Max();
			var ymax = data.Select(v => v.y).Max();
			return new Vector4(xmin, ymin, xmax, ymax);
		}

		protected virtual void ApplyLightmapCorrections(MeshRenderer mesh_renderer, int source_index) {
			if (!GetFilterSafe(mesh_renderer, source_index, out var mesh_filter, out var mesh))
				return;
			var gobj = mesh_renderer.gameObject;
			var log_token = $"{logToken}: Source #{source_index} Lightmap correction: ";

			var has_uv0 = mesh.HasVertexAttribute(VertexAttribute.TexCoord0);
			var has_uv1 = mesh.HasVertexAttribute(VertexAttribute.TexCoord1);

			if (!has_uv0 && !has_uv1) {
				Debug.LogWarning($"{log_token}: Mesh have no UV 0 or 1 at {gobj.KawaGetFullPath()}", mesh_filter);
				return;
			}

			var data = new List<Vector2>();

			if (has_uv1) {
				var dim_uv1 = mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord1);
				if (dim_uv1 != 2) {
					Debug.LogWarning($"{log_token}: Mesh have TexCoord1 dimension={dim_uv1} at {gobj.KawaGetFullPath()}", mesh_filter);
				} else {
					mesh.GetUVs(1, data);
				}
			}

			if (data.Count == 0 && has_uv0) {
				var dim_uv0 = mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0);
				if (dim_uv0 != 2) {
					Debug.LogWarning($"{log_token}: Mesh have TexCoord0 dimension={dim_uv0} at {gobj.KawaGetFullPath()}", mesh_filter);
				} else {
					mesh.GetUVs(0, data);
				}
			}

			if (data.Count < 1) {
				Debug.LogError($"{log_token}: was not able to get UV data at {gobj.KawaGetFullPath()}", mesh_filter);
				return;
			}

			// Шаг 1: нормализация к 0..1
			NormalizeBounds(data);

			// Шаг 2: отступы
			ApplyPadding(data, 0.1f);

			// Шаг 3: перенос в квадрат
			var gx = source_index % lightmapTableSize;
			var gy = source_index / lightmapTableSize;
			Debug.Log($"{log_token}: Grid coord is ({gx}, {gy}) for {gobj.KawaGetFullPath()}", mesh_filter);
			ApplyGrid(data, lightmapTableSize, gx, gy);

			// Теперь кладём данные назад, но в копию меша.
			var mesh_copy = Object.Instantiate(mesh);
			tempObjectsDestroyLater.Add(mesh_copy);
			mesh_copy.name = $"TmpCopy_{parent.ID}_{indexMRG}_{mesh.name}";
			mesh_copy.SetUVs(1, data);
			mesh_copy.MarkModified();
			mesh_filter.sharedMesh = mesh_copy;
		}

		protected virtual void ApplyLightmapCorrections() {
			// Мы не можем применять коррекцию LM UV во время комбинирования, 
			// т.к. UV это свойство всей меши, а мы комбайним саб меши (слоты материалов)
			// По этому нужно сначала применить скейлы и квадратную упаковку к LM UV, 
			// а потом комбинировать. Ну и так тупо проще.

			if (!IsLightmapped()) {
				Debug.Log($"{logToken}: Assume is not light map able group. No UV corrections.");
				return;
			}

			FindTableSize();

			for (var i = 0; i < sources.Count; ++i) {
				var mesh_renderer = sources[i];
				ApplyLightmapCorrections(mesh_renderer, i);
			}
		}

		protected virtual void GroupSubMeshes(MeshRenderer mesh_renderer, int source_index) {
			if (!GetFilterSafe(mesh_renderer, source_index, out var mesh_filter, out var mesh))
				return;

			var shared_materials = mesh_renderer.sharedMaterials;

			var src2world = mesh_renderer.transform.localToWorldMatrix;
			var world2target = parent.Target.transform.worldToLocalMatrix;
			var matrix = world2target * src2world;

			var n = Math.Min(mesh.subMeshCount, shared_materials.Length);
			for (var i = 0; i < n; i++) {
				var material = shared_materials[i];
				var smi = new SubMeshInfo(mesh_renderer, mesh, i, matrix);
				// TODO можно оптимизировать?
				foreach (var smg in materialGroups) {
					if (smg.material == material) {
						smg.sources.Add(smi);
						return;
					}
				}
				materialGroups.Add(new SubMeshGroup(parent.ID, indexMRG, materialGroups.Count, material, smi));
			}
		}

		protected virtual void GroupSubMeshes() {
			Debug.Log($"{logToken}: Grouping sub meshes of {sources.Count} MeshRenderers...");
			for (var i = 0; i < sources.Count; ++i) {
				var mesh_renderer = sources[i];
				GroupSubMeshes(mesh_renderer, i);
			}
			Debug.Log($"{logToken}: Grouped sub meshes to {materialGroups.Count} material groups.");
		}

		protected virtual void Combine() {
			Debug.Log($"{logToken}: Combining...");
			var apply_lm_corrections = IsLightmapped();
			var cis = materialGroups.Select(smg => smg.CombineInstance()).ToArray();
			targetMesh.CombineMeshes(cis, false, false, false); // TODO hasLightmapData
			targetMesh.RecalculateBounds();
			MeshUtility.Optimize(targetMesh);
			targetMesh.MarkModified();
			EditorUtility.SetDirty(targetMesh);
			int mgc = materialGroups.Count, smc = targetMesh.subMeshCount;
			if (mgc != smc) {
				Debug.LogError($"{logToken}: material groups count {mgc} doesn't match combined sub mesh count {smc}.");
			} else {
				Debug.Log($"{logToken}: Combined into {smc} sub meshes / materials.");
			}
		}

		protected virtual void ApplyMaterialsToTarget() {
			targetMeshRenderer.sharedMaterials = materialGroups.Select(g => g.material).ToArray();
			targetMeshRenderer.ResetLocalBounds();
			targetMeshRenderer.ResetBounds();
		}

		protected virtual void RemoveOriginals(MeshRenderer mesh_renderer, int source_index) {
			mesh_renderer.enabled = false;

			if (mesh_renderer.TryGetComponent<MeshFilter>(out var mesh_filter) && mesh_filter != null)
				Object.DestroyImmediate(mesh_filter);

			Object.DestroyImmediate(mesh_renderer);
		}

		protected virtual void RemoveOriginals() {
			Debug.Log($"{logToken}: Removing original mesh renderers...");
			for (var i = 0; i < sources.Count; i++) {
				var source = sources[i];
				if (source != null)
					RemoveOriginals(source, i);
			}
		}

		protected virtual void Destroy() {
			foreach (var mat_group in materialGroups)
				mat_group.Destroy();
			foreach (var tmp in tempObjectsDestroyLater)
				Object.DestroyImmediate(tmp);
			// Поможем мусорщику
			sources.Clear();
			materialGroups.Clear();
		}

		public virtual void Process() {
			Debug.Log($"{logToken}: Begin processing...");

			maxScaleInLightmap = sources.Select(r => r.scaleInLightmap).Max();

			CreateTarget();
			ConfigureTarget();

			// Лайтмап коррекция должна применяться до группировки, т.к. группы референсят меш объект
			// и надо что бы они референсили копию с исправленым лайтмапом
			ApplyLightmapCorrections();

			GroupSubMeshes();

			Combine();
			ApplyMaterialsToTarget();
			RemoveOriginals();
			Destroy();
			Debug.Log($"{logToken}: Processed.");
		}
	}
}
#endif