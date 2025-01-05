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
		public GameObject[] Hierarchy;
		public bool IgnoreEditorOnly = true;
		public bool IgnoreDisabled = true;
		public bool IgnoreDynamic = true;
		public bool IgnoreNonLightmapped = true;

		[Space]
		// Гейм-объект в который будут объеденённые меши.
		public GameObject Container = null;

		[Space]
		// Применять или нет базовую перепаковку второго UV слоая, если он есть. 
		// Перепаковка имеет базовую, не очень эффективную реализацию, 
		// но необходима для корректной работы лайтмап на объединённой меши.
		public bool RepackLightmapUV = true;

		// false - рендереры с разным scaleInLightmap считаются не комбинируемыми
		// true - рендереры с разным scaleInLightmap кобминируются и UV1 корректируется на это масштаб
		// Может быть переопределено в кастомном MeshRendererEquality,
		// но корректировка будет применяться как указано тут.
		public bool ApplyScaleInLightmap = true;

		/* internals */

		// Исходные MeshRendererы которые нужно объеденить.
		public readonly List<MeshRenderer> originals = new List<MeshRenderer>();
		protected bool originalsResolved = false;

		protected List<MeshRendererGroup> groups = new List<MeshRendererGroup>();

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
			originals.Clear();
			originalsResolved = false;
		}

		protected virtual List<MeshRenderer> ResolveHierarchy() {
			// Рекурсивная!
			if (originalsResolved)
				return originals;

			LogDebug($"Looking for original mesh renderers for {gameObject.name}...");
			originals.Clear();

			if (!EnsureNoOther()) {
				originalsResolved = true;
				return originals;
			}

			IEnumerable<MeshRenderer> hrs = null;
			IEnumerable<MeshCombiner> sub = null;
			if (Hierarchy == null || Hierarchy.Length < 1) {
				// При пустом Hierarchy ищем в самом себе
				hrs = gameObject.GetComponentsInChildren<MeshRenderer>(!IgnoreDisabled);
				sub = gameObject.GetComponentsInChildren<MeshCombiner>(true);
			} else {
				hrs = Hierarchy.SelectMany(g => gameObject.GetComponentsInChildren<MeshRenderer>(!IgnoreDisabled)).Distinct();
				sub = Hierarchy.SelectMany(g => gameObject.GetComponentsInChildren<MeshCombiner>(true)).Distinct();
			}

			// Не включать в этот меш комбайнер рендереры от других меш комбайнеров внутри иерархии.
			var subr = sub.Where(c => c != this).SelectMany(c => c.ResolveHierarchy()).Distinct().ToHashSet();
			hrs = hrs.Except(subr);

			if (IgnoreEditorOnly)
				hrs = hrs.RuntimeOnly();
			if (IgnoreDisabled)
				hrs = hrs.Where(r => r.enabled && r.gameObject.activeInHierarchy);
			if (IgnoreDynamic)
				hrs = hrs.Where(r => IsStatic(r));
			if (IgnoreNonLightmapped)
				hrs = hrs.Where(r => IsLightmapped(r));

			originals.Clear();
			originals.AddRange(hrs);
			originalsResolved = true;
			LogDebug($"Found {originals.Count} mesh renderers for {gameObject.name}.");
			return originals;
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

		protected virtual void GroupMeshRenderers() {
			LogDebug($"Trying to group {originals.Count} mesh renderers...");
			groups.Clear();
			foreach (var mr in originals) {
				// Тут по сути сложность N^3, но я не думаю что будет много мешей.
				var matched = groups.Where(g => g.Match(mr)).ToList();
				if (matched.Count == 0) {
					var group = new MeshRendererGroup(this, groups.Count);
					groups.Add(group);
					group.Add(mr);
				} else if (matched.Count == 1) {
					matched[0].Add(mr);
				} else {
					// TODO подробнее
					ThrowException(new Exception(
						$"Matches {mr} with {matched.Count} groups, but must be one or nothing."), mr);
				}
			}
			Assert.IsTrue(groups.Count > 0);

			var groups_s = new List<string>(groups.Count);
			for (var i = 0; i < groups.Count; ++i) {
				var group = groups[i];
				var items = string.Join("\n", group.originals.Select(mr => mr.gameObject.KawaGetFullPath()));
				groups_s.Add($"Group №{i}:\n{items}");
			}
			Log($"Created {groups.Count} groups from {originals.Count} original mesh renderers:\n" +
				string.Join("\n\n", groups_s));
		}

		public virtual IEnumerator Run() {
			Init();

			ResolveHierarchy();
			if (originals.Count < 1) {
				LogWarning($"No mesh renderers found to combine!");
				yield break;
			}
			yield return null;

			GroupMeshRenderers();
			yield return null;

			foreach (var group in groups) {
				group.Process();
				yield return null;
			}
		}

	}
}
#endif
