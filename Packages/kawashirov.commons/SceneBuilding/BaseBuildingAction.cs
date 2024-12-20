using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov.SceneBuilding {
	public class BaseBuildingAction : MonoBehaviour {
#if UNITY_EDITOR

		public virtual void Prepare() {

		}

		public virtual void Run() {

		}

		[CustomEditor(typeof(BaseBuildingAction), true)]
		public class BaseBuildingActionEditor : Editor {
			protected bool IKnowWhatIamDoing = false;

			public virtual bool ShowIKnowWhatIamDoing() => false;

			public virtual void IKnowWhatIamDoingGUI() {
				if (ShowIKnowWhatIamDoing()) {
					IKnowWhatIamDoing = GUILayout.Toggle(IKnowWhatIamDoing, "I know what I am doing");
				}
			}

			public virtual void BuildingActionGUI() {
				IKnowWhatIamDoingGUI();
			}

			public override void OnInspectorGUI() {
				DrawDefaultInspector();
				BuildingActionGUI();
			}
		}

#endif
	}
}