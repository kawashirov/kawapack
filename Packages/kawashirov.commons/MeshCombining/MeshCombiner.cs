#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace Kawashirov.MeshCombining {
	[DisallowMultipleComponent]
	public class MeshCombiner : KawaEditorBehaviour {
		public static readonly StaticEditorFlags STATIC_CHECK_MASK = StaticEditorFlags.OccluderStatic |
			StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
		public static readonly StaticEditorFlags STATIC_EQ_MASK = StaticEditorFlags.ContributeGI | STATIC_CHECK_MASK;

		[Tooltip("Where on scene search renderers to combine.")]
		public List<GameObject> Hierarchy = new List<GameObject>();
		public bool IgnoreEditorOnly = true;
		public bool IgnoreDisabled = true;
		public bool IgnoreDynamic = true;
		public bool IgnoreNonLightmapped = true;

		[Space]
		// Применять или нет базовую перепаковку второго UV слоая, если он есть. 
		// Перепаковка имеет базовую, не очень эффективную реализацию, 
		// но необходима для корректной работы лайтмап на объединённой меши.
		public LightmapCorrectionMode LightmapUVCorrection = LightmapCorrectionMode.GenerateAtlas;

		// false - рендереры с разным scaleInLightmap считаются не комбинируемыми
		// true - рендереры с разным scaleInLightmap кобминируются и UV1 корректируется на это масштаб
		// Может быть переопределено в кастомном MeshRendererEquality,
		// но корректировка будет применяться как указано тут.
		public bool ApplyScaleInLightmap = true;
		public float LightmapPadding = 0.1f;

		[Space]
		// Гейм-объект в который будут объеденённые меши.
		public GameObject Container = null;

		[Header("Properties below are auto-generated")]
		// Исходные MeshRendererы которые нужно объеденить.
		public List<MeshRenderer> OriginalRenderers = new List<MeshRenderer>();
		public bool OriginalRenderersResolved = false;
		public List<MeshCombineGroupMeta> CombineGroups = new List<MeshCombineGroupMeta>();

		/* internals */

		protected virtual void Init() {
			// TODO
		}

		public GameObject GetContainer() => Container != null ? Container : gameObject;

		public virtual bool IsLightmapped(MeshRenderer mr) {
			// Меш считается лайтмапируемым, если выполнены все условия:
			// - Установлен флаг ContributeGI (aka LightmapStatic)
			// - receiveGI == Lightmaps
			// - scaleInLightmap > 0

			var gobj = mr.gameObject;
			if (!GameObjectUtility.AreStaticEditorFlagsSet(gobj, StaticEditorFlags.ContributeGI))
				return false;

			if (mr.receiveGI != ReceiveGI.Lightmaps)
				return false;

			if (mr.scaleInLightmap <= 0)
				return false;

			return true;
		}

		public virtual bool IsStatic(MeshRenderer mr) {
			// Меш считается статической, если есть хотя бы один из признаков:
			// - любой из флагов STATIC_CHECK_MASK
			// - IsLightmapped
			var flags = GameObjectUtility.GetStaticEditorFlags(mr.gameObject);
			return (flags & STATIC_CHECK_MASK) != 0 || IsLightmapped(mr);
		}

		protected virtual bool EnsureNoOther() {
			var others = gameObject.GetComponents<MeshCombiner>().Count(c => c != this);
			if (others == 0)
				return true;
			LogError($"There's more than one mesh combiner on same game object \"{gameObject.name}\". " +
				"This is not allowed as it complicates hierarchy resolving.");
			return false;
		}

		protected virtual void ResetHierarchy() {
			OriginalRenderers.Clear();
			OriginalRenderersResolved = false;
			SetDirty();
		}

		protected virtual List<MeshRenderer> ResolveHierarchy() {
			// Рекурсивная!
			if (OriginalRenderersResolved)
				return OriginalRenderers;

			LogDebug($"Looking for original mesh renderers for {gameObject.name}...");
			OriginalRenderers.Clear();

			if (!EnsureNoOther()) {
				OriginalRenderersResolved = true;
				return OriginalRenderers;
			}

			IEnumerable<MeshRenderer> hrs = null;
			IEnumerable<MeshCombiner> sub = null;
			if (Hierarchy == null || Hierarchy.Count < 1) {
				// При пустом Hierarchy ищем в самом себе
				hrs = gameObject.GetComponentsInChildren<MeshRenderer>(!IgnoreDisabled);
				sub = gameObject.GetComponentsInChildren<MeshCombiner>(true);
			} else {
				var gobjs = Hierarchy.UnityNotNull().Distinct().ToList();
				hrs = gobjs.SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(!IgnoreDisabled)).Distinct();
				sub = gobjs.SelectMany(g => g.GetComponentsInChildren<MeshCombiner>(true)).Distinct();
			}

			hrs = hrs.ToList();
			LogDebug($"Found {hrs.Count()} potential mesh renderers before filtering for {gameObject.name}.");

			// Не включать в этот меш комбайнер рендереры от других меш комбайнеров внутри иерархии.
			var subr = sub.Where(c => c != this).SelectMany(c => c.ResolveHierarchy()).Distinct().ToHashSet();
			LogDebug($"Found {subr.Count} mesh renderers to exclude from {gameObject.name}.");
			hrs = hrs.Except(subr);

			if (IgnoreEditorOnly)
				hrs = hrs.RuntimeOnly();
			if (IgnoreDisabled)
				hrs = hrs.Where(r => r.enabled && r.gameObject.activeInHierarchy);
			if (IgnoreDynamic)
				hrs = hrs.Where(r => IsStatic(r));
			if (IgnoreNonLightmapped)
				hrs = hrs.Where(r => IsLightmapped(r));

			OriginalRenderers.Clear();
			OriginalRenderers.AddRange(hrs);
			OriginalRenderersResolved = true;
			LogDebug($"Found {OriginalRenderers.Count} mesh renderers for {gameObject.name}.");
			return OriginalRenderers;
		}

		protected virtual bool DiffBasic(MeshRenderer x, MeshRenderer y) {
			// Базовые универсальные настройки
			// FIXME probeAnchor сравнивается по референсу
			if (x.shadowCastingMode != y.shadowCastingMode)
				return true;
			if (x.receiveShadows != y.receiveShadows)
				return true;
			if (x.motionVectorGenerationMode != y.motionVectorGenerationMode)
				return true;
			if (x.reflectionProbeUsage != y.reflectionProbeUsage)
				return true;
			// Далее reflectionProbeUsage одинаковый
			// Если отражения используются, то probeAnchor должен быть одинаковый
			if (x.reflectionProbeUsage != ReflectionProbeUsage.Off && x.probeAnchor != y.probeAnchor)
				return true;
			return false;
		}

		protected virtual bool DiffStaticFlags(MeshRenderer x, MeshRenderer y, out StaticEditorFlags flags) {
			// Проверка статических флагов
			var xobj = x.gameObject;
			var yobj = y.gameObject;
			var xflags = GameObjectUtility.GetStaticEditorFlags(xobj) & STATIC_EQ_MASK;
			var yflags = GameObjectUtility.GetStaticEditorFlags(yobj) & STATIC_EQ_MASK;
			flags = xflags;
			return xflags != yflags;
		}

		protected virtual bool DiffGI(MeshRenderer x, MeshRenderer y, StaticEditorFlags flags) {
			var is_gi = (flags & StaticEditorFlags.ContributeGI) != 0;
			if (is_gi) {
				// В режиме ContributeGI имеет значение scaleInLightmap и receiveGI
				if (!ApplyScaleInLightmap && !Mathf.Approximately(x.scaleInLightmap, y.scaleInLightmap))
					return true;
				if (x.receiveGI != y.receiveGI)
					return true;
			}

			var x_uses_lp = is_gi && (x.receiveGI == ReceiveGI.LightProbes) || !is_gi;
			var y_uses_lp = is_gi && (y.receiveGI == ReceiveGI.LightProbes) || !is_gi;
			if (x_uses_lp != y_uses_lp) {
				// x и y используют лайтпробы по-разному.
				return true;
			} else if (x_uses_lp && y_uses_lp) {
				// и x, и y используют лайтпробы, тогда нужно сравнить настройки.
				if (x.lightProbeUsage != y.lightProbeUsage)
					return true;
				if (x.probeAnchor != y.probeAnchor)
					return true;
			}
			return false;
		}


		protected virtual bool DiffDynamicOcclusion(MeshRenderer x, MeshRenderer y, StaticEditorFlags flags) {
			if ((flags & StaticEditorFlags.BatchingStatic) != 0)
				return false;
			// Когда не BatchingStatic, то имеет значение allowOcclusionWhenDynamic 
			return x.allowOcclusionWhenDynamic != y.allowOcclusionWhenDynamic;
		}

		public virtual bool IsSimilar(MeshRenderer x, MeshRenderer y) {
			if (x == y)
				return true;
			if (DiffBasic(x, y))
				return false;
			if (DiffStaticFlags(x, y, out var flags))
				return false;
			if (DiffGI(x, y, flags))
				return false;
			if (DiffDynamicOcclusion(x, y, flags))
				return false;

			return true;
		}

		protected virtual MeshCombineGroupMeta CreateNewGroup() {
			var index_mrg_new = CombineGroups.Count;
			LogDebug($"Creating new mesh renderers group №{index_mrg_new}...");
			var gobj = new GameObject($"CmbGroup_{index_mrg_new}");

			var transform = gobj.transform;
			transform.SetParent(GetContainer().transform, false);
			transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			transform.localScale = Vector3.one;

			var group_new = gobj.AddComponent<MeshCombineGroupMeta>();
			group_new.debugMode = debugMode;
			group_new.Combiner = this;
			group_new.GroupIndex = index_mrg_new;
			group_new.Init();
			CombineGroups.Add(group_new);
			SetDirty();
			LogDebug($"Created new mesh renderers group {gobj}...", group_new);
			return group_new;
		}

		protected virtual void GroupMeshRenderers() {
			LogDebug($"Trying to group {OriginalRenderers.Count} mesh renderers...");
			CombineGroups.Clear();
			SetDirty();
			foreach (var mr in OriginalRenderers) {
				// Тут по сути сложность N^3, но я не думаю что будет много мешей.
				var matched = CombineGroups.Where(g => g.Match(mr)).ToList();
				if (matched.Count == 0) {
					var group = CreateNewGroup();
					group.Add(mr);
				} else if (matched.Count == 1) {
					matched[0].Add(mr);
				} else {
					// TODO подробнее
					ThrowException(new Exception(
						$"Matches {mr} with {matched.Count} groups, but must be one or nothing."), mr);
				}
			}
			Assert.IsNotNull(CombineGroups);
			Assert.IsTrue(CombineGroups.Count > 0);

			var groups_s = new List<string>(CombineGroups.Count);
			for (var i = 0; i < CombineGroups.Count; ++i) {
				var group = CombineGroups[i];
				var count = group.OriginalRenderers.Count;
				var items = string.Join("\n", group.OriginalRenderers.Select(mr => mr.gameObject.KawaGetFullPath()));
				groups_s.Add($"Group №{i} ({count} items):\n{items}");
			}
			Log($"Created {groups_s.Count} groups from {OriginalRenderers.Count} original mesh renderers:\n" +
				string.Join("\n\n", groups_s));
		}

		public virtual IEnumerator Run() {
			Init();

			ResolveHierarchy();
			if (OriginalRenderers.Count < 1) {
				LogWarning($"No mesh renderers found to combine!");
				yield break;
			}
			yield return null;

			GroupMeshRenderers();
			yield return null;

			LogDebug($"Processing {CombineGroups.Count} combine meshes groups...");
			foreach (var group in CombineGroups) {
				try {
					group.Process();
				} catch (Exception exc) {
					LogException($"Failed to process combine meshes group {group}", exc, group);
					throw exc;
				}
				yield return null;
			}
			Log($"Processed {CombineGroups.Count} combine meshes groups. Done.");
		}

	}
}
#endif
