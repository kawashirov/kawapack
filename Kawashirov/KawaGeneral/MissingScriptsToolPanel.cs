#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Kawashirov;
using Kawashirov.ToolsGUI;
using Object = UnityEngine.Object;

namespace Kawashirov {
	[ToolsWindowPanel("Objects/Missing Scripts")]
	public class MissingScriptsToolPanel : AbstractToolPanel {

		private struct FoundObject {
			public GameObject gobj;
			public GameObject pingObject;
			public List<int> indexes;
		}

		private static readonly List<FoundObject> foundObjects = new List<FoundObject>();
		private static readonly List<Component> tempComponents = new List<Component>();
		private static List<int> tempIndexes = new List<int>();

		[NonSerialized] public bool onlyInSelection = false;
		[NonSerialized] public int foundObjectsDisplaySize = 20;
		[NonSerialized] public float foundObjectsDisplayOffset = 0;

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
			Debug.Log($"Searching GameObjects with missing scripts...", this);
			foundObjects.Clear();
			try {
				var gameObjects = onlyInSelection ? Selection.GetFiltered<GameObject>(SelectionMode.TopLevel | SelectionMode.Editable) : KawaUtilities.IterScenesRoots();
				foreach (var gobj in gameObjects.SelectMany(KawaUtilities.Traverse)) {
					if (gobj != null)
						FindMissingScripts(gobj);
				}
			} finally {
				tempComponents.Clear();
				Debug.Log($"Found {foundObjects.Count} with missing scripts.", this);
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
				Debug.Log($"Searching GameObjects with missing scripts...", this);
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
					Debug.Log($"Found {foundObjects.Count} with missing scripts.", this);
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
			onlyInSelection = EditorGUILayout.ToggleLeft("Search Only in Selection", onlyInSelection);
			var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 2));
			var rects = rect.RectSplitHorisontal(1, 1).ToArray();
			if (GUI.Button(rects[0], "On Scenes")) {
				FindMissingScriptsInHierarchy();
			}
			if (GUI.Button(rects[1], "In Project")) {
				FindMissingScriptsInProject();
			}
		}

		private void ToolsGUI_List() {
			foundObjects.RemoveAll(x => x.gobj == null); // Remove destroyed objects if any
			if (foundObjects.Count < 1) {
				EditorGUILayout.LabelField("No Objects");
				return;
			}

			foundObjectsDisplaySize = EditorGUILayout.IntField("Max Display Items", foundObjectsDisplaySize);
			var numberOfItemsToShow = Mathf.Min(foundObjectsDisplaySize, foundObjects.Count);
			EditorGUILayout.LabelField($"{numberOfItemsToShow}/{foundObjects.Count} Objects");
			var list = ScrollableList.AutoLayout(foundObjects.Count, numberOfItemsToShow, null, 0);
			list.DrawList(ref foundObjectsDisplayOffset);
			{
				// Header
				var rects = list.GetHeader().RectSplitHorisontal(1, 6, 3, 1, 2).ToArray();
				using (new KawaGUIUtility.ZeroIndentScope()) {
					EditorGUI.LabelField(rects[0], "#");
					EditorGUI.LabelField(rects[1], "GameObject", labelCentred.Value);
					EditorGUI.LabelField(rects[2], "(Asset)", labelCentred.Value);
					EditorGUI.LabelField(rects[3], "Miss", labelCentred.Value);
					EditorGUI.LabelField(rects[4], "Select", labelCentred.Value);
				}
			}
			for (var i = 0; i < numberOfItemsToShow; ++i) {
				var rects = list.GetRow(i, out var index).RectSplitHorisontal(1, 9, 1, 2).ToArray();
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
					EditorGUI.LabelField(rects[2], $"{found.indexes.Count}", labelCentred.Value);
					if (GUI.Button(rects[3], "Select")) {
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

			EditorGUILayout.LabelField("Find Missing Scripts on GameObjects:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				ToolsGUI_Buttons();
			}

			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Found Missing Scripts on GameObjects:", EditorStyles.boldLabel);
			using (new EditorGUI.IndentLevelScope(1)) {
				ToolsGUI_List();
			}
		}

	}
}
#endif
