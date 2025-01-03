#if UNITY_EDITOR
using System.Collections;
using UnityEditor;

namespace Kawashirov.SceneBuilding {
	public class BakeOcclusionSBA : BaseSBA {
		public override IEnumerator RunAsync(BuildingScenario scenario) {
			Log($"Clearing old occlusion culling...");

			if (StaticOcclusionCulling.isRunning) {
				StaticOcclusionCulling.Cancel();
				yield return null;
			}

			StaticOcclusionCulling.Clear();
			yield return null;

			Log($"Computing new occlusion culling...");
			StaticOcclusionCulling.GenerateInBackground();
			yield return null;
			var loops = 0;
			while (StaticOcclusionCulling.isRunning) {
				++loops;
				yield return null;
			}

			Log($"Computed new occlusion: {loops} loops, {StaticOcclusionCulling.umbraDataSize} bytes");
		}
	}
}
#endif
