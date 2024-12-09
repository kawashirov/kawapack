using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Kawashirov;
using Kawashirov.Udon;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SelectableSpawn : CommonUSharpBehaviour {
	public StateSwitch SwitchButton;
	public SpawnGroup[] SpawnGroups;
	// public string AutoBindSpawnGroupProgram = "SpawnGroup"; // Not working yet

	[Space]
	public bool PlayerActive = false;

	public override void Start() {
		base.Start();
		logName = nameof(SelectableSpawn);
		PlayerActive = false;

		_Ensure(SwitchButton, $"{nameof(SpawnGroups)} is invalid!");
		_EnsureCountValid(SpawnGroups, true, nameof(SpawnGroups));
	}

	public void _RespawnLocalPlayer(VRCPlayerApi player) {
		if (!(Utilities.IsValid(player) && player.isLocal))
			return;

		if (!Utilities.IsValid(SpawnGroups)) {
			_Error($"Can't respawn player: {nameof(SpawnGroups)} is invalid!");
			return;
		}

		var group_idx = SwitchButton.CurrentState % SpawnGroups.Length;
		var group = SpawnGroups[group_idx];
		if (!Utilities.IsValid(group)) {
			_Error($"Can't respawn player: {nameof(SpawnGroups)}[{group_idx}] is invalid!");
			return;
		}

		var spawn = group._PickRandom();
		if (!Utilities.IsValid(spawn)) {
			_Error($"Can't respawn player: {nameof(SpawnGroups)}[{group_idx}] picked invalid spawn!");
			return;
		}

		_Info($"Respawning at {nameof(SpawnGroups)}[{group_idx}]: {spawn}...");
		player.TeleportTo(spawn.position, spawn.rotation);
	}

	private void _PlayerActive() => PlayerActive = true;

	public override void InputJump(bool value, UdonInputEventArgs args) => _PlayerActive();
	public override void InputUse(bool value, UdonInputEventArgs args) => _PlayerActive();
	public override void InputGrab(bool value, UdonInputEventArgs args) => _PlayerActive();
	public override void InputDrop(bool value, UdonInputEventArgs args) => _PlayerActive();
	public override void InputMoveHorizontal(float value, UdonInputEventArgs args) => _PlayerActive();
	public override void InputMoveVertical(float value, UdonInputEventArgs args) => _PlayerActive();

	public void _UpdateState() {
		// Вызывается из SwitchButton, 
		// когда пользователь сменил настройку сам или она подгрузилась из Persistence.
		// Если пользователь не был активен, значит скорее всего это обновление из-за Persistence в самом начале,
		// тогда игрока нужно заспавнить в корректном месте.
		if (PlayerActive)
			return;
		_Info($"Respawning due to persistence update. Probably...");
		_RespawnLocalPlayer(Networking.LocalPlayer);
	}

	public override void OnPlayerJoined(VRCPlayerApi player) {
		_RespawnLocalPlayer(player);
	}

	public override void OnPlayerRespawn(VRCPlayerApi player) {
		_RespawnLocalPlayer(player);
	}

#if !COMPILER_UDONSHARP && UNITY_EDITOR
	private bool Validate_AutoBindSpawnGroupProgram() {
		// TODO
		// KawaUdonUtilities.ValidProgramName(AutoBindSpawnGroupProgram, $"{nameof(AutoBindSpawnGroupProgram)}", this);
		return false;
	}

	private bool Validate_SpawnGroups() {
		// TODO
		// Мы не можем произвольно пере-записывать SpawnGroups т.к. порядок и соответствие индексов имеют значение.
		return false;
	}

	public override void Refresh() {
		// KawaUdonUtilities.ValidateSafe(Validate_AutoBindSpawnGroupProgram, this, nameof(AutoBindSpawnGroupProgram));
		// KawaUdonUtilities.ValidateSafe(Validate_SpawnGroups, this, nameof(SpawnGroups));
	}
#endif
}
