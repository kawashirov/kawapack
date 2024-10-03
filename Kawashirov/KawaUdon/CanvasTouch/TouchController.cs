using System;
using UdonSharp;
using UnityEngine;
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

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class TouchController : CommonUSharpBehaviour {
	public Transform editorPointer;
	public TouchButton[] buttons;

	public override void Start() {
		base.Start();
		logName = "Kawa|TouchController";
	}

	private bool _TryInteract(Vector3 tracking_world) {
		var had_interaction = false;
		for (var i = 0; i < buttons.Length; ++i) {
			var button = buttons[i];
			if (Utilities.IsValid(button) && button.gameObject.activeInHierarchy && button._TryClick(tracking_world))
				had_interaction = true;
		}
		return had_interaction;
	}

	private void _TryInteractHand(VRCPlayerApi player, VRC_Pickup.PickupHand pickup_hand, HumanBodyBones finger_bone) {
		if (Utilities.IsValid(player.GetPickupInHand(pickup_hand)))
			return; // Игрок держит что-то этой рукой.
		var finger_pos = player.GetBonePosition(finger_bone);
		if (finger_pos.magnitude < 0.001f)
			return; // Палец в нулях -> палец инвалид.
		if (_TryInteract(finger_pos)) {
			player.PlayHapticEventInHand(pickup_hand, 0.5f, 0.5f, 0.5f);
		}
	}

	public void Update() {
		var player = Networking.LocalPlayer;
		if (Utilities.IsValid(player) && player.IsUserInVR()) {
			_TryInteractHand(player, VRC_Pickup.PickupHand.Left, HumanBodyBones.LeftIndexDistal);
			_TryInteractHand(player, VRC_Pickup.PickupHand.Right, HumanBodyBones.RightIndexDistal);
		} else if (Utilities.IsValid(editorPointer)) {
			// Assume Editor
			_TryInteract(editorPointer.position);
		}
	}

#if !COMPILER_UDONSHARP && UNITY_EDITOR
	private bool Validate_buttons() {
		var all_buttons = gameObject.scene.GetRootGameObjects()
			.SelectMany(g => g.GetComponentsInChildren<UdonBehaviour>())
			.Where(UdonSharpEditorUtility.IsUdonSharpBehaviour)
			.RuntimeOnly()
			.Select(UdonSharpEditorUtility.GetProxyBehaviour).OfType<TouchButton>()
			.Distinct().ToArray();
		return KawaUdonUtilities.ModifyArray(this, nameof(buttons), ref buttons, all_buttons);
	}

	public override void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_buttons, this, nameof(Validate_buttons));
	}

	public void OnDrawGizmosSelected() {
		var self_pos = transform.position;

		Gizmos.color = Color.white.Alpha(KawaGizmos.GizmosAplha);
		Gizmos.DrawWireSphere(self_pos, 0.1f);

		Gizmos.color = Color.green.Alpha(KawaGizmos.GizmosAplha);
		foreach (var receivers in buttons)
			if (Utilities.IsValid(receivers))
				Gizmos.DrawLine(self_pos, receivers.transform.position);
	}
#endif
}
