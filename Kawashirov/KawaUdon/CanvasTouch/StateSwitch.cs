using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using Kawashirov;
using Kawashirov.Udon;
using VRC.SDK3.Persistence;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class StateSwitch : CommonUSharpBehaviour {
	[Tooltip("Must be UdonBehaviours only.")]
	public Component[] EventReceivers;
	public string EventName = "_UpdateState";
	public int EventDelay = 1;
	public GameObject[] States;

	public int DefaultState = 0;
	public string PersistenceKey = "";

	[ReadOnly] public int CurrentState = 0;
	[ReadOnly] public bool PersistenceRestored = false;

	public override void Start() {
		base.Start();
		logName = "Kawa|StateSwitch";

		if (_EnsureValid(States, true, "States array is invalid!")) {
			_Ensure(States.Length > 1, $"states.Length={States.Length}");
		}

		PersistenceRestored = false;
		SendCustomEventDelayedSeconds(nameof(_UpdateState), 1f);
	}

	public void _SetDefault() {
		// Set Default value if previous wasnt restored yet
		if (PersistenceRestored)
			return;
		CurrentState = DefaultState;
		_UpdateState();
	}

	public override void OnPlayerRestored(VRCPlayerApi player) {
		if (!(Utilities.IsValid(player) && player.isLocal) || string.IsNullOrWhiteSpace(PersistenceKey))
			return;
		_Info($"PlayerData restored, figuring out prev state...");

		if (PlayerData.TryGetByte(player, PersistenceKey, out var value)) {
			_Info($"Got persistent \"{PersistenceKey}\"={value}");
			CurrentState = value;
		} else {
			_Info($"Got no persistent value, setting default \"{PersistenceKey}\"={DefaultState}...");
			CurrentState = DefaultState;
		}
		PersistenceRestored = true;
		// Avoid PlayerData rewrite on the same frame
		SendCustomEventDelayedFrames(nameof(_UpdateState), 1);
	}

	public void _UpdateState() {
		// _Info(nameof(_UpdateState));
		CurrentState %= States.Length;
		for (var i = 0; i < States.Length; ++i) {
			var state_go = States[i];
			if (Utilities.IsValid(state_go))
				state_go.SetActive(CurrentState == i);
		}
		for (var i = 0; i < EventReceivers.Length; ++i) {
			var receiver_c = EventReceivers[i];
			if (!Utilities.IsValid(receiver_c))
				continue;
			var receiver = (UdonBehaviour)receiver_c;
			_Info($"{receiver}.{EventName}(), delay={EventDelay}.");
			if (EventDelay < 1) {
				receiver.SendCustomEvent(EventName);
			} else {
				receiver.SendCustomEventDelayedFrames(EventName, EventDelay);
			}
		}
		if (PersistenceRestored && !string.IsNullOrWhiteSpace(PersistenceKey)) {
			PlayerData.SetByte(PersistenceKey, (byte)CurrentState);
		}
	}

	public void _NextState() {
		CurrentState = (CurrentState + 1) % States.Length;
		_UpdateState();
	}

#if !COMPILER_UDONSHARP && UNITY_EDITOR
	private bool Validate_states() =>
		KawaUdonUtilities.DistinctArray(this, nameof(States), ref States);

	private bool Validate_eventReceivers() =>
		KawaUdonUtilities.ValidateComponentsArrayOfUdonSharpBehaviours(this, nameof(EventReceivers), ref EventReceivers);

	public override void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_states, this, nameof(States));
		KawaUdonUtilities.ValidateSafe(Validate_eventReceivers, this, nameof(EventReceivers));

		if (gameObject.GetComponent<RectTransform>() == null)
			gameObject.AddComponent<RectTransform>();
	}

	public void OnDrawGizmosSelected() {
		var self_pos = transform.position;

		Gizmos.color = Color.white.Alpha(KawaGizmos.GizmosAplha);
		Gizmos.DrawWireSphere(self_pos, 0.1f);

		Gizmos.color = Color.green.Alpha(KawaGizmos.GizmosAplha);
		foreach (var receivers in EventReceivers)
			if (Utilities.IsValid(receivers))
				Gizmos.DrawLine(self_pos, receivers.transform.position);
	}
#endif
}
