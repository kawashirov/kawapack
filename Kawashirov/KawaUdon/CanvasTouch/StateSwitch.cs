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
public class StateSwitch : UdonSharpBehaviour
#if !COMPILER_UDONSHARP
	, IRefreshable
#endif
{
	public Component[] eventReceivers;
	public string eventName = "Derp";
	public int eventDelay = 1;

	public GameObject[] states;
	public int currentState = 0;

	[NonSerialized] public string path__ = "";

	public void Start() {
		path__ = _GetPath(transform);

		_UpdateState();
	}

	public void _UpdateState() {
		for (var i = 0; i < states.Length; ++i) {
			var state_go = states[i];
			if (Utilities.IsValid(state_go))
				state_go.SetActive(currentState == i);
		}
		for (var i = 0; i < eventReceivers.Length; ++i) {
			var receiver_c = eventReceivers[i];
			if (!Utilities.IsValid(receiver_c))
				continue;
			var receiver = (UdonBehaviour)receiver_c;
			if (eventDelay < 1) {
				receiver.SendCustomEvent(eventName);
			} else {
				receiver.SendCustomEventDelayedFrames(eventName, eventDelay);
			}
		}
	}

	public void _NextState() {
		currentState = (currentState + 1) % states.Length;
		_UpdateState();
	}

	private string _GetPath(Transform t) {
		var path = t.name;
		while (t.parent != null) {
			t = t.parent;
			path = t.name + "/" + path;
		}
		return path;
	}

#if !COMPILER_UDONSHARP && UNITY_EDITOR

	[CustomEditor(typeof(StateSwitch))]
	public class Editor : UnityEditor.Editor {
		public override void OnInspectorGUI() {
			if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
				return;
			DrawDefaultInspector();
			KawaGizmos.DrawEditorGizmosGUI();
			this.EditorRefreshableGUI();
		}
	}

	private bool Validate_states() => KawaUdonUtilities.DistinctArray(ref states);
	private bool Validate_eventReceivers() => KawaUdonUtilities.ValidateComponentsArrayOfUdonSharpBehaviours(ref eventReceivers);

	public void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_states, this, nameof(states));
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
