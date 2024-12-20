using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.SceneBuilding {
#if UNITY_EDITOR
	public class BakeOcclusionAction : BaseBuildingAction {
		public override void Run() {
			Debug.Log($"Clearing old occlusion culling...", this);
			StaticOcclusionCulling.Clear();
			Debug.Log($"Computing new occlusion culling...", this);
			StaticOcclusionCulling.Compute();
			Debug.Log($"Computed new occlusion.", this);
		}
#endif
	}
}
