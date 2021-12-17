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
				Debug.LogErrorFormat(this, "[KawaEditor] Source Object Name is not set or empty! Can not refresh. @ <i>{0}</i>", kawaHierarchyPath);
				return;
			}

			if (!syncPosition && !syncRotation) {
				Debug.LogErrorFormat(this, "[KawaEditor] Both position and rotation sync disabled. Nothing to sync. @ <i>{0}</i>", kawaHierarchyPath);
				return;
			}

			if (applyToParent && transform.parent == null) {
				Debug.LogErrorFormat(this, "[KawaEditor] Apply to parent is set, but there is no parent. @ <i>{0}</i>", kawaHierarchyPath);
				return;
			}

			var source_game_objects = this.gameObject.scene.GetRootGameObjects()
				.SelectMany(g => g.Traverse()).Select(g => g.transform)
				.Where(t => t.name.Equals(sourceObjectName, System.StringComparison.InvariantCultureIgnoreCase)).ToList();

			if (source_game_objects.Count == 0) {
				Debug.LogErrorFormat(this, "[KawaEditor] Source Object \"{1}\" not found! . @ <i>{0}</i>", kawaHierarchyPath, sourceObjectName);
				return;
			}

			if (source_game_objects.Count > 1) {
				var names = string.Join(", ", source_game_objects.Select(t => t.KawaGetHierarchyPath()));
				Debug.LogErrorFormat(this, "[KawaEditor] Multipe Source Objects with name \"{1}\" found: {2} {3} @ <i>{0}</i>", kawaHierarchyPath, sourceObjectName, source_game_objects.Count, names);
				return;
			}

			var controlling = applyToParent ? transform.parent : applyToTransform != null ? applyToTransform : transform;

			if (syncPosition)
				controlling.position = source_game_objects[0].position;

			if (syncRotation)
				controlling.rotation = source_game_objects[0].rotation;

			Debug.LogFormat(this, "[KawaEditor] Synced \"{1}\" to match \"{2}\" @ <i>{0}</i>", kawaHierarchyPath, controlling, source_game_objects[0]);

		}

#endif // UNITY_EDITOR
	}
}
