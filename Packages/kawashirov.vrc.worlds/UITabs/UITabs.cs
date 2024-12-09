using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Kawashirov;
using Kawashirov.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class UITabs : CommonUSharpBehaviour {
	public UITab[] tabs;
	public int currentTab = 0;

	public override void Start() {
		base.Start();
		logName = "Kawa|UITabs";

		_EnsureAll(tabs, true, 1, nameof(tabs));

		SendCustomEventDelayedSeconds("_UpdateState", 1f);
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
			_Error($"Activating not valid tab!");
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

#if !COMPILER_UDONSHARP && UNITY_EDITOR

	private bool Validate_tabs() => KawaUdonUtilities.DistinctArray(this, nameof(tabs), ref tabs);

	private void Validate_tab_in_tabs(UITab tab) {
		KawaUdonUtilities.EnsureIsValid(tab);
		if (!Utilities.IsValid(tab.tabs)) {
			tab.tabs = this;
			tab.ApplyProxyModificationsAndSetDirty();
		} else if (tab.tabs != this) {
			throw new ArgumentException($"Children UITab is not bound to this UITabs! @ {tab.gameObject.KawaGetFullPath()}");
		}
	}

	public override void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_tabs, this, nameof(tabs));

		// Ensure children UITab is bound to this UITabs
		KawaUdonUtilities.ValidateSafeForEach(tabs, Validate_tab_in_tabs, this, nameof(tabs));
	}

	public void OnDrawGizmosSelected() {
		var self_pos = transform.position;
		Gizmos.color = Color.green.Alpha(KawaGizmos.GizmosAplha);
		foreach (var receivers in tabs)
			if (Utilities.IsValid(receivers))
				Gizmos.DrawLine(self_pos, receivers.transform.position);
	}

#endif
}
