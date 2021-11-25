using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Kawashirov;

#if UNITY_EDITOR
using UnityEditor;
using static Kawashirov.KawaUtilities;
#endif

public static class HiddenObjects {
#if UNITY_EDITOR

	[MenuItem("Kawashirov/Reveal Hidden Objects In Loaded Scenes")]
	public static void ReportMissingScriptsInLoadedScenes() {
		Debug.Log("Searching hidden objects in loaded scenes...");
		var roots = IterScenesRoots().ToList();
		var counter = 0;
		try {
			for (var i = 0; i < roots.Count; ++i) {
				if (EditorUtility.DisplayCancelableProgressBar("Searching Hidden Objects", string.Format("{0}/{1}", i + 1, roots.Count), (i + 1.0f) / (roots.Count + 1.0f))) {
					break;
				}
				counter += RevealInGameObject(roots[i]);
			}
		} finally {
			EditorUtility.ClearProgressBar();
		}
		if (counter > 0) {
			Debug.LogWarningFormat("Found {0} hidden objects in loaded scenes!", counter);
		} else {
			Debug.Log("There is no hidden objects in loaded scenes!");
		}
	}

	private static int RevealInGameObject(GameObject go) {
		var queue = new Queue<GameObject>();
		queue.Enqueue(go);
		var counter = 0;
		while (queue.Count > 0) {
			go = queue.Dequeue();
			if (RevealObject(go, go))
				++counter;
			foreach(var component in go.GetComponents<Component>().Where(c => c != null))
				if (RevealObject(component, go))
					++counter;
			foreach (Transform child in go.transform) {
				queue.Enqueue(child.gameObject);
			}
		}
		return counter;
	}

	private static bool RevealObject(Object obj, GameObject source) {
		var flags = (int)obj.hideFlags;
		flags &= ~(int)HideFlags.HideInHierarchy;
		flags &= ~(int)HideFlags.HideInInspector;
		flags &= ~(int)HideFlags.NotEditable;
		if (flags != (int)obj.hideFlags) {
			var old_flags = (int)obj.hideFlags;
			obj.hideFlags = (HideFlags)flags;
			EditorUtility.SetDirty(obj);
			if (source) {
				Debug.LogWarningFormat(source, "Changing flags of {0} in {1} from {2} to {3}...", obj, source.KawaGetFullPath(), old_flags, flags);
			}
			return true;
		}
		return false;
	}

#endif
}
