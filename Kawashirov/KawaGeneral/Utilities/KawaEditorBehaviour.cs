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
	public abstract class KawaEditorBehaviour : MonoBehaviour, IRefreshable {
#if UNITY_EDITOR

		[CustomEditor(typeof(KawaEditorBehaviour), true)]
		public class KawaEditorBehaviourEditor : Editor {
			public override void OnInspectorGUI() {
				DrawDefaultInspector();
				this.BehaviourRefreshGUI();
			}
		}

		public string kawaHierarchyPath => transform.KawaGetHierarchyPath();

		public virtual void Refresh() { }

		public UnityEngine.Object AsUnityObject() => this;

		public string RefreshablePath() => gameObject.KawaGetFullPath();

#endif
	}
}
