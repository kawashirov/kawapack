using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
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

public class ThrottledUpdate : UdonSharpBehaviour
#if !COMPILER_UDONSHARP
	, IRefreshable
#endif
{
	[Tooltip("UdonBehaviours.\nUdonBehaviours[] is not exposed, so it's Component[].\nNon-UdonBehaviours here -> script crash.")]
	public Component[] EventReceivers;

	[Tooltip("Name of event to be sent to UdonBehaviours.")]
	public string ThrottledUpdateEventName = "_ThrottledUpdate";

	[Tooltip("Number of events will be sent per frame.\nAt least one event will be tried to sent.\nDuplicate events will not be sent to the one UdonBehaviour.")]
	public int UpdatesPerFrame = 1;

	[Tooltip("ShuffleFactor * EventReceivers.Length = number of randm swaps while shuffling EventReceivers.\n0 means no shuffling.\nRecommended <10, because large values can cause major lag.")]
	public float ShuffleFactor = 5.0f;

	public UnityEngine.Object[] AutoBindPrograms; // AbstractUdonProgramSource

	// Debug purposes only
	public ulong _UpdatesCalled = 0;

	private int ReceiversIndex = 0;

	/* Internal */

	private string path__ = "";

	public void Start() {
		path__ = GetPath(transform);
		_UpdatesCalled = 0;

		if (!Utilities.IsValid(EventReceivers) || EventReceivers.Length < 1) {
			Debug.LogErrorFormat(gameObject, "[Kawa|ThrottledUpdate] No EventReceivers is set! @ {0}", path__);
		} else {
			var length = EventReceivers.Length; // getter
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

		if (string.IsNullOrWhiteSpace(ThrottledUpdateEventName)) {
			Debug.LogErrorFormat(gameObject, "[Kawa|ThrottledUpdate] ThrottledUpdateEventName is null or empty! @ {0}", path__);
		}
	}

	public void Update() {
		if (!Utilities.IsValid(EventReceivers))
			return;
		var length = EventReceivers.Length; // getter
		if (length < 1)
			return;
		// No updates more than EventReceivers, but always atleast 1 per frame.
		var updates = Mathf.Clamp(UpdatesPerFrame, 1, length);
		for (var i = 0; i < updates; ++i) {
			ReceiversIndex = (ReceiversIndex + 1) % length;
			var reciever = (UdonBehaviour)EventReceivers[ReceiversIndex];
			if (!(Utilities.IsValid(reciever) && reciever.gameObject.activeInHierarchy && reciever.enabled))
				continue;
			// TODO: isActiveAndEnabled not exposed.
			(reciever).SendCustomEvent(ThrottledUpdateEventName);
			++_UpdatesCalled;
		}
	}

	public override void Interact() {
		if (!Utilities.IsValid(EventReceivers))
			return;
		var length = EventReceivers.Length; // getter
		if (length < 1)
			return;
		for (var i = 0; i < length; ++i) {
			var reciever = (UdonBehaviour)EventReceivers[ReceiversIndex];
			if (!(Utilities.IsValid(reciever) && reciever.gameObject.activeInHierarchy && reciever.enabled))
				continue;
			// TODO: isActiveAndEnabled not exposed.
			reciever.SendCustomEvent(ThrottledUpdateEventName);
		}

		ReceiversIndex = UnityEngine.Random.Range(0, length);
	}

	/* Utils */

	private string GetPath(Transform t) {
		var path = t.name;
		while (!Utilities.IsValid(t.parent)) {
			t = t.parent;
			path = t.name + "/" + path;
		}
		return path;
	}

#if !COMPILER_UDONSHARP && UNITY_EDITOR

	[CustomEditor(typeof(ThrottledUpdate))]
	public class Editor : UnityEditor.Editor {
		public override void OnInspectorGUI() {
			if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
				return;
			DrawDefaultInspector();
			KawaGizmos.DrawEditorGizmosGUI();
			this.EditorRefreshableGUI();
		}
	}

	public void OnDrawGizmosSelected() {
		var self_pos = transform.position;
		Gizmos.color = Color.cyan.Alpha(KawaGizmos.GizmosAplha);
		if (Utilities.IsValid(EventReceivers))
			foreach (var gobj in EventReceivers)
				if (Utilities.IsValid(gobj))
					Gizmos.DrawLine(self_pos, gobj.transform.position);
	}

	private void Validate_ThrottledUpdateEventName() {
		if (string.IsNullOrEmpty(ThrottledUpdateEventName))
			throw new ArgumentException("ThrottledUpdateEventName is null or empty!");
	}

	private bool Validate_AutoBindPrograms() {
		var new_programs = AutoBindPrograms.OfType<AbstractUdonProgramSource>().Cast<UnityEngine.Object>().ToArray();
		return KawaUdonUtilities.ModifyArray(ref AutoBindPrograms, new_programs, KawaUdonUtilities.UnityInequality);
	}

	private bool Validate_EventReceivers() {
		var auto_event_recievers = gameObject.scene.GetRootGameObjects()
			.SelectMany(g => g.GetComponentsInChildren<UdonBehaviour>(true))
			.Where(u => AutoBindPrograms.Contains(u.programSource) && u.IsRuntime());
		var fixed_event_recievers = EventReceivers
			.SelectMany(KawaUdonUtilities.ConvertToUdonBehaviour);
		var new_receivers = fixed_event_recievers.Concat(auto_event_recievers).Distinct().Cast<Component>().ToArray();
		return KawaUdonUtilities.ModifyArray(ref EventReceivers, new_receivers, KawaUdonUtilities.UnityInequality);
	}

	private void Validate_EventReceiver_EntryPoint(Component component) {
		var udon = component as UdonBehaviour;
		if (!string.IsNullOrEmpty(ThrottledUpdateEventName) && !udon.HasEntryPoint(ThrottledUpdateEventName))
			throw new ArgumentException($"UdonBehaviour does not have entry point {ThrottledUpdateEventName} @ {udon.gameObject.KawaGetFullPath()}");
	}

	public void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_AutoBindPrograms, this, nameof(AutoBindPrograms));
		KawaUdonUtilities.ValidateSafe(Validate_ThrottledUpdateEventName, this, nameof(ThrottledUpdateEventName));
		KawaUdonUtilities.ValidateSafe(Validate_EventReceivers, this, nameof(EventReceivers));
		KawaUdonUtilities.ValidateSafeForEach(EventReceivers, Validate_EventReceiver_EntryPoint, this, nameof(EventReceivers));
	}

	public UnityEngine.Object AsUnityObject() => this;

	public string RefreshablePath() => gameObject.KawaGetFullPath();

#endif
}
