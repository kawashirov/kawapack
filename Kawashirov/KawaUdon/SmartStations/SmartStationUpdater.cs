using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class SmartStationUpdater : UdonSharpBehaviour {
	public SmartStationController Controller;


	[Space, Tooltip("Will use this transform as reference for seated location.")]
	public Transform ReferenceSeat;

	[Tooltip("Will use this transform as reference for exit location.")]
	public Transform ReferenceExit;

	[Space]
	[Tooltip("Transform used as Enter in VRCStation.\nWill be dynamically updated at run-time.")]
	public Transform DynamicSeat;

	[Tooltip("Transform used as Exit in VRCStation.\nWill be dynamically updated at run-time.")]
	public Transform DynamicExit;

	[Space]
	[Tooltip("Should try to keep player vertical in world-space?")]
	public bool KeepVertical = false;

	[Space]
	[Tooltip("Allow players to rotate themselves while being seat on station using Move Left-Right input (A/D keys).\n"
		+ "Make sure VRCStation.disableStationExit set to true.")]
	public bool AllowRotation = false;

	[Tooltip("Maximum allowed rotation angle to left side direction in degrees.\n"
		+ "Set both MaxLeftRotation and MaxRightRotation to >180 or <0 to remove limits.")]
	public float MaxLeftRotation = 60;

	[Tooltip("Maximum allowed rotation angle to right side direction in degrees.\n"
		+ "Set both MaxLeftRotation and MaxRightRotation to >180 or <0 to remove limits.")]
	public float MaxRightRotation = 60;

	[Tooltip("Degrees per second.")]
	public float RotationSpeed = 60; // 6 sec = full circle

	[Tooltip("Current rotation of seat. Can be read (by anyone) or writen (by owner/occupant) from other udon scripts at run-time.")]
	[UdonSynced(UdonSyncMode.Linear)] public float CurrentRotation = 0;


	[Space]
	[Tooltip("Allow players to move themselves up and down while being seat on station using Move Forward-Back input (W/S keys).\n"
		+ "Make sure VRCStation.disableStationExit set to true.")]
	public bool AllowLift = false;

	[Tooltip("Maximum allowed seat upwards movement distance.\nTo disable set both MaxLiftUp and MaxLiftDown to 0.")]
	public float MaxLiftUp = 0.1f;
	[Tooltip("Maximum allowed seat downwards movement distance.\nTo disable set both MaxLiftUp and MaxLiftDown to 0.")]
	public float MaxLiftDown = 0.1f;

	[Tooltip("Meters per second.")]
	public float LiftSpeed = 0.2f;

	[Tooltip("Current lift of seat. Can be read (by anyone) or writen (by owner/occupant) from other udon scripts at run-time.")]
	[UdonSynced(UdonSyncMode.Linear)] public float CurrentLift = 0;

	[Space]
	[Tooltip("If 0: No custom reference.\nIf 1: Use SingleBone as Reference.\nIf 2: Use median ReferenceBoneA and ReferenceBoneB.")]
	public int UseCustomReference = 0;

	[Tooltip("If any Reference used, offset vector will be scaled by this value.\nRecomended values from 0.9 to 1.1.")]
	public float CustomReferenceOffsetMultiplier = 1.0f;

	[Tooltip("Will try to put this bone of player avatar to seat Transform location.\nRecomended to use only mandatory humanoid bones.\nUsed when UseCustomReference == 1.")]
	public HumanBodyBones SingleReferenceBone = HumanBodyBones.Hips;

	[Tooltip("Recomended to use only mandatory humanoid bones.\nUsed when UseCustomReference == 2.")]
	public HumanBodyBones ReferenceBoneA = HumanBodyBones.LeftUpperLeg;

	[Tooltip("Recomended to use only mandatory humanoid bones.\nUsed when UseCustomReference == 2.")]
	public HumanBodyBones ReferenceBoneB = HumanBodyBones.RightUpperLeg;

	[Range(0.001f, 0.999f)]
	public float LerpSpeed = 0.9f;

	/* Internal variables */
	[NonSerialized] private bool _should_exit = false;
	[NonSerialized] private float _horizontal_axis = 0;
	[NonSerialized] private float _vertical_axis = 0;

	[NonSerialized] private string _path = "";

	void Start() {
		_path = GetPath(transform);

		if (Controller == null) {
			Debug.LogErrorFormat(gameObject, "[Kawa|SmartStationUpdater] KawaSmartStationController is not set! @ {0}", _path);
			gameObject.SetActive(false);
		}

		if (ReferenceSeat == null) {
			Debug.LogWarningFormat(gameObject, "[Kawa|SmartStationUpdater] ReferenceSeat is not set. Will use station itself as reference. @ {0}", _path);
			ReferenceSeat = transform;
		}

		if (ReferenceExit == null) {
			Debug.LogWarningFormat(gameObject, "[Kawa|SmartStationUpdater] ReferenceSeat is not set. Will use station itself as reference. @ {0}", _path);
			ReferenceExit = transform;
		}

		if (DynamicSeat == null) {
			Debug.LogErrorFormat(gameObject, "[Kawa|SmartStationUpdater] DynamicSeat is not set! @ {0}", _path);
			gameObject.SetActive(false);
		}

		if (DynamicExit == null) {
			Debug.LogErrorFormat(gameObject, "[Kawa|SmartStationUpdater] DynamicExit is not set! @ {0}", _path);
			gameObject.SetActive(false);
		}

		Debug.LogFormat(gameObject, "[Kawa|SmartStationUpdater] Initialized. @ {0}", _path);

		if (Controller != null) {
			UpdateOwnership();
			UpdateDynamicExit();
			UpdateDynamicSeat();
		}
	}

	private void OnEnable() {
		Debug.LogFormat(gameObject, "[Kawa|SmartStationUpdater] Enabled. @ {0}", _path);
		if (Controller != null) {
			UpdateOwnership();
			UpdateDynamicSeat();
			UpdateDynamicExit();
		}
	}

	private void OnDisable() {
		Debug.LogFormat(gameObject, "[Kawa|SmartStationUpdater] Disabled. @ {0}", _path);
	}

	private void Update() {
		if (Controller != null) {
			UpdateOwnership();
			UpdateDynamicSeat();
			UpdateDynamicExit();
		}
	}

	private void UpdateOwnership() {
		var owner_this = Networking.GetOwner(gameObject);
		var owner_ctrl = Networking.GetOwner(Controller.gameObject);
		if (owner_this != owner_ctrl)
			Networking.SetOwner(owner_ctrl, gameObject);
	}

	private void UpdateDynamicExit() {
		var forward = ReferenceExit.forward;
		forward.y = 0;
		// Когда forward (0,0,0) оно работает как (0,0,1), так что всё ок.
		DynamicExit.SetPositionAndRotation(ReferenceExit.position, Quaternion.LookRotation(forward));
	}

	private void UpdateDynamicSeat() {
		var occupant = Controller.Occupant;
		if (occupant == null) {
			DynamicSeat.SetPositionAndRotation(ReferenceSeat.position, Quaternion.identity);
		} else {
			UpdateDynamicSeatOccupied(occupant);
		}
	}

	private void UpdateDynamicSeatOccupied(VRCPlayerApi occupant) {
		// Обновляет DynamicSeat если в нйм сидит occupant

		var ref_seat_pos = ReferenceSeat.position;

		if (AllowLift) {
			if (Networking.IsOwner(Controller.gameObject)) {
				var custom_lift = CurrentLift;
				custom_lift += _vertical_axis * Time.deltaTime * LiftSpeed;
				custom_lift = Mathf.Clamp(custom_lift, -MaxLiftDown, MaxLiftUp);
				if (Mathf.Abs(custom_lift - CurrentLift) > 0.001f)
					CurrentLift = custom_lift;
			}
			ref_seat_pos += ReferenceSeat.up * CurrentLift;
		}

		var position = DynamicSeat.position;
		var rotation = ReferenceSeat.rotation;

		if (UseCustomReference == 1) {
			// Смещение позиции по одной кости (SingleReferenceBone)
			var ref_bone_pos = occupant.GetBonePosition(SingleReferenceBone);
			var player_pos = occupant.GetPosition();
			var target_pos = ref_seat_pos - (ref_bone_pos - player_pos) * CustomReferenceOffsetMultiplier;
			position = Vector3.Lerp(target_pos, position, LerpSpeed);

		} else if (UseCustomReference == 2) {
			// Смещение позиции по двум костям (ReferenceBoneA и ReferenceBoneB)
			var ref_a_pos = occupant.GetBonePosition(ReferenceBoneA);
			var ref_b_pos = occupant.GetBonePosition(ReferenceBoneB);
			var ref_bone_pos = (ref_a_pos * 0.5f) + (ref_b_pos * 0.5f);
			var player_pos = occupant.GetPosition();
			var target_pos = ref_seat_pos - (ref_bone_pos - player_pos) * CustomReferenceOffsetMultiplier;
			position = Vector3.Lerp(target_pos, position, LerpSpeed);
		}

		if (KeepVertical) {
			var forward = ReferenceSeat.forward;
			forward.y = 0;
			// Когда forward (0,0,0) оно работает как (0,0,1), так что всё ок.
			rotation = Quaternion.LookRotation(forward);
		}

		if (AllowRotation) {
			if (Networking.IsOwner(Controller.gameObject)) {
				var custom_rotation = CurrentRotation;
				custom_rotation += _horizontal_axis * Time.deltaTime * RotationSpeed;
				if (MaxLeftRotation >= 0f && MaxLeftRotation <= 180f)
					custom_rotation = Mathf.Max(custom_rotation, -MaxLeftRotation);
				if (MaxRightRotation >= 0f && MaxRightRotation <= 180f)
					custom_rotation = Mathf.Min(custom_rotation, MaxRightRotation);
				if (custom_rotation < -180f)
					custom_rotation += 360f;
				if (custom_rotation > 180f)
					custom_rotation -= 360f;
				if (Mathf.Abs(custom_rotation - CurrentRotation) > 0.001f)
					CurrentRotation = custom_rotation;
			}

			rotation *= Quaternion.Euler(0f, CurrentRotation, 0f);
		}

		// Выход 
		if (occupant.isLocal && _should_exit) {
			Controller.Station.ExitStation(occupant);
			_should_exit = false;
		}

		DynamicSeat.SetPositionAndRotation(position, rotation);
	}

	private bool IsLocalOccupant() => Utilities.IsValid(Controller.Occupant) && Controller.Occupant.isLocal;

	public override void InputJump(bool value, UdonInputEventArgs args) {
		if (value && !_should_exit && IsLocalOccupant())
			_should_exit = true;
	}

	public override void InputMoveVertical(float value, UdonInputEventArgs args) {
		if (AllowLift) {
			_vertical_axis = value;
		} else {
			// Если вертикальное перемещение выключено, 
			// то шаг вперед (более чем на 50% для защиты от дрифта) для выхода из сидушки
			_vertical_axis = 0;
			if (value > 0.5f && !_should_exit && IsLocalOccupant())
				_should_exit = true;
		}
	}

	public override void InputMoveHorizontal(float value, UdonInputEventArgs args) {
		if (IsLocalOccupant())
			_horizontal_axis = value;
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
