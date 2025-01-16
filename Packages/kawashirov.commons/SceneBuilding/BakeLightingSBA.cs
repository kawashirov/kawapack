#if UNITY_EDITOR
using System.Collections;
using UnityEditor;

namespace Kawashirov.SceneBuilding {
	public class BakeLightingSBA : BaseSBA {
		protected bool bakeRegistered = false;
		protected bool bakeCompleted = false;

		protected void OnBakeCompleted() {
			bakeCompleted = true;
		}

		protected void EnsureBakeCompletedReg() {
			if (!bakeRegistered)
				Lightmapping.bakeCompleted += OnBakeCompleted;
		}

		public override IEnumerator RunAsync(BuildingScenario scenario) {
			Log($"Clearing old lightmaps...");

			while (Lightmapping.isRunning) {
				Lightmapping.Cancel();
				yield return null;
			}

			Lightmapping.ClearLightingDataAsset();
			yield return null;

			EnsureBakeCompletedReg();
			bakeCompleted = false;
			var loops = 0;
			Log($"Baking new lightmaps...");
			Lightmapping.BakeAsync();
			yield return null;
			while (Lightmapping.isRunning || !bakeCompleted) {
				++loops;
				yield return null;
			}
			bakeCompleted = false;

			Log($"Baked new lightmaps: {loops} loops");
		}
	}
}
#endif
