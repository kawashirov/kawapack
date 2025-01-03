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

		public string FormatForLog(string message, Object override_context, out Object context) {
			var type_name = GetType().Name;
			context = override_context != null ? override_context : this;

			var path = $"<i>@ {this.KawaGetFullPath()}</i>";
			if (override_context != null)
				path = $"<i>@ {override_context.KawaGetFullPath()}</i>\n{path}";

			return $"[{type_name}] {message}\n{path}";
		}

		[HideInCallstack]
		public void Log(string message, Object override_context = null) {
			Debug.Log(FormatForLog(message, override_context, out var context), context);
		}

		[HideInCallstack]
		public void LogWarning(string message, Object override_context = null) {
			Debug.LogWarning(FormatForLog(message, override_context, out var context), context);
		}

		[HideInCallstack]
		public void LogError(string message, Object override_context = null) {
			Debug.LogError(FormatForLog(message, override_context, out var context), context);
		}

		[HideInCallstack]
		public void ThrowException(Exception exc, Object override_context = null) {
			Debug.LogError(FormatForLog(exc.Message, override_context, out var context), context);
			throw exc;
		}

		[HideInCallstack]
		public void ThrowException(Func<string, Exception> constructor, string message, Object override_context = null) {
			Debug.LogError(FormatForLog(message, override_context, out var context), context);
			throw constructor(message);
		}

		[HideInCallstack]
		public void LogException(Exception exception, Object override_context = null) {
			var context = override_context != null ? override_context : this;
			Debug.LogException(exception, context);
		}

		[HideInCallstack]
		public void LogException(string message, Exception exception, Object override_context = null) {
			var msg = FormatForLog(message, override_context, out var context);
			Debug.LogException(exception, context);
			Debug.LogError(msg + "\n\n" + exception.Message, context);
		}

		public void SetDirty() => EditorUtility.SetDirty(this);

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
