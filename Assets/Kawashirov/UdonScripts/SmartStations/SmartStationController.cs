
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
	[NonSerialized] public bool LocallyOccupied = false;
	[NonSerialized, UdonSynced(UdonSyncMode.None)] public int OccupantID = -1;

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

		OccupantID = -1;
		LocallyOccupied = false;

		Station = (VRCStation)GetComponent(typeof(VRCStation));
		if (Station == null) {
			Debug.LogErrorFormat(gameObject, "[Kawa|SmartStationController] No VRCStation attached! @ {0}", _path);
			gameObject.SetActive(false);
		}

		Debug.LogFormat(gameObject, "[Kawa|SmartStationController] Initialized. @ {0}", _path);

		UpdateOccupant();
		CheckUpdater();
	}

	public void OnLocalStationEntered() {
		LocallyOccupied = true;
		UpdateOccupant();
	}

	public void OnLocalStationExited() {
		LocallyOccupied = false;
		UpdateOccupant();
	}

	public void OnRemoteStationEntered() {
		LocallyOccupied = false;
		UpdateOccupant();
	}

	public void OnRemoteStationExited() {
		LocallyOccupied = false;
		UpdateOccupant();
	}

	/*
	public override void OnOwnershipTransferred()
	{
			Debug.LogFormat(gameObject, "[Kawa|SmartStationController] Updating OnOwnershipTransferred... @ {0}", _path);
			SendCustomNetworkEvent(NetworkEventTarget.All, "UpdateOccupant");
	}
	*/

	public override void OnPlayerLeft(VRCPlayerApi player) {
		if (OccupantID != player.playerId)
			return;
		LogChangeOccupant(OccupantID, -1, "OnPlayerLeft");
		OccupantID = -1;
		CheckUpdater();
	}

	public override void OnDeserialization() {
		// Если OccupantID изменился
		CheckUpdater();
	}

	/* Logics */

	public void UpdateOccupant() {
		var player_local = Networking.LocalPlayer;
		if (player_local == null)
			return;
		var player_local_id = player_local.playerId;
		var is_owner_ctrl = Networking.IsOwner(gameObject);

		var current_occupant_id = OccupantID;

		// var occupant_str = PlayerIDToString(current_occupant_id);
		// Debug.LogFormat(gameObject, "[Kawa|SmartStationController] Updating occupant: {1} @ {0}", _path, occupant_str);

		if (LocallyOccupied && is_owner_ctrl && current_occupant_id != player_local_id) {
			LogChangeOccupant(current_occupant_id, player_local_id, "locally occupied");
			OccupantID = player_local_id;
			CheckUpdater();
		}

		if (!LocallyOccupied && is_owner_ctrl && current_occupant_id == player_local_id) {
			LogChangeOccupant(current_occupant_id, -1, "not locally occupied");
			OccupantID = -1;
			CheckUpdater();
		}

		if (LocallyOccupied && !is_owner_ctrl) {
			Networking.SetOwner(player_local, gameObject);
		}

		if (Updater != null) {
			var updater_go = Updater.gameObject; // getter
			if (LocallyOccupied && !Networking.IsOwner(updater_go)) {
				Networking.SetOwner(player_local, updater_go);
			}
		}
	}

	private void CheckUpdater() {
		if (Updater != null) {
			var is_occupied = OccupantID != -1;
			var updater_go = Updater.gameObject;
			if (is_occupied != updater_go.activeSelf) {
				Debug.LogFormat(gameObject, "[Kawa|SmartStationController] Setting updataer state to {1}. @ {0}", _path, is_occupied);
				updater_go.SetActive(is_occupied);
			}
		}
	}

	/* Utils */

	private void LogChangeOccupant(int old_occupant, int new_occupant, string reason) {
		var old_occupant_str = PlayerIDToString(old_occupant);
		var new_occupant_str = PlayerIDToString(new_occupant);
		Debug.LogFormat(gameObject, "[Kawa|SmartStationController] Changing occupant {1} -> {2}, reason: {3}. @ {0}", _path, old_occupant_str, new_occupant_str, reason);
	}

	private string PlayerIDToString(int player) {
		return PlayerToString(player < 0 ? null : VRCPlayerApi.GetPlayerById(player));
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
