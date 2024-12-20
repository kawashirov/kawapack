using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.SceneBuilding {
#if UNITY_EDITOR
	public class DeleteObjectsAction : BaseBuildingAction {
		// Этот класс просто тест для proof of concept и не имеет смысла

		public GameObject[] GameObjects;

		public override void Run() {
			for (var i = 0; i < GameObjects.Length; ++i) {
				var gobj = GameObjects[i];
				if (gobj == null || gobj.scene != gameObject.scene)
					continue; // TODO Errors
				DestroyImmediate(gobj);
			}
		}
#endif
	}
}
