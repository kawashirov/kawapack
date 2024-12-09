using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kawashirov {
	public static class KawaGizmos {
		public static float GizmosAplha = 0.2f;

#if UNITY_EDITOR

		public static void DrawEditorGizmosGUI() {
			using (var check = new EditorGUI.ChangeCheckScope()) {
				var v = EditorGUILayout.Slider("Gizmos opacity", GizmosAplha, 0f, 1f);
				if (check.changed)
					GizmosAplha = v;
			}
		}
#endif
	}
}
