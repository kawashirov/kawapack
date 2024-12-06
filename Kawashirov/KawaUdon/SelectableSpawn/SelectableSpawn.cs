using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Kawashirov;
using Kawashirov.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class SelectableSpawn : CommonUSharpBehaviour {
	//public Desc
	public StateSwitch SwitchButton;
	public SpawnGroup[] SpawnGroups;
	public string AutoBindSpawnGroupProgram = "SpawnGroup";

	public override void Start() {
		base.Start();
		logName = "SelectableSpawn";

		_Ensure(SwitchButton, "SwitchButton is invalid!");
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

	public override void OnPlayerJoined(VRCPlayerApi player) {
		_RespawnLocalPlayer(player);
	}

	public override void OnPlayerRespawn(VRCPlayerApi player) {
		_RespawnLocalPlayer(player);
	}

#if !COMPILER_UDONSHARP && UNITY_EDITOR
	private bool Validate_AutoBindSpawnGroupProgram() {
		KawaUdonUtilities.ValidProgramName(AutoBindSpawnGroupProgram, $"{nameof(AutoBindSpawnGroupProgram)}", this);
		return false;
	}

	private bool Validate_SpawnGroups() {
		// TODO
		return false;
	}

	public override void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_AutoBindSpawnGroupProgram, this, nameof(AutoBindSpawnGroupProgram));
		KawaUdonUtilities.ValidateSafe(Validate_SpawnGroups, this, nameof(SpawnGroups));
	}
#endif
}
