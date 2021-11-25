using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.LightProbeProxyVolume;

#if UNITY_EDITOR
using UnityEditor; 
#endif

namespace Kawashirov {
	[RequireComponent(typeof(LightProbeProxyVolume))]
	public class LightProbeProxyVolumeGenerator : BaseLightProbeGenerator {

#if UNITY_EDITOR

		protected override Bounds GetBounds() {
			var lppv = gameObject.GetComponent<LightProbeProxyVolume>();
			if (lppv == null) {
				Debug.LogErrorFormat(this, "[KawaLPG] <b>{0}</b> does not have LightProbeProxyVolume, can not get bounds!", kawaHierarchyPath);
				throw new NullReferenceException(string.Format("{0} does not have LightProbeProxyVolume, can not get bounds!", kawaHierarchyPath));
			}
			return lppv.boundsGlobal;
		}

		protected override void OnDrawGizmosSelected() {
			base.OnDrawGizmosSelected();
		}

		public float[] MakeGridFixedCount(float left, float right, int count, ProbePositionMode mode) {
			if (count < 2)
				return new float[1] { Mathf.Lerp(left, right, 0.5f) };

			var result = new float[count];

			if (mode == ProbePositionMode.CellCorner) {
				for (var i = 0; i < count; ++i)
					result[i] = Mathf.Lerp(left, right, 1.0f * i / (count - 1));
			} else // ProbePositionMode.CellCenter
				{
				for (var i = 0; i < count; ++i)
					result[i] = Mathf.Lerp(left, right, (0.5f + i) / count);
			}
			return result;
		}

		public float[] MakeGridDensity(float left, float right, float density, ProbePositionMode mode) {
			// TODO more tests and wrold vs local scaling
			var count = Mathf.CeilToInt(Mathf.Abs(left - right) / density);
			return MakeGridFixedCount(left, right, count, mode);
		}

		public override void Refresh() {
			var lppv = gameObject.GetOrAddComponent<LightProbeProxyVolume>();

			if (lppv.resolutionMode == ResolutionMode.Automatic) {
				Debug.LogErrorFormat(this, "[Kawa-LPG-LPPV] ResolutionMode=Automatic is not supported yet! @ <i>{0}</i>", kawaHierarchyPath);
				return;
			}

			if (lppv.boundingBoxMode == BoundingBoxMode.AutomaticLocal) {
				Debug.LogErrorFormat(this, "[Kawa-LPG-LPPV] BoundingBoxMode=AutomaticLocal is not supported yet! @ <i>{0}</i>", kawaHierarchyPath);
				return;
			}

			var bounds = lppv.boundingBoxMode == BoundingBoxMode.AutomaticWorld ? lppv.boundsGlobal : new Bounds(lppv.originCustom, lppv.sizeCustom);

			var points_x = MakeGridFixedCount(bounds.min.x, bounds.max.x, lppv.gridResolutionX, lppv.probePositionMode);
			var points_y = MakeGridFixedCount(bounds.min.y, bounds.max.y, lppv.gridResolutionY, lppv.probePositionMode);
			var points_z = MakeGridFixedCount(bounds.min.z, bounds.max.z, lppv.gridResolutionZ, lppv.probePositionMode);
			var points = new List<Vector3>(points_x.Length * points_y.Length * points_z.Length);
			foreach (var px in points_x)
				foreach (var py in points_y)
					foreach (var pz in points_z)
						points.Add(new Vector3(px, py, pz));

			if (lppv.boundingBoxMode == BoundingBoxMode.AutomaticWorld) {
				// world to local space
				for (var i = 0; i < points.Count; ++i)
					points[i] = transform.InverseTransformPoint(points[i]);
			}

			var lpg = gameObject.GetOrAddComponent<LightProbeGroup>();
			lpg.probePositions = points.ToArray();
			Debug.LogFormat(this, "[Kawa-LPG-LPPV] Placed <b>{1}</b> probes. @ <i>{0}</i>", kawaHierarchyPath, points.Count);
		}


#endif
	}
}
