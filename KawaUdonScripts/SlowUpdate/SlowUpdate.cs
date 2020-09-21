using UdonSharp;
using UnityEngine;
using VRC.Udon;

public class SlowUpdate : UdonSharpBehaviour {
	[Tooltip("UdonBehaviours.\nUdonBehaviours[] is not exposed, so it's Component[].\nNon-UdonBehaviours here -> script crash.")]
	public Component[] EventReceivers;

	[Tooltip("Name of event to be sent to UdonBehaviours.")]
	public string SlowUpdateEventName = "SlowUpdate";

	[Tooltip("Number of events will be sent per frame.\nAt least one event will be tried to sent.\nDuplicate events will not be sent to the one UdonBehaviour.")]
	public int UpdatesPerFrame = 1;

	[Tooltip("ShuffleFactor * EventReceivers.Length = number of randm swaps while shuffling EventReceivers.\n0 means no shuffling.\nRecommended <10, because large values can cause major lag.")]
	public float ShuffleFactor = 5.0f;

	// Debug purposes only
	[System.NonSerialized] public ulong UpdatesCalled = 0;

	private int ReceiversIndex = 0;

	/* Internal */

	private string _path = "";

	public void Start() {
		_path = GetPath(transform);
		UpdatesCalled = 0;

		var length = EventReceivers.Length; // getter

		if (length < 1) {
			Debug.LogErrorFormat(gameObject, "[Kawa|SlowUpdate] No EventReceivers is set! @ {0}", _path);
		} else {
			var shuffle_n = Mathf.RoundToInt(EventReceivers.Length * Mathf.Clamp(ShuffleFactor, 1.0f, 100.0f));
			for (var i = 0; i < shuffle_n; ++i) {
				int a = UnityEngine.Random.Range(0, length), b = UnityEngine.Random.Range(0, length);
				if (a != b) {
					var t = EventReceivers[a];
					EventReceivers[a] = EventReceivers[b];
					EventReceivers[b] = t;
				}
			}
			ReceiversIndex = UnityEngine.Random.Range(0, length);
		}

		// TODO if string.IsNullOrWhiteSpace(SlowUpdateEventName)
	}

	public void Update() {
		var length = EventReceivers.Length; // getter
		if (length < 1)
			return;
		// No updates more than EventReceivers, but always atleast 1 per frame.
		var updates = Mathf.Clamp(UpdatesPerFrame, 1, length);
		for (var i = 0; i < updates; ++i) {
			var index = ReceiversIndex++ % length;
			var component = EventReceivers[index];
			if (component == null || !component.gameObject.activeInHierarchy)
				continue;
			var reciever = (UdonBehaviour)component;
			// TODO: isActiveAndEnabled not exposed.
			reciever.SendCustomEvent(SlowUpdateEventName);
			++UpdatesCalled;
		}
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
