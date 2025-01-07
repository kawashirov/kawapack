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
using System.Collections.Generic;

namespace Kawashirov {
	[ExecuteAlways]
	public abstract class KawaEditorBehaviour : MonoBehaviour, IRefreshable {
#if UNITY_EDITOR

		[NonSerialized]
		public bool debugMode = false;

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
			if (debugMode)
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
			protected Dictionary<string, SerializedProperty> properties;

			public virtual bool ShowIKnowWhatIamDoing() => true;
			public virtual bool ShowDebugMode() => false;

			public virtual void IKnowWhatIamDoingGUI() {
				if (ShowIKnowWhatIamDoing()) {
					IKnowWhatIamDoing = GUILayout.Toggle(IKnowWhatIamDoing, "I know what I am doing");
				}
			}

			public virtual void DebugModeGUI() {
				var debugMode = targets.OfType<KawaEditorBehaviour>().Any(b => b.debugMode);
				if (debugMode || IKnowWhatIamDoing || ShowDebugMode()) {
					EditorGUI.BeginChangeCheck();
					debugMode = GUILayout.Toggle(debugMode, "Debug mode");
					if (EditorGUI.EndChangeCheck()) {
						foreach (var b in targets.OfType<KawaEditorBehaviour>())
							b.debugMode = debugMode;
					}
				}
			}

			protected SerializedProperty Prop(string key) {
				if (properties == null) {
					properties = new Dictionary<string, SerializedProperty>();
					var itr = serializedObject.GetIterator();
					itr.Next(true);
					do {
						// Debug.Log($"{itr.name} = {itr}");
						properties[itr.name] = itr.Copy();
					} while (itr.Next(false));
				}
				return properties.TryGetValue(key, out var prop) ? prop : null;
			}

			protected void ScriptGUI() {
				using (new EditorGUI.DisabledScope(true)) {
					EditorGUILayout.PropertyField(Prop("m_Script"));
				}
			}

			protected virtual void OnEnable() { }

			public override void OnInspectorGUI() {
				IKnowWhatIamDoingGUI();
				DebugModeGUI();
				DrawDefaultInspector();
				this.BehaviourRefreshGUI();
			}
		}

#endif
	}
}
