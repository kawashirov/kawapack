#if UNITY_EDITOR
using System;
using System.Collections;

namespace Kawashirov.SceneBuilding {
	public class RunBakerySBA : BaseSBA {
		public override IEnumerator RunAsync(BuildingScenario scenario) {
#if BAKERY_INCLUDED
			Log($"Running bakery...");
			var storage = ftRenderLightmap.FindRenderSettingsStorage();
			var bakery = ftRenderLightmap.instance != null ? ftRenderLightmap.instance : new ftRenderLightmap();
			bakery.LoadRenderSettings();
			bakery.unloadScenesInDeferredMode = false;
			bakery.RenderButton(false);
			yield return null;
			var loops = 0;
			while (ftRenderLightmap.bakeInProgress) {
				++loops;
				yield return null;
			}
			Log($"Bakery done: {loops} loops");
			yield break;
#else // BAKERY_INCLUDED
			ThrowException(new Exception("Bakery not found"));
			yield break;
#endif // BAKERY_INCLUDED
		}
	}
}
#endif // UNITY_EDITOR
