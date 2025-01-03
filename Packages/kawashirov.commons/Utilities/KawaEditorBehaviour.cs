using System;
using UnityEngine;
using Kawashirov.Refreshables;
using System.Diagnostics;

#if UNITY_EDITOR
using UnityEditor;
#endif

using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using System.Linq;

namespace Kawashirov {
	[ExecuteAlways]
	public abstract class KawaEditorBehaviour : MonoBehaviour, IRefreshable {
#if UNITY_EDITOR

		[HideInInspector]
		[Tooltip("Provide more info messages")]
		public bool DebugMode = false;

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
		[Conditional("UNITY_ASSERTIONS")]
		public void LogAssert(bool condition, string message, Object override_context = null) {
			if (!condition)
				Debug.Log(FormatForLog(message, override_context, out var context), context);
		}

		[HideInCallstack]
		public void LogDebug(string message, Object override_context = null) {
			if (DebugMode)
				Debug.Log(FormatForLog(message, override_context, out var context), context);
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
			protected bool IKnowWhatIamDoing = false;
			protected SerializedProperty DebugMode = null;

			public virtual void OnEnable() {
				DebugMode = serializedObject.FindProperty(nameof(DebugMode));
				Debug.LogWarning($"DebugMode={DebugMode}");
			}

			public virtual bool ShowIKnowWhatIamDoing() => true;
			public virtual bool ShowDebugMode() => false;

			public virtual void IKnowWhatIamDoingGUI() {
				if (ShowIKnowWhatIamDoing()) {
					IKnowWhatIamDoing = GUILayout.Toggle(IKnowWhatIamDoing, "I know what I am doing");
				}
			}

			public virtual void DebugModeGUI() {
				if (DebugMode == null)
					return;
				if (IKnowWhatIamDoing || ShowDebugMode() || DebugMode.hasMultipleDifferentValues || DebugMode.boolValue) {
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField(DebugMode);
					if (EditorGUI.EndChangeCheck()) {
						serializedObject.ApplyModifiedProperties();
					}
				}
			}

			public override void OnInspectorGUI() {
				DrawDefaultInspector();
				DebugModeGUI();
				IKnowWhatIamDoingGUI();
				this.BehaviourRefreshGUI();
			}
		}

#endif
	}
}
