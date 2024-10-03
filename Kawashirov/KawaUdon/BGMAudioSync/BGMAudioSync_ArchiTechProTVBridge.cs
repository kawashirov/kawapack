using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

using Kawashirov;
using Kawashirov.Udon;

/*
	Не используем using ArchiTech.ProTV;
	Потому что удон сосёт хуй и не подсасывает дефайны 
	из юнити асембли дефенишин в удон асембли дефенишин 
*/

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BGMAudioSync_ArchiTechProTVBridge : CommonUSharpBehaviour {
	public UdonBehaviour TVManager;
	public BGMAudioSync Main;

	[Space]
	[ReadOnly] public bool IsPlaying = false;
	[ReadOnly] public bool IsMuted = false;
	[ReadOnly] public float Volume = 1.0f;

	[Space]
	[ReadOnly] public float OUT_VOLUME = 1.0f;

	public override void Start() {
		base.Start();
		logName = "Kawa|BGMAudioSync|ArchiTechProTVPlugin";

		IsPlaying = false;
		IsMuted = false;
		Volume = 1.0f;

		OUT_VOLUME = 1.0f;

		if (_EnsureValid(TVManager, true, "TVManager is invalid!")) {
			TVManager.SetProgramVariable("IN_LISTENER", this);
			TVManager.SendCustomEventDelayedSeconds("_RegisterListener", 1f);
		}

		_EnsureValid(Main, "Main is invalid!");
	}

	public void _TvPlay() {
		IsPlaying = true;
		_NotifyMain();
	}

	public void _TvPause() {
		IsPlaying = false;
		_NotifyMain();
	}

	public void _TvStop() {
		IsPlaying = false;
		_NotifyMain();
	}

	public void _TvMute() {
		IsMuted = true;
		_NotifyMain();
	}

	public void _TvUnMute() {
		IsMuted = true;
		_NotifyMain();
	}

	public void _TvVolumeChange() {
		Volume = OUT_VOLUME;
		_NotifyMain();
	}

	private void _NotifyMain() {
		if (Utilities.IsValid(Main))
			Main.SendCustomEventDelayedFrames(nameof(Main._UpdateVolume), 1);
	}

	public bool _ShouldMuteBGM() {
		return IsPlaying && Volume > Mathf.Epsilon && !IsMuted;
	}


#if !COMPILER_UDONSHARP && UNITY_EDITOR

	public override void Refresh() {
		if (Utilities.IsValid(Main)) {
			KawaUdonUtilities.EnsureAppended(Main,
				nameof(Main.ArchiTechProTVBridges), ref Main.ArchiTechProTVBridges, this);
		}
	}

#endif
}

