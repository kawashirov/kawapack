#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Kawashirov;
using Kawashirov.ToolsGUI;
using static Kawashirov.KawaUtilities;
using Object = UnityEngine.Object;

namespace Kawashirov {
	[ToolsWindowPanel("Objects/Missing Scripts")]
	public class MissingScriptsToolPanel : AbstractToolPanel {

		private struct FoundObject {
			public GameObject gobj;
			public GameObject pingObject;
			public List<int> indexes;
		}

		private List<FoundObject> foundObjects = new List<FoundObject>();
		[NonSerialized] public int foundObjectsDisplaySize = 20;
		[NonSerialized] public float foundObjectsDisplayOffset = 0;

		private List<Component> tempComponents = new List<Component>();
		private List<int> tempIndexes = new List<int>();

		private bool FindMissingScripts(GameObject gobj, GameObject pingObject = null) {
			tempComponents.Clear();
			tempIndexes.Clear();
			gobj.GetComponents(typeof(Component), tempComponents);
			for (var i = 0; i < tempComponents.Count; ++i) {
				if (tempComponents[i] == null)
					tempIndexes.Add(i);
			}
			if (tempIndexes.Count > 0) {
				foundObjects.Add(new FoundObject() {
					gobj = gobj,
					pingObject = pingObject ? pingObject : gobj,
					indexes = tempIndexes
				});
				tempIndexes = new List<int>();
				return true;
			}
			return false;
		}

		public void FindMissingScriptsInHierarchy() {
			foundObjects.Clear();
			try {
				foreach (var gobj in IterScenesRoots().SelectMany(KawaUtilities.Traverse)) {
					if (gobj != null)
						FindMissingScripts(gobj);
				}
			} finally {
				tempComponents.Clear();
			}
		}

		private void FindMissingScriptsInProject(string assetPath, ref int totalObjects) {
			var wasLoaded = AssetDatabase.IsMainAssetAtPathLoaded(assetPath);
			var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
			if (mainAsset == null)
				return; // Странная ситуация, но бывет.
			var mainGameObject = mainAsset as GameObject;
			var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
			foreach (var asset in assets) {
				if (asset is GameObject gobj) {
					++totalObjects;
					FindMissingScripts(gobj, mainGameObject);
				} else if (!wasLoaded && !(asset is Component)) {
					Resources.UnloadAsset(asset);
				}
			}
		}

		public void FindMissingScriptsInProject() {
			try {
				EditorUtility.DisplayProgressBar("Searching Missing Scripts...", "Preparing...", 0);
				var totalObjects = 0;
				foundObjects.Clear();
				var GUIDs = AssetDatabase.FindAssets("t:GameObject");
				var assetPaths = GUIDs.Select(AssetDatabase.GUIDToAssetPath).ToArray();
				var progressBarTime = -1f;
				for (var i = 0; i < assetPaths.Length; ++i) {
					if (progressBarTime < Time.realtimeSinceStartup) {
						//Resources.UnloadUnusedAssets();
						progressBarTime = Time.realtimeSinceStartup + 0.1f;
						var msg = $"Objects: {foundObjects.Count}/{totalObjects}, Files: {i + 1}/{assetPaths.Length}...";
						if (EditorUtility.DisplayCancelableProgressBar("Searching Missing Scripts...", msg, (i + 1f) / (assetPaths.Length + 1f))) {
							foundObjects.Clear();
							break;
						}
					}
					FindMissingScriptsInProject(assetPaths[i], ref totalObjects);
				}
			} finally {
				try {
					tempComponents.Clear();
					EditorUtility.DisplayProgressBar("Searching Missing Scripts...", "Unloading Unused Assets...", 0);
					EditorUtility.UnloadUnusedAssetsImmediate();
				} finally {
					EditorUtility.ClearProgressBar();
				}
			}
		}

		public static Lazy<GUIStyle> labelCentred = new Lazy<GUIStyle>(() => new GUIStyle(EditorStyles.label) {
			alignment = TextAnchor.MiddleCenter
		});

		public override void ToolsGUI() {

			EditorGUILayout.LabelField("Find Missing Scripts on GameObjects:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2));
				var rects = rect.RectSplitHorisontal(1, 1).ToArray();
				if (GUI.Button(rects[0], "On Scenes")) {
					FindMissingScriptsInHierarchy();
				}
				if (GUI.Button(rects[1], "In Project")) {
					FindMissingScriptsInProject();
				}
			}

			EditorGUILayout.LabelField("Found Missing Scripts on GameObjects:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				foundObjects.RemoveAll(x => x.gobj == null); // Remove destroyed objects if any
				if (foundObjects.Count < 1) {
					EditorGUILayout.LabelField("No Objects");
				} else {
					foundObjectsDisplaySize = EditorGUILayout.IntField("Max Display Items", foundObjectsDisplaySize);
					var numberOfItemsToShow = Mathf.Min(foundObjectsDisplaySize, foundObjects.Count);
					var list = ScrollableList.AutoLayout(foundObjects.Count, numberOfItemsToShow, null, 0);
					list.DrawList(ref foundObjectsDisplayOffset);
					{
						// Header 
						var rect = list.GetHeader();
						var rects = rect.RectSplitHorisontal(1, 8, 1, 2).ToArray();
						using (new KawaGUIUtility.ZeroIndentScope()) {
							EditorGUI.LabelField(rects[0], "#");
							EditorGUI.LabelField(rects[1], "GameObject", labelCentred.Value);
							EditorGUI.LabelField(rects[2], "Miss", labelCentred.Value);
							EditorGUI.LabelField(rects[3], "Select", labelCentred.Value);
						}
					}
					for (var i = 0; i < numberOfItemsToShow; ++i) {
						var rect = list.GetRow(i, out var index);
						var found = foundObjects[index];
						var rects = rect.RectSplitHorisontal(1, 8, 1, 2).ToArray();
						using (new KawaGUIUtility.ZeroIndentScope()) {
							EditorGUI.LabelField(rects[0], $"{index + 1}");
							using (new EditorGUI.DisabledScope(true)) {
								EditorGUI.ObjectField(rects[1], found.gobj, typeof(GameObject), true);
							}
							EditorGUI.LabelField(rects[2], $"{found.indexes.Count}", labelCentred.Value);
							if (GUI.Button(rects[3], "Select")) {
								Selection.objects = new[] { found.gobj };
								Selection.activeObject = found.gobj;
								KawaGUIUtility.OpenInspector();
								EditorGUIUtility.PingObject(found.gobj);
								KawaGUIUtility.OpenInspector();
								EditorGUIUtility.PingObject(found.pingObject);
							}
						}
					}
				}
			}
		}

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

	}
}
#endif
