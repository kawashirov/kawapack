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
	public class MeshCombineGroupMeta : KawaEditorBehaviour {
		public MeshCombiner Combiner = null;
		public int GroupIndex = -1;
		public List<MeshRenderer> OriginalRenderers = new List<MeshRenderer>();
		public List<GameObject> OriginalGameObjects = new List<GameObject>();

		[Space]
		// Является ли эта группа лайтмапируемой (да, если хотя бы один, но на самом деле они все)
		public bool IsLightmapped = false;
		public float MaxScaleInLightmap = 0;
		public int LightmapTableSize = 0;

		[Space]
		public Mesh CmbMesh = null; // in PrepareCombinedGObj
		public MeshFilter CmbMeshFilter = null; // in PrepareCombinedGObj
		public MeshRenderer CmbMeshRenderer = null; // in PrepareCombinedGObj

		/**/

		protected readonly List<SubMeshGroup> matGroups = new List<SubMeshGroup>();
		protected readonly List<Object> tempObjectsDestroyLater = new List<Object>();

		internal virtual void Init() {
			OriginalRenderers.Clear();
		}

		protected virtual bool MatchSimilarButDifferent(MeshRenderer x, MeshRenderer y) {
			// Должны быть разные объекты, но с теме же характеристиками.
			return x != y && Combiner.IsSimilar(x, y);
		}

		public virtual bool Match(MeshRenderer mr) {
			// Тут пока так. 
			// Мы сравниваем меш с каждой в группе, не смотря на то, что можно было бы и
			// с любой одной, просто что бы однаружить возможные баги в IsSimilar.
			var eq_count = OriginalRenderers.Where(s => MatchSimilarButDifferent(s, mr)).Count();
			if (eq_count == OriginalRenderers.Count) {
				return true;
			} else if (eq_count == 0) {
				return false;
			} else {
				// Подробный баг-репорт
				var mr_str = mr.gameObject.KawaGetFullPath();
				var matched = OriginalRenderers.Where(s => MatchSimilarButDifferent(s, mr)).ToList();
				var matched_str = string.Join("\n", matched.Select(x => x.gameObject.KawaGetFullPath()));
				var miss_str = string.Join("\n", OriginalRenderers.Except(matched).Select(x => x.gameObject.KawaGetFullPath()));
				ThrowException(new Exception(
					$"Matches {mr_str} with {eq_count} of {OriginalRenderers.Count} renderers, but must be all or nothing.\n" +
					$"\n\tMatched objects:\n{matched_str}\n\tMiss-matched objects:\n{miss_str}"), mr);
				return false;
			}
		}

		public virtual void Add(MeshRenderer mr) {
			OriginalRenderers.Add(mr);
			IsLightmapped |= Combiner.IsLightmapped(mr);
			SetDirty();
		}

		protected virtual void PrepareAndConfigure() {
			CmbMesh = new Mesh { name = $"Cmb_{Combiner.gameObject.name}_group_{GroupIndex}" };

			CmbMeshFilter = gameObject.AddComponent<MeshFilter>();
			CmbMeshFilter.sharedMesh = CmbMesh;

			CmbMeshRenderer = gameObject.AddComponent<MeshRenderer>();
			LogDebug($"Prepared combined objects, configuring:\n{CmbMeshFilter},\n{CmbMeshRenderer}");

			// Копирование настроек MeshRenderer c любого из группы (они должны быть одинаковые у всех)
			var reference = OriginalRenderers.First();

			CmbMeshRenderer.shadowCastingMode = reference.shadowCastingMode;
			CmbMeshRenderer.receiveShadows = reference.receiveShadows;
			CmbMeshRenderer.motionVectorGenerationMode = reference.motionVectorGenerationMode;

			var flags = GameObjectUtility.GetStaticEditorFlags(reference.gameObject);
			GameObjectUtility.SetStaticEditorFlags(gameObject, flags & MeshCombiner.STATIC_EQ_MASK);

			CmbMeshRenderer.receiveGI = reference.receiveGI;

			CmbMeshRenderer.lightProbeUsage = reference.lightProbeUsage;
			CmbMeshRenderer.reflectionProbeUsage = reference.reflectionProbeUsage;
			CmbMeshRenderer.probeAnchor = reference.probeAnchor;

			CmbMeshRenderer.allowOcclusionWhenDynamic = reference.allowOcclusionWhenDynamic;

			// Кроме scaleInLightmap, для него используем наибольшее значение, 
			// т.к. все остальные будут перескейлены 
			CmbMeshRenderer.scaleInLightmap = MaxScaleInLightmap;

			LogDebug($"Configured combined mesh renderer: {CmbMeshRenderer}");
		}

		protected void LightmapFindTableSize() {
			// Пока что используем простой алгоритм упаковки.
			// Разбиваем UV квадрат на сетку из квадратиков и каждую меш переносим в него.
			var table_size = 0;
			while (table_size * table_size < OriginalRenderers.Count)
				++table_size;
			Combiner.Log($"Using Lightmap UV packing table of size {table_size}...");
			LightmapTableSize = table_size;
		}

		protected virtual bool GetFilterSafe(MeshRenderer mesh_renderer, int source_index,
			out MeshFilter mesh_filter, out Mesh mesh) {

			mesh_filter = null;
			mesh = null;
			var log_token = $"Source №{source_index}";

			if (mesh_renderer == null) {
				Combiner.LogWarning($"{log_token}: MeshRenderer doesn't exist anymore!");
				return false;
			}

			var shared_materials = mesh_renderer.sharedMaterials;
			if (shared_materials == null || shared_materials.Length < 1) {
				Combiner.LogWarning($"{log_token}: MeshRenderer have no Materials!", mesh_renderer);
				return false;
			}

			if (!mesh_renderer.TryGetComponent(out mesh_filter) || mesh_filter == null) {
				Combiner.LogWarning($"{log_token}: There is no MeshFilter!", mesh_renderer);
				return false;
			}

			mesh = mesh_filter.sharedMesh;
			if (mesh == null) {
				Combiner.LogWarning($"{log_token}: MeshFilter have no Mesh!", mesh_filter);
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
			var log_token = $"Source №{source_index} Lightmap correction: ";

			var has_uv0 = mesh.HasVertexAttribute(VertexAttribute.TexCoord0);
			var has_uv1 = mesh.HasVertexAttribute(VertexAttribute.TexCoord1);

			if (!has_uv0 && !has_uv1) {
				Combiner.LogWarning($"{log_token}: Mesh have no UV 0 or 1!", mesh_filter);
				return;
			}

			var data = new List<Vector2>();

			if (has_uv1) {
				var dim_uv1 = mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord1);
				if (dim_uv1 != 2) {
					Combiner.LogWarning($"{log_token}: Mesh have TexCoord1 dimension={dim_uv1}", mesh_filter);
				} else {
					mesh.GetUVs(1, data);
				}
			}

			if (data.Count == 0 && has_uv0) {
				var dim_uv0 = mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0);
				if (dim_uv0 != 2) {
					Combiner.LogWarning($"{log_token}: Mesh have TexCoord0 dimension={dim_uv0}!", mesh_filter);
				} else {
					mesh.GetUVs(0, data);
				}
			}

			if (data.Count < 1) {
				Combiner.LogError($"{log_token}: was not able to get UV data!", mesh_filter);
				return;
			}

			// Шаг 1: нормализация к 0..1
			LightmapNormalizeBounds(data);

			// Шаг 2: отступы
			LightmapApplyPadding(data, 0.1f);

			// Шаг 3: перенос в квадрат
			var gx = source_index % LightmapTableSize;
			var gy = source_index / LightmapTableSize;
			Combiner.Log($"{log_token}: Grid coord is ({gx}, {gy})!", mesh_filter);
			LightmapApplyGrid(data, LightmapTableSize, gx, gy);

			// Теперь кладём данные назад, но в копию меша.
			var mesh_copy = Object.Instantiate(mesh);
			tempObjectsDestroyLater.Add(mesh_copy);
			mesh_copy.name = $"TmpCopy_{Combiner.gameObject.name}_{GroupIndex}_{mesh.name}";
			mesh_copy.SetUVs(1, data);
			mesh_copy.MarkModified();
			mesh_filter.sharedMesh = mesh_copy;
		}

		protected virtual void LightmapApplyCorrections() {
			// Мы не можем применять коррекцию LM UV во время комбинирования, 
			// т.к. UV это свойство всей меши, а мы комбайним саб меши (слоты материалов)
			// По этому нужно сначала применить скейлы и квадратную упаковку к LM UV, 
			// а потом комбинировать. Ну и так тупо проще.

			if (!IsLightmapped) {
				Combiner.LogDebug($"Assume is not light map able group. No UV corrections.", CmbMeshRenderer);
				return;
			}

			LightmapFindTableSize();

			for (var i = 0; i < OriginalRenderers.Count; ++i) {
				var mesh_renderer = OriginalRenderers[i];
				LightmapApplyCorrections(mesh_renderer, i);
			}
		}

		protected virtual void GroupSubMeshes(MeshRenderer mesh_renderer, int source_index) {
			if (!GetFilterSafe(mesh_renderer, source_index, out var mesh_filter, out var mesh))
				return;

			var shared_materials = mesh_renderer.sharedMaterials;

			var orig2world = mesh_renderer.transform.localToWorldMatrix;
			var world2container = Combiner.GetContainer().transform.worldToLocalMatrix;
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
				matGroups.Add(new SubMeshGroup(this, GroupIndex, matGroups.Count, material, smi));
			}
		}

		protected virtual void GroupSubMeshes() {
			LogDebug($"Grouping sub meshes of {OriginalRenderers.Count} mesh renderers...");
			for (var i = 0; i < OriginalRenderers.Count; ++i)
				GroupSubMeshes(OriginalRenderers[i], i);
			LogDebug($"Grouped sub meshes to {matGroups.Count} material groups.");
		}

		protected virtual void CombineMaterialGroups() {
			LogDebug($"Combining {matGroups} material groups...");
			var cis = matGroups.Select(smg => smg.CombineInstance()).ToArray();
			CmbMesh.CombineMeshes(cis, false, false, false); // TODO hasLightmapData
			Assert.IsTrue(matGroups.Count == CmbMesh.subMeshCount, $"{matGroups.Count}, {CmbMesh.subMeshCount}");
			MeshUtility.Optimize(CmbMesh);
			CmbMesh.RecalculateBounds();
			CmbMesh.MarkModified();
			EditorUtility.SetDirty(CmbMesh);
			// cmbMesh уже забинджен в MeshFilter
			CmbMeshRenderer.sharedMaterials = matGroups.Select(g => g.material).ToArray();
			CmbMeshRenderer.ResetLocalBounds();
			CmbMeshRenderer.ResetBounds();
			Combiner.LogDebug($"Combined into {CmbMesh.subMeshCount} sub meshes / materials.");
		}

		protected virtual void RemoveOriginals(MeshRenderer mesh_renderer, int source_index) {
			mesh_renderer.enabled = false;

			if (mesh_renderer.TryGetComponent<MeshFilter>(out var mesh_filter) && mesh_filter != null)
				DestroyImmediate(mesh_filter);

			DestroyImmediate(mesh_renderer);
		}

		protected virtual void RemoveOriginals() {
			Combiner.Log($"Removing original mesh renderers...");
			for (var i = 0; i < OriginalRenderers.Count; i++) {
				var orig = OriginalRenderers[i];
				if (orig != null)
					RemoveOriginals(orig, i);
			}
			// OriginalRenderers.Clear();
			// SetDirty();
		}

		protected virtual void DestroyTemp() {
			foreach (var mat_group in matGroups)
				mat_group.DestroyTemp();
			foreach (var tmp in tempObjectsDestroyLater)
				DestroyImmediate(tmp);
			matGroups.Clear();
		}

		public virtual void Process() {
			Log($"Processing combine meshes group \"{gameObject.name}\"...");

			OriginalGameObjects.Clear();
			OriginalGameObjects.AddRange(OriginalRenderers.Distinct()
				.UnityNotNull().Select(r => r.gameObject));
			SetDirty();

			MaxScaleInLightmap = OriginalRenderers.Select(r => r.scaleInLightmap).Max();

			PrepareAndConfigure();
			LightmapApplyCorrections();
			// Лайтмап коррекция должна применяться до группировки, т.к. группы референсят 
			// меш объект и надо что бы они референсили копию с исправленым лайтмапом.
			GroupSubMeshes();
			CombineMaterialGroups();
			RemoveOriginals();
			DestroyTemp();

			// TODO stats
			Log($"Processed combine meshes group \"{gameObject.name}\".");
		}

		[CustomEditor(typeof(MeshCombineGroupMeta), true)]
		public class MeshCombineGroupMetaEditor : KawaEditorBehaviourEditor {
			public override void OnInspectorGUI() {
				EditorGUILayout.HelpBox(
					"This is auto-generated component that only holds data for scripting. You must not create it manually! " +
					"Its properties exposed only for debug/info purposes. You must not edit anything here!",
					MessageType.Warning, true);
				// KawaGUIUtility.HelpBoxRich();
				using (new EditorGUI.DisabledScope(!IKnowWhatIamDoing)) {
					DrawDefaultInspector();
				}
				DebugModeGUI();
				IKnowWhatIamDoingGUI();
			}
		}

	}
}
#endif