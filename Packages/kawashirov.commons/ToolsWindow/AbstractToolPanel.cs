#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace Kawashirov.ToolsGUI {
	public abstract class AbstractToolPanel : ScriptableObject {
		private readonly char[] pathSplitChars = new char[] { '/', '\\' };

		[NonSerialized] public bool active = false;

		public virtual string[] GetMenuPath() {
			var path = GetType().GetCustomAttribute<ToolsWindowPanelAttribute>().path;
			return path.Split(pathSplitChars, StringSplitOptions.RemoveEmptyEntries);
		}

		public virtual GUIContent GetMenuButtonContent() {
			return null;
		}

		public virtual GUIContent GetMenuHeaderContent() {
			return GetMenuButtonContent();
		}

		public virtual void ToolsGUI() { }

		public virtual void DrawGizmos() { }

		public virtual void Update() { }

		public virtual void OnSceneGUI(SceneView sceneView) { }

		public virtual bool ShouldCallSceneGUIDrawMesh(SceneView sceneView) => false;
		public virtual void OnSceneGUIDrawMesh(SceneView sceneView) { }

	}
}

#endif // UNITY_EDITOR
