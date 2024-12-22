#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kawashirov.MeshCombining {
	public class MeshRendererEquality : IEqualityComparer<MeshRenderer> {
		public static readonly StaticEditorFlags MASK = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
			StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;

		protected readonly MeshCombineOp parent;
		public MeshRendererEquality(MeshCombineOp parent) => this.parent = parent;

		public virtual bool IsLightmapped(MeshRenderer mesh_renderer) {
			// Меш считается лайтмапируемым, если выполнены все условия:
			// - Установлен флаг ContributeGI (aka LightmapStatic)
			// - receiveGI == Lightmaps
			// - scaleInLightmap > 0

			var gobj = mesh_renderer.gameObject;
			if (!GameObjectUtility.AreStaticEditorFlagsSet(gobj, StaticEditorFlags.ContributeGI))
				return false;

			if (mesh_renderer.receiveGI != ReceiveGI.Lightmaps)
				return false;

			if (mesh_renderer.scaleInLightmap <= 0)
				return false;

			return true;
		}

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
			var xflags = GameObjectUtility.GetStaticEditorFlags(xobj) & MASK;
			var yflags = GameObjectUtility.GetStaticEditorFlags(yobj) & MASK;
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
}
#endif