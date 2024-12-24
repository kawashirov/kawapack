using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.SceneBuilding {
	public class BakeOcclusionAction : BaseBuildingAction {
#if UNITY_EDITOR
		public override IEnumerator RunAsync() {
			Debug.Log($"Clearing old occlusion culling...", this);

			if (StaticOcclusionCulling.isRunning)
				StaticOcclusionCulling.Cancel();

			StaticOcclusionCulling.Clear();

			Debug.Log($"Computing new occlusion culling...", this);
			StaticOcclusionCulling.GenerateInBackground();
			var loops = 0;
			while (StaticOcclusionCulling.isRunning) {
				++loops;
				yield return null;
			}

			Debug.Log($"Computed new occlusion: {loops} loops, {StaticOcclusionCulling.umbraDataSize} bytes", this);
			yield break;
		}
#endif
	}
}
