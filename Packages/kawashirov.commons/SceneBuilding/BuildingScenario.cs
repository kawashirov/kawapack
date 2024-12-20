using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.SceneBuilding {
	public class BuildingScenario : MonoBehaviour {
#if UNITY_EDITOR

		[HideInInspector] public BuildingStatus status = BuildingStatus.NotStarted;
		public BaseBuildingAction[] Actions;

		public void ResetScenarioStatus() {
			status = BuildingStatus.NotStarted;
			EditorUtility.SetDirty(this);
		}

		public void RunScenario(bool progress_gui) {
			status = BuildingStatus.Running;
			EditorUtility.SetDirty(this);
			var title = $"Running Building Scenario...";
			try {
				if (progress_gui) {
					if (EditorUtility.DisplayCancelableProgressBar(title, "Preparing...", 0f))
						throw new CancelBuilding();
				}
				// TODO call prepares
				for (var i = 0; i < Actions.Length; ++i) {
					var action = Actions[i];
					if (action == null) {
						Debug.LogWarning($"Building action #{i} is empty, skip!", this);
						continue;
					}
					if (!action.enabled) {
						Debug.LogWarning($"Building action #{i} is not enabled, skip!", this);
						continue;
					}
					if (progress_gui) {
						var progress = (i + 1f) / (Actions.Length + 1f);
						var info = $"Running #{i} {action.GetType()} {action}...";
						if (EditorUtility.DisplayCancelableProgressBar(title, info, progress))
							throw new CancelBuilding();
					}
					Debug.Log($"Running Building action #{i} {action.GetType()} {action}...", this);
					try {
						action.Run();
					} catch (Exception exc) {
						Debug.LogException(exc, this);
						Debug.LogError($"Building action #{i} {action.GetType()} failed: {exc}", this);
						throw exc;
					}
					Debug.Log($"Building action #{i} {action.GetType()} success!", this);
				}
				status = BuildingStatus.Success;
			} catch (Exception exc) {
				if (exc is CancelBuilding) {
					status = BuildingStatus.Cancelled;
					Debug.LogError($"Building cancelled.", this);
				} else {
					status = BuildingStatus.Failed;
					Debug.LogException(exc, this);
					Debug.LogError($"Building failed: {exc}", this);
				}
			} finally {
				if (progress_gui)
					EditorUtility.ClearProgressBar();
			}
		}

		[CustomEditor(typeof(BuildingScenario), true)]
		public class BaseBuildingActionEditor : Editor {
			protected bool IKnowWhatIamDoing = false;

			public virtual void BuildingScenarioStatusGUI(BuildingScenario target) {
				var prev_color = GUI.color;
				var color = prev_color; // No change by default
				if (target.status == BuildingStatus.NotStarted) {
					color = Color.blue;
				} else if (target.status == BuildingStatus.Success) {
					color = Color.green;
				} else if (target.status == BuildingStatus.Failed) {
					color = Color.red;
				} else if (target.status == BuildingStatus.Cancelled) {
					color = Color.red * 0.5f + Color.yellow * 0.5f;
				} else if (target.status == BuildingStatus.Running) {
					color = Color.green * 0.5f + Color.yellow * 0.5f;
				}
				GUI.color = GUI.color * 0.5f + color * 0.5f;
				GUILayout.Label($"Status: {target.status}");
				GUI.color = prev_color;
			}

			public virtual void BuildingScenarioGUI() {
				var target = this.target as BuildingScenario;
				if (target == null)
					return;

				BuildingScenarioStatusGUI(target);

				using (new EditorGUI.DisabledScope(target.status != BuildingStatus.NotStarted)) {
					if (GUILayout.Button("Run Scenario")) {
						target.RunScenario(true);
					}
				}

				using (new EditorGUI.DisabledScope(!IKnowWhatIamDoing)) {
					if (GUILayout.Button("Reset Scenario Status")) {
						target.ResetScenarioStatus();
						IKnowWhatIamDoing = false;
					}
				}

				IKnowWhatIamDoing = GUILayout.Toggle(IKnowWhatIamDoing, "I know what I am doing");
			}

			public override void OnInspectorGUI() {
				DrawDefaultInspector();
				BuildingScenarioGUI();
			}
		}


#endif
	}
}