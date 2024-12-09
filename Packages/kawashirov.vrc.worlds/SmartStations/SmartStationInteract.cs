using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SmartStationInteract : CommonUSharpBehaviour {
	/* Config variables */
	public SmartStationController Controller;
	[Tooltip("If 0: Do not control rotation.\n"
		+ "If 1: Set rotation = ExplicitRotation.\n"
		+ "If 2: Compute rotation from player view direction.\n"
		+ "If 3: Same as 2, but opposite.")]
	public int ShouldSetRotation = 0;
	public float ExplicitRotation = 0;

	public override void Start() {
		base.Start();
		logName = "Kawa|SmartStation|Interact";

		_EnsureValid(Controller, true, "Controller is invalid!");
	}

	public override void Interact() {
		if (!Utilities.IsValid(Controller) || Utilities.IsValid(Controller.Occupant))
			return;
		var updater = Controller.Updater;
		if (!Utilities.IsValid(updater)) {
			_Error("Controller.Updater is not set!");
			return;
		}

		if (ShouldSetRotation == 1) {
			updater.CurrentRotation = ExplicitRotation;
		} else if (ShouldSetRotation == 2 || ShouldSetRotation == 3) {
			var ref_t = updater.ReferenceSeat;
			if (Utilities.IsValid(ref_t)) {
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
}
