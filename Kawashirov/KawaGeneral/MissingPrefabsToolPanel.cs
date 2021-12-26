#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Kawashirov;
using Kawashirov.ToolsGUI;
using System;

namespace Kawashirov {
	[ToolsWindowPanel("Objects/Missing Prefabs")]
	public class MissingPrefabsToolPanel : AbstractToolPanel {

		private struct FoundObject {
			public GameObject gobj;
			// public PrefabInstanceStatus status;
			public GameObject pingObject;
		}

		private static readonly List<FoundObject> foundObjects = new List<FoundObject>();
		private static readonly Queue<GameObject> searchQueue = new Queue<GameObject>();

		[NonSerialized] public bool checkForMissingPrefabInName = true;
		[NonSerialized] public bool onlyInSelection = false;
		[NonSerialized] public int foundObjectsDisplaySize = 20;
		[NonSerialized] public float foundObjectsDisplayOffset = 0;

		private int FindMissingPrefabs(GameObject gobj, GameObject pingObject = null) {
			if (gobj == null)
				return 0;
			searchQueue.Clear();
			searchQueue.Enqueue(gobj);
			var totalObjects = 0;
			while (searchQueue.Count > 0) {
				gobj = searchQueue.Dequeue();
				++totalObjects;
				var status = PrefabUtility.GetPrefabInstanceStatus(gobj);
				var badName = checkForMissingPrefabInName && gobj.name.Contains("(Missing Prefab)");
				if (status == PrefabInstanceStatus.MissingAsset || status == PrefabInstanceStatus.Disconnected || badName) {
					if (status == PrefabInstanceStatus.MissingAsset) {
						Debug.LogWarning($"GameObject {gobj.KawaGetFullPath()} contains missing Prefab reference!", gobj);
					} else if (status == PrefabInstanceStatus.Disconnected) {
						Debug.LogWarning($"GameObject {gobj.KawaGetFullPath()} is disconnected from Prefab asset!", gobj);
					} else {
						Debug.LogWarning($"GameObject {gobj.KawaGetFullPath()} have \"(Missing Prefab)\" in it's name!", gobj);
					}
					foundObjects.Add(new FoundObject() {
						gobj = gobj,
						// status = status,
						pingObject = pingObject ? pingObject : gobj
					});
				} else {
					foreach (Transform child in gobj.transform) {
						if (child.gameObject)
							searchQueue.Enqueue(child.gameObject);
					}
				}
			}
			return totalObjects;
		}

		public void FindMissingPrefabsInHierarchy() {
			Debug.Log($"Searching GameObjects with missing/disconnected Prefab asset...", this);
			foundObjects.Clear();
			var gameObjects = onlyInSelection ? Selection.GetFiltered<GameObject>(SelectionMode.TopLevel | SelectionMode.Editable) : KawaUtilities.IterScenesRoots();
			foreach (var gobj in gameObjects.Where(x => x != null)) {
				FindMissingPrefabs(gobj);
			}
			Debug.Log($"Found {foundObjects.Count} GameObjects with missing/disconnected Prefab asset!", this);
		}

		private void FindMissingPrefabsInProject(string assetPath, ref int totalObjects) {
			var wasLoaded = AssetDatabase.IsMainAssetAtPathLoaded(assetPath);
			var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
			if (mainAsset is GameObject mainGameObject) {
				totalObjects += FindMissingPrefabs(mainGameObject, mainGameObject);
			} else if (wasLoaded && mainAsset != null && !(mainAsset is Component)) {
				Resources.UnloadAsset(mainAsset);
			}
		}

		public void FindMissingPrefabsInProject() {
			Debug.Log($"Searching GameObjects with missing/disconnected Prefab asset...", this);
			try {
				EditorUtility.DisplayProgressBar("Searching Missing Scripts...", "Preparing...", 0);
				var totalObjects = 0;
				foundObjects.Clear();
				var assetPaths = onlyInSelection ? AssetUtility.GetSelectedAssetPaths() : AssetUtility.FindAllGameObjectAssetPaths();
				var progressBarTime = -1f;
				for (var i = 0; i < assetPaths.Length; ++i) {
					if (progressBarTime < Time.realtimeSinceStartup) {
						progressBarTime = Time.realtimeSinceStartup + 0.1f;
						var msg = $"Objects: {foundObjects.Count}/{totalObjects}, Files: {i + 1}/{assetPaths.Length}...";
						if (EditorUtility.DisplayCancelableProgressBar("Searching Missing Scripts...", msg, (i + 1f) / (assetPaths.Length + 1f))) {
							foundObjects.Clear();
							break;
						}
					}
					FindMissingPrefabsInProject(assetPaths[i], ref totalObjects);
				}
			} finally {
				try {
					Debug.Log($"Found {foundObjects.Count} GameObjects with missing/disconnected Prefab asset!", this);
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

		private void ToolsGUI_Buttons() {
			checkForMissingPrefabInName = EditorGUILayout.ToggleLeft("Also Check for \"(Missing Prefab)\" in Name.", checkForMissingPrefabInName);
			onlyInSelection = EditorGUILayout.ToggleLeft("Search Only in Selection", onlyInSelection);
			var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2));
			var rects = rect.RectSplitHorisontal(1, 1).ToArray();
			if (GUI.Button(rects[0], "On Scenes")) {
				FindMissingPrefabsInHierarchy();
			}
			if (GUI.Button(rects[1], "In Project")) {
				FindMissingPrefabsInProject();
			}
		}

		private void ToolsGUI_List() {
			foundObjects.RemoveAll(x => x.gobj == null); // Remove destroyed objects if any

			if (foundObjects.Count < 1) {
				EditorGUILayout.LabelField("No Game Objects");
				return;
			}

			foundObjectsDisplaySize = EditorGUILayout.IntField("Max Display Items", foundObjectsDisplaySize);
			var numberOfItemsToShow = Mathf.Min(foundObjectsDisplaySize, foundObjects.Count);
			EditorGUILayout.LabelField($"{numberOfItemsToShow}/{foundObjects.Count} Objects");
			var list = ScrollableList.AutoLayout(foundObjects.Count, numberOfItemsToShow, null, 0);
			list.DrawList(ref foundObjectsDisplayOffset);
			{ // Header 
				var rects = list.GetHeader().RectSplitHorisontal(1, 6, 3, 2).ToArray();
				using (new KawaGUIUtility.ZeroIndentScope()) {
					EditorGUI.LabelField(rects[0], "#");
					EditorGUI.LabelField(rects[1], "GameObject", labelCentred.Value);
					EditorGUI.LabelField(rects[2], "(Asset)", labelCentred.Value);
					EditorGUI.LabelField(rects[3], "Select", labelCentred.Value);
				}
			}
			for (var i = 0; i < numberOfItemsToShow; ++i) {
				var rects = list.GetRow(i, out var index).RectSplitHorisontal(1, 9, 2).ToArray();
				var found = foundObjects[index];
				using (new KawaGUIUtility.ZeroIndentScope()) {
					EditorGUI.LabelField(rects[0], $"{index + 1}");
					using (new EditorGUI.DisabledScope(true)) {
						if (found.gobj == found.pingObject) {
							EditorGUI.ObjectField(rects[1], found.gobj, typeof(GameObject), true);
						} else {
							var objRects = rects[1].RectSplitHorisontal(2, 1).ToArray();
							EditorGUI.ObjectField(objRects[0], found.gobj, typeof(GameObject), true);
							EditorGUI.ObjectField(objRects[1], found.pingObject, typeof(GameObject), true);
						}
					}
					if (GUI.Button(rects[2], "Select")) {
						Selection.objects = new[] { found.gobj };
						Selection.activeObject = found.gobj;
						KawaGUIUtility.OpenInspector();
						EditorGUIUtility.PingObject(found.gobj);
						EditorUtility.FocusProjectWindow();
						EditorGUIUtility.PingObject(found.pingObject);
					}
				}
			}

		}

		public override void ToolsGUI() {

			EditorGUILayout.LabelField("List Missing Prefabs:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				ToolsGUI_Buttons();
			}

			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Found Missing Prefabs:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				ToolsGUI_List();
			}

		}

	}
}
#endif
