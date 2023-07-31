using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using UdonSharp;
using Kawashirov;
using Kawashirov.Refreshables;
using Kawashirov.Udon;

#if UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
#endif

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class BGMAudioSync : UdonSharpBehaviour
#if !COMPILER_UDONSHARP
    , IRefreshable
#endif
{
	public StateSwitch switch_button;
	public AudioClip[] clips;
	[UdonSynced] public int current_clip = 0;
	[UdonSynced] public int current_sample = 0;

	public AudioSource source_;
	private string path_ = "";

	void Start()
	{
		path_ = _GetPath(transform);

		float length = 0;
		for (var i = 0; i < clips.Length; ++i)
		{
			var clip = clips[i];
			clip.LoadAudioData();
			length += clip.length;
		}
		Debug.LogFormat(gameObject, "Loaded {1} audio clips, total time: {2} @ {0}", path_, clips.Length, length);

		switch_button.currentState = 1;
		switch_button._UpdateState();
	}

	private void Update()
	{
		if (source_.isPlaying)
			return;
		if (!Networking.IsOwner(gameObject))
			return;

		var next_clip = Random.Range(0, clips.Length);
		if (next_clip == current_clip)
			next_clip = (next_clip + 1) % clips.Length;
		current_clip = next_clip;
		current_sample = 0;
		var clip = clips[current_clip];
		Debug.LogFormat(gameObject, "Choosen new audio clip #{1} (\"{2}\") @ {0}", path_, current_clip, clip.name);

		source_.Stop();
		source_.clip = clip;
		source_.timeSamples = 0;
		source_.Play();
		source_.timeSamples = 0;

		RequestSerialization();
	}

	public override void OnPreSerialization()
	{
		Debug.LogFormat(gameObject, "Serializing audio data: clip={1}, sample={2} @ {0}", path_, current_clip, current_sample);
		current_sample = source_.timeSamples;
	}

	public override void OnDeserialization()
	{
		Debug.LogFormat(gameObject, "Received audio data: clip={1}, sample={2} @ {0}", path_, current_clip, current_sample);
		source_.Stop();
		source_.clip = clips[current_clip];
		source_.timeSamples = current_sample;
		source_.Play();
		source_.timeSamples = current_sample;
	}

	public override void OnPostSerialization(SerializationResult result)
	{
		if (result.success)
		{
			Debug.LogFormat(gameObject, "Successfuly serialized {1} bytes of audio data. @ {0}", path_, result.byteCount);
		}
		else
		{
			Debug.LogFormat(gameObject, "Failed to serialize audio data. @ {0}", path_);
			SendCustomEventDelayedSeconds("_RequestSerializationDelayed", 5);
		}
	}

	public override void OnPlayerJoined(VRCPlayerApi player) => RequestSerialization();

	public override void OnOwnershipTransferred(VRCPlayerApi player) => RequestSerialization();

	public void _RequestSerializationDelayed() => RequestSerialization();

	public void _UpdateState()
	{
		var state = switch_button.currentState == 1;
		source_.volume = state ? 0.05f : 0.0f;
	}

	/* Utils */

	private string _GetPath(Transform t)
	{
		var path = t.name;
		while (t.parent != null)
		{
			t = t.parent;
			path = t.name + "/" + path;
		}
		return path;
	}


#if !COMPILER_UDONSHARP && UNITY_EDITOR

	[CustomEditor(typeof(BGMAudioSync))]
	public class Editor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
				return;
			DrawDefaultInspector();
			this.EditorRefreshableGUI();
		}
	}

	public void Refresh()
	{
		KawaUdonUtilities.DistinctArray(this, nameof(clips), ref clips);

		if (Utilities.IsValid(switch_button)) {
			KawaUdonUtilities.EnsureAppendedAsUdonBehaviour(switch_button, nameof(switch_button.eventReceivers), ref switch_button.eventReceivers, this);
		}
	}

	public UnityEngine.Object AsUnityObject() => this;

	public string RefreshablePath() => gameObject.KawaGetFullPath();

#endif
}
