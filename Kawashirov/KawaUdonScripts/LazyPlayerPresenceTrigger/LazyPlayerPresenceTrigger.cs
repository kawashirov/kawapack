
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class LazyPlayerPresenceTrigger : UdonSharpBehaviour {
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
	[Space, Tooltip("This is set by script at run-time.\nRead this from other Udon scripts.")]
	public bool IsPlayerPresent = false;

	/*  Internal Runtime */
	private string _path = "";

	public void Start() {
		_path = GetPath(transform);

		if (Triggers == null || Triggers.Length < 1) {
			Debug.LogWarningFormat(gameObject, "[Kawa|LazyPlayerPresenceTrigger] There is no player tracking colliders! @ {0}", _path);
		} else {
			var layer = LayerMask.NameToLayer("Ignore Raycast");
			for (var i = 0; i < Triggers.Length; ++i) {
				var trigger = Triggers[i];
				if (trigger == null) {
					Debug.LogWarningFormat(gameObject, "[Kawa|LazyPlayerPresenceTrigger] Missing trigger at #{1}. @ {0}", _path, i);
				} else {
					if (!trigger.isTrigger) {
						Debug.LogWarningFormat(gameObject, "[Kawa|LazyPlayerPresenceTrigger] Collider at #{1} is not trigger. Trying to set isTrigger... @ {0}", _path, i);
						trigger.isTrigger = true;
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

			foreach (var receiver in EventReceivers) {
				if (receiver == null || !receiver.gameObject.activeInHierarchy)
					continue;
				var udon = (UdonBehaviour)receiver;
				// TODO: can not check UdonBehaviour.enabled yet.
				udon.SendCustomEvent(OnChangedEventName);
			}

			foreach (var animator in AnimatorsSetBool) {
				if (animator == null || !animator.gameObject.activeInHierarchy)
					continue;
				animator.SetBool(AnimatorsSetBoolName, IsPlayerPresent);
			}
		}
	}

	public void SlowUpdate() {
		if (Triggers == null || Triggers.Length < 1) {
			SetState(false);
			return;
		}

		var player_local = Networking.LocalPlayer;
		if (player_local == null) {
			SetState(IsPlayerPresentInEditor);
			return;
		}

		var t = player_local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
		var position = t.position;

		for (var i = 0; i < Triggers.Length; ++i) {
			var trigger = Triggers[i];
			if (trigger != null) {
				var closest = trigger.ClosestPoint(position);
				if (Vector3.SqrMagnitude(closest - position) < 0.01f * 0.01f) // 1cm
				{
					SetState(true);
					return;
				}
			}
		}
		SetState(false);
	}

	/* Utils */

	private string GetPath(Transform t) {
		var path = t.name;
		while (t.parent != null) {
			t = t.parent;
			path = t.name + "/" + path;
		}
		return path;
	}

}
