using System;
using UnityEngine;
using Kawashirov.Refreshables;

#if UNITY_EDITOR
using UnityEditor;
#endif

using Object = UnityEngine.Object;

namespace Kawashirov {
	[ExecuteAlways]
	public abstract class KawaEditorBehaviour : MonoBehaviour, IRefreshable {
#if UNITY_EDITOR

		/* EditorBehaviour "API" (mostly shortcuts) */

		public string KawaGetFullPath() => KawaUtilities.KawaGetFullPath(this); // to avoid this. syntax

		public void Log(string message, Object override_context = null) {
			var type_name = GetType().Name;
			var context = override_context != null ? override_context : this;
			Debug.Log($"[{type_name}] {message} <i>@ {KawaGetFullPath()}</i>", context);
		}

		public void LogWarning(string message, Object override_context = null) {
			var type_name = GetType().Name;
			var context = override_context != null ? override_context : this;
			Debug.LogWarning($"[{type_name}] {message} <i>@ {KawaGetFullPath()}</i>", context);
		}

		public void LogError(string message, Object override_context = null) {
			var type_name = GetType().Name;
			var context = override_context != null ? override_context : this;
			Debug.LogError($"[{type_name}] {message} <i>@ {KawaGetFullPath()}</i>", context);
		}

		public void LogException(Exception exception, Object override_context = null) {
			var context = override_context != null ? override_context : this;
			Debug.LogException(exception, context);
		}

		public void LogException(string message, Exception exception, Object override_context = null) {
			var type_name = GetType().Name;
			var context = override_context != null ? override_context : this;
			Debug.LogException(exception, context);
			Debug.LogError($"[{type_name}] {message} <i>@ {KawaGetFullPath()}</i>", context);
		}

		public void EnsureDontSaveInBuild() {
			if ((hideFlags & HideFlags.DontSaveInBuild) != 0)
				return;
			hideFlags |= HideFlags.DontSaveInBuild;
			EditorUtility.SetDirty(this);
			Log($"Self-assigned DontSaveInBuild.");
		}

		public virtual void Awake() {
			EnsureDontSaveInBuild();
		}

		public virtual void Refresh() { } // optional

		/* Default Editor */

		[CustomEditor(typeof(KawaEditorBehaviour), true)]
		public class KawaEditorBehaviourEditor : Editor {
			public override void OnInspectorGUI() {
				DrawDefaultInspector();
				this.BehaviourRefreshGUI();
			}
		}

#endif
	}
}
