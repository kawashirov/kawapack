using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using Kawashirov;
using Kawashirov.Udon;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
#endif

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LazyPlayerPresenceTrigger : CommonUSharpBehaviour {
	/* Global Config */

	[Tooltip("These triggers will be used for testing player's POV presence.\n"
	+ "Make sure these colliders are triggers and has Ignore Raycast layer.")]
	public Collider[] Triggers;

	[Space, Tooltip("These GameObjects will be active only when player's POV INSIDE any trigger.")]
	public GameObject[] ActiveWhenPresent;
	[Tooltip("These GameObjects will be active only when player's POV OUTSIDE any trigger.")]
	public GameObject[] ActiveWhenAbsent;

	[Space, Tooltip("This bool variable will be set to IsPlayerPresent value on given Animators.")]
	public string AnimatorsSetBoolName = "IsPlayerPresent";
	public Animator[] AnimatorsSetBool;

	[Space, Tooltip("This event will be sent to EventReceivers when IsPlayerPresent will be changed.\n")]
	public string OnChangedEventName = "_OnPlayerPresenceChanged";

	[Tooltip("Delay in frames before sending OnChangedEventName (_OnPlayerPresenceChanged) event.\n")]
	public int OnChangedEventDelay = 1;

	[Tooltip("UdonBehaviours that will recive OnChangedEventName (_OnPlayerPresenceChanged) event.\n"
		+ "All components must be UdonBehaviours, otherwise script will crash.")]
	public Component[] EventReceivers;

	[Space, Tooltip("If there is no local player (that's OK in editor play mode)\n"
		+ "the script will use this value as IsPlayerPresent.")]
	public bool IsPlayerPresentInEditor = true;

	/* Public Runtime */
	[NonSerialized] public bool IsPlayerPresent = false;

	/* Debug Runtime */
	[NonSerialized] public string Debug_PlayerPositionSource = "";
	[NonSerialized] public int Debug_TriggerSource = -1;


	public override void Start() {
		base.Start();
		logName = "Kawa|LazyPlayerPresenceTrigger";

		if (_EnsureValid(Triggers, "Triggers is invalid") && Triggers.Length > 0) {
			var layer = LayerMask.NameToLayer("Ignore Raycast");
			for (var i = 0; i < Triggers.Length; ++i) {
				var trigger = Triggers[i];
				if (!Utilities.IsValid(trigger)) {
					_Warning($"Missing trigger at #{i}.");
				} else {
					if (!trigger.isTrigger) {
						_Warning($"Collider at #{i} is not trigger. Trying to set isTrigger...");
						trigger.isTrigger = true;
					}
					if (!trigger.enabled) {
						_Warning($"Collider at #{i} is not enabled. Trying to enable...");
						trigger.enabled = true;
					}
					var g = trigger.gameObject; // getter
					var g_layer = g.layer;
					if (g_layer != layer) {
						var g_layer_name = LayerMask.LayerToName(g_layer);
						var layer_name = LayerMask.LayerToName(layer);
						_Warning($"Trigger at #{i} has wrong layer: {g_layer} ({g_layer_name}). Changing to layer {layer} ({layer_name})...");
						g.layer = layer;
					}
				}
			}
		}

		SendCustomEventDelayedSeconds(nameof(_SetStateFalse), 1f);

		// _Info($"Initialized.");
	}

	public void _SetStateFalse() => _SetState(false, true);

	public void _Apply() {
		if (Utilities.IsValid(ActiveWhenPresent))
			foreach (var go in ActiveWhenPresent)
				if (Utilities.IsValid(go))
					go.SetActive(IsPlayerPresent);

		if (Utilities.IsValid(ActiveWhenAbsent))
			foreach (var go in ActiveWhenAbsent)
				if (Utilities.IsValid(go))
					go.SetActive(!IsPlayerPresent);

		if (Utilities.IsValid(EventReceivers))
			foreach (var component in EventReceivers) {
				var receiver = (UdonBehaviour)component;
				if (!(Utilities.IsValid(receiver) && receiver.gameObject.activeInHierarchy && receiver.enabled))
					continue;
				if (OnChangedEventDelay < 1) {
					receiver.SendCustomEvent(OnChangedEventName);
				} else {
					receiver.SendCustomEventDelayedFrames(OnChangedEventName, OnChangedEventDelay);
				}
			}

		if (Utilities.IsValid(AnimatorsSetBool))
			foreach (var animator in AnimatorsSetBool)
				if (Utilities.IsValid(animator))
					animator.SetBool(AnimatorsSetBoolName, IsPlayerPresent);
	}

	private void _SetState(bool state, bool force) {
		var changed = IsPlayerPresent != state;
		IsPlayerPresent = state;
		if (changed || force)
			_Apply();
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
		if (!Utilities.IsValid(Triggers) || Triggers.Length < 1) {
			_SetState(false, false);
			return;
		}

		var player_local = Networking.LocalPlayer;
		if (!Utilities.IsValid(player_local)) {
			Debug_TriggerSource = -1;
			Debug_PlayerPositionSource = "Editor";
			_SetState(IsPlayerPresentInEditor, false);
			return;
		}

		var position = _GetPosition(player_local);

		for (var i = 0; i < Triggers.Length; ++i) {
			var trigger = Triggers[i];
			if (Utilities.IsValid(trigger)) {
				var closest = trigger.ClosestPoint(position);
				if ((closest - position).magnitude < 0.01f) {
					Debug_TriggerSource = i;
					_SetState(true, false);
					return;
				}
			}
		}
		Debug_TriggerSource = -1;
		_SetState(false, false);
	}

	public override void Interact() => _ThrottledUpdate();

#if !COMPILER_UDONSHARP && UNITY_EDITOR

	[CustomEditor(typeof(LazyPlayerPresenceTrigger))]
	public new class Editor : CommonUSharpBehaviour.Editor {
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

	private bool Validate_Triggers() =>
		KawaUdonUtilities.DistinctArray(this, nameof(Triggers), ref Triggers);

	private bool Validate_ActiveWhenPresent() =>
		KawaUdonUtilities.DistinctArray(this, nameof(ActiveWhenPresent), ref ActiveWhenPresent);

	private bool Validate_ActiveWhenAbsent() =>
		KawaUdonUtilities.DistinctArray(this, nameof(ActiveWhenAbsent), ref ActiveWhenAbsent);

	private bool Validate_AnimatorsSetBool() =>
		KawaUdonUtilities.DistinctArray(this, nameof(AnimatorsSetBool), ref AnimatorsSetBool);

	private bool Validate_EventReceivers() =>
		KawaUdonUtilities.ValidateComponentsArrayOfUdonSharpBehaviours(this, nameof(EventReceivers), ref EventReceivers);

	private void Validate_Trigger(Collider collider) {
		if (!collider.enabled)
			collider.enabled = true;
		var layer = LayerMask.NameToLayer("Ignore Raycast");
		if (collider.gameObject.layer != layer)
			collider.gameObject.layer = layer;
	}

	public override void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_Triggers, this, nameof(Triggers));
		KawaUdonUtilities.ValidateSafe(Validate_ActiveWhenPresent, this, nameof(ActiveWhenPresent));
		KawaUdonUtilities.ValidateSafe(Validate_ActiveWhenAbsent, this, nameof(ActiveWhenAbsent));
		KawaUdonUtilities.ValidateSafe(Validate_AnimatorsSetBool, this, nameof(AnimatorsSetBool));
		KawaUdonUtilities.ValidateSafe(Validate_EventReceivers, this, nameof(EventReceivers));
		KawaUdonUtilities.ValidateSafeForEach(Triggers, Validate_Trigger, this, nameof(Triggers));
	}

	public void OnDrawGizmosSelected() {
		var self_pos = transform.position;

		Gizmos.color = Color.blue.Alpha(KawaGizmos.GizmosAplha);
		if (Utilities.IsValid(ActiveWhenPresent))
			foreach (var gobj in ActiveWhenPresent)
				if (Utilities.IsValid(gobj))
					Gizmos.DrawLine(self_pos, gobj.transform.position);

		Gizmos.color = Color.red.Alpha(KawaGizmos.GizmosAplha);
		if (Utilities.IsValid(ActiveWhenAbsent))
			foreach (var gobj in ActiveWhenAbsent)
				if (Utilities.IsValid(gobj))
					Gizmos.DrawLine(self_pos, gobj.transform.position);

		Gizmos.color = Color.yellow.Alpha(KawaGizmos.GizmosAplha);
		if (Utilities.IsValid(AnimatorsSetBool))
			foreach (var animator in AnimatorsSetBool)
				if (Utilities.IsValid(animator))
					Gizmos.DrawLine(self_pos, animator.transform.position);

		Gizmos.color = Color.green.Alpha(KawaGizmos.GizmosAplha);
		if (Utilities.IsValid(EventReceivers))
			foreach (var component in EventReceivers)
				if (Utilities.IsValid(component))
					Gizmos.DrawLine(self_pos, component.transform.position);
	}
#endif
}
