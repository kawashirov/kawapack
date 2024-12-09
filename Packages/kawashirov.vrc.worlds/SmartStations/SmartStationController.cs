using System;
using UdonSharp;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SmartStationController : CommonUSharpBehaviour {
	/* Config variables */

	public SmartStationUpdater Updater;

	/* Runtime variables */
	[NonSerialized] public VRCStation Station;
	[NonSerialized] public VRCPlayerApi Occupant = null;

	public override void Start() {
		base.Start();
		logName = "Kawa|SmartStation|Controller";

		_EnsureValid(Updater, true, "SmartStationUpdater is invalid!");

		Station = GetComponent<VRCStation>();
		_EnsureValid(Station, true, "No VRCStation attached!");

		SendCustomEventDelayedSeconds(nameof(_UpdateOccupant_OnStartDelayed), 2);

		// _Info($"Initialized.");
	}

	public void _UpdateOccupant_OnStartDelayed() => _UpdateState_Internal(nameof(Start));

	public override void OnStationEntered(VRCPlayerApi player) {
		if (Utilities.IsValid(Occupant) && Occupant != player) {
			var old_occupant_str = _PlayerToString(Occupant);
			var new_occupant_str = _PlayerToString(player);
			_Warning($"OnStationEntered fired for {new_occupant_str}, but OnStationExited wasnt for {old_occupant_str}! Desync!");
			Station.ExitStation(Occupant);
			// Не обновляем Occupant на player ..?
			// Повторный _UpdateState в след. кадре что бы попытаться засинхрониться.
			SendCustomEventDelayedFrames(nameof(_UpdateState_OnStationEntered_Delayed), 2);
		} else {
			// Occupant: null -> player
			_LogChangeOccupant(Occupant, player, nameof(OnStationEntered));
			Occupant = player;
		}

		_UpdateState_Internal(nameof(OnStationEntered));
	}

	public void _UpdateState_OnStationEntered_Delayed() => _UpdateState_Internal(nameof(OnStationEntered));

	public override void OnStationExited(VRCPlayerApi player) {
		if (Occupant != player) {
			var exited_str = _PlayerToString(player);
			if (Utilities.IsValid(Occupant)) {
				var occupant_str = _PlayerToString(Occupant);
				_Warning($"OnStationExited fired for {exited_str}, but Occupant was {occupant_str}! Desync!");
				Station.ExitStation(Occupant);
			} else {
				_Warning($"OnStationExited fired for {exited_str}, but Occupant was null! Desync!");
			}
			// Повторный _UpdateState в след. кадре что бы попытаться засинхрониться.
			SendCustomEventDelayedFrames(nameof(_UpdateState_OnStationExited_Delayed), 2);
		}

		_LogChangeOccupant(Occupant, null, nameof(OnStationExited));
		Occupant = null;

		_UpdateState_Internal(nameof(OnStationExited));
	}

	public void _UpdateState_OnStationExited_Delayed() => _UpdateState_Internal(nameof(OnStationEntered));

	public override void OnPlayerLeft(VRCPlayerApi player) {
		if (Occupant == player) {
			_LogChangeOccupant(Occupant, null, nameof(OnPlayerLeft));
			Occupant = null;
			_UpdateState_Internal(nameof(OnPlayerLeft));
		}
	}

	public override void OnDeserialization() {
		_UpdateState_Internal(nameof(OnDeserialization));
	}

	public override bool OnOwnershipRequest(VRCPlayerApi requestingPlayer, VRCPlayerApi requestedOwner) {
		// Если стул свободен, то любой может быть овнером, если занят то только сидящий на нём.
		SendCustomEventDelayedFrames(nameof(_UpdateState_Internal), 2);
		return !Utilities.IsValid(Occupant) || requestedOwner == Occupant;
	}

	public override void OnOwnershipTransferred(VRCPlayerApi player) {
		_UpdateState_Internal(nameof(OnOwnershipTransferred));
		SendCustomEventDelayedFrames(nameof(_UpdateState_Internal), 2);
	}

	/* Logics */

	public void _UpdateState() => _UpdateState_Internal(nameof(_UpdateState));

	private void _UpdateState_Internal(string cause) {
		// _Info($"_UpdateState_Internal");
		var is_occupied = Utilities.IsValid(Occupant);
		var occupant_str = _PlayerToString(Occupant);
		var updater_go = Utilities.IsValid(Updater) ? Updater.gameObject : null;
		// _Info($"_UpdateState_Internal: is_occupied={is_occupied}, occupant_str={occupant_str}, updater_go={updater_go}");

		if (is_occupied && !Occupant.IsOwner(gameObject)) {
			_Info($"Setting Controller owner to {occupant_str}, updated by {cause}.");
			Networking.SetOwner(Occupant, gameObject);
		}

		if (is_occupied && Utilities.IsValid(updater_go) && !Occupant.IsOwner(updater_go)) {
			_Info($"Setting Updater owner to {occupant_str}, updated by {cause}.");
			Networking.SetOwner(Occupant, updater_go);
		}

		if (Utilities.IsValid(updater_go) && is_occupied != updater_go.activeSelf) {
			_Info($"Setting updataer state to {is_occupied} as {occupant_str} is sitting, updated by {cause}.");
			updater_go.SetActive(is_occupied);
		}
	}

	/* Utils */

	private void _LogChangeOccupant(VRCPlayerApi old_occupant, VRCPlayerApi new_occupant, string reason) {
		var old_occupant_str = _PlayerToString(old_occupant);
		var new_occupant_str = _PlayerToString(new_occupant);
		if (Utilities.IsValid(old_occupant) && Utilities.IsValid(new_occupant)) {
			_Warning($"Changing occupant {old_occupant_str} -> {new_occupant_str}, reason: {reason}.");
		} else {
			_Info($"Changing occupant {old_occupant_str} -> {new_occupant_str}, reason: {reason}.");
		}
	}

	protected override string _PlayerToString_Tag(VRCPlayerApi player, string tags) {
		return (player.IsUserInVR() ? "vr" : "desktop") + "," + tags;
	}
}
