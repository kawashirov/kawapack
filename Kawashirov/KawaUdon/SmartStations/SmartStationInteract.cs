
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class SmartStationInteract : UdonSharpBehaviour {
	/* Config variables */

	public SmartStationController Controller;
	[Tooltip("If 0: Do not control rotation.\nIf 1: Set rotation = ExplicitRotation.\nIf 2: Compute rotation from player view direction.\nIf 3: Same as 2, but opposite.")]
	public int ShouldSetRotation = 0;
	public float ExplicitRotation = 0;

	/* Internal variables */

	private string _path = "";

	public void Start() {
		_path = GetPath(transform);

		if (Controller == null) {
			Debug.LogErrorFormat(gameObject, "[Kawa|SmartStationInteract] Controller is not set! @ {0}", _path);
		}
	}

	public override void Interact() {
		if (Controller == null || Controller.Occupant != null)
			return;
		var updater = Controller.Updater;
		if (updater == null) {
			Debug.LogErrorFormat(gameObject, "[Kawa|SmartStationInteract] Controller.Updater is not set! @ {0}", _path);
			return;
		}

		if (ShouldSetRotation == 1) {
			updater.CurrentRotation = ExplicitRotation;
		} else if (ShouldSetRotation == 2 || ShouldSetRotation == 3) {
			var ref_t = updater.ReferenceSeat;
			if (ref_t != null) {
				var data = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

				var local_dir_player = ref_t.InverseTransformDirection(data.rotation * Vector3.forward);
				local_dir_player.y = 0; // Remove local Y, projecting on XZ.
				if (ShouldSetRotation == 3)
					local_dir_player = -local_dir_player;

				updater.CurrentRotation = Vector3.SignedAngle(Vector3.forward, local_dir_player, Vector3.up);
			}
		}

		Controller.Station.UseStation(Networking.LocalPlayer);
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
