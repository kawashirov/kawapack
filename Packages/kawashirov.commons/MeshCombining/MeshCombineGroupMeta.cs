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
		protected List<RendererInfo> renderers = new List<RendererInfo>();
		protected readonly List<SubMeshGroup> matGroups = new List<SubMeshGroup>();


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

		protected virtual void LightmapApplyCorrections_TableGrid() {
			var islands = renderers
				.Where(r => r.lightmapUV.Count > 0 && r.lightmapIsland.IsFinite())
				.ToList();
			if (islands.Count < 1)
				return;
			// Пока что используем простой алгоритм упаковки.
			// Разбиваем UV квадрат на сетку из квадратиков и каждую меш переносим в него.
			var table_size = Mathf.FloorToInt(Mathf.Sqrt(islands.Count));
			while (table_size * table_size < islands.Count)
				++table_size;
			Log($"Apply Lightmap UV table grid correction of size {table_size} for {islands.Count} islands...");
			LightmapTableSize = table_size;
			for (var i = 0; i < islands.Count; ++i) {
				var r = islands[i];
				var gx = i % table_size;
				var gy = i / table_size;
				var cell_size = 1f / table_size;
				var cell_pad = cell_size * Combiner.LightmapPadding / 2;
				var cell_island = new UVIsland(
					gx * cell_size + cell_pad,
					gy * cell_size + cell_pad,
					(gx + 1) * cell_size - cell_pad,
					(gy + 1) * cell_size - cell_pad);
				var orig_island = r.lightmapIsland.ExpandToSquare();
				Log($"{r.logToken}: TableGrid ({gx}, {gy}): {orig_island} -> {cell_island}", r.meshFilter);
				var uvs = r.lightmapUV;
				for (var j = 0; j < uvs.Count; j++) {
					var uv = uvs[j];
					if (!float.IsFinite(uv.x) || !float.IsFinite(uv.y))
						continue;
					uv = orig_island.InverseLerp(uv);
					uv = cell_island.Lerp(uv);
					Assert.IsTrue(float.IsFinite(uv.x) && float.IsFinite(uv.y), $"{r.logToken}: i={i}, j={j}: {uvs[j]} -> {uv}");
					uvs[j] = uv;
				}
			}
			Log($"Applied Lightmap UV table grid correction of size {table_size} for {islands.Count} islands.");
		}

		protected virtual void LightmapApplyCorrections_Repack() {
			var islands = renderers
				.Where(r => r.lightmapUV.Count > 0 && r.lightmapIsland.IsFinite())
				.ToList();
		}

		protected virtual void LightmapApplyCorrections() {
			// Мы не можем применять коррекцию LM UV во время комбинирования, 
			// т.к. UV это свойство всей меши, а мы комбайним саб меши (слоты материалов)
			// По этому нужно сначала применить скейлы и квадратную упаковку к LM UV, 
			// а потом комбинировать. Ну и так тупо проще.

			if (!IsLightmapped) {
				LogDebug($"Assume is not light map able group. No UV corrections.", CmbMeshRenderer);
				return;
			}

			if (Combiner.LightmapUVCorrection == LightmapCorrectionMode.Disabled) {
				LogDebug($"{nameof(Combiner.LightmapUVCorrection)} is Disabled. No UV corrections.", CmbMeshRenderer);
				return;
			}

			MaxScaleInLightmap = OriginalRenderers.Select(r => r.scaleInLightmap).Max();

			foreach (var info in renderers) {
				info.LightmapUVPrepare();
			}

			if (Combiner.LightmapUVCorrection == LightmapCorrectionMode.Grid) {
				LightmapApplyCorrections_TableGrid();
			} else if (Combiner.LightmapUVCorrection == LightmapCorrectionMode.Repack) {
				LightmapApplyCorrections_Repack();
			}

			foreach (var info in renderers) {
				info.LightmapUVApply();
			}
		}

		protected virtual void GroupSubMeshes(RendererInfo info) {
			var mesh = info.GetMeshForCombining();
			if (mesh == null || info.renderer == null)
				return;
			var shared_materials = info.renderer.sharedMaterials;

			var orig2world = info.renderer.transform.localToWorldMatrix;
			var world2container = Combiner.GetContainer().transform.worldToLocalMatrix;
			var matrix = world2container * orig2world;

			var n = Math.Min(mesh.subMeshCount, shared_materials.Length);
			for (var i = 0; i < n; i++) {
				var material = shared_materials[i];
				var smi = new SubMeshInfo(info.renderer, mesh, i, matrix);
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
			LogDebug($"Grouping sub meshes of {renderers.Count} mesh renderers...");
			foreach (var renderer in renderers)
				GroupSubMeshes(renderer);
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
			LogDebug($"Combined into {CmbMesh.subMeshCount} sub meshes / materials.");
		}

		protected virtual void RemoveOriginals() {
			LogDebug($"Removing original mesh renderers...");
			foreach (var info in renderers)
				info.DestroyOriginals();
			// OriginalRenderers.Clear();
			// SetDirty();
			LogDebug($"Removed original mesh renderers.");
		}

		protected virtual void DestroyTemp() {
			foreach (var mat_group in matGroups)
				mat_group.DestroyTemp();
			foreach (var info in renderers)
				info.DestroyModified();
			matGroups.Clear();
		}

		public virtual void Process() {
			Log($"Processing combine meshes group \"{gameObject.name}\"...");

			renderers.Clear();
			var renderers_filter = OriginalRenderers.Distinct().UnityNotNull().Select((x, i) => (x, i));
			foreach (var (renderer, i) in renderers_filter) {
				var info = new RendererInfo(this, i, renderer);
				renderers.Add(info);
				info.Init();
			}

			OriginalGameObjects.Clear();
			OriginalGameObjects.AddRange(OriginalRenderers.Distinct()
				.UnityNotNull().Select(r => r.gameObject));
			SetDirty();

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
				IKnowWhatIamDoingGUI();
				DebugModeGUI();
				EditorGUILayout.HelpBox(
					"This is auto-generated component that only holds data for scripting. You must not create it manually! " +
					"Its properties exposed only for debug/info purposes. You must not edit anything here!",
					MessageType.Warning, true);
				// KawaGUIUtility.HelpBoxRich();
				using (new EditorGUI.DisabledScope(!IKnowWhatIamDoing)) {
					DrawDefaultInspector();
				}
			}
		}

	}
}
#endif