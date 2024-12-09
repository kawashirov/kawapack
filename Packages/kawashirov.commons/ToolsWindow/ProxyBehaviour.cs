#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Kawashirov.ToolsGUI {
	[ExecuteInEditMode]
	public class ProxyBehaviour : MonoBehaviour {
		[CustomEditor(typeof(ProxyBehaviour))]
		internal class ProxyBehaviourEditor : Editor {
			public override void OnInspectorGUI() {
				EditorGUILayout.HelpBox("This is temporary GameObject, it will not be saved.", MessageType.Warning, true);
				var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 3);
				if (GUI.Button(rect, "Open Kawashirov's Toolbox Window")) {
					ToolsWindow.ShowToolsWindow();
				}
			}
		}

		public void OnDrawGizmos() {
			if (ToolsWindow.window != null)
				ToolsWindow.window.OnDrawGizmosProxy();
		}

		public void Focus(bool select = true) {
			EditorGUIUtility.PingObject(gameObject);
			EditorGUIUtility.PingObject(this);
			var bounds = new Bounds(transform.position, Vector3.one);
			foreach (var obj in SceneView.sceneViews) {
				if (obj is SceneView view) {
					view.Frame(bounds);
				}
			}
			if (select) {
				var objects = Selection.objects;
				ArrayUtility.AddRange(ref objects, new UnityEngine.Object[] { gameObject, transform, this });
				Selection.objects = objects;
				Selection.activeGameObject = gameObject;
				Selection.activeTransform = transform;
				Selection.activeObject = this;
			}
		}
	}
}
#endif // UNITY_EDITOR
