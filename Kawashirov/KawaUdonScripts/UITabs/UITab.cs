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
using UnityEngine.UI;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UdonSharpEditor;
#endif

public class UITab : UdonSharpBehaviour
#if !COMPILER_UDONSHARP
	, IRefreshable
#endif
{
	public UITabs tabs;

	public bool isActive = false;
	public GameObject tabObject;
	public Animator buttonAnimator;
	public string buttonAnimatorBoolParameter = "Active";

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

	[CustomEditor(typeof(UITab))]
	public class Editor : UnityEditor.Editor {
		public override void OnInspectorGUI() {
			if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
				return;
			DrawDefaultInspector();
			KawaGizmos.DrawEditorGizmosGUI();
			this.EditorRefreshableGUI();
		}
	}

	private void Validate_tabObject() => KawaUdonUtilities.EnsureIsValid(tabObject, nameof(tabObject));

	private void Validate_tabs() => KawaUdonUtilities.EnsureAppended(tabs, ref tabs.tabs, this.ToEnumerable());
	
	public void Refresh() {
		KawaUdonUtilities.ValidateSafe(Validate_tabObject, this, nameof(tabObject));
		KawaUdonUtilities.ValidateSafe(Validate_tabs, this, nameof(tabs));
	}

	public UnityEngine.Object AsUnityObject() => this;

	public string RefreshablePath() => gameObject.KawaGetFullPath();

	public void OnDrawGizmosSelected() {
		if (Utilities.IsValid(tabs)) {
			Gizmos.color = Color.green.Alpha(KawaGizmos.GizmosAplha);
			Gizmos.DrawLine(transform.position, tabs.transform.position);
		}
	}

#endif
}
