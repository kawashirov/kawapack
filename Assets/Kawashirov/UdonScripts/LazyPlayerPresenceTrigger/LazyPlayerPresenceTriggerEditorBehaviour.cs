using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Kawashirov;
using Kawashirov.Udon;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class LazyPlayerPresenceTriggerEditorBehaviour : CommonUdonEditorBehaviour, KawaGizmos.IKawaGizmos {
#if UNITY_EDITOR

	private Vector3[] _gizmos_present_ = new Vector3[0];
	private Vector3[] _gizmos_absent_ = new Vector3[0];
	private Vector3[] _gizmos_animators_ = new Vector3[0];
	private Vector3[] _gizmos_receivers_ = new Vector3[0];
	private double _gizmos_next_update = -1.0f;

	[CustomEditor(typeof(LazyPlayerPresenceTriggerEditorBehaviour))]
	public new class Editor : CommonUdonEditorBehaviour.Editor {

		public override void OnInspectorGUI() {
			OnInspectorRefreshGUI();

			EditorGUILayout.Space();

			KawaGizmos.DrawEditorGizmosGUI();
		}

	}

	public void OnDrawGizmosSelected() {
		OnDrawGizmosUpdate();
		var self_pos = transform.position;

		Gizmos.color = Color.white.Alpha(KawaGizmos.GizmosAplha);
		Gizmos.DrawWireSphere(self_pos, 0.1f);

		Gizmos.color = Color.blue.Alpha(KawaGizmos.GizmosAplha);
		foreach (var p in _gizmos_present_)
			Gizmos.DrawLine(self_pos, p);

		Gizmos.color = Color.red.Alpha(KawaGizmos.GizmosAplha);
		foreach (var p in _gizmos_absent_)
			Gizmos.DrawLine(self_pos, p);

		Gizmos.color = Color.yellow.Alpha(KawaGizmos.GizmosAplha);
		foreach (var p in _gizmos_animators_)
			Gizmos.DrawLine(self_pos, p);

		Gizmos.color = Color.green.Alpha(KawaGizmos.GizmosAplha);
		foreach (var p in _gizmos_receivers_)
			Gizmos.DrawLine(self_pos, p);
	}

	public void OnDrawGizmosUpdate() {
		if (EditorApplication.timeSinceStartup < _gizmos_next_update)
			return;

		var udon = GetSingleUdonBehaviour(false);
		if (udon == null)
			return;

		var ActiveWhenPresent = udon.GetPubVarValue<GameObject[]>("ActiveWhenPresent", null);
		if (ActiveWhenPresent != null)
			_gizmos_present_ = ActiveWhenPresent.Select(g => g.transform.position).ToArray();

		var ActiveWhenAbsent = udon.GetPubVarValue<GameObject[]>("ActiveWhenAbsent", null);
		if (ActiveWhenAbsent != null)
			_gizmos_absent_ = ActiveWhenAbsent.Select(g => g.transform.position).ToArray();

		var AnimatorsSetBool = udon.GetPubVarValue<Animator[]>("AnimatorsSetBool", null);
		if (AnimatorsSetBool != null)
			_gizmos_animators_ = AnimatorsSetBool.Select(a => a.transform.position).ToArray();

		var EventReceivers = udon.GetPubVarValue<Component[]>("EventReceivers", null);
		if (EventReceivers != null)
			_gizmos_receivers_ = EventReceivers.Select(c => c.transform.position).ToArray();

		_gizmos_next_update = EditorApplication.timeSinceStartup + 10;
	}

#endif
}
