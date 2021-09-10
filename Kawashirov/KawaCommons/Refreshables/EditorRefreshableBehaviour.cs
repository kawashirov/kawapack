using UnityEngine;
using System.Linq;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.Refreshables {
	public abstract class EditorRefreshableBehaviour : EditorCommonBehaviour, IRefreshable {
#if UNITY_EDITOR

		[CanEditMultipleObjects, CustomEditor(typeof(EditorRefreshableBehaviour), true)]
		public new class Editor : EditorCommonBehaviour.Editor {
			public override void OnInspectorGUI() {
				base.OnInspectorGUI();
				EditorGUILayout.Space();
				OnInspectorRefreshGUI();
			}

			public virtual void OnInspectorRefreshGUI() {
				if (GUILayout.Button("Only refresh this")) {
					var targets = this.targets.OfType<IRefreshable>().UnityNotNull().ToList();
					targets.RefreshMultiple();
				}

				var scenes = targets.OfType<Component>().UnityNotNull().Select(r => r.gameObject.scene).Distinct().ToList();
				var scene_str = string.Join(", ", scenes.Select(s => s.name));

				var types = targets.Select(t => t.GetType()).Distinct().ToList();
				var types_str = string.Join(", ", types.Select(t => t.Name));

				var types_btn = string.Format("Refresh every {0} on scene: {1}", types_str, scene_str);
				if (GUILayout.Button(types_btn)) {
					var all_targets = scenes.SelectMany(s => s.GetRootGameObjects())
							.SelectMany(g => types.SelectMany(t => g.GetComponentsInChildren(t, true)))
							.UnityNotNull().Distinct().OfType<IRefreshable>().ToList();
					all_targets.RefreshMultiple();
				}

				var scene_btn = string.Format("Refresh every IRefreshable on scene: {0}", scene_str);
				if (GUILayout.Button(scene_btn)) {
					var all_targets = scenes.SelectMany(s => s.GetRootGameObjects())
							.SelectMany(g => g.GetComponentsInChildren<IRefreshable>(true)).ToList();
					all_targets.RefreshMultiple();
				}
			}

		}

		public virtual void Refresh() { }

		public UnityEngine.Object AsUnityObject() => this;

		public string RefreshablePath() => gameObject.KawaGetFullPath();

#endif
	}
}
