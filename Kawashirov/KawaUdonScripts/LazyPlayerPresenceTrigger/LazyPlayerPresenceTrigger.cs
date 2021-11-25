using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using Kawashirov;
using Kawashirov.Udon;
using System.Linq;
using Kawashirov.Refreshables;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
#endif

public class LazyPlayerPresenceTrigger : UdonSharpBehaviour
#if !COMPILER_UDONSHARP
	, IRefreshable
#endif
{
	/* Global Config */

	[Tooltip("These triggers will be used for testing player's POV presence.\nMake sure these colliders are triggers and has Ignore Raycast layer.")]
	public Collider[] Triggers;

	[Space, Tooltip("These GameObjects will be active only when player's POV INSIDE any trigger.")]
	public GameObject[] ActiveWhenPresent;
	[Tooltip("These GameObjects will be active only when player's POV OUTSIDE any trigger.")]
	public GameObject[] ActiveWhenAbsent;

	[Space, Tooltip("This bool variable will be set to IsPlayerPresent value on given Animators.")]
	public string AnimatorsSetBoolName = "IsPlayerPresent";
	public Animator[] AnimatorsSetBool;

	[Space, Tooltip("This event will be sent to receivers if IsPlayerPresent will be changed.\nAll components should be UdonBehaviours, otherwise script will crash.")]
	public string OnChangedEventName = "OnPlayerPresenceChanged";
	public Component[] EventReceivers;

	[Space, Tooltip("If there is no local player (that's OK in editor play mode) script will use this value as IsPlayerPresent.")]
	public bool IsPlayerPresentInEditor = true;

	/* Public Runtime */
	[NonSerialized] public bool IsPlayerPresent = false;

	/* Debug Runtime */
	[NonSerialized] public string Debug_PlayerPositionSource = "";
	[NonSerialized] public int Debug_TriggerSource = -1;

	/*  Internal Runtime */
	private string _path = "";

	public void Start() {
		_path = GetPath(transform);

		if (!Utilities.IsValid(Triggers) || Triggers.Length < 1) {
			Debug.LogWarningFormat(gameObject, "[Kawa|LazyPlayerPresenceTrigger] There is no player tracking colliders! @ {0}", _path);
		} else {
			var layer = LayerMask.NameToLayer("Ignore Raycast");
			for (var i = 0; i < Triggers.Length; ++i) {
				var trigger = Triggers[i];
				if (!Utilities.IsValid(trigger)) {
					Debug.LogWarningFormat(gameObject, "[Kawa|LazyPlayerPresenceTrigger] Missing trigger at #{1}. @ {0}", _path, i);
				} else {
					if (!trigger.isTrigger) {
						Debug.LogWarningFormat(gameObject, "[Kawa|LazyPlayerPresenceTrigger] Collider at #{1} is not trigger. Trying to set isTrigger... @ {0}", _path, i);
						trigger.isTrigger = true;
					}
					if (!trigger.enabled) {
						Debug.LogWarningFormat(gameObject, "[Kawa|LazyPlayerPresenceTrigger] Collider at #{1} is not enabled. Trying to enable... @ {0}", _path, i);
						trigger.enabled = true;
					}
					var g = trigger.gameObject; // getter
					var g_layer = g.layer;
					if (g_layer != layer) {
						var g_layer_name = LayerMask.LayerToName(g_layer);
						var layer_name = LayerMask.LayerToName(layer);
						Debug.LogWarningFormat(gameObject, "[Kawa|LazyPlayerPresenceTrigger] Trigger at #{1} has wrong layer: {2} ({3}). Changing to layer {4} ({5})... @ {0}", _path, i, g_layer, g_layer_name, layer, layer_name);
						g.layer = layer;
					}
				}
			}
		}

		Debug.LogFormat(gameObject, "[Kawa|LazyPlayerPresenceTrigger] Initialized. @ {0}", _path);
	}

	private void SetState(bool state) {
		var changed = IsPlayerPresent != state;
		IsPlayerPresent = state;
		if (changed) {
			foreach (var go in ActiveWhenPresent)
				go.SetActive(IsPlayerPresent);

			foreach (var go in ActiveWhenAbsent)
				go.SetActive(!IsPlayerPresent);

			foreach (var component in EventReceivers) {
				var receiver = (UdonBehaviour)component;
				if (Utilities.IsValid(receiver) && receiver.gameObject.activeInHierarchy && receiver.enabled)
					receiver.SendCustomEvent(OnChangedEventName);
			}

			foreach (var animator in AnimatorsSetBool) {
				if (Utilities.IsValid(animator))
					animator.SetBool(AnimatorsSetBoolName, IsPlayerPresent);
			}
		}
	}

	private Vector3 _GetPosition(VRCPlayerApi player) {
		Vector3 position;

		// Try head tracking
		position = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
		if (position.magnitude > 0.01f) {
			Debug_PlayerPositionSource = "TrackingHead";
			return position;
		}

		// Try eyes 
		var eye_left = player.GetBonePosition(HumanBodyBones.LeftEye);
		var eye_right = player.GetBonePosition(HumanBodyBones.RightEye);
		position = eye_left * 0.5f + eye_right * 0.5f;
		if (position.magnitude > 0.01f) {
			Debug_PlayerPositionSource = "HumanBodyEyes";
			return position;
		}

		// Try head bone
		position = player.GetBonePosition(HumanBodyBones.Head);
		if (position.magnitude > 0.01f) {
			Debug_PlayerPositionSource = "HumanBodyHead";
			return position;
		}

		// Try base position
		Debug_PlayerPositionSource = "PlayerPosition";
		return player.GetPosition();
	}

	public void _ThrottledUpdate() {
		if (Triggers == null || Triggers.Length < 1) {
			SetState(false);
			return;
		}

		var player_local = Networking.LocalPlayer;
		if (!Utilities.IsValid(player_local)) {
			Debug_TriggerSource = -1;
			Debug_PlayerPositionSource = "Editor";
			SetState(IsPlayerPresentInEditor);
			return;
		}

		var position = _GetPosition(player_local);

		for (var i = 0; i < Triggers.Length; ++i) {
			var trigger = Triggers[i];
			if (Utilities.IsValid(trigger)) {
				var closest = trigger.ClosestPoint(position);
				if ((closest - position).magnitude < 0.01f) {
					Debug_TriggerSource = i;
					SetState(true);
					return;
				}
			}
		}
		Debug_TriggerSource = -1;
		SetState(false);
	}

	public override void Interact() => _ThrottledUpdate();

	/* Utils */

	private string GetPath(Transform t) {
		var path = t.name;
		while (t.parent != null) {
			t = t.parent;
			path = t.name + "/" + path;
		}
		return path;
	}

#if !COMPILER_UDONSHARP && UNITY_EDITOR

	[CustomEditor(typeof(LazyPlayerPresenceTrigger))]
	public class Editor : UnityEditor.Editor {
		public override void OnInspectorGUI() {
			if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
				return;
			DrawDefaultInspector();
			if (target is LazyPlayerPresenceTrigger ush) {
				EditorGUILayout.LabelField("IsPlayerPresent", ush.IsPlayerPresent.ToString());
				EditorGUILayout.LabelField("Debug: PlayerPositionSource", ush.Debug_PlayerPositionSource.ToString());
				EditorGUILayout.LabelField("Debug: TriggerSource", ush.Debug_TriggerSource.ToString());
			}
			KawaGizmos.DrawEditorGizmosGUI();
			this.EditorRefreshableGUI();
		}
	}

	private bool Validate_Triggers() => KawaUdonUtilities.DistinctArray(ref Triggers);
	private bool Validate_ActiveWhenPresent() => KawaUdonUtilities.DistinctArray(ref ActiveWhenPresent);
	private bool Validate_ActiveWhenAbsent() => KawaUdonUtilities.DistinctArray(ref ActiveWhenAbsent);
	private bool Validate_AnimatorsSetBool() => KawaUdonUtilities.DistinctArray(ref AnimatorsSetBool);
	private bool Validate_EventReceivers() => KawaUdonUtilities.ValidateComponentsArrayOfUdonSharpBehaviours(ref EventReceivers);

	public void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_Triggers, this, nameof(Triggers));
		KawaUdonUtilities.ValidateSafe(Validate_ActiveWhenPresent, this, nameof(ActiveWhenPresent));
		KawaUdonUtilities.ValidateSafe(Validate_ActiveWhenAbsent, this, nameof(ActiveWhenAbsent));
		KawaUdonUtilities.ValidateSafe(Validate_AnimatorsSetBool, this, nameof(AnimatorsSetBool));
		KawaUdonUtilities.ValidateSafe(Validate_EventReceivers, this, nameof(EventReceivers));
	}

	public UnityEngine.Object AsUnityObject() => this;

	public string RefreshablePath() => gameObject.KawaGetFullPath();

	public void OnDrawGizmosSelected() {
		var self_pos = transform.position;

		Gizmos.color = Color.white.Alpha(KawaGizmos.GizmosAplha);
		Gizmos.DrawWireSphere(self_pos, 0.1f);

		Gizmos.color = Color.blue.Alpha(KawaGizmos.GizmosAplha);
		foreach (var gobj in ActiveWhenPresent)
			if (Utilities.IsValid(gobj))
				Gizmos.DrawLine(self_pos, gobj.transform.position);

		Gizmos.color = Color.red.Alpha(KawaGizmos.GizmosAplha);
		foreach (var gobj in ActiveWhenAbsent)
			if (Utilities.IsValid(gobj))
				Gizmos.DrawLine(self_pos, gobj.transform.position);

		Gizmos.color = Color.yellow.Alpha(KawaGizmos.GizmosAplha);
		foreach (var animator in AnimatorsSetBool)
			if (Utilities.IsValid(animator))
				Gizmos.DrawLine(self_pos, animator.transform.position);

		Gizmos.color = Color.green.Alpha(KawaGizmos.GizmosAplha);
		foreach (var component in EventReceivers)
			if (Utilities.IsValid(component))
				Gizmos.DrawLine(self_pos, component.transform.position);
	}

#endif
}
