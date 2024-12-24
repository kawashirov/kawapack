using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using Kawashirov.Refreshables;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov {
	[ExecuteAlways]
	public abstract class KawaEditorBehaviour : MonoBehaviour, IRefreshable {
#if UNITY_EDITOR

		[CustomEditor(typeof(KawaEditorBehaviour), true)]
		public class KawaEditorBehaviourEditor : Editor {
			public override void OnInspectorGUI() {
				DrawDefaultInspector();
				this.BehaviourRefreshGUI();
			}
		}

		public virtual void Awake() {
			EnsureDontSaveInBuild();
		}

		public void EnsureDontSaveInBuild() {
			if ((hideFlags & HideFlags.DontSaveInBuild) != 0)
				return;
			hideFlags |= HideFlags.DontSaveInBuild;
			EditorUtility.SetDirty(this);
			Debug.Log($"Assigned DontSaveInBuild to {kawaHierarchyPath}", this);
		}

		public string kawaHierarchyPath => this.KawaGetFullPath();

		public virtual void Refresh() { }

#endif
	}
}
