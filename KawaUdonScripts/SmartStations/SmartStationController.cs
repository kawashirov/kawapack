
using System;
using System.Text;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

public class SmartStationController : UdonSharpBehaviour {
	/* Config variables */

	public SmartStationUpdater Updater;

	/* Runtime variables */
	[NonSerialized] public VRCStation Station;
	[NonSerialized] public VRCPlayerApi Occupant = null;

	/* Internal variables */

	[NonSerialized] private string _path = "";

	/* Events */

	private void Start() {
		var trying_to_evade_bug = transform;
		_path = GetPath(trying_to_evade_bug);

		if (Updater == null) {
			Debug.LogErrorFormat(gameObject, "[Kawa|SmartStationController] KawaSmartStationUpdater is not set! @ {0}", _path);
			gameObject.SetActive(false);
		}

		Station = (VRCStation)GetComponent(typeof(VRCStation));
		if (Station == null) {
			Debug.LogErrorFormat(gameObject, "[Kawa|SmartStationController] No VRCStation attached! @ {0}", _path);
			gameObject.SetActive(false);
		}

		Debug.LogFormat(gameObject, "[Kawa|SmartStationController] Initialized. @ {0}", _path);

		UpdateOccupant();
	}

	public override void OnStationEntered(VRCPlayerApi player) {
		VRCPlayerApi exit = Occupant != null && Occupant != player ? player : null;

		if (Occupant == null || Occupant == player) {
			LogChangeOccupant(Occupant, player, "OnStationEntered");
			Occupant = player;
		}

		UpdateOccupant();

		if (exit != null)
			Station.ExitStation(exit);
	}

	public override void OnStationExited(VRCPlayerApi player) {
		VRCPlayerApi exit = Occupant == player ? null : Occupant;

		LogChangeOccupant(Occupant, null, "OnStationExited");
		Occupant = null;

		UpdateOccupant();
		
		if (exit != null)
			Station.ExitStation(exit);
	}

	public override void OnPlayerLeft(VRCPlayerApi player) {
		if (Occupant == player) {
			LogChangeOccupant(Occupant, null, "OnPlayerLeft");
			Occupant = null;
			UpdateOccupant();
		}
	}

	public override void OnDeserialization() {
		// ???
		UpdateOccupant();
	}

	/* Logics */

	public void UpdateOccupant() {
		var is_occupied = Occupant != null;
		var occupant_str = PlayerToString(Occupant);
		var updater_go = Updater != null ? Updater.gameObject : null;

		if (is_occupied && !Occupant.IsOwner(gameObject)) {
			Debug.LogFormat(gameObject, "[Kawa|SmartStationController] Setting Controller owner to {1}. @ {0}", _path, occupant_str);
			Networking.SetOwner(Occupant, gameObject);
		}

		if (is_occupied && updater_go != null && !Occupant.IsOwner(updater_go)) {
			Debug.LogFormat(gameObject, "[Kawa|SmartStationController] Setting Updater owner to {1}. @ {0}", _path, occupant_str);
			Networking.SetOwner(Occupant, updater_go);
		}
		
		if (updater_go != null && is_occupied != updater_go.activeSelf) {
			Debug.LogFormat(gameObject, "[Kawa|SmartStationController] Setting updataer state to {1}. @ {0}", _path, is_occupied);
			updater_go.SetActive(is_occupied);
		}
	}

	/* Utils */

	private void LogChangeOccupant(VRCPlayerApi old_occupant, VRCPlayerApi new_occupant, string reason) {
		var old_occupant_str = PlayerToString(old_occupant);
		var new_occupant_str = PlayerToString(new_occupant);
		if (old_occupant != null && new_occupant != null) {
			Debug.LogWarningFormat(gameObject, "[Kawa|SmartStationController] Changing occupant {1} -> {2}, reason: {3}. @ {0}", _path, old_occupant_str, new_occupant_str, reason);
		} else {
			Debug.LogFormat(gameObject, "[Kawa|SmartStationController] Changing occupant {1} -> {2}, reason: {3}. @ {0}", _path, old_occupant_str, new_occupant_str, reason);
		}
	}

	private string PlayerToString(VRCPlayerApi player) {
		if (player == null)
			return "null";
		var tags = player.isLocal ? "local" : "remote";
		if (player.isMaster)
			tags = "master," + tags;
		tags = (player.IsUserInVR() ? "vr" : "desktop") + "," + tags;
		return string.Format("\"{0}\" ({2}#{1})", player.displayName, player.playerId, tags);
	}

	private string GetPath(Transform t) {
		var path = t.name;
		while (t.parent != null) {
			t = t.parent;
			path = t.name + "/" + path;
		}
		return path;
	}
}
