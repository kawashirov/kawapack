#if UNITY_EDITOR
using UnityEngine;

namespace Kawashirov.SceneBuilding {
	public class DeleteObjectsDemoSBA : BaseSBA {
		// Этот класс просто тест для proof of concept и не имеет смысла
		public GameObject[] GameObjects;

		public override void RunSync(BuildingScenario scenario) {
			for (var i = 0; i < GameObjects.Length; ++i) {
				var gobj = GameObjects[i];
				if (gobj == null || gobj.scene != gameObject.scene)
					continue; // TODO Errors
				DestroyImmediate(gobj);
			}
		}
	}
}
#endif
