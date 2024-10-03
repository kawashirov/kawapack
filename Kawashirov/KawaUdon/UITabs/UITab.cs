using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Kawashirov;
using Kawashirov.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class UITab : CommonUSharpBehaviour {
	public UITabs tabs;

	public bool isActive = false;
	public GameObject tabObject;
	public Animator buttonAnimator;
	public string buttonAnimatorBoolParameter = "Active";

	public override void Start() {
		base.Start();
		logName = "Kawa|UITab";

		_EnsureValid(tabs, true, "tabs is invalid!");

		SendCustomEventDelayedSeconds(nameof(_UpdateState), 1f);
	}

	public void _Activate() {
		if (Utilities.IsValid(tabs)) {
			tabs._Activate(this);
		} else {
			// Если родитель отвалился, то самоотключаемся.
			isActive = false;
			_UpdateState();
		}
	}

	public void _UpdateState() {
		if (Utilities.IsValid(tabObject))
			tabObject.SetActive(isActive);
		if (Utilities.IsValid(buttonAnimator))
			buttonAnimator.SetBool(buttonAnimatorBoolParameter, isActive);
	}

	public override void Interact() => _Activate();

#if !COMPILER_UDONSHARP && UNITY_EDITOR
	private void Validate_tabObject() => KawaUdonUtilities.EnsureIsValid(tabObject, nameof(tabObject));

	private void Validate_tabs() => KawaUdonUtilities.EnsureAppended(tabs, nameof(tabs.tabs), ref tabs.tabs, this);

	public override void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_tabObject, this, nameof(tabObject));
		KawaUdonUtilities.ValidateSafe(Validate_tabs, this, nameof(tabs));
	}

	public void OnDrawGizmosSelected() {
		if (Utilities.IsValid(tabs)) {
			Gizmos.color = Color.green.Alpha(KawaGizmos.GizmosAplha);
			Gizmos.DrawLine(transform.position, tabs.transform.position);
		}
	}
#endif
}
