using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Kawashirov;

#if UNITY_EDITOR
using UnityEditor;
using static Kawashirov.KawaUtilities;
#endif

public static class MissingScripts {
#if UNITY_EDITOR

	[MenuItem("Kawashirov/Report Missing Scripts/In Loaded Scenes")]
	public static void ReportMissingScriptsInLoadedScenes() {
		Debug.Log("Searching Missing Scripts in loaded scenes...");
		var roots = IterScenesRoots().ToList();
		var counter = 0;
		try {
			for (var i = 0; i < roots.Count; ++i) {
				if (EditorUtility.DisplayCancelableProgressBar("Searching Missing Scripts", string.Format("{0}/{1}", i + 1, roots.Count), (i + 1.0f) / (roots.Count + 1.0f))) {
					break;
				}
				counter += ReportInGameObject(roots[i]);
			}
		} finally {
			EditorUtility.ClearProgressBar();
		}
		if (counter > 0) {
			Debug.LogWarningFormat("Found {0} missing scripts in loaded scenes!", counter);
		} else {
			Debug.Log("There is no missing scripts in loaded scenes!");
		}
	}

	[MenuItem("Kawashirov/Report Missing Scripts/In All Prefabs")]
	public static void ReportMissingScriptsInAllPrefabs() {
		var counter = 0;
		try {
			Debug.Log("Searching missing scripts in project prefabs...");
			EditorUtility.DisplayProgressBar("Searching Missing Scripts", "Searching prefabs...", 0f);
			// var guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(GameObject))).Distinct().ToList();
			var guids = AssetDatabase.FindAssets("t:GameObject").Distinct().ToList();
			Debug.LogFormat("Found {0} prefabs.", guids.Count);
			for (var i = 0; i < guids.Count; ++i) {
				if (EditorUtility.DisplayCancelableProgressBar("Searching Missing Scripts", string.Format("{0}/{1}", i + 1, guids.Count), (i + 1.0f) / (guids.Count + 1.0f))) {
					break;
				}
				var asset_path = AssetDatabase.GUIDToAssetPath(guids[i]);
				var asset = AssetDatabase.LoadAssetAtPath<GameObject>(asset_path);
				if (asset != null) 
					counter += ReportInGameObject(asset);
			}

		} finally {
			EditorUtility.ClearProgressBar();
		}
		if (counter > 0) {
			Debug.LogWarningFormat("Found {0} missing scripts in project prefabs!", counter);
		} else {
			Debug.Log("There is no missing scripts in project prefabs!");
		}
	}

	private static int ReportInGameObject(GameObject go) {
		var queue = new Queue<GameObject>();
		queue.Enqueue(go);
		var counter = 0;
		while (queue.Count > 0) {
			go = queue.Dequeue();
			var missing = go.GetComponents<Component>().Where(c => c == null).Count();
			if (missing > 0) {
				counter += missing;
				Debug.LogWarningFormat(go, "Object {0} contains {1} missing scripts!", go.KawaGetFullPath(), missing);
			}
			foreach (Transform child in go.transform) {
				if (child.gameObject)
					queue.Enqueue(child.gameObject);
			}
		}
		return counter;
	}

#endif
}
