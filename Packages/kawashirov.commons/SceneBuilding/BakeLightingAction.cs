using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.SceneBuilding {
	public class BakeLightingAction : BaseBuildingAction {
#if UNITY_EDITOR
		public override IEnumerator RunAsync() {
			Debug.Log($"Clearing old lightmaps...", this);

			if (Lightmapping.isRunning)
				Lightmapping.Cancel();

			Lightmapping.ClearLightingDataAsset();

			Debug.Log($"Baking new lightmaps...", this);
			Lightmapping.BakeAsync();
			var loops = 0;
			while (Lightmapping.isRunning) {
				++loops;
				yield return null;
			}

			Debug.Log($"Baked new lightmaps: {loops} loops", this);
			yield break;
		}
#endif
	}
}
