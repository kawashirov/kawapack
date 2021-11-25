using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Kawashirov;

#if UNITY_EDITOR
using UnityEditor;
using static Kawashirov.KawaUtilities;
#endif

public static class MissingPrefabs {
#if UNITY_EDITOR

	[MenuItem("Kawashirov/Report Missing Prefabs/In Loaded Scenes")]
	public static void ReportMissingPrefabsInLoadedScenes() {
		Debug.Log("Searching Missing Prefabs in loaded scenes...");
		var roots = IterScenesRoots().ToList();
		var counter = 0;
		try {
			for (var i = 0; i < roots.Count; ++i) {
				if (EditorUtility.DisplayCancelableProgressBar("Searching Missing Prefabs", string.Format("{0}/{1}", i + 1, roots.Count), (i + 1.0f) / (roots.Count + 1.0f))) {
					break;
				}
				counter += ReportInGameObject(roots[i]);
			}
		} finally {
			EditorUtility.ClearProgressBar();
		}
		if (counter > 0) {
			Debug.LogWarningFormat("Found {0} missing prefabs in loaded scenes!", counter);
		} else {
			Debug.Log("There is no missing prefabs in loaded scenes!");
		}
	}

	[MenuItem("Kawashirov/Report Missing Prefabs/In All Prefabs")]
	public static void ReportMissingPrefabsInAllPrefabs() {
		var counter = 0;
		try {
			Debug.Log("Searching missing prefabs in project...");
			EditorUtility.DisplayProgressBar("Searching Missing Prefabs", "Searching prefabs...", 0f);
			var guids = AssetDatabase.FindAssets("t:GameObject").Distinct().ToList();
			Debug.LogFormat("Found {0} prefabs.", guids.Count);
			for (var i = 0; i < guids.Count; ++i) {
				if (EditorUtility.DisplayCancelableProgressBar("Searching Missing Prefabs", string.Format("{0}/{1}", i + 1, guids.Count), (i + 1.0f) / (guids.Count + 1.0f))) {
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
			Debug.LogWarningFormat("Found {0} missing prefabs references in project!", counter);
		} else {
			Debug.Log("There is no missing prefabs references in project!");
		}
	}


	private static int ReportInGameObject(GameObject go) {
		var queue = new Queue<GameObject>();
		queue.Enqueue(go);
		var counter = 0;
		while (queue.Count > 0) {
			go = queue.Dequeue();
			if (PrefabUtility.IsPrefabAssetMissing(go)) {
				++counter;
				Debug.LogWarningFormat(go, "Object {0} contains missing prefab reference!", go.KawaGetFullPath());
			} else if (PrefabUtility.IsDisconnectedFromPrefabAsset(go)) {
				++counter;
				Debug.LogWarningFormat(go, "Object {0} is disconnected from prefab asset!", go.KawaGetFullPath());
			} else {
				foreach (Transform child in go.transform) {
					if (child.gameObject)
						queue.Enqueue(child.gameObject);
				}
			}
		}
		return counter;
	}

#endif
}
