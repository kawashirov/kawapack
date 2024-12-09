using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class DoorPhysHandle : CommonUSharpBehaviour {
	/* Config variables */

	[Tooltip("The door object.\nShould contain Rigidbody and HingeJoint.\nCan not be changed at run-time.")]
	public GameObject Door;
	[Tooltip("Where handle should be placed when idle (not held).\nShould be children of door GameObject.\nCan be changed at run-time.")]
	public Transform HandleHome;

	[Space, Tooltip("Do not set Rigidbody.angularDrag.\nThis script will override it.\nSet desired angular drag here.\nCan be changed at run-time.")]
	public float BasicAngularDrag = 0.05f;
	[Tooltip("Angular drag added when handle is nearby.")]
	public float HandleAngularDrag = 10.0f;
	[Tooltip("If angle between door and handle is less tahn this, then HandleAngularDrag will be applied non-lineary.")]
	public float HandleDragAngle = 5.0f;

	[Space, Tooltip("Basic torque applied to door to pull it towards handle.")]
	public float HandleTorque = 1.0f;
	[Tooltip("Maximum torque applied to door to prevent physics issues.")]
	public float HandleTorqueMax = 10.0f;

	[Space, Tooltip("If distance between handle and it's home place is more than this, then handle will be respawned.")]
	public float HandleMaxDistance = 0.5f;

	/* Runtime variables */

	// Инерция плохо синхронизируется в принципе, еще хуже когда Rigidbody.angularDrag меняется.
	// Ручная синхронизация вращения: сопротивление, вращение и скорость врещения.

	[UdonSynced(UdonSyncMode.Linear)] private float SyncAngularDrag = 0;
	[UdonSynced(UdonSyncMode.Linear)] private Quaternion SyncRotation = Quaternion.identity;
	[UdonSynced(UdonSyncMode.Linear)] private Vector3 SyncAngularVelocity = Vector3.zero;

	/* Internal variables */

	private VRC_Pickup _ThisPickup = null;
	private Rigidbody _DoorRigidbody = null;
	private HingeJoint _DoorHinge = null;

	/* Events */

	public override void Start() {
		base.Start();
		logName = "Kawa|DoorPhysHandle";

		_EnsureValid(Door, true, "Door GameObject is invalid!");
		_EnsureValid(HandleHome, true, "HandleHome Transform is invalid!");

		_Ensure(HandleHome.IsChildOf(Door.transform), true, "HandleHome is not child of Door!");

		_ThisPickup = GetComponent<VRC_Pickup>();
		_EnsureValid(_ThisPickup, true, "There is no VRC_Pickup here!");

		_DoorRigidbody = Door.GetComponent<Rigidbody>();
		_EnsureValid(_ThisPickup, true, "Door GameObject is missing Rigidbody!");
		
		_DoorHinge = Door.GetComponent<HingeJoint>();
		_EnsureValid(_ThisPickup, true, "Door GameObject is missing HingeJoint!");

		// _Info($"Initialized.");
	}

	public void FixedUpdate() {
		var handle_owner = Networking.GetOwner(gameObject);
		var is_editor = !Utilities.IsValid(handle_owner); // TODO delet this
		if (!(is_editor || handle_owner.isLocal))
			return;
		// Дальнейшая обработка только владельцем.

		// var handle_t = gameObject.transform;
		var global_handle_pos = transform.position;
		var global_handle_home = HandleHome.position;
		var distance = Vector3.Distance(global_handle_pos, global_handle_home);
		// Сброс рукоятки, если расстояние между домом и рукояткой слишком большое.
		// Не зависимо от того, удерживается кем-то или нет.
		// Не используем Magnitude и Sqr что бы уменьшить число удон инструкций на value типах.
		if (distance > HandleMaxDistance) {
			_ThisPickup.Drop();
			transform.SetPositionAndRotation(global_handle_home, HandleHome.rotation);
		}

		var is_held = is_editor || _ThisPickup.IsHeld; // В редакторе ручка всегда держится
		if (is_held) {
			var door_t = _DoorHinge.gameObject.transform;
			var local_door_axis = _DoorHinge.axis;
			var local_anchor = _DoorHinge.anchor;
			// глобальные координаты -> локлаьные -> проекция -> угол -> момент вращения
			// Вычисления угла делаются в локальных координатах двери
			var local_handle_home = door_t.InverseTransformPoint(global_handle_home);
			var local_handle_pos = door_t.InverseTransformPoint(global_handle_pos);
			var proj_handle_home = Vector3.ProjectOnPlane(local_handle_home - local_anchor, local_door_axis);
			var proj_handle_pos = Vector3.ProjectOnPlane(local_handle_pos - local_anchor, local_door_axis);
			var handle_angle = Vector3.SignedAngle(proj_handle_home, proj_handle_pos, local_door_axis);

			var handle_drag = ComputeDrag(handle_angle, HandleDragAngle, HandleAngularDrag);
			var angular_drag = BasicAngularDrag + handle_drag;
			_DoorRigidbody.angularDrag = angular_drag;
			var torque_scale = Mathf.Clamp(handle_angle * HandleTorque, -HandleTorqueMax, HandleTorqueMax);
			_DoorRigidbody.AddTorque(local_door_axis * torque_scale, ForceMode.Acceleration);
		} else if (distance > 0.001f /* 1 mm */) {
			transform.SetPositionAndRotation(global_handle_home, HandleHome.rotation);
		}

		// Если дверь "активна": т.е. на неё действует ручка или она не спит.
		if (is_held || !_DoorRigidbody.IsSleeping())
			SaveSync();
	}

	public override void OnDrop() {
		_Info($"Handle pickup dropped!");
		// Сброс позиции ручки в домашнее, если владелец
		if (Networking.IsOwner(gameObject))
			transform.SetPositionAndRotation(HandleHome.position, HandleHome.rotation);
	}

	public override void OnDeserialization() {
		// Получены данные
		LoadSync();
	}

	/* 
	// TODO
	public override void OnPlayerJoined(VRCPlayerApi player)
	{
			if (Networking.IsOwner(gameObject) && !_ThisPickup.IsHeld)
			{
					// Если ручка не держится то рандомно меняем владельца ручки на нового игрока что бы распределить нагрузку.
					if (UnityEngine.Random.Range(0, VRCPlayerApi.AllPlayers.Count + 1) == 0)
					{
							Debug.Log("[Kawa|DoorPhysHandle] Randomply transfering ownership to new player \"{player.displayName}\" @ {_path}", this);
							Networking.SetOwner(player, this_go);
					}
			}
	}
	*/

	/* Logics */

	public void LoadSync() {
		_DoorRigidbody.angularDrag = SyncAngularDrag;
		_DoorRigidbody.angularVelocity = SyncAngularVelocity;
		_DoorRigidbody.MoveRotation(SyncRotation);
	}

	public void SaveSync() {
		// Пока что не делаем проверки на приращение

		var angular_drag = _DoorRigidbody.angularDrag;
		//if (Mathf.Abs(SyncAngularDrag - angular_drag) > 0.001f)
		SyncAngularDrag = angular_drag;

		var angular_velocity = _DoorRigidbody.angularVelocity;
		//if (Vector3.SqrMagnitude(SyncAngularVelocity - angular_velocity) > 0.00001f)
		SyncAngularVelocity = angular_velocity;

		var rotation = _DoorRigidbody.rotation;
		// if (Quaternion.Angle(SyncRotation, rotation) > 0.001f)
		SyncRotation = rotation;
	}

	private float ComputeDrag(float angle, float drag_angle, float drag) {
		// [-drag_angle, 0, +drag_angle] -> [0, drag, 0]
		// 1. angle нормируется по drag_angle
		// 2. норма угла откусывается до [0, 1]
		// 3. [0, 1] -> [drag, 0]
		return Mathf.SmoothStep(drag, 0, Mathf.Clamp01(Mathf.Abs(angle / drag_angle)));
	}
}
