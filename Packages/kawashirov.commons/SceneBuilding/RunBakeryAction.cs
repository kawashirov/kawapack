using System;
using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.SceneBuilding {
	public class RunBakeryAction : BaseBuildingAction {
#if UNITY_EDITOR
		public override IEnumerator RunAsync() {
#if BAKERY_INCLUDED
			Debug.Log($"Running bakery...", this);
			var storage = ftRenderLightmap.FindRenderSettingsStorage();
			var bakery = ftRenderLightmap.instance != null ? ftRenderLightmap.instance : new ftRenderLightmap();
			bakery.LoadRenderSettings();
			bakery.unloadScenesInDeferredMode = false;
			bakery.RenderButton(false);
			var loops = 0;
			while (ftRenderLightmap.bakeInProgress) {
				++loops;
				yield return null;
			}
			Debug.Log($"Bakery done: {loops} loops", this);
			yield break;
#else // BAKERY_INCLUDED
			Debug.LogError("Bakery not found", this);
			throw new Exception("Bakery not found");
#endif // BAKERY_INCLUDED
		}
#endif // UNITY_EDITOR
	}
}
