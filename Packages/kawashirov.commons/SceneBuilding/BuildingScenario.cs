#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using UnityEditor;
using Unity.EditorCoroutines.Editor;

namespace Kawashirov.SceneBuilding {
	public class BuildingScenario : KawaEditorBehaviour {

		[HideInInspector] public BuildingStatus status = BuildingStatus.NotStarted;
		public BaseSBA[] Actions;

		public void ResetScenarioStatus() {
			status = BuildingStatus.NotStarted;
			EditorUtility.SetDirty(this);
		}

		protected IEnumerator RunActionSafe(int i, BaseSBA action, bool progress_gui, string title) {
			if (action == null) {
				LogWarning($"Building action №{i} is empty, skip!");
				yield break;
			}
			if (!action.enabled || !action.gameObject.activeInHierarchy) {
				LogWarning($"Building action №{i} is not active/enabled, skip!");
				yield break;
			}
			if (progress_gui) {
				var progress = (i + 1f) / (Actions.Length + 1f);
				var info = $"Running №{i} {action.GetType()} {action}...";
				if (EditorUtility.DisplayCancelableProgressBar(title, info, progress))
					throw new CancelBuilding();
			}
			Log($"Running Building action №{i} {action.GetType()} {action}...");
			var task = action.RunAsync(this);
			while (true) {
				// C# момент, нельзя просто взять и запустить.	
				// Cannot yield a value in the body of a try block with a catch clause CS1626
				try {
					if (!task.MoveNext())
						break;
				} catch (Exception exc) {
					LogException($"Building action №{i} {action.GetType()} failed: {exc}", exc, this);
					throw exc;
				}
				yield return task.Current;
			}

			Log($"Building action №{i} {action.GetType()} success!");
		}

		protected IEnumerator RunActionsSafe(bool progress_gui) {
			var title = $"Running Building Scenario...";
			if (progress_gui) {
				if (EditorUtility.DisplayCancelableProgressBar(title, "Preparing...", 0f))
					throw new CancelBuilding();
			}
			// TODO call prepares
			for (var i = 0; i < Actions.Length; ++i) {
				var action = Actions[i];
				var task = RunActionSafe(i, action, progress_gui, title);
				while (task.MoveNext())
					yield return task.Current;
			}
			status = BuildingStatus.Success;
			EditorUtility.SetDirty(this);
		}

		public IEnumerator RunScenario(bool progress_gui) {
			status = BuildingStatus.Running;
			EditorUtility.SetDirty(this);
			var task = RunActionsSafe(progress_gui);
			while (true) {
				// C# момент, нельзя просто взять и запустить.	
				// Cannot yield a value in the body of a try block with a catch clause CS1626
				try {
					if (!task.MoveNext())
						break;
				} catch (Exception exc) {
					if (exc is CancelBuilding) {
						status = BuildingStatus.Cancelled;
						Debug.LogError($"Building scenario cancelled.", this);
					} else {
						status = BuildingStatus.Failed;
						Debug.LogException(exc, this);
						Debug.LogError($"Building scenario failed: {exc}", this);
					}
				} finally {
					if (progress_gui)
						EditorUtility.ClearProgressBar();
				}
				yield return task.Current;
			}
		}

		[CustomEditor(typeof(BuildingScenario), true)]
		public class BaseBuildingActionEditor : KawaEditorBehaviourEditor {

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
						EditorCoroutineUtility.StartCoroutine(target.RunScenario(true), target);
					}
				}

				using (new EditorGUI.DisabledScope(!IKnowWhatIamDoing)) {
					if (GUILayout.Button("Reset Scenario Status")) {
						target.ResetScenarioStatus();
						IKnowWhatIamDoing = false;
					}
				}

			}

			public override bool ShowIKnowWhatIamDoing() => true;

			public override void OnInspectorGUI() {
				DrawDefaultInspector();
				BuildingScenarioGUI();
				IKnowWhatIamDoingGUI();
			}
		}
	}
}
#endif