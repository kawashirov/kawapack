#if UNITY_EDITOR
using System.Collections;
using UnityEditor;

namespace Kawashirov.SceneBuilding {
	public class BakeLightingSBA : BaseSBA {
		public override IEnumerator RunAsync(BuildingScenario scenario) {
			Log($"Clearing old lightmaps...");

			if (Lightmapping.isRunning) {
				Lightmapping.Cancel();
				yield return null;
			}

			Lightmapping.ClearLightingDataAsset();
			yield return null;

			Log($"Baking new lightmaps...");
			Lightmapping.BakeAsync();
			yield return null;
			var loops = 0;
			while (Lightmapping.isRunning) {
				++loops;
				yield return null;
			}

			Log($"Baked new lightmaps: {loops} loops");
		}
	}
}
#endif
