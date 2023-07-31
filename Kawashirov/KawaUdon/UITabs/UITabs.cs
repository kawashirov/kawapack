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

public class UITabs : UdonSharpBehaviour
#if !COMPILER_UDONSHARP
	, IRefreshable
#endif
{
	public UITab[] tabs;
	public int currentTab = 0;

	[NonSerialized] public string path__ = "";

	public void Start() {
		path__ = _GetPath(transform);

		if (!Utilities.IsValid(tabs)) {
			Debug.LogErrorFormat(gameObject, "Tabs is not valid! @ {0}", path__);
		} else {
			for (var i = 0; i < tabs.Length; ++i) {
				var tab = tabs[i];
				if (!Utilities.IsValid(tab))
					Debug.LogErrorFormat(gameObject, "Tab #{1} is not valid! @ {0}", path__, i);
			}
		}
		_UpdateState();
	}

	public void _UpdateState() {
		if (!Utilities.IsValid(tabs))
			return;
		for (var i = 0; i < tabs.Length; ++i) {
			var tab = tabs[i];
			if (!Utilities.IsValid(tab))
				continue;
			tab.isActive = i == currentTab;
			tab._UpdateState();
		}
	}

	public void _Activate(UITab activated_tab) {
		if (!Utilities.IsValid(activated_tab)) {
			Debug.LogErrorFormat(gameObject, "Activating not valid tab! @ {0}", path__);
			return;
		}
		if (!Utilities.IsValid(tabs))
			return;

		for (var i = 0; i < tabs.Length; ++i) {
			var tab = tabs[i];
			if (!Utilities.IsValid(tab))
				continue;
			if (tab == activated_tab) {
				tab.isActive = true;
				currentTab = i;
			} else {
				tab.isActive = false;
			}
			tab._UpdateState();
		}
	}

	public override void Interact() => _UpdateState();

	private string _GetPath(Transform t) {
		var path = t.name;
		while (t.parent != null) {
			t = t.parent;
			path = t.name + "/" + path;
		}
		return path;
	}

#if !COMPILER_UDONSHARP && UNITY_EDITOR

	[CustomEditor(typeof(UITabs))]
	public class Editor : UnityEditor.Editor {
		public override void OnInspectorGUI() {
			if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
				return;
			DrawDefaultInspector();
			KawaGizmos.DrawEditorGizmosGUI();
			this.EditorRefreshableGUI();
		}
	}

	private bool Validate_tabs() => KawaUdonUtilities.DistinctArray(this, nameof(tabs), ref tabs);

	private void Validate_tab_in_tabs(UITab tab) {
		KawaUdonUtilities.EnsureIsValid(tab);
		if (!Utilities.IsValid(tab.tabs)) {
			tab.tabs = this;
			tab.ApplyProxyModificationsAndSetDirty();
		} else if (tab.tabs != this) {
			throw new ArgumentException(string.Format("Children UITab is not bound to this UITabs! @ {0}", tab.gameObject.KawaGetFullPath()));
		}
	}

	public void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_tabs, this, nameof(tabs));

		// Ensure children UITab is bound to this UITabs
		KawaUdonUtilities.ValidateSafeForEach(tabs, Validate_tab_in_tabs, this, nameof(tabs));
	}

	public UnityEngine.Object AsUnityObject() => this;

	public string RefreshablePath() => gameObject.KawaGetFullPath();

	public void OnDrawGizmosSelected() {
		var self_pos = transform.position;
		Gizmos.color = Color.green.Alpha(KawaGizmos.GizmosAplha);
		foreach (var receivers in tabs)
			if (Utilities.IsValid(receivers))
				Gizmos.DrawLine(self_pos, receivers.transform.position);
	}

#endif
}
