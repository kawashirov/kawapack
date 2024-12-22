#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Kawashirov;

using Object = UnityEngine.Object;

namespace Kawashirov.MeshCombining {
	public class MeshRendererEquality : IEqualityComparer<MeshRenderer> {
		protected readonly MeshCombineOp parent;
		public MeshRendererEquality(MeshCombineOp parent) => this.parent = parent;

		public virtual bool Equals(MeshRenderer x, MeshRenderer y) {
			// FIXME probeAnchor сравнивается по референсу

			if (x == y)
				return true;

			// Базовые универсальные настройки
			if (x.shadowCastingMode != y.shadowCastingMode)
				return false;
			if (x.receiveShadows != y.receiveShadows)
				return false;
			if (x.motionVectorGenerationMode != y.motionVectorGenerationMode)
				return false;

			// Проверка флагов
			var xobj = x.gameObject;
			var yobj = y.gameObject;
			var mask = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
				StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
			var xflags = GameObjectUtility.GetStaticEditorFlags(xobj) & mask;
			var yflags = GameObjectUtility.GetStaticEditorFlags(yobj) & mask;
			if (xflags != yflags)
				return false;
			// Далее xflags тоже самое что и yflags

			var is_gi = (xflags & StaticEditorFlags.ContributeGI) == StaticEditorFlags.ContributeGI;
			if (is_gi) {
				// В режиме ContributeGI имеет значение scaleInLightmap и receiveGI
				if (!parent.ApplyScaleInLightmap && !Mathf.Approximately(x.scaleInLightmap, y.scaleInLightmap))
					return false;
				if (x.receiveGI != y.receiveGI)
					return false;
			}

			var x_uses_lp = is_gi && (x.receiveGI == ReceiveGI.LightProbes) || !is_gi;
			var y_uses_lp = is_gi && (y.receiveGI == ReceiveGI.LightProbes) || !is_gi;
			if (x_uses_lp != y_uses_lp) {
				// x и y используют лайтпробы по-разному.
				return false;
			} else if (x_uses_lp && y_uses_lp) {
				// и x, и y используют лайтпробы, тогда нужно сравнить настройки.
				if (x.lightProbeUsage != y.lightProbeUsage)
					return false;
				if (x.probeAnchor != y.probeAnchor)
					return false;
			}

			var is_batching = (xflags & StaticEditorFlags.BatchingStatic) == StaticEditorFlags.BatchingStatic;
			if (!is_batching) {
				// Когда не BatchingStatic, то имеет значение allowOcclusionWhenDynamic 
				if (x.allowOcclusionWhenDynamic != y.allowOcclusionWhenDynamic)
					return false;
			}

			if (x.reflectionProbeUsage != y.reflectionProbeUsage)
				return false;
			// Далее reflectionProbeUsage одинаковый
			// Если отражения используются, то probeAnchor должен быть probeAnchor одинаковый
			if (x.reflectionProbeUsage != ReflectionProbeUsage.Off && x.probeAnchor != y.probeAnchor)
				return false;

			return true;
		}

		public int GetHashCode(MeshRenderer obj) {
			return obj.GetInstanceID().GetHashCode();
		}
	}

	public readonly struct SubMeshInfo {
		// Самый атомарный элемент - часть меши меш рендерера
		// Здесь нет ничего для отладки т.к. ничего не делает, просто данные.

		public readonly MeshRenderer meshRenderer;
		public readonly Mesh mesh;
		public readonly int subMeshIndex;
		// Матрица для перехода из локальных координат оригинального MeshRenderer в новый целевой.
		public readonly Matrix4x4 transform;

		public SubMeshInfo(MeshRenderer meshRenderer, Mesh mesh, int subMeshIndex, Matrix4x4 transform) {
			this.meshRenderer = meshRenderer;
			this.mesh = mesh;
			this.subMeshIndex = subMeshIndex;
			this.transform = transform;
		}

		public CombineInstance CombineInstance() {
			// Подготовить инфу для комбинирования всех саб мешей одного материала в одну временную
			return new CombineInstance {
				// TODO lightmapScaleOffset
				mesh = mesh,
				// TODO realtimeLightmapScaleOffset
				subMeshIndex = subMeshIndex,
				transform = transform,
			};
		}
	}

	public readonly struct SubMeshGroup {
		// Здесь сообираются вместе все саб меши одного материала всех меш рендереров

		// Эти 4 для логов и отладки
		public readonly string id;
		public readonly int indexMRG;
		public readonly int indexSMG;
		private readonly string logToken;

		public readonly Material material;
		public readonly List<SubMeshInfo> sources;

		// Временная меш, в которую будут скомбинированы все саб меши одного материала,
		// прежде чем будут скомбинированы в одну большую с разными материалами.
		public readonly Mesh tmpMesh;

		public SubMeshGroup(string id, int mrg_index, int smg_index, Material material, SubMeshInfo init) {
			this.id = id;
			indexMRG = mrg_index;
			indexSMG = smg_index;
			logToken = $"MeshCombine {id}/#{mrg_index}/#{smg_index}";
			this.material = material;
			sources = new List<SubMeshInfo>();
			tmpMesh = new Mesh { name = $"Temp_{id}_{mrg_index}_{smg_index}" };
			sources.Add(init);
			tmpMesh.MarkDynamic();
		}

		public Mesh Combine() {
			// Скомбинировать все саб меши одного материала в одну временную
			Debug.Log($"{logToken}: Combining {sources.Count} sub meshes of same material {material} to temporary mesh...");
			var cis = sources.Select(smi => smi.CombineInstance()).ToArray();
			tmpMesh.CombineMeshes(cis, true, true, false); // TODO hasLightmapData
			tmpMesh.RecalculateBounds();
			tmpMesh.MarkModified();
			EditorUtility.SetDirty(tmpMesh);
			var info = tmpMesh.GetSubMesh(0);
			Debug.Log($"{logToken}: Combined {sources.Count} sub meshes of same material {material} to temporary mesh: {info}");
			return tmpMesh;
		}

		public CombineInstance CombineInstance() {
			return new CombineInstance {
				// TODO lightmapScaleOffset
				mesh = Combine(),
				// TODO realtimeLightmapScaleOffset
				subMeshIndex = 0,
				transform = Matrix4x4.identity, // Уже преобразованы
			};
		}

		public void Destroy() {
			if (tmpMesh != null)
				Object.DestroyImmediate(tmpMesh);
			// Поможем мусорщику
			sources.Clear();
		}

	}

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

		public MeshRendererGroup(MeshCombineOp parent, int index_mrg, MeshRenderer init) {
			this.parent = parent;
			indexMRG = index_mrg;
			logToken = $"MeshCombine {parent.ID}/#{index_mrg}";

			sources = new List<MeshRenderer> { init };
			materialGroups = new List<SubMeshGroup>();
			targetMesh = new Mesh { name = $"CombinedGroup_{parent.ID}_{indexMRG}" };
			targetMesh.MarkDynamic();
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
			// TODO copy settings
			Debug.Log($"{logToken}: Configured target MeshRenderer: {gobj.KawaGetFullPath()}", gobj);
		}

		protected virtual void GroupSubMeshes(MeshRenderer mesh_renderer, int source_index) {
			var log_token = $"{logToken}: Source #{source_index}";

			if (mesh_renderer == null) {
				Debug.LogError($"{log_token}: MeshRenderer doesn't exist anymore!", targetMesh);
				return;
			}
			var gobj = mesh_renderer.gameObject;

			var shared_materials = mesh_renderer.sharedMaterials;
			if (shared_materials == null || shared_materials.Length < 1) {
				Debug.LogError($"{log_token}: MeshRenderer have no Materials at {gobj.KawaGetFullPath()}", mesh_renderer);
				return;
			}

			if (!mesh_renderer.TryGetComponent<MeshFilter>(out var mesh_filter) || mesh_filter == null) {
				Debug.LogError($"{log_token}: There is no MeshFilter at {gobj.KawaGetFullPath()}", mesh_renderer);
				return;
			}

			var mesh = mesh_filter.sharedMesh;
			if (mesh == null) {
				Debug.LogError($"{log_token}: MeshFilter have no Mesh at {gobj.KawaGetFullPath()}", mesh_filter);
				return;
			}

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
				var meshRenderer = sources[i];
				GroupSubMeshes(meshRenderer, i);
			}
			Debug.Log($"{logToken}: Grouped sub meshes to {materialGroups.Count} material groups.");
		}

		protected virtual void Combine() {
			Debug.Log($"{logToken}: Combining...");
			var cis = materialGroups.Select(smg => smg.CombineInstance()).ToArray();
			targetMesh.CombineMeshes(cis, false, false, false); // TODO hasLightmapData
			targetMesh.RecalculateBounds();
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
			// Поможем мусорщику
			sources.Clear();
			materialGroups.Clear();
		}

		public virtual void Process() {
			Debug.Log($"{logToken}: Begin processing...");
			CreateTarget();
			ConfigureTarget();
			GroupSubMeshes();
			Combine();
			ApplyMaterialsToTarget();
			RemoveOriginals();
			Destroy();
			Debug.Log($"{logToken}: Processed.");
		}

	}

	public class MeshCombineOp {
		// Уникальный ИД операции, все созданные ассеты будут иметь его в названии.
		public string ID = null;
		private string logToken = null;

		// Возможность кастомного сравнивания MeshRendererов на объединяемость.
		public MeshRendererEquality mre = null;

		// Исходные MeshRendererы которые нужно объеденить.
		public List<MeshRenderer> Sources = new List<MeshRenderer>();

		// Гейм-объект в который будут объеденённые меши.
		public GameObject Target = null;

		// false - рендереры с разным scaleInLightmap считаются не комбинируемыми
		// true - рендереры с разным scaleInLightmap кобминируются и UV1 корректируется на это масштаб
		// Может быть переопределено в кастомном MeshRendererEquality,
		// но корректировка будет применяться как указано тут.
		public bool ApplyScaleInLightmap = false;

		/* internals */

		protected List<MeshRendererGroup> groups = new List<MeshRendererGroup>();

		protected virtual void GroupMeshRenderers() {
			Debug.Log($"{logToken}: Trying to group {Sources.Count} source MeshRenderers...");
			groups.Clear();

			var sources = Sources.Distinct().UnityNotNull().ToList();
			if (Sources.Count != sources.Count) {
				Debug.LogWarning($"{logToken}: Sources reduced {Sources.Count} -> {sources.Count}, there is might be empty/dupe elements.");
			}

			foreach (var mr in sources) {
				// Тут по сути сложность N^3, но я не думаю что будет много мешей.
				var matched = groups.Where(g => g.Match(mr)).ToList();
				if (matched.Count == 0) {
					groups.Add(new MeshRendererGroup(this, groups.Count, mr));
				} else if (matched.Count == 1) {
					matched[0].sources.Add(mr);
				} else {
					// TODO подробнее
					var mr_str = mr.gameObject.KawaGetFullPath();
					var msg = $"{logToken}: Matches {mr_str} with {matched.Count} groups, but must be one or nothing.";
					Debug.LogError(msg, mr);
					throw new Exception(msg);
				}
			}

			Debug.Log($"{logToken}: Created {groups.Count} groups from {sources.Count} source MeshRenderers.");
			/*
			for (var i = 0; i < groups.Count; ++i) {
				var group = groups[i];
				var items = string.Join("\n", group.sources.Select(mr => mr.gameObject.KawaGetFullPath()));
				Debug.Log($"{logToken}: Group #{i}:\n{items}");
				// TODO убрать это
			}
			*/
		}

		public virtual void Run() {
			mre ??= new MeshRendererEquality(this);

			ID ??= new System.Random().Next().ToString();

			logToken = $"MeshCombine {ID}";

			GroupMeshRenderers();

			foreach (var group in groups)
				group.Process();
		}

	}
}
#endif
