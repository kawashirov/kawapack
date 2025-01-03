#if UNITY_EDITOR
using System.Collections;
using UnityEditor;

namespace Kawashirov.SceneBuilding {
	public class BaseSBA : KawaEditorBehaviour {
		// SBA = Scene Building Action
		// Naming converstion: WhatEverSBA

		public virtual IEnumerator PrepareAsync(BuildingScenario scenario) {
			PrepareSync(scenario);
			yield break;
		}

		public virtual void PrepareSync(BuildingScenario scenario) { }

		public virtual IEnumerator RunAsync(BuildingScenario scenario) {
			RunSync(scenario);
			yield break;
		}

		public virtual void RunSync(BuildingScenario scenario) { }

		[CustomEditor(typeof(BaseSBA), true)]
		public class BaseSBAEditor : KawaEditorBehaviourEditor {

			public virtual void BuildingActionGUI() { }

			public override void OnInspectorGUI() {
				DrawDefaultInspector();
				BuildingActionGUI();
				DebugModeGUI();
				IKnowWhatIamDoingGUI();
			}
		}
	}
}
#endif