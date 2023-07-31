using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using Kawashirov;
using Kawashirov.Refreshables;
using System.Linq;
using Kawashirov.Udon;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
#endif

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class TouchButton : UdonSharpBehaviour
#if !COMPILER_UDONSHARP
	, IRefreshable
#endif
	{
	public float clickDistance = 1f;
	public float clickTime = 0.5f;

	public Component[] eventReceivers;
	public string eventName = "Derp";

	[NonSerialized] public RectTransform rect__;
	[NonSerialized] public float next_press__;
	[NonSerialized] public string path__ = "";

	public void Start() {
		path__ = _GetPath(transform);
		rect__ = gameObject.GetComponent<RectTransform>();
		next_press__ = -1f;
	}

	public bool _TryClick(Vector3 source) {
		if (!Utilities.IsValid(rect__)) // RectTransform поломан
			return false;
		var source_local = rect__.InverseTransformPoint(source);
		var rect = rect__.rect;
		if (!rect.Contains(source_local)) // Точка не в зоне прямоугольника
			return false;
		var z = Mathf.Abs(source_local.z);
		if (z > clickDistance) // Слишком далеко
			return false;
		if (next_press__ > Time.time) // Слишком быстро
			return false;
		// Debug.LogFormat(gameObject, "Click: rect={1}, local={2} @ {0}.", path__, rect, source_local);
		next_press__ = Time.time + clickTime;
		_Click();
		return true;
	}

	public void _Click() {
		for (var i = 0; i < eventReceivers.Length; ++i) {
			var receiver_c = eventReceivers[i];
			if (!Utilities.IsValid(receiver_c))
				continue;
			var receiver = (UdonBehaviour)receiver_c;
			receiver.SendCustomEvent(eventName);
		}
	}

	public override void Interact() => _Click();

	private string _GetPath(Transform t) {
		var path = t.name;
		while (t.parent != null) {
			t = t.parent;
			path = t.name + "/" + path;
		}
		return path;
	}

#if !COMPILER_UDONSHARP && UNITY_EDITOR

	[CustomEditor(typeof(TouchButton))]
	public class Editor : UnityEditor.Editor {
		public override void OnInspectorGUI() {
			if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
				return;
			DrawDefaultInspector();
			if (target is TouchButton ush) {
				EditorGUILayout.ObjectField("Debug: rect__", ush.rect__, typeof(RectTransform), true);
				EditorGUILayout.LabelField("Debug: next_press__", ush.next_press__.ToString());
			}
			KawaGizmos.DrawEditorGizmosGUI();
			this.EditorRefreshableGUI();
		}
	}

	private bool Validate_eventReceivers() => 
		KawaUdonUtilities.ValidateComponentsArrayOfUdonSharpBehaviours(this, nameof(eventReceivers), ref eventReceivers);

	public void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_eventReceivers, this, nameof(eventReceivers));

		if (gameObject.GetComponent<RectTransform>() == null)
			gameObject.AddComponent<RectTransform>();
	}

	public UnityEngine.Object AsUnityObject() => this;
	public string RefreshablePath() => gameObject.KawaGetFullPath();

	public void OnDrawGizmosSelected() {
		var self_pos = transform.position;

		Gizmos.color = Color.white.Alpha(KawaGizmos.GizmosAplha);
		Gizmos.DrawWireSphere(self_pos, 0.1f);

		Gizmos.color = Color.green.Alpha(KawaGizmos.GizmosAplha);
		foreach (var receivers in eventReceivers)
			if (Utilities.IsValid(receivers))
				Gizmos.DrawLine(self_pos, receivers.transform.position);
	}

#endif
}
