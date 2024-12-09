using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;
using UdonSharp;
using Kawashirov.Udon;
using Kawashirov;
using System.Linq;


[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BGMAudioSync : CommonUSharpBehaviour {
	[Range(0, 1)] public float Volume = 0.05f;
	public AudioClip[] Clips;

	[Space]

	[Tooltip("Toggle BGM manually by your code.")]
	public bool PlayBGM = true;

	[Tooltip("(Optional) Use StateSwitch state to toggle BGM.")]
	public StateSwitch SwitchButton;

	[Tooltip("(Optional) Use TVPlugins for ArchiTechPro to mute BGM while video playing.")]
	public BGMAudioSync_ArchiTechProTVBridge[] ArchiTechProTVBridges;

	[Space]

	[ReadOnly][UdonSynced] public int CurrentClip = 0;
	[ReadOnly][UdonSynced] public int CurrentSample = 0;
	[ReadOnly] public AudioSource Source;

	public override void Start() {
		base.Start();
		logName = "Kawa|BGMAudioSync";

		if (_EnsureValid(SwitchButton, "SwitchButton is invalid!")) {
			var event_name = nameof(_UpdateVolume);
			var curr_name = SwitchButton.EventName;
			if (!string.Equals(curr_name, event_name)) {
				_Warning($"Updating SwitchButton.eventName: {curr_name} -> {event_name}");
				SwitchButton.EventName = event_name;
			}
			SendCustomEventDelayedSeconds(nameof(_SetDefault), 0.9f);
		}

		if (_EnsureAll(ArchiTechProTVBridges, nameof(ArchiTechProTVBridges)))
			foreach (var bridge in ArchiTechProTVBridges)
				if (Utilities.IsValid(bridge))
					bridge.Main = this;

		Source = GetComponent<AudioSource>();
		_EnsureValid(Source, true, "AudioSource is invalid!");

		if (_EnsureValid(Clips, true, "Clips array is invalid!")) {
			float length = 0;
			_Ensure(Clips.Length > 0, true, "Clips array is empty!");
			for (var i = 0; i < Clips.Length; ++i) {
				var clip = Clips[i];
				if (_Ensure(clip, true, $"Clips[{i}] is invalid!")) {
					clip.LoadAudioData();
					length += clip.length;
				}
			}
			_Info($"Loaded {Clips.Length} audio clips, total time: {length}");
		}
	}

	public void _SetDefault() {
		if (Utilities.IsValid(SwitchButton)) {
			SwitchButton.CurrentState = 1;
			SwitchButton._UpdateState();
		}
		SendCustomEventDelayedFrames(nameof(_UpdateVolume), 1);
	}

	public void Update() {
		if (Source.isPlaying)
			return;
		if (!Networking.IsOwner(gameObject))
			return;

		var next_clip = Random.Range(0, Clips.Length);
		if (next_clip == CurrentClip)
			next_clip = (next_clip + 1) % Clips.Length;
		CurrentClip = next_clip;
		CurrentSample = 0;
		var clip = Clips[CurrentClip];
		_Info($"Choosen new audio clip #{CurrentClip} (\"{clip.name}\").");

		Source.Stop();
		Source.clip = clip;
		Source.timeSamples = 0;
		Source.Play();
		Source.timeSamples = 0;

		RequestSerialization();
	}

	public override void OnPreSerialization() {
		_Info($"Serializing audio data: clip={CurrentClip}, sample={CurrentSample}.");
		CurrentSample = Source.timeSamples;
	}

	public override void OnDeserialization() {
		_Info($"Received audio data: clip={CurrentClip}, sample={CurrentSample}.");
		Source.Stop();
		Source.clip = Clips[CurrentClip];
		Source.timeSamples = CurrentSample;
		Source.Play();
		Source.timeSamples = CurrentSample;
	}

	public override void OnPostSerialization(SerializationResult result) {
		if (result.success) {
			_Info($"Successfuly serialized {result.byteCount} bytes of audio data.");
		} else {
			// Повторить попытку через 5 сек
			_Info($"Failed to serialize audio data.");
			SendCustomEventDelayedSeconds("_RequestSerializationDelayed", 5);
		}
	}

	public override void OnPlayerJoined(VRCPlayerApi player) => RequestSerialization();

	public override void OnPlayerSuspendChanged(VRCPlayerApi player) => RequestSerialization();

	// public override void OnPlayerRespawn(VRCPlayerApi player) => RequestSerialization();

	public override void OnOwnershipTransferred(VRCPlayerApi player) => RequestSerialization();

	public void _RequestSerializationDelayed() => RequestSerialization();

	public bool _ShouldMuteBGM() {
		if (!PlayBGM)
			return false;

		if (Utilities.IsValid(SwitchButton) && SwitchButton.CurrentState != 1)
			return false;

		if (Utilities.IsValid(ArchiTechProTVBridges))
			foreach (var bridge in ArchiTechProTVBridges)
				if (Utilities.IsValid(bridge) && bridge._ShouldMuteBGM())
					return false;

		return true;
	}

	public void _UpdateVolume() {
		var must_v = _ShouldMuteBGM() ? Volume : 0.0f;
		var curr_v = Source.volume;
		if (!Mathf.Approximately(curr_v, must_v))
			Source.volume = must_v;
	}

#if !COMPILER_UDONSHARP && UNITY_EDITOR

	private bool Validate_Clips() => KawaUdonUtilities.DistinctArray(this, nameof(Clips), ref Clips);

	private void Validate_SwitchButton() {
		if (Utilities.IsValid(SwitchButton))
			KawaUdonUtilities.EnsureAppendedAsUdonBehaviour(SwitchButton,
				nameof(SwitchButton.EventReceivers), ref SwitchButton.EventReceivers, this);
	}

	private bool Validate_Bridges() {
		if (!Utilities.IsValid(ArchiTechProTVBridges)) {
			ArchiTechProTVBridges = new BGMAudioSync_ArchiTechProTVBridge[0];
			return true;
		}
		var bridges = ArchiTechProTVBridges.UnityNotNull().RuntimeOnly().Distinct().ToArray();
		return KawaUdonUtilities.ModifyArray(this, nameof(ArchiTechProTVBridges), ref ArchiTechProTVBridges, bridges);
	}

	private void Validate_Bridges_Entries(BGMAudioSync_ArchiTechProTVBridge bridge) {
		if (Utilities.IsValid(bridge) && bridge.Main != this) {
			// TODO Ensure Set + warning
			bridge.Main = this;
			bridge.ApplyProxyModificationsAndSetDirty();
		}
	}

	public override void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_Clips, this, nameof(Clips));
		KawaUdonUtilities.ValidateSafe(Validate_SwitchButton, this, nameof(SwitchButton));
		KawaUdonUtilities.ValidateSafe(Validate_Bridges, this, nameof(ArchiTechProTVBridges));
		KawaUdonUtilities.ValidateSafeForEach(ArchiTechProTVBridges, Validate_Bridges_Entries, this, nameof(ArchiTechProTVBridges));
	}

#endif
}
