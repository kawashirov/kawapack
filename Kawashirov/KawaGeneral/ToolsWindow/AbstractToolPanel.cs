#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace Kawashirov.ToolsGUI {
	public abstract class AbstractToolPanel : ScriptableObject {

		[NonSerialized] public bool active = false;

		public virtual void ToolsGUI() { }

		public virtual void DrawGizmos() { }

		public virtual void Update() { }

		public virtual void OnSceneGUI(SceneView sceneView) { }

		public virtual bool ShouldCallSceneGUIDrawMesh(SceneView sceneView) => false;
		public virtual void OnSceneGUIDrawMesh(SceneView sceneView) { }

	}
}

#endif // UNITY_EDITOR
