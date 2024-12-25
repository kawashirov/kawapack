using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Kawashirov;
using Kawashirov.Refreshables;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Kawashirov {
	[ExecuteAlways]
	public class StaticConstraint : KawaEditorBehaviour {
		public string sourceObjectName = "";
		public bool syncPosition = true;
		public bool syncRotation = true;
		public bool applyToParent = true;
		public Transform applyToTransform = null;

#if UNITY_EDITOR

		[CustomEditor(typeof(StaticConstraint))]
		public class Editor : UnityEditor.Editor {
			public override void OnInspectorGUI() {
				DrawDefaultInspector();
				this.BehaviourRefreshGUI();
			}
		}

		public override void Refresh() {

			if (string.IsNullOrWhiteSpace(sourceObjectName)) {
				LogError("Source Object Name is not set or empty! Can not refresh.");
				return;
			}

			if (!syncPosition && !syncRotation) {
				LogError("Both position and rotation sync disabled. Nothing to sync.");
				return;
			}

			if (applyToParent && transform.parent == null) {
				LogError("Apply to parent is set, but there is no parent.");
				return;
			}

			var source_game_objects = this.gameObject.scene.GetRootGameObjects()
				.SelectMany(KawaUtilities.Traverse).Select(g => g.transform)
				.Where(t => t.name.Equals(sourceObjectName, System.StringComparison.InvariantCultureIgnoreCase)).ToList();

			if (source_game_objects.Count == 0) {
				LogError($"Source Object \"{sourceObjectName}\" not found!");
				return;
			}

			if (source_game_objects.Count > 1) {
				var names = string.Join("\n", source_game_objects.Select(KawaUtilities.KawaGetFullPath));
				LogError($"Multipe Source Objects with name \"{sourceObjectName}\" found: {source_game_objects.Count}\n{names}\n");
				return;
			}

			var controlling = applyToParent ? transform.parent : applyToTransform != null ? applyToTransform : transform;

			if (syncPosition)
				controlling.position = source_game_objects[0].position;

			if (syncRotation)
				controlling.rotation = source_game_objects[0].rotation;

			Log($"Synced \"{controlling}\" to match \"{source_game_objects[0]}\".");
		}

#endif // UNITY_EDITOR
	}
}
